using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Authorize]
public sealed class StatisticsController(SchoolDbContext db, AccessService access) : ControllerBase
{
    // Roditelj ne vidi tuđe ocene, ali dobija zbirne podatke: prosek odeljenja i mesto svog deteta.
    [HttpGet("api/students/{studentId:int}/statistics")]
    public async Task<IActionResult> GetStatistics(int studentId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewStudentAsync(User, studentId, cancellationToken)) return Forbid();
        var classId = await db.Students.Where(item => item.Id == studentId).Select(item => item.ClassId).SingleAsync(cancellationToken);
        var subjectIds = await access.VisibleSubjectIdsAsync(User, classId, cancellationToken);
        var (allSubjects, allowedSubjects) = (subjectIds is null, subjectIds ?? []);

        // Veronauka se ocenjuje opisno i ne ulazi u prosek. Predmetni nastavnik dobija podatke samo za svoje predmete.
        var classGrades = await db.Grades
            .Where(grade => grade.Student!.ClassId == classId && !grade.Subject!.UsesDescriptiveScale && (allSubjects || allowedSubjects.Contains(grade.SubjectId)))
            .Select(grade => new { grade.StudentId, Subject = grade.Subject!.Name, grade.Value })
            .ToListAsync(cancellationToken);

        var studentAverages = classGrades.GroupBy(grade => grade.StudentId)
            .ToDictionary(group => group.Key, group => group.Average(grade => grade.Value));
        int? rank = studentAverages.TryGetValue(studentId, out var average)
            ? 1 + studentAverages.Values.Count(other => other > average)
            : null;

        return Ok(new
        {
            Limited = subjectIds is not null,
            ClassAverage = studentAverages.Count == 0 ? (double?)null : Math.Round(studentAverages.Values.Average(), 2),
            Rank = rank,
            RankedStudents = studentAverages.Count,
            Subjects = classGrades.GroupBy(grade => grade.Subject)
                .OrderBy(group => group.Key)
                .Select(group => new { Subject = group.Key, ClassAverage = Math.Round(group.Average(grade => grade.Value), 2) }),
        });
    }

    // Izveštaj o uspehu odeljenja za nastavnike: po učeniku prosek, uspeh i izostanci, plus raspodela uspeha.
    // Uspeh se računa iz zaključnih ocena tekućeg polugodišta; gde zaključna još ne postoji, koristi se predlog iz proseka.
    [Authorize(Roles = Roles.Staff)]
    [HttpGet("api/classes/{classId:int}/report")]
    public async Task<IActionResult> GetClassReport(int classId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewClassAsync(User, classId, cancellationToken)) return Forbid();
        var schoolYear = await db.SchoolYears.OrderBy(item => item.Id).FirstAsync(cancellationToken);
        var term = schoolYear.TermOf(DateOnly.FromDateTime(DateTime.UtcNow));

        // Predmetni nastavnik vidi izveštaj samo za svoje predmete; opšti uspeh vidi razredni starešina.
        var subjectIds = await access.VisibleSubjectIdsAsync(User, classId, cancellationToken);
        var (allSubjects, allowedSubjects) = (subjectIds is null, subjectIds ?? []);
        var limited = subjectIds is not null;

        var students = await db.Students.Where(item => item.ClassId == classId).OrderBy(item => item.FullName)
            .Select(item => new { item.Id, item.FullName }).ToListAsync(cancellationToken);
        var grades = await db.Grades
            .Where(item => item.Student!.ClassId == classId && !item.Subject!.UsesDescriptiveScale && (allSubjects || allowedSubjects.Contains(item.SubjectId)))
            .Select(item => new { item.StudentId, item.SubjectId, Subject = item.Subject!.Name, item.Value })
            .ToListAsync(cancellationToken);
        var finals = await db.FinalGrades
            .Where(item => item.Student!.ClassId == classId && item.Term == term && !item.Subject!.UsesDescriptiveScale && (allSubjects || allowedSubjects.Contains(item.SubjectId)))
            .Select(item => new { item.StudentId, item.SubjectId, item.Value })
            .ToListAsync(cancellationToken);
        var absences = await db.Absences.Where(item => item.Student!.ClassId == classId)
            .GroupBy(item => item.StudentId)
            .Select(group => new { StudentId = group.Key, Total = group.Sum(item => item.LessonHours), Unexcused = group.Where(item => !item.IsExcused).Sum(item => item.LessonHours) })
            .ToListAsync(cancellationToken);

        var rows = students.Select(student =>
        {
            var studentGrades = grades.Where(item => item.StudentId == student.Id).ToList();
            var subjectFinals = studentGrades.GroupBy(item => item.SubjectId).Select(group =>
                finals.FirstOrDefault(item => item.StudentId == student.Id && item.SubjectId == group.Key)?.Value
                ?? (int)Math.Floor(group.Average(item => item.Value) + 0.5)).ToList();
            var absence = absences.FirstOrDefault(item => item.StudentId == student.Id);
            return new
            {
                StudentId = student.Id,
                student.FullName,
                Average = studentGrades.Count == 0 ? (double?)null : Math.Round(studentGrades.Average(item => item.Value), 2),
                Success = limited ? null : Success(subjectFinals),
                RecordedFinals = finals.Count(item => item.StudentId == student.Id),
                AbsenceHours = absence?.Total ?? 0,
                UnexcusedHours = absence?.Unexcused ?? 0,
            };
        }).ToList();

        var averages = rows.Where(row => row.Average is not null).Select(row => row.Average!.Value).ToList();
        return Ok(new
        {
            Term = term,
            Limited = limited,
            ClassAverage = averages.Count == 0 ? (double?)null : Math.Round(averages.Average(), 2),
            AbsenceHours = rows.Sum(row => row.AbsenceHours),
            UnexcusedHours = rows.Sum(row => row.UnexcusedHours),
            Distribution = SuccessLevels.Select(level => new { Success = level, Count = rows.Count(row => row.Success == level) }),
            Subjects = grades.GroupBy(item => item.Subject).OrderBy(group => group.Key)
                .Select(group => new { Subject = group.Key, Average = Math.Round(group.Average(item => item.Value), 2) }),
            Students = rows,
        });
    }

    private static readonly string[] SuccessLevels = ["Odličan", "Vrlo dobar", "Dobar", "Dovoljan", "Nedovoljan"];

    private static string? Success(List<int> finals)
    {
        if (finals.Count == 0) return null;
        if (finals.Contains(1)) return "Nedovoljan";
        var mean = finals.Average();
        return mean >= 4.5 ? "Odličan" : mean >= 3.5 ? "Vrlo dobar" : mean >= 2.5 ? "Dobar" : "Dovoljan";
    }
}
