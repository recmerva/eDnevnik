using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

public sealed record NotificationItem(
    string Key,
    string Kind,
    string Title,
    string Detail,
    DateTime Date,
    string Page,
    int? StudentId,
    int? ClassId);

// Zvonce u zaglavlju: sve novo što je bitno za ulogu korisnika, skupljeno na jednom mestu.
[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController(SchoolDbContext db, AccessService access) : ControllerBase
{
    private const int RecentDays = 14;

    [HttpGet]
    public async Task<IActionResult> GetNotifications(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var since = DateTime.UtcNow.AddDays(-RecentDays);
        var isParent = User.IsInRole(Roles.Parent);
        var userId = User.GetUserId();
        var classIds = access.VisibleClassIds(User);
        var studentIds = access.VisibleStudentIds(User);
        var items = new List<NotificationItem>();

        var unreadMessages = await db.Messages
            .Where(item => studentIds.Contains(item.StudentId) && item.ReadAt == null && (isParent ? !item.SentByParent : item.SentByParent && item.TeacherUserId == userId))
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, item.StudentId, item.Student!.ClassId, StudentName = item.Student.FullName, item.SenderName, item.Content, item.CreatedAt })
            .ToListAsync(cancellationToken);
        items.AddRange(unreadMessages.Take(5).Select(item => new NotificationItem(
            $"message-{item.Id}", "message", $"Nova poruka: {item.SenderName}",
            $"{item.StudentName} · {Preview(item.Content)}", item.CreatedAt, "Poruke", item.StudentId, item.ClassId)));

        var testLimit = today.AddDays(isParent ? RecentDays : 7);
        var upcomingTests = await db.Tests
            .Where(item => classIds.Contains(item.ClassId) && item.HeldOn >= today && item.HeldOn <= testLimit)
            .OrderBy(item => item.HeldOn).Take(6)
            .Select(item => new { item.Id, item.ClassId, ClassName = item.Class!.Name, item.Title, item.HeldOn })
            .ToListAsync(cancellationToken);
        items.AddRange(upcomingTests.Select(item => new NotificationItem(
            $"test-{item.Id}", "test", item.Title, $"{item.ClassName} · zakazan test",
            item.HeldOn.ToDateTime(TimeOnly.MinValue), "Obaveze", null, item.ClassId)));

        // Podsetnik za domaće zadatke kojima rok ističe u narednih 7 dana.
        var homeworkLimit = today.AddDays(7);
        var homework = await db.Homework
            .Where(item => classIds.Contains(item.ClassId) && item.DueDate >= today && item.DueDate <= homeworkLimit)
            .OrderBy(item => item.DueDate).Take(6)
            .Select(item => new { item.Id, item.ClassId, ClassName = item.Class!.Name, Subject = item.Subject!.Name, item.Title, item.DueDate })
            .ToListAsync(cancellationToken);
        items.AddRange(homework.Select(item => new NotificationItem(
            $"homework-{item.Id}", "homework", $"Domaći: {item.Title}", $"{item.ClassName} · {item.Subject}",
            item.DueDate.ToDateTime(TimeOnly.MinValue), "Obaveze", null, item.ClassId)));

        var announcements = await db.Announcements
            .Where(item => (item.ClassId == null || classIds.Contains(item.ClassId.Value)) && item.CreatedAt >= since && item.AuthorUserId != userId)
            .OrderByDescending(item => item.CreatedAt).Take(5)
            .Select(item => new { item.Id, item.ClassId, item.AuthorName, item.Content, item.CreatedAt })
            .ToListAsync(cancellationToken);
        items.AddRange(announcements.Select(item => IsScheduleChange(item.Content)
            ? new NotificationItem($"announcement-{item.Id}", "schedule", "Izmena rasporeda časova", Preview(item.Content), item.CreatedAt, "Raspored", null, item.ClassId)
            : new NotificationItem(
            $"announcement-{item.Id}", "announcement", $"Obaveštenje: {item.AuthorName}",
            Preview(item.Content), item.CreatedAt, "Pregled", null, item.ClassId)));

        if (isParent)
        {
            var sinceDate = DateOnly.FromDateTime(since);
            var grades = await db.Grades
                .Where(item => studentIds.Contains(item.StudentId) && item.GradedOn >= sinceDate)
                .OrderByDescending(item => item.GradedOn).ThenByDescending(item => item.Id).Take(6)
                .Select(item => new { item.Id, item.StudentId, item.Student!.ClassId, StudentName = item.Student.FullName, Subject = item.Subject!.Name, item.Value, item.GradeType, item.GradedOn })
                .ToListAsync(cancellationToken);
            items.AddRange(grades.Select(item => new NotificationItem(
                $"grade-{item.Id}", "grade", $"Nova ocena {item.Value}: {item.Subject}",
                $"{item.StudentName} · {item.GradeType}", item.GradedOn.ToDateTime(TimeOnly.MinValue), "Ocene", item.StudentId, item.ClassId)));

            var absences = await db.Absences
                .Where(item => studentIds.Contains(item.StudentId) && item.Date >= sinceDate)
                .OrderByDescending(item => item.Date).ThenByDescending(item => item.Id).Take(5)
                .Select(item => new { item.Id, item.StudentId, item.Student!.ClassId, StudentName = item.Student.FullName, item.Date, item.LessonHours, item.IsExcused })
                .ToListAsync(cancellationToken);
            items.AddRange(absences.Select(item => new NotificationItem(
                $"absence-{item.Id}-{(item.IsExcused ? "e" : "u")}", "absence", $"Izostanak: {item.LessonHours} {LessonWord(item.LessonHours)}",
                $"{item.StudentName} · {(item.IsExcused ? "opravdan" : "neopravdan")}", item.Date.ToDateTime(TimeOnly.MinValue), "Izostanci i žalbe", item.StudentId, item.ClassId)));

            var answered = await db.Complaints
                .Where(item => studentIds.Contains(item.StudentId) && item.RespondedAt >= since)
                .OrderByDescending(item => item.RespondedAt).Take(5)
                .Select(item => new { item.Id, item.StudentId, item.Student!.ClassId, item.RespondedByName, item.Response, item.RespondedAt })
                .ToListAsync(cancellationToken);
            items.AddRange(answered.Select(item => new NotificationItem(
                $"complaint-{item.Id}-answer", "complaint", $"Odgovor na žalbu: {item.RespondedByName}",
                Preview(item.Response ?? ""), item.RespondedAt!.Value, "Izostanci i žalbe", item.StudentId, item.ClassId)));
        }
        else
        {
            var pending = await db.Complaints
                .Where(item => studentIds.Contains(item.StudentId) && item.Status == Complaint.Pending
                    && item.Student!.Class!.Assignments.Any(assignment => assignment.TeacherUserId == userId && assignment.IsHomeroom))
                .OrderByDescending(item => item.SubmittedAt).Take(10)
                .Select(item => new { item.Id, item.StudentId, item.Student!.ClassId, StudentName = item.Student.FullName, item.ParentName, item.SubmittedAt })
                .ToListAsync(cancellationToken);
            items.AddRange(pending.Select(item => new NotificationItem(
                $"complaint-{item.Id}", "complaint", $"Žalba čeka odgovor: {item.ParentName}",
                item.StudentName, item.SubmittedAt, "Izostanci i žalbe", item.StudentId, item.ClassId)));
        }

        return Ok(new
        {
            UnreadMessages = unreadMessages.Count,
            // Prvo novosti od najnovije, a predstojeći testovi i domaći na kraju, od najbližeg roka.
            Items = items.Where(item => !IsUpcoming(item)).OrderByDescending(item => item.Date)
                .Concat(items.Where(IsUpcoming).OrderBy(item => item.Date))
                .ToList(),
        });
    }

    private static bool IsUpcoming(NotificationItem item) => item.Kind is "test" or "homework";

    // Obaveštenja o izmeni rasporeda pravi ScheduleController sa ovim prefiksom.
    private static bool IsScheduleChange(string content) => content.StartsWith("Izmena rasporeda časova", StringComparison.Ordinal);

    private static string LessonWord(int hours) => hours == 1 ? "čas" : hours < 5 ? "časa" : "časova";

    private static string Preview(string text) => text.Length <= 80 ? text : $"{text[..77]}...";
}
