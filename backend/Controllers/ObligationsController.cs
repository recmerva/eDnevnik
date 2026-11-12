using backend.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

// Školske obaveze odeljenja: testovi (kontrolni i pismeni) i domaći zadaci sa rokovima.
[ApiController]
[Authorize]
public sealed class ObligationsController(SchoolDbContext db, AccessService access, ActivityService activity) : ControllerBase
{
    [HttpGet("api/classes/{classId:int}/tests")]
    public async Task<IActionResult> GetTests(int classId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewClassAsync(User, classId, cancellationToken)) return Forbid();
        var tests = await db.Tests.Where(test => test.ClassId == classId)
            .OrderByDescending(test => test.HeldOn)
            .Select(test => new { test.Id, test.Title, test.SubjectId, Subject = test.Subject!.Name, test.HeldOn })
            .ToListAsync(cancellationToken);
        return Ok(tests);
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpPost("api/classes/{classId:int}/tests")]
    public async Task<IActionResult> AddTest(int classId, TestRequest request, CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.SingleOrDefaultAsync(item => item.Id == request.SubjectId, cancellationToken);
        if (subject is null) return BadRequest(new { message = "Izabrani predmet ne postoji." });
        if (subject.UsesDescriptiveScale) return BadRequest(new { message = "Za ovaj predmet se ne zakazuju testovi." });
        if (!await access.CanTeachSubjectAsync(User, classId, subject.Id, cancellationToken)) return ForbidSubject();

        var test = new SchoolTest { ClassId = classId, SubjectId = subject.Id, Title = request.Title.Trim(), HeldOn = request.HeldOn };
        db.Tests.Add(test);
        activity.Log("Zakazan test", $"{await ClassNameAsync(classId, cancellationToken)}: {test.Title} ({subject.Name}), {test.HeldOn:dd.MM.yyyy}.");
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/classes/{classId}/tests", new { test.Id, test.Title, test.SubjectId, Subject = subject.Name, test.HeldOn });
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpDelete("api/tests/{testId:int}")]
    public async Task<IActionResult> DeleteTest(int testId, CancellationToken cancellationToken)
    {
        var test = await db.Tests.SingleOrDefaultAsync(item => item.Id == testId, cancellationToken);
        if (test is null) return NotFound(new { message = "Test ne postoji." });
        if (!await access.CanTeachSubjectAsync(User, test.ClassId, test.SubjectId, cancellationToken)) return ForbidSubject();
        db.Tests.Remove(test);
        activity.Log("Obrisan test", $"{await ClassNameAsync(test.ClassId, cancellationToken)}: {test.Title}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("api/classes/{classId:int}/homework")]
    public async Task<IActionResult> GetHomework(int classId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewClassAsync(User, classId, cancellationToken)) return Forbid();
        var homework = await db.Homework.Where(item => item.ClassId == classId)
            .OrderByDescending(item => item.DueDate)
            .Select(item => new { item.Id, item.SubjectId, Subject = item.Subject!.Name, item.Title, item.Description, item.DueDate, item.AuthorName, item.CreatedAt })
            .ToListAsync(cancellationToken);
        return Ok(homework);
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpPost("api/classes/{classId:int}/homework")]
    public async Task<IActionResult> AddHomework(int classId, HomeworkRequest request, CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.SingleOrDefaultAsync(item => item.Id == request.SubjectId, cancellationToken);
        if (subject is null) return BadRequest(new { message = "Izabrani predmet ne postoji." });
        if (request.DueDate < DateOnly.FromDateTime(DateTime.UtcNow)) return BadRequest(new { message = "Rok izrade ne može biti u prošlosti." });
        if (!await access.CanTeachSubjectAsync(User, classId, subject.Id, cancellationToken)) return ForbidSubject();

        var homework = new Homework
        {
            ClassId = classId,
            SubjectId = subject.Id,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DueDate = request.DueDate,
            AuthorUserId = User.GetUserId(),
            AuthorName = User.GetFullName(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Homework.Add(homework);
        activity.Log("Domaći zadatak", $"{await ClassNameAsync(classId, cancellationToken)}: {homework.Title} ({subject.Name}), rok {homework.DueDate:dd.MM.yyyy}.");
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/classes/{classId}/homework", new
        {
            homework.Id, homework.SubjectId, Subject = subject.Name, homework.Title, homework.Description, homework.DueDate, homework.AuthorName, homework.CreatedAt,
        });
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpDelete("api/homework/{homeworkId:int}")]
    public async Task<IActionResult> DeleteHomework(int homeworkId, CancellationToken cancellationToken)
    {
        var homework = await db.Homework.SingleOrDefaultAsync(item => item.Id == homeworkId, cancellationToken);
        if (homework is null) return NotFound(new { message = "Domaći zadatak ne postoji." });
        if (!await access.CanTeachSubjectAsync(User, homework.ClassId, homework.SubjectId, cancellationToken)) return ForbidSubject();
        db.Homework.Remove(homework);
        activity.Log("Obrisan domaći", $"{await ClassNameAsync(homework.ClassId, cancellationToken)}: {homework.Title}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private ObjectResult ForbidSubject() =>
        StatusCode(StatusCodes.Status403Forbidden, new { message = "Ovo može da unese samo nastavnik tog predmeta u odeljenju ili razredni starešina." });

    private Task<string> ClassNameAsync(int classId, CancellationToken cancellationToken) =>
        db.Classes.Where(item => item.Id == classId).Select(item => item.Name).SingleAsync(cancellationToken);
}
