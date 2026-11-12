using backend.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Administrator)]
public sealed class AdminController(SchoolDbContext db, IPasswordHasher<ApplicationUser> passwordHasher, ActivityService activity) : ControllerBase
{
    // ---------- Korisnički nalozi ----------

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? role, CancellationToken cancellationToken)
    {
        var query = db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(user => user.Role == role);
        var users = await query.OrderBy(user => user.Role).ThenBy(user => user.FullName)
            .Select(user => new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.Role,
                ClassCount = user.Assignments.Select(item => item.ClassId).Distinct().Count(),
                ChildCount = user.Children.Count,
            })
            .ToListAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateStaffUser(StaffUserRequest request, CancellationToken cancellationToken)
    {
        if (request.Role is not (Roles.Teacher or Roles.Administrator))
        {
            return BadRequest(new { message = "Administrator kreira naloge nastavnika i administratora; roditelji se registruju kodom učenika." });
        }
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return Conflict(new { message = "Nalog sa ovom email adresom već postoji." });
        }

        var user = new ApplicationUser { FullName = request.FullName.Trim(), Email = email, PasswordHash = string.Empty, Role = request.Role };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        activity.Log("Novi nalog", $"{user.FullName} ({user.Email}) – {user.Role}.");
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/admin/users/{user.Id}", new { user.Id, user.FullName, user.Email, user.Role, ClassCount = 0, ChildCount = 0 });
    }

    // Izmena naloga: ime, email, uloga (samo za osoblje) i, po potrebi, nova lozinka.
    [HttpPut("users/{userId:int}")]
    public async Task<IActionResult> UpdateUser(int userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return NotFound(new { message = "Nalog ne postoji." });
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(item => item.Email == email && item.Id != userId, cancellationToken))
        {
            return Conflict(new { message = "Nalog sa ovom email adresom već postoji." });
        }
        if (user.Role == Roles.Parent ? request.Role != Roles.Parent : request.Role is not (Roles.Teacher or Roles.Administrator))
        {
            return BadRequest(new { message = "Uloga roditelja se ne menja; osoblje može biti nastavnik ili administrator." });
        }
        if (userId == User.GetUserId() && request.Role != Roles.Administrator)
        {
            return BadRequest(new { message = "Ne možete sebi oduzeti administratorska prava." });
        }

        if (user.Role == Roles.Teacher && request.Role == Roles.Administrator)
        {
            await db.TeacherAssignments.Where(item => item.TeacherUserId == userId).ExecuteDeleteAsync(cancellationToken);
        }
        var changes = new List<string>();
        if (user.FullName != request.FullName.Trim()) changes.Add("ime");
        if (user.Email != email) changes.Add("email");
        if (user.Role != request.Role) changes.Add($"uloga {user.Role} → {request.Role}");
        if (!string.IsNullOrEmpty(request.NewPassword)) changes.Add("nova lozinka");

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.Role = request.Role;
        if (!string.IsNullOrEmpty(request.NewPassword)) user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        activity.Log("Izmena naloga", $"{user.FullName}: {(changes.Count == 0 ? "bez promena" : string.Join(", ", changes))}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("users/{userId:int}")]
    public async Task<IActionResult> DeleteUser(int userId, CancellationToken cancellationToken)
    {
        if (userId == User.GetUserId()) return BadRequest(new { message = "Ne možete obrisati sopstveni nalog." });
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return NotFound(new { message = "Nalog ne postoji." });

        // Brisanjem roditelja njegova deca dobijaju nove kodove za registraciju.
        var children = await db.Students.Where(item => item.ParentUserId == userId).ToListAsync(cancellationToken);
        children.ForEach(child => { child.ParentUserId = null; child.RegistrationCode = RegistrationCodes.Generate(); });
        db.Users.Remove(user);
        activity.Log("Obrisan nalog", $"{user.FullName} ({user.Email}) – {user.Role}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ---------- Dodele nastavnika ----------

    [HttpGet("teacher-assignments")]
    public async Task<IActionResult> GetAssignments(CancellationToken cancellationToken)
    {
        var assignments = await db.TeacherAssignments
            .OrderBy(item => item.Class!.GradeLevel).ThenBy(item => item.Class!.Name).ThenByDescending(item => item.IsHomeroom)
            .ThenBy(item => item.Teacher!.FullName)
            .Select(item => new
            {
                item.Id,
                TeacherId = item.TeacherUserId,
                TeacherName = item.Teacher!.FullName,
                item.Teacher.Email,
                item.ClassId,
                ClassName = item.Class!.Name,
                item.SubjectId,
                Subject = item.Subject != null ? item.Subject.Name : null,
                item.IsHomeroom,
            })
            .ToListAsync(cancellationToken);
        return Ok(assignments);
    }

    [HttpPost("teacher-assignments")]
    public async Task<IActionResult> AssignTeacher(TeacherAssignmentRequest request, CancellationToken cancellationToken)
    {
        var teacher = await db.Users.SingleOrDefaultAsync(user => user.Id == request.TeacherUserId && user.Role == Roles.Teacher, cancellationToken);
        var schoolClass = await db.Classes.SingleOrDefaultAsync(item => item.Id == request.ClassId, cancellationToken);
        var subject = request.SubjectId is int subjectId ? await db.Subjects.SingleOrDefaultAsync(item => item.Id == subjectId, cancellationToken) : null;
        if (teacher is null || schoolClass is null || (request.SubjectId is not null && subject is null))
        {
            return BadRequest(new { message = "Nastavnik, odeljenje ili predmet ne postoje." });
        }
        if (subject is null && !request.IsHomeroom)
        {
            return BadRequest(new { message = "Izaberite predmet ili označite nastavnika kao razrednog starešinu." });
        }
        if (await db.TeacherAssignments.AnyAsync(item => item.TeacherUserId == teacher.Id && item.ClassId == schoolClass.Id && item.SubjectId == request.SubjectId, cancellationToken))
        {
            return Conflict(new { message = "Ova dodela već postoji." });
        }
        // Jedan predmet u odeljenju predaje jedan nastavnik; zamena ide preko izmene postojeće dodele.
        if (subject is not null && await db.TeacherAssignments.AnyAsync(item => item.ClassId == schoolClass.Id && item.SubjectId == subject.Id, cancellationToken))
        {
            return Conflict(new { message = $"Predmet {subject.Name} u odeljenju {schoolClass.Name} već ima nastavnika." });
        }

        var assignment = new TeacherClassAssignment { TeacherUserId = teacher.Id, ClassId = schoolClass.Id, SubjectId = subject?.Id };
        db.TeacherAssignments.Add(assignment);
        if (request.IsHomeroom) await SetHomeroomAsync(assignment, cancellationToken);
        activity.Log("Dodela nastavnika", $"{teacher.FullName} → {schoolClass.Name}{(subject is null ? "" : $", {subject.Name}")}{(request.IsHomeroom ? ", razredni starešina" : "")}.");
        await db.SaveChangesAsync(cancellationToken);
        return Created("/api/admin/teacher-assignments", new { assignment.Id });
    }

    [HttpPut("teacher-assignments/{assignmentId:int}")]
    public async Task<IActionResult> UpdateHomeroom(int assignmentId, HomeroomRequest request, CancellationToken cancellationToken)
    {
        var assignment = await db.TeacherAssignments.Include(item => item.Teacher).Include(item => item.Class)
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (assignment is null) return NotFound(new { message = "Dodela ne postoji." });
        if (!request.IsHomeroom && assignment.SubjectId is null)
        {
            return BadRequest(new { message = "Ova dodela postoji samo zbog razrednog starešinstva; uklonite je umesto toga." });
        }
        if (request.IsHomeroom) await SetHomeroomAsync(assignment, cancellationToken);
        else assignment.IsHomeroom = false;
        activity.Log("Razredni starešina", $"{assignment.Teacher!.FullName}, {assignment.Class!.Name}: {(request.IsHomeroom ? "postavljen" : "uklonjen")}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("teacher-assignments/{assignmentId:int}")]
    public async Task<IActionResult> RemoveAssignment(int assignmentId, CancellationToken cancellationToken)
    {
        var assignment = await db.TeacherAssignments.Include(item => item.Teacher).Include(item => item.Class).Include(item => item.Subject)
            .SingleOrDefaultAsync(item => item.Id == assignmentId, cancellationToken);
        if (assignment is null) return NotFound(new { message = "Dodela ne postoji." });
        db.TeacherAssignments.Remove(assignment);
        activity.Log("Uklonjena dodela", $"{assignment.Teacher!.FullName} – {assignment.Class!.Name}{(assignment.Subject is null ? "" : $", {assignment.Subject.Name}")}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ---------- Učenici i kodovi za registraciju roditelja ----------

    [HttpGet("students")]
    public async Task<IActionResult> GetStudents([FromQuery] int? classId, CancellationToken cancellationToken)
    {
        var query = db.Students.AsQueryable();
        if (classId is not null) query = query.Where(student => student.ClassId == classId);
        var students = await query
            .OrderBy(student => student.Class!.GradeLevel).ThenBy(student => student.Class!.Name).ThenBy(student => student.FullName)
            .Select(student => new
            {
                StudentId = student.Id,
                student.FullName,
                student.ClassId,
                ClassName = student.Class!.Name,
                RegistrationCode = student.ParentUserId == null ? student.RegistrationCode : null,
                ParentName = student.Parent != null ? student.Parent.FullName : null,
                ParentEmail = student.Parent != null ? student.Parent.Email : null,
            })
            .ToListAsync(cancellationToken);
        return Ok(students);
    }

    [HttpPost("students")]
    public async Task<IActionResult> AddStudent(StudentRequest request, CancellationToken cancellationToken)
    {
        var schoolClass = await db.Classes.SingleOrDefaultAsync(item => item.Id == request.ClassId, cancellationToken);
        if (schoolClass is null) return BadRequest(new { message = "Odeljenje ne postoji." });
        var student = new Student { FullName = request.FullName.Trim(), ClassId = schoolClass.Id, RegistrationCode = RegistrationCodes.Generate() };
        db.Students.Add(student);
        activity.Log("Novi učenik", $"{student.FullName} – {schoolClass.Name}.");
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/admin/students/{student.Id}", new { StudentId = student.Id, student.RegistrationCode });
    }

    [HttpPut("students/{studentId:int}")]
    public async Task<IActionResult> UpdateStudent(int studentId, StudentRequest request, CancellationToken cancellationToken)
    {
        var student = await db.Students.Include(item => item.Class).SingleOrDefaultAsync(item => item.Id == studentId, cancellationToken);
        if (student is null) return NotFound(new { message = "Učenik ne postoji." });
        var schoolClass = await db.Classes.SingleOrDefaultAsync(item => item.Id == request.ClassId, cancellationToken);
        if (schoolClass is null) return BadRequest(new { message = "Odeljenje ne postoji." });
        activity.Log("Izmena učenika", $"{student.FullName} ({student.Class!.Name}) → {request.FullName.Trim()} ({schoolClass.Name}).");
        student.FullName = request.FullName.Trim();
        student.ClassId = schoolClass.Id;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("students/{studentId:int}")]
    public async Task<IActionResult> DeleteStudent(int studentId, CancellationToken cancellationToken)
    {
        var student = await db.Students.Include(item => item.Class).SingleOrDefaultAsync(item => item.Id == studentId, cancellationToken);
        if (student is null) return NotFound(new { message = "Učenik ne postoji." });
        db.Students.Remove(student);
        activity.Log("Obrisan učenik", $"{student.FullName} – {student.Class!.Name}, sa svim ocenama i izostancima.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("students/{studentId:int}/registration-code")]
    public async Task<IActionResult> RegenerateCode(int studentId, CancellationToken cancellationToken)
    {
        var student = await db.Students.SingleOrDefaultAsync(item => item.Id == studentId, cancellationToken);
        if (student is null) return NotFound(new { message = "Učenik ne postoji." });
        if (student.ParentUserId is not null) return Conflict(new { message = "Učenik već ima povezanog roditelja." });
        student.RegistrationCode = RegistrationCodes.Generate();
        activity.Log("Novi kod učenika", student.FullName);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { student.RegistrationCode });
    }

    // Odvezivanje roditelja uvek izdaje novi kod, da stari ne bi mogao ponovo da se iskoristi.
    [HttpDelete("students/{studentId:int}/parent")]
    public async Task<IActionResult> UnlinkParent(int studentId, CancellationToken cancellationToken)
    {
        var student = await db.Students.Include(item => item.Parent).SingleOrDefaultAsync(item => item.Id == studentId, cancellationToken);
        if (student is null) return NotFound(new { message = "Učenik ne postoji." });
        if (student.Parent is null) return Conflict(new { message = "Učenik nema povezanog roditelja." });
        activity.Log("Odvezan roditelj", $"{student.Parent.FullName} – {student.FullName}.");
        student.ParentUserId = null;
        student.RegistrationCode = RegistrationCodes.Generate();
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { student.RegistrationCode });
    }

    // ---------- Odeljenja i predmeti ----------

    [HttpPost("classes")]
    public async Task<IActionResult> AddClass(ClassRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim().ToUpperInvariant();
        if (await db.Classes.AnyAsync(item => item.Name == name, cancellationToken)) return Conflict(new { message = "Odeljenje sa tim nazivom već postoji." });
        var schoolClass = new SchoolClass { Name = name, GradeLevel = request.GradeLevel };
        db.Classes.Add(schoolClass);
        activity.Log("Novo odeljenje", name);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/admin/classes/{schoolClass.Id}", new { schoolClass.Id });
    }

    [HttpPut("classes/{classId:int}")]
    public async Task<IActionResult> UpdateClass(int classId, ClassRequest request, CancellationToken cancellationToken)
    {
        var schoolClass = await db.Classes.SingleOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (schoolClass is null) return NotFound(new { message = "Odeljenje ne postoji." });
        var name = request.Name.Trim().ToUpperInvariant();
        if (await db.Classes.AnyAsync(item => item.Name == name && item.Id != classId, cancellationToken)) return Conflict(new { message = "Odeljenje sa tim nazivom već postoji." });
        activity.Log("Izmena odeljenja", $"{schoolClass.Name} → {name}, {request.GradeLevel}. razred.");
        schoolClass.Name = name;
        schoolClass.GradeLevel = request.GradeLevel;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("classes/{classId:int}")]
    public async Task<IActionResult> DeleteClass(int classId, CancellationToken cancellationToken)
    {
        var schoolClass = await db.Classes.SingleOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (schoolClass is null) return NotFound(new { message = "Odeljenje ne postoji." });
        if (await db.Students.AnyAsync(item => item.ClassId == classId, cancellationToken))
        {
            return Conflict(new { message = "Odeljenje ima učenike. Prvo ih premestite ili obrišite." });
        }
        db.Classes.Remove(schoolClass);
        activity.Log("Obrisano odeljenje", schoolClass.Name);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("subjects")]
    public async Task<IActionResult> AddSubject(SubjectRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await db.Subjects.AnyAsync(item => item.Name == name, cancellationToken)) return Conflict(new { message = "Predmet sa tim nazivom već postoji." });
        var subject = new Subject { Name = name, UsesDescriptiveScale = request.UsesDescriptiveScale };
        db.Subjects.Add(subject);
        activity.Log("Novi predmet", name);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/admin/subjects/{subject.Id}", new { subject.Id, subject.Name, subject.UsesDescriptiveScale });
    }

    [HttpPut("subjects/{subjectId:int}")]
    public async Task<IActionResult> UpdateSubject(int subjectId, SubjectRequest request, CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.SingleOrDefaultAsync(item => item.Id == subjectId, cancellationToken);
        if (subject is null) return NotFound(new { message = "Predmet ne postoji." });
        var name = request.Name.Trim();
        if (await db.Subjects.AnyAsync(item => item.Name == name && item.Id != subjectId, cancellationToken)) return Conflict(new { message = "Predmet sa tim nazivom već postoji." });
        activity.Log("Izmena predmeta", $"{subject.Name} → {name}.");
        subject.Name = name;
        subject.UsesDescriptiveScale = request.UsesDescriptiveScale;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("subjects/{subjectId:int}")]
    public async Task<IActionResult> DeleteSubject(int subjectId, CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.SingleOrDefaultAsync(item => item.Id == subjectId, cancellationToken);
        if (subject is null) return NotFound(new { message = "Predmet ne postoji." });
        var inUse = await db.Grades.AnyAsync(item => item.SubjectId == subjectId, cancellationToken)
            || await db.FinalGrades.AnyAsync(item => item.SubjectId == subjectId, cancellationToken)
            || await db.Tests.AnyAsync(item => item.SubjectId == subjectId, cancellationToken)
            || await db.Homework.AnyAsync(item => item.SubjectId == subjectId, cancellationToken)
            || await db.Schedule.AnyAsync(item => item.SubjectId == subjectId, cancellationToken);
        if (inUse) return Conflict(new { message = "Predmet se koristi u ocenama, rasporedu ili obavezama i ne može se obrisati." });
        db.Subjects.Remove(subject);
        activity.Log("Obrisan predmet", subject.Name);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // ---------- Pregled aktivnosti ----------

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = db.ActivityLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.UserName.Contains(term) || item.Action.Contains(term) || item.Details.Contains(term));
        }
        var logs = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).Take(200)
            .Select(item => new { item.Id, item.UserName, item.Action, item.Details, item.CreatedAt })
            .ToListAsync(cancellationToken);
        return Ok(logs);
    }

    private async Task SetHomeroomAsync(TeacherClassAssignment assignment, CancellationToken cancellationToken)
    {
        var current = await db.TeacherAssignments
            .Where(item => item.ClassId == assignment.ClassId && item.IsHomeroom && item.Id != assignment.Id)
            .ToListAsync(cancellationToken);
        current.ForEach(item => item.IsHomeroom = false);
        assignment.IsHomeroom = true;
    }
}
