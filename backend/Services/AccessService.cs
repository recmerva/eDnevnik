using System.Security.Claims;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

// Sva pravila "ko sme da vidi/menja šta" su na jednom mestu da ne bi bila rasuta po kontrolerima.
public sealed class AccessService(SchoolDbContext db)
{
    public IQueryable<int> VisibleClassIds(ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        if (user.IsInRole(Roles.Administrator)) return db.Classes.Select(item => item.Id);
        if (user.IsInRole(Roles.Teacher))
        {
            return db.TeacherAssignments.Where(item => item.TeacherUserId == userId).Select(item => item.ClassId);
        }
        return db.Students.Where(item => item.ParentUserId == userId).Select(item => item.ClassId);
    }

    public IQueryable<int> EditableClassIds(ClaimsPrincipal user)
    {
        var userId = user.GetUserId();
        if (user.IsInRole(Roles.Administrator)) return db.Classes.Select(item => item.Id);
        if (user.IsInRole(Roles.Teacher))
        {
            return db.TeacherAssignments.Where(item => item.TeacherUserId == userId).Select(item => item.ClassId);
        }
        return db.Classes.Where(_ => false).Select(item => item.Id);
    }

    // Roditelj vidi samo svoje dete, osoblje vidi sve učenike svojih odeljenja.
    public IQueryable<int> VisibleStudentIds(ClaimsPrincipal user)
    {
        if (user.IsInRole(Roles.Parent))
        {
            var userId = user.GetUserId();
            return db.Students.Where(item => item.ParentUserId == userId).Select(item => item.Id);
        }
        var classIds = VisibleClassIds(user);
        return db.Students.Where(item => classIds.Contains(item.ClassId)).Select(item => item.Id);
    }

    public Task<bool> CanViewClassAsync(ClaimsPrincipal user, int classId, CancellationToken cancellationToken) =>
        VisibleClassIds(user).AnyAsync(id => id == classId, cancellationToken);

    public Task<bool> CanEditClassAsync(ClaimsPrincipal user, int classId, CancellationToken cancellationToken) =>
        EditableClassIds(user).AnyAsync(id => id == classId, cancellationToken);

    public Task<bool> CanViewStudentAsync(ClaimsPrincipal user, int studentId, CancellationToken cancellationToken) =>
        VisibleStudentIds(user).AnyAsync(id => id == studentId, cancellationToken);

    // Izostanke i beleške o učeniku upisuju samo nastavnici; administrator ima samo uvid.
    public Task<bool> CanEditStudentAsync(ClaimsPrincipal user, int studentId, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(Roles.Teacher)) return Task.FromResult(false);
        var classIds = EditableClassIds(user);
        return db.Students.AnyAsync(item => item.Id == studentId && classIds.Contains(item.ClassId), cancellationToken);
    }

    // Ocene, zaključne ocene, testove i domaće iz predmeta unosi samo nastavnik kome je taj predmet dodeljen u odeljenju.
    // Administrator vodi nalog škole, ali ne unosi ocene ni ništa drugo što rade nastavnici.
    public async Task<bool> CanTeachSubjectAsync(ClaimsPrincipal user, int classId, int subjectId, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(Roles.Teacher)) return false;
        var userId = user.GetUserId();
        return await db.TeacherAssignments.AnyAsync(
            item => item.TeacherUserId == userId && item.ClassId == classId && item.SubjectId == subjectId,
            cancellationToken);
    }

    // Samo razredni starešina opravdava izostanke i odgovara na žalbe roditelja.
    public async Task<bool> IsHomeroomOfStudentAsync(ClaimsPrincipal user, int studentId, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(Roles.Teacher)) return false;
        var userId = user.GetUserId();
        return await db.Students.AnyAsync(
            student => student.Id == studentId && student.Class!.Assignments.Any(item => item.TeacherUserId == userId && item.IsHomeroom),
            cancellationToken);
    }

    // Koje ocene osoblje vidi: null znači sve (administrator, razredni starešina/učitelj),
    // a predmetni nastavnik vidi samo predmete koje predaje u tom odeljenju. Roditelj vidi sve ocene svog deteta.
    public async Task<List<int>?> VisibleSubjectIdsAsync(ClaimsPrincipal user, int classId, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(Roles.Teacher)) return null;
        var userId = user.GetUserId();
        var mine = await db.TeacherAssignments.Where(item => item.TeacherUserId == userId && item.ClassId == classId)
            .Select(item => new { item.SubjectId, item.IsHomeroom })
            .ToListAsync(cancellationToken);
        if (mine.Any(item => item.IsHomeroom)) return null;
        return mine.Where(item => item.SubjectId != null).Select(item => item.SubjectId!.Value).ToList();
    }

    public async Task<List<int>?> VisibleSubjectIdsForStudentAsync(ClaimsPrincipal user, int studentId, CancellationToken cancellationToken)
    {
        if (!user.IsInRole(Roles.Teacher)) return null;
        var classId = await db.Students.Where(item => item.Id == studentId).Select(item => item.ClassId).SingleAsync(cancellationToken);
        return await VisibleSubjectIdsAsync(user, classId, cancellationToken);
    }

    public async Task<bool> CanTeachStudentSubjectAsync(ClaimsPrincipal user, int studentId, int subjectId, CancellationToken cancellationToken)
    {
        var classId = await db.Students.Where(item => item.Id == studentId).Select(item => (int?)item.ClassId).SingleOrDefaultAsync(cancellationToken);
        return classId is not null && await CanTeachSubjectAsync(user, classId.Value, subjectId, cancellationToken);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public static string GetFullName(this ClaimsPrincipal user) => user.Identity?.Name ?? "Korisnik škole";

    public static bool IsStaff(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Administrator) || user.IsInRole(Roles.Teacher);
}
