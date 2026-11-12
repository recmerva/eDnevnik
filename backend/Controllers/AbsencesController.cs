using backend.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
public sealed class AbsencesController(SchoolDbContext db, AccessService access, ActivityService activity) : ControllerBase
{
    [HttpGet("api/students/{studentId:int}/absences")]
    public async Task<IActionResult> GetAbsences(int studentId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewStudentAsync(User, studentId, cancellationToken)) return Forbid();
        var absences = await db.Absences.Where(item => item.StudentId == studentId)
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id)
            .Select(item => new { item.Id, item.Date, item.LessonHours, item.IsExcused, item.Note })
            .ToListAsync(cancellationToken);
        return Ok(absences);
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpPost("api/students/{studentId:int}/absences")]
    public async Task<IActionResult> AddAbsence(int studentId, AbsenceRequest request, CancellationToken cancellationToken)
    {
        if (!await access.CanEditStudentAsync(User, studentId, cancellationToken)) return Forbid();
        if (request.Date > Today) return BadRequest(new { message = "Izostanak ne može biti evidentiran unapred." });
        if (request.IsExcused && !await access.IsHomeroomOfStudentAsync(User, studentId, cancellationToken)) return ForbidExcuse();

        var absence = new Absence { StudentId = studentId };
        Apply(absence, request);
        db.Absences.Add(absence);
        activity.Log("Izostanak", $"{await StudentNameAsync(studentId, cancellationToken)}: {absence.Date:dd.MM.yyyy}, {absence.LessonHours} čas(a), {(absence.IsExcused ? "opravdan" : "neopravdan")}.");
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/students/{studentId}/absences", ToResponse(absence));
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpPut("api/absences/{absenceId:int}")]
    public async Task<IActionResult> UpdateAbsence(int absenceId, AbsenceRequest request, CancellationToken cancellationToken)
    {
        var absence = await db.Absences.SingleOrDefaultAsync(item => item.Id == absenceId, cancellationToken);
        if (absence is null) return NotFound(new { message = "Izostanak ne postoji." });
        if (!await access.CanEditStudentAsync(User, absence.StudentId, cancellationToken)) return Forbid();
        if (request.Date > Today) return BadRequest(new { message = "Izostanak ne može biti evidentiran unapred." });

        if (!await access.IsHomeroomOfStudentAsync(User, absence.StudentId, cancellationToken)) return ForbidExcuse();
        Apply(absence, request);
        activity.Log("Izmena izostanka", $"{await StudentNameAsync(absence.StudentId, cancellationToken)}: {absence.Date:dd.MM.yyyy} – {(absence.IsExcused ? "opravdan" : "neopravdan")}.");
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(absence));
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpDelete("api/absences/{absenceId:int}")]
    public async Task<IActionResult> DeleteAbsence(int absenceId, CancellationToken cancellationToken)
    {
        var absence = await db.Absences.SingleOrDefaultAsync(item => item.Id == absenceId, cancellationToken);
        if (absence is null) return NotFound(new { message = "Izostanak ne postoji." });
        if (!await access.IsHomeroomOfStudentAsync(User, absence.StudentId, cancellationToken)) return ForbidExcuse();
        db.Absences.Remove(absence);
        activity.Log("Brisanje izostanka", $"{await StudentNameAsync(absence.StudentId, cancellationToken)}: {absence.Date:dd.MM.yyyy}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private ObjectResult ForbidExcuse() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "Predmetni nastavnik samo upisuje izostanak; opravdava, menja i briše ga razredni starešina." });

    private Task<string> StudentNameAsync(int studentId, CancellationToken cancellationToken) =>
        db.Students.Where(item => item.Id == studentId).Select(item => item.FullName + " (" + item.Class!.Name + ")").SingleAsync(cancellationToken);

    private static void Apply(Absence absence, AbsenceRequest request)
    {
        absence.Date = request.Date;
        absence.LessonHours = request.LessonHours;
        absence.IsExcused = request.IsExcused;
        absence.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
    }

    private static object ToResponse(Absence absence) =>
        new { absence.Id, absence.Date, absence.LessonHours, absence.IsExcused, absence.Note };
}
