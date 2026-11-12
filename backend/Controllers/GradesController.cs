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
public sealed class GradesController(SchoolDbContext db, AccessService access, ActivityService activity) : ControllerBase
{
    [HttpGet("api/students/{studentId:int}/grades")]
    public async Task<IActionResult> GetGrades(int studentId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewStudentAsync(User, studentId, cancellationToken)) return Forbid();
        var subjectIds = await access.VisibleSubjectIdsForStudentAsync(User, studentId, cancellationToken);
        var (allSubjects, allowedSubjects) = (subjectIds is null, subjectIds ?? []);
        var grades = await db.Grades.Where(grade => grade.StudentId == studentId && (allSubjects || allowedSubjects.Contains(grade.SubjectId)))
            .OrderByDescending(grade => grade.GradedOn).ThenByDescending(grade => grade.Id)
            .Include(grade => grade.Subject)
            .ToListAsync(cancellationToken);
        return Ok(grades.Select(GradeResponse.From));
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpPost("api/students/{studentId:int}/grades")]
    public async Task<IActionResult> AddGrade(int studentId, GradeRequest request, CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.SingleOrDefaultAsync(item => item.Id == request.SubjectId, cancellationToken);
        var error = Validate(request, subject);
        if (error is not null) return BadRequest(new { message = error });
        if (!await access.CanTeachStudentSubjectAsync(User, studentId, request.SubjectId, cancellationToken)) return ForbidSubject();

        var grade = new Grade
        {
            StudentId = studentId,
            Subject = subject,
            Value = request.Value,
            GradeType = request.GradeType,
            GradedOn = request.GradedOn ?? Today,
        };
        db.Grades.Add(grade);
        activity.Log("Unos ocene", $"{await StudentNameAsync(studentId, cancellationToken)}: {subject!.Name} {request.Value} ({request.GradeType}).");
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/students/{studentId}/grades", GradeResponse.From(grade));
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpPut("api/grades/{gradeId:int}")]
    public async Task<IActionResult> UpdateGrade(int gradeId, GradeRequest request, CancellationToken cancellationToken)
    {
        var grade = await db.Grades.Include(item => item.Subject).SingleOrDefaultAsync(item => item.Id == gradeId, cancellationToken);
        if (grade is null) return NotFound(new { message = "Ocena ne postoji." });
        var subject = await db.Subjects.SingleOrDefaultAsync(item => item.Id == request.SubjectId, cancellationToken);
        var error = Validate(request, subject);
        if (error is not null) return BadRequest(new { message = error });
        if (!await access.CanTeachStudentSubjectAsync(User, grade.StudentId, grade.SubjectId, cancellationToken)
            || !await access.CanTeachStudentSubjectAsync(User, grade.StudentId, request.SubjectId, cancellationToken)) return ForbidSubject();

        activity.Log("Izmena ocene", $"{await StudentNameAsync(grade.StudentId, cancellationToken)}: {grade.Subject!.Name} {grade.Value} → {subject!.Name} {request.Value}.");
        grade.Subject = subject;
        grade.Value = request.Value;
        grade.GradeType = request.GradeType;
        grade.GradedOn = request.GradedOn ?? grade.GradedOn;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(GradeResponse.From(grade));
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpDelete("api/grades/{gradeId:int}")]
    public async Task<IActionResult> DeleteGrade(int gradeId, CancellationToken cancellationToken)
    {
        var grade = await db.Grades.Include(item => item.Subject).SingleOrDefaultAsync(item => item.Id == gradeId, cancellationToken);
        if (grade is null) return NotFound(new { message = "Ocena ne postoji." });
        if (!await access.CanTeachStudentSubjectAsync(User, grade.StudentId, grade.SubjectId, cancellationToken)) return ForbidSubject();
        db.Grades.Remove(grade);
        activity.Log("Brisanje ocene", $"{await StudentNameAsync(grade.StudentId, cancellationToken)}: {grade.Subject!.Name} {grade.Value}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("api/students/{studentId:int}/final-grades")]
    public async Task<IActionResult> GetFinalGrades(int studentId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewStudentAsync(User, studentId, cancellationToken)) return Forbid();
        var subjectIds = await access.VisibleSubjectIdsForStudentAsync(User, studentId, cancellationToken);
        var (allSubjects, allowedSubjects) = (subjectIds is null, subjectIds ?? []);
        var finals = await db.FinalGrades.Where(item => item.StudentId == studentId && (allSubjects || allowedSubjects.Contains(item.SubjectId)))
            .OrderBy(item => item.Subject!.Name).ThenBy(item => item.Term)
            .Select(item => new { item.Id, item.SubjectId, Subject = item.Subject!.Name, item.Subject.UsesDescriptiveScale, item.Term, item.Value, item.RecordedByName, item.RecordedAt })
            .ToListAsync(cancellationToken);
        return Ok(finals);
    }

    // Zaključivanje ocene: ako za predmet i polugodište već postoji zaključna ocena, ona se menja.
    [Authorize(Roles = Roles.Staff)]
    [HttpPut("api/students/{studentId:int}/final-grades")]
    public async Task<IActionResult> SetFinalGrade(int studentId, FinalGradeRequest request, CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.SingleOrDefaultAsync(item => item.Id == request.SubjectId, cancellationToken);
        if (subject is null) return BadRequest(new { message = "Izabrani predmet ne postoji." });
        if (!await access.CanTeachStudentSubjectAsync(User, studentId, request.SubjectId, cancellationToken)) return ForbidSubject();

        var final = await db.FinalGrades.SingleOrDefaultAsync(
            item => item.StudentId == studentId && item.SubjectId == request.SubjectId && item.Term == request.Term, cancellationToken);
        if (final is null)
        {
            final = new FinalGrade { StudentId = studentId, SubjectId = subject.Id, Term = request.Term, RecordedByName = "" };
            db.FinalGrades.Add(final);
        }
        final.Value = request.Value;
        final.RecordedByName = User.GetFullName();
        final.RecordedAt = DateTime.UtcNow;
        activity.Log("Zaključna ocena", $"{await StudentNameAsync(studentId, cancellationToken)}: {subject.Name}, {request.Term}. polugodište – {request.Value}.");
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { final.Id, final.SubjectId, Subject = subject.Name, subject.UsesDescriptiveScale, final.Term, final.Value, final.RecordedByName, final.RecordedAt });
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpDelete("api/final-grades/{finalGradeId:int}")]
    public async Task<IActionResult> DeleteFinalGrade(int finalGradeId, CancellationToken cancellationToken)
    {
        var final = await db.FinalGrades.Include(item => item.Subject).SingleOrDefaultAsync(item => item.Id == finalGradeId, cancellationToken);
        if (final is null) return NotFound(new { message = "Zaključna ocena ne postoji." });
        if (!await access.CanTeachStudentSubjectAsync(User, final.StudentId, final.SubjectId, cancellationToken)) return ForbidSubject();
        db.FinalGrades.Remove(final);
        activity.Log("Poništena zaključna ocena", $"{await StudentNameAsync(final.StudentId, cancellationToken)}: {final.Subject!.Name}, {final.Term}. polugodište.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private ObjectResult ForbidSubject() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "Možete da ocenjujete samo predmete koje predajete u ovom odeljenju." });

    private Task<string> StudentNameAsync(int studentId, CancellationToken cancellationToken) =>
        db.Students.Where(item => item.Id == studentId).Select(item => item.FullName + " (" + item.Class!.Name + ")").SingleAsync(cancellationToken);

    private static string? Validate(GradeRequest request, Subject? subject)
    {
        if (subject is null) return "Izabrani predmet ne postoji.";
        if (!Grade.Types.Contains(request.GradeType)) return "Izabrana vrsta ocene ne postoji.";
        if (request.GradedOn > Today) return "Datum ocene ne može biti u budućnosti.";
        return null;
    }
}

public sealed record GradeResponse(
    int Id,
    int SubjectId,
    string Subject,
    bool UsesDescriptiveScale,
    int Value,
    string? Description,
    string GradeType,
    DateOnly GradedOn)
{
    public static GradeResponse From(Grade grade) => new(
        grade.Id,
        grade.SubjectId,
        grade.Subject!.Name,
        grade.Subject.UsesDescriptiveScale,
        grade.Value,
        grade.Subject.UsesDescriptiveScale ? Grade.DescriptiveScale[grade.Value - 1] : null,
        grade.GradeType,
        grade.GradedOn);
}
