using backend.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/classes/{classId:int}/schedule")]
[Authorize]
public sealed class ScheduleController(SchoolDbContext db, AccessService access, ActivityService activity) : ControllerBase
{
    private static readonly string[] DayNames = ["ponedeljak", "utorak", "sreda", "četvrtak", "petak"];

    // Raspored odeljenja i nastavnici po predmetima (iz dodela koje pravi administrator).
    [HttpGet]
    public async Task<IActionResult> GetSchedule(int classId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewClassAsync(User, classId, cancellationToken)) return Forbid();
        var teachers = await db.TeacherAssignments
            .Where(item => item.ClassId == classId && item.SubjectId != null)
            .OrderBy(item => item.Teacher!.FullName)
            .Select(item => new { SubjectId = item.SubjectId!.Value, item.Teacher!.FullName })
            .ToListAsync(cancellationToken);
        var entries = await db.Schedule.Where(item => item.ClassId == classId)
            .OrderBy(item => item.Day).ThenBy(item => item.LessonNumber)
            .Select(item => new { item.Id, item.Day, item.LessonNumber, item.SubjectId, Subject = item.Subject!.Name })
            .ToListAsync(cancellationToken);

        string? TeacherOf(int subjectId)
        {
            var names = teachers.Where(item => item.SubjectId == subjectId).Select(item => item.FullName).ToList();
            return names.Count == 0 ? null : string.Join(", ", names);
        }

        var subjectIds = entries.Select(item => item.SubjectId).Distinct().ToList();
        var subjects = await db.Subjects.Where(item => subjectIds.Contains(item.Id)).OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Name }).ToListAsync(cancellationToken);

        return Ok(new
        {
            Entries = entries.Select(item => new { item.Id, item.Day, item.LessonNumber, item.SubjectId, item.Subject, Teacher = TeacherOf(item.SubjectId) }),
            Subjects = subjects.Select(item => new
            {
                SubjectId = item.Id,
                Subject = item.Name,
                Teacher = TeacherOf(item.Id),
                WeeklyLessons = entries.Count(entry => entry.SubjectId == item.Id),
            }),
        });
    }

    // Izmena jednog časa; roditelji i nastavnici odeljenja dobijaju obaveštenje o promeni.
    [Authorize(Roles = Roles.Administrator)]
    [HttpPut]
    public async Task<IActionResult> SetLesson(int classId, ScheduleRequest request, CancellationToken cancellationToken)
    {
        var schoolClass = await db.Classes.SingleOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (schoolClass is null) return NotFound(new { message = "Odeljenje ne postoji." });
        Subject? subject = null;
        if (request.SubjectId is int subjectId)
        {
            subject = await db.Subjects.SingleOrDefaultAsync(item => item.Id == subjectId, cancellationToken);
            if (subject is null) return BadRequest(new { message = "Izabrani predmet ne postoji." });
        }

        var entry = await db.Schedule.Include(item => item.Subject)
            .SingleOrDefaultAsync(item => item.ClassId == classId && item.Day == request.Day && item.LessonNumber == request.LessonNumber, cancellationToken);
        var previous = entry?.Subject?.Name;
        if (previous == subject?.Name) return NoContent();

        if (subject is null) db.Schedule.Remove(entry!);
        else if (entry is null) db.Schedule.Add(new ScheduleEntry { ClassId = classId, Day = request.Day, LessonNumber = request.LessonNumber, SubjectId = subject.Id });
        else entry.SubjectId = subject.Id;

        var lesson = $"{DayNames[request.Day - 1]}, {request.LessonNumber}. čas";
        var change = subject is null ? $"čas {previous} je otkazan" : previous is null ? $"dodat je čas {subject.Name}" : $"{subject.Name} umesto {previous}";
        db.Announcements.Add(new Announcement
        {
            ClassId = classId,
            AuthorUserId = User.GetUserId(),
            AuthorName = User.GetFullName(),
            Content = $"Izmena rasporeda časova ({lesson}): {change}.",
            CreatedAt = DateTime.UtcNow,
        });
        activity.Log("Izmena rasporeda", $"{schoolClass.Name}, {lesson}: {change}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
