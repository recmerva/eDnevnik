using backend.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

// Nastavnici po odeljenjima, onako kako ih škola vodi: za svaki predmet u odeljenju jedan nastavnik,
// plus razredni starešina (u I–IV razredu učitelj, koji predaje sve predmete osim engleskog i veronauke).
// Dodela ostaje dok je administrator ne promeni ili ne ukloni.
[ApiController]
[Route("api/admin/staff")]
[Authorize(Roles = Roles.Administrator)]
public sealed class StaffController(SchoolDbContext db, ActivityService activity) : ControllerBase
{
    private static readonly string[] NotTaughtByClassTeacher = ["Engleski jezik", "Veronauka"];

    [HttpGet]
    public async Task<IActionResult> GetStaff(CancellationToken cancellationToken)
    {
        var classes = await db.Classes.OrderBy(item => item.GradeLevel).ThenBy(item => item.Name)
            .Select(item => new { item.Id, item.Name, item.GradeLevel }).ToListAsync(cancellationToken);
        var assignments = await db.TeacherAssignments
            .Select(item => new { item.ClassId, item.SubjectId, item.TeacherUserId, item.Teacher!.FullName, item.IsHomeroom })
            .ToListAsync(cancellationToken);
        var lessons = await db.Schedule.GroupBy(item => new { item.ClassId, item.SubjectId })
            .Select(group => new { group.Key.ClassId, group.Key.SubjectId, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var subjects = await db.Subjects.ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var teachers = await db.Users.Where(item => item.Role == Roles.Teacher).OrderBy(item => item.FullName)
            .Select(item => new { item.Id, item.FullName }).ToListAsync(cancellationToken);

        return Ok(new
        {
            Teachers = teachers.Select(teacher => new
            {
                teacher.Id,
                teacher.FullName,
                SubjectIds = assignments.Where(item => item.TeacherUserId == teacher.Id && item.SubjectId != null)
                    .Select(item => item.SubjectId!.Value).Distinct().ToList(),
            }),
            Classes = classes.Select(schoolClass =>
            {
                var mine = assignments.Where(item => item.ClassId == schoolClass.Id).ToList();
                var homeroom = mine.FirstOrDefault(item => item.IsHomeroom);
                // Predmeti odeljenja: oni iz rasporeda i oni kojima je već dodeljen nastavnik.
                var subjectIds = lessons.Where(item => item.ClassId == schoolClass.Id).Select(item => item.SubjectId)
                    .Concat(mine.Where(item => item.SubjectId != null).Select(item => item.SubjectId!.Value))
                    .Distinct();
                return new
                {
                    ClassId = schoolClass.Id,
                    schoolClass.Name,
                    schoolClass.GradeLevel,
                    HomeroomId = homeroom?.TeacherUserId,
                    HomeroomName = homeroom?.FullName,
                    Subjects = subjectIds.Select(subjectId =>
                    {
                        var teacher = mine.FirstOrDefault(item => item.SubjectId == subjectId);
                        return new
                        {
                            SubjectId = subjectId,
                            Subject = subjects[subjectId],
                            WeeklyLessons = lessons.FirstOrDefault(item => item.ClassId == schoolClass.Id && item.SubjectId == subjectId)?.Count ?? 0,
                            TeacherId = teacher?.TeacherUserId,
                            TeacherName = teacher?.FullName,
                        };
                    }).OrderBy(item => item.Subject).ToList(),
                };
            }),
        });
    }

    // Nastavnik predmeta u odeljenju; null uklanja dodelu.
    [HttpPut("{classId:int}/subjects/{subjectId:int}")]
    public async Task<IActionResult> SetSubjectTeacher(int classId, int subjectId, StaffChangeRequest request, CancellationToken cancellationToken)
    {
        var schoolClass = await db.Classes.SingleOrDefaultAsync(item => item.Id == classId, cancellationToken);
        var subject = await db.Subjects.SingleOrDefaultAsync(item => item.Id == subjectId, cancellationToken);
        if (schoolClass is null || subject is null) return NotFound(new { message = "Odeljenje ili predmet ne postoje." });
        var teacher = await FindTeacherAsync(request.TeacherUserId, cancellationToken);
        if (request.TeacherUserId is not null && teacher is null) return BadRequest(new { message = "Izabrani nastavnik ne postoji." });

        var rows = await db.TeacherAssignments.Where(item => item.ClassId == classId).ToListAsync(cancellationToken);
        var current = rows.Where(item => item.SubjectId == subjectId).ToList();
        if (current.Count == 1 && current[0].TeacherUserId == teacher?.Id) return NoContent();

        foreach (var row in current) RemoveKeepingHomeroom(row, rows);
        if (teacher is not null)
        {
            // Ako je nastavnik već razredni bez predmeta u ovom odeljenju, taj red dobija predmet.
            var homeroomOnly = rows.FirstOrDefault(item => item.TeacherUserId == teacher.Id && item.SubjectId == null);
            if (homeroomOnly is not null) homeroomOnly.SubjectId = subjectId;
            else db.TeacherAssignments.Add(new TeacherClassAssignment { TeacherUserId = teacher.Id, ClassId = classId, SubjectId = subjectId });
        }

        activity.Log("Nastavnik predmeta", $"{schoolClass.Name}, {subject.Name}: {teacher?.FullName ?? "uklonjen nastavnik"}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    // Razredni starešina; u I–IV razredu to je učitelj, pa dobija i sve predmete osim engleskog i veronauke.
    [HttpPut("{classId:int}/homeroom")]
    public async Task<IActionResult> SetHomeroom(int classId, StaffChangeRequest request, CancellationToken cancellationToken)
    {
        var schoolClass = await db.Classes.SingleOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (schoolClass is null) return NotFound(new { message = "Odeljenje ne postoji." });
        var teacher = await FindTeacherAsync(request.TeacherUserId, cancellationToken);
        if (request.TeacherUserId is not null && teacher is null) return BadRequest(new { message = "Izabrani nastavnik ne postoji." });

        var rows = await db.TeacherAssignments.Where(item => item.ClassId == classId).ToListAsync(cancellationToken);
        var previousId = rows.FirstOrDefault(item => item.IsHomeroom)?.TeacherUserId;
        if (previousId == teacher?.Id) return NoContent();

        foreach (var row in rows.Where(item => item.IsHomeroom)) row.IsHomeroom = false;
        foreach (var row in rows.Where(item => item.SubjectId == null).ToList()) { db.TeacherAssignments.Remove(row); rows.Remove(row); }

        var classTeacher = schoolClass.GradeLevel <= 4;
        if (classTeacher)
        {
            var excluded = await db.Subjects.Where(item => NotTaughtByClassTeacher.Contains(item.Name)).Select(item => item.Id).ToListAsync(cancellationToken);
            var scheduled = await db.Schedule.Where(item => item.ClassId == classId).Select(item => item.SubjectId).Distinct().ToListAsync(cancellationToken);
            // Predmeti prethodnog učitelja i oni bez nastavnika prelaze na novog učitelja (ili ostaju bez nastavnika).
            var classSubjects = scheduled.Concat(rows.Where(item => item.SubjectId != null).Select(item => item.SubjectId!.Value))
                .Distinct().Where(id => !excluded.Contains(id)).ToList();
            foreach (var subjectId in classSubjects)
            {
                var taught = rows.Where(item => item.SubjectId == subjectId).ToList();
                var byPrevious = taught.Where(item => item.TeacherUserId == previousId).ToList();
                if (taught.Count > 0 && byPrevious.Count == 0) continue; // predmet ima drugog nastavnika, ne dira se
                foreach (var row in byPrevious) { db.TeacherAssignments.Remove(row); rows.Remove(row); }
                if (teacher is null) continue;
                var added = new TeacherClassAssignment { TeacherUserId = teacher.Id, ClassId = classId, SubjectId = subjectId };
                db.TeacherAssignments.Add(added);
                rows.Add(added);
            }
        }

        if (teacher is not null)
        {
            var own = rows.FirstOrDefault(item => item.TeacherUserId == teacher.Id);
            if (own is not null) own.IsHomeroom = true;
            else db.TeacherAssignments.Add(new TeacherClassAssignment { TeacherUserId = teacher.Id, ClassId = classId, IsHomeroom = true });
        }

        var role = classTeacher ? "Učitelj" : "Razredni starešina";
        activity.Log(role, $"{schoolClass.Name}: {teacher?.FullName ?? "uklonjen"}.");
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private Task<ApplicationUser?> FindTeacherAsync(int? teacherId, CancellationToken cancellationToken) =>
        teacherId is null
            ? Task.FromResult<ApplicationUser?>(null)
            : db.Users.SingleOrDefaultAsync(item => item.Id == teacherId && item.Role == Roles.Teacher, cancellationToken);

    // Kad se ukloni predmet razrednom starešini, starešinstvo ostaje (na drugom njegovom redu ili kao red bez predmeta).
    private void RemoveKeepingHomeroom(TeacherClassAssignment row, List<TeacherClassAssignment> rows)
    {
        rows.Remove(row);
        if (row.IsHomeroom)
        {
            var other = rows.FirstOrDefault(item => item.TeacherUserId == row.TeacherUserId);
            if (other is not null) other.IsHomeroom = true;
            else
            {
                row.SubjectId = null;
                rows.Add(row);
                return;
            }
        }
        db.TeacherAssignments.Remove(row);
    }
}
