using backend.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace backend.Controllers;

[ApiController]
[Route("api")]
public sealed class SchoolController(SchoolDbContext db, IOptions<SchoolOptions> school, ActivityService activity) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("school")]
    public async Task<IActionResult> GetSchool(CancellationToken cancellationToken)
    {
        var year = await db.SchoolYears.OrderBy(item => item.Id).FirstAsync(cancellationToken);
        return Ok(new
        {
            school.Value.Name,
            school.Value.City,
            SchoolYear = year.Name,
            CurrentTerm = year.TermOf(DateOnly.FromDateTime(DateTime.UtcNow)),
            year.FirstTermStart,
            year.FirstTermEnd,
            year.SecondTermStart,
            year.SecondTermEnd,
        });
    }

    [Authorize(Roles = Roles.Administrator)]
    [HttpPut("admin/school-year")]
    public async Task<IActionResult> UpdateSchoolYear(SchoolYearRequest request, CancellationToken cancellationToken)
    {
        if (!(request.FirstTermStart < request.FirstTermEnd && request.FirstTermEnd < request.SecondTermStart && request.SecondTermStart < request.SecondTermEnd))
        {
            return BadRequest(new { message = "Datumi polugodišta moraju ići redom: početak i kraj prvog, pa početak i kraj drugog." });
        }

        var year = await db.SchoolYears.OrderBy(item => item.Id).FirstAsync(cancellationToken);
        year.Name = request.Name.Trim();
        year.FirstTermStart = request.FirstTermStart;
        year.FirstTermEnd = request.FirstTermEnd;
        year.SecondTermStart = request.SecondTermStart;
        year.SecondTermEnd = request.SecondTermEnd;
        activity.Log("Školska godina", $"Podešena školska godina {year.Name}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("subjects")]
    public async Task<IActionResult> GetSubjects(CancellationToken cancellationToken)
    {
        var subjects = await db.Subjects.OrderBy(subject => subject.Name)
            .Select(subject => new { subject.Id, subject.Name, subject.UsesDescriptiveScale })
            .ToListAsync(cancellationToken);
        return Ok(subjects);
    }
}
