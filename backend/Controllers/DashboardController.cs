using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

// Početna strana osoblja: nastavnik vidi svoje časove u nedelji, administrator raspored cele škole
// i spisak stvari koje još treba podesiti (odeljenja bez razrednog, predmeti bez nastavnika).
[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = Roles.Staff)]
public sealed class DashboardController(SchoolDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var isAdmin = User.IsInRole(Roles.Administrator);
        var assignments = await db.TeacherAssignments.Where(item => item.SubjectId != null)
            .Select(item => new { item.TeacherUserId, item.ClassId, SubjectId = item.SubjectId!.Value, item.Teacher!.FullName })
            .ToListAsync(cancellationToken);
        var entries = await db.Schedule
            .Select(item => new { item.Day, item.LessonNumber, item.ClassId, ClassName = item.Class!.Name, item.Class.GradeLevel, item.SubjectId, Subject = item.Subject!.Name })
            .ToListAsync(cancellationToken);

        var lessons = entries
            .Select(entry => new
            {
                entry.Day,
                entry.LessonNumber,
                entry.ClassId,
                entry.ClassName,
                entry.GradeLevel,
                entry.Subject,
                Teacher = assignments.FirstOrDefault(item => item.ClassId == entry.ClassId && item.SubjectId == entry.SubjectId)?.FullName,
                Mine = assignments.Any(item => item.ClassId == entry.ClassId && item.SubjectId == entry.SubjectId && item.TeacherUserId == userId),
            })
            .Where(entry => isAdmin || entry.Mine)
            .OrderBy(entry => entry.Day).ThenBy(entry => entry.LessonNumber).ThenBy(entry => entry.GradeLevel).ThenBy(entry => entry.ClassName)
            .ToList();

        if (!isAdmin) return Ok(new { Lessons = lessons, Attention = Array.Empty<object>(), Counts = (object?)null });

        var classes = await db.Classes.OrderBy(item => item.GradeLevel).ThenBy(item => item.Name)
            .Select(item => new { item.Id, item.Name, HasHomeroom = item.Assignments.Any(assignment => assignment.IsHomeroom) })
            .ToListAsync(cancellationToken);
        var attention = new List<object>();
        foreach (var schoolClass in classes)
        {
            if (!schoolClass.HasHomeroom) attention.Add(new { Kind = "homeroom", ClassId = schoolClass.Id, Text = $"{schoolClass.Name}: nema razrednog starešinu" });
            var missing = entries.Where(entry => entry.ClassId == schoolClass.Id)
                .Select(entry => entry.Subject).Distinct()
                .Where(subject => !lessons.Any(lesson => lesson.ClassId == schoolClass.Id && lesson.Subject == subject && lesson.Teacher is not null))
                .Order().ToList();
            if (missing.Count > 0) attention.Add(new { Kind = "subject", ClassId = schoolClass.Id, Text = $"{schoolClass.Name}: bez nastavnika – {string.Join(", ", missing)}" });
        }

        return Ok(new
        {
            Lessons = lessons,
            Attention = attention,
            Counts = new
            {
                Teachers = await db.Users.CountAsync(item => item.Role == Roles.Teacher, cancellationToken),
                Students = await db.Students.CountAsync(cancellationToken),
                LinkedStudents = await db.Students.CountAsync(item => item.ParentUserId != null, cancellationToken),
            },
        });
    }
}
