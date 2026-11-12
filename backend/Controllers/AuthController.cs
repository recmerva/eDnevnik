using System.Security.Claims;
using backend.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(SchoolDbContext db, IPasswordHasher<ApplicationUser> passwordHasher, ActivityService activity) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await db.Users.SingleOrDefaultAsync(user => user.Email == email, cancellationToken);
        if (existing is not null)
        {
            // Roditelj koji već ima nalog (za drugo dete) može ponovo da se "registruje" istim podacima:
            // ako se lozinka poklapa, novo dete se dodaje na postojeći nalog umesto da se pravi novi.
            var samePassword = existing.Role == Roles.Parent
                && passwordHasher.VerifyHashedPassword(existing, existing.PasswordHash, request.Password) != PasswordVerificationResult.Failed;
            if (!samePassword)
            {
                return Conflict(new { message = "Nalog sa ovom email adresom već postoji. Prijavite se, pa dete dodajte u meniju sa imenom deteta → „Poveži još jedno dete”." });
            }
            var (child, error) = await FindStudentForParentAsync(request.StudentCode, existing.Id, cancellationToken);
            if (child is null) return BadRequest(new { message = error });
            child.ParentUserId = existing.Id;
            activity.Log("Povezano dete", $"Roditelj {existing.Email} povezan sa učenikom {child.FullName}.", existing.Id, existing.FullName);
            await db.SaveChangesAsync(cancellationToken);
            await SignInAsync(existing);
            return Ok(await BuildProfileAsync(existing, cancellationToken));
        }

        var (student, codeError) = await FindStudentForParentAsync(request.StudentCode, null, cancellationToken);
        if (student is null)
        {
            return BadRequest(new { message = codeError });
        }

        var user = new ApplicationUser
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = string.Empty,
            Role = Roles.Parent,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        student.Parent = user;
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        activity.Log("Registracija", $"Roditelj {user.Email} povezan sa učenikom {student.FullName}.", user.Id, user.FullName);
        await db.SaveChangesAsync(cancellationToken);

        await SignInAsync(user);
        return Ok(await BuildProfileAsync(user, cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(item => item.Email == email, cancellationToken);
        var result = user is null
            ? PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (user is null || result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Email ili lozinka nisu ispravni." });
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }
        activity.Log("Prijava", $"{user.Email} ({user.Role})", user.Id, user.FullName);
        await db.SaveChangesAsync(cancellationToken);

        await SignInAsync(user);
        return Ok(await BuildProfileAsync(user, cancellationToken));
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == User.GetUserId(), cancellationToken);
        if (user is null) return Unauthorized();
        return Ok(await BuildProfileAsync(user, cancellationToken));
    }

    [Authorize]
    [EnableRateLimiting("auth")]
    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == User.GetUserId(), cancellationToken);
        if (user is null) return Unauthorized();
        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "Trenutna lozinka nije ispravna." });
        }
        if (request.CurrentPassword == request.NewPassword)
        {
            return BadRequest(new { message = "Nova lozinka mora biti drugačija od trenutne." });
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        activity.Log("Promena lozinke", user.Email);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // Roditelj koji već ima nalog može da poveže još jedno dete kodom koji je dobio od škole.
    [Authorize(Roles = Roles.Parent)]
    [EnableRateLimiting("auth")]
    [HttpPost("children")]
    public async Task<IActionResult> LinkChild(LinkChildRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == User.GetUserId(), cancellationToken);
        if (user is null) return Unauthorized();
        var (student, error) = await FindStudentForParentAsync(request.StudentCode, user.Id, cancellationToken);
        if (student is null)
        {
            return BadRequest(new { message = error });
        }

        student.ParentUserId = user.Id;
        activity.Log("Povezano dete", $"Roditelj {user.Email} povezan sa učenikom {student.FullName}.", user.Id, user.FullName);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildProfileAsync(user, cancellationToken));
    }

    // Traži učenika po kodu i objašnjava tačno zašto povezivanje ne može (kod pogrešan, dete već na ovom nalogu ili kod iskorišćen).
    private async Task<(Student? Student, string Error)> FindStudentForParentAsync(string code, int? parentId, CancellationToken cancellationToken)
    {
        var normalized = RegistrationCodes.Normalize(code);
        var student = normalized.Length == 0 ? null : await db.Students.SingleOrDefaultAsync(item => item.RegistrationCode == normalized, cancellationToken);
        if (student is null) return (null, "Kod učenika nije ispravan. Proverite da li ste ga uneli tačno kako ste ga dobili u školi.");
        if (student.ParentUserId is null) return (student, string.Empty);
        return student.ParentUserId == parentId
            ? (null, $"Dete {student.FullName} je već povezano sa vašim nalogom.")
            : (null, "Ovaj kod je već iskorišćen. Obratite se školi da vam izda novi kod.");
    }

    private async Task<object> BuildProfileAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var children = await db.Students.Where(item => item.ParentUserId == user.Id)
            .OrderBy(item => item.FullName)
            .Select(item => new { item.Id, item.FullName, item.ClassId, ClassName = item.Class!.Name })
            .ToListAsync(cancellationToken);
        return new { user.Id, user.FullName, user.Email, user.Role, Children = children };
    }

    private async Task SignInAsync(ApplicationUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
