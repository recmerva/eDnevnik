using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/classes")]
[Authorize]
public sealed class ClassesController(SchoolDbContext db, AccessService access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetClasses(CancellationToken cancellationToken)
    {
        var classIds = access.VisibleClassIds(User);
        var userId = User.GetUserId();
        var classes = await db.Classes.Where(item => classIds.Contains(item.Id))
            .OrderBy(item => item.GradeLevel).ThenBy(item => item.Name)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.GradeLevel,
                StudentCount = item.Students.Count,
                HomeroomTeacher = item.Assignments.Where(assignment => assignment.IsHomeroom)
                    .Select(assignment => assignment.Teacher!.FullName)
                    .FirstOrDefault(),
                IsMyHomeroom = item.Assignments.Any(assignment => assignment.TeacherUserId == userId && assignment.IsHomeroom),
                MySubjectIds = item.Assignments.Where(assignment => assignment.TeacherUserId == userId && assignment.SubjectId != null)
                    .Select(assignment => assignment.SubjectId!.Value)
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        // Predmeti koje nastavnik sme da ocenjuje u odeljenju; administrator i roditelj ne ocenjuju.
        var isTeacher = User.IsInRole(Roles.Teacher);
        return Ok(classes.Select(item => new
        {
            item.Id,
            item.Name,
            item.GradeLevel,
            item.StudentCount,
            item.HomeroomTeacher,
            TeachableSubjectIds = isTeacher ? item.MySubjectIds : new List<int>(),
            // Razredni starešina opravdava izostanke i odgovara na žalbe.
            IsMyHomeroom = isTeacher && item.IsMyHomeroom,
        }));
    }

    [HttpGet("{classId:int}/students")]
    public async Task<IActionResult> GetStudents(int classId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewClassAsync(User, classId, cancellationToken)) return Forbid();

        // Roditelj vidi samo imena drugara iz odeljenja, a osoblje i ko je roditelj.
        var showParent = User.IsStaff();
        var students = await db.Students.Where(student => student.ClassId == classId)
            .OrderBy(student => student.FullName)
            .Select(student => new
            {
                student.Id,
                student.FullName,
                ClassName = student.Class!.Name,
                ParentName = showParent && student.Parent != null ? student.Parent.FullName : null,
            })
            .ToListAsync(cancellationToken);
        return Ok(students);
    }
}
