using backend.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/announcements")]
[Authorize]
public sealed class AnnouncementsController(SchoolDbContext db, AccessService access, ActivityService activity) : ControllerBase
{
    // Vraća obaveštenja za celu školu i za odeljenja koja korisnik vidi.
    [HttpGet]
    public async Task<IActionResult> GetAnnouncements([FromQuery] int? classId, CancellationToken cancellationToken)
    {
        if (classId is int id && !await access.CanViewClassAsync(User, id, cancellationToken)) return Forbid();
        var classIds = access.VisibleClassIds(User);
        var query = db.Announcements.Where(item => item.ClassId == null || classIds.Contains(item.ClassId.Value));
        if (classId is not null) query = query.Where(item => item.ClassId == null || item.ClassId == classId);

        var userId = User.GetUserId();
        var canDeleteAll = User.IsInRole(Roles.Administrator);
        var announcements = await query.OrderByDescending(item => item.CreatedAt).Take(20)
            .Select(item => new
            {
                item.Id,
                item.ClassId,
                ClassName = item.Class != null ? item.Class.Name : null,
                item.AuthorName,
                item.Content,
                item.CreatedAt,
                CanDelete = canDeleteAll || item.AuthorUserId == userId,
            })
            .ToListAsync(cancellationToken);
        return Ok(announcements);
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpPost]
    public async Task<IActionResult> Publish(AnnouncementRequest request, CancellationToken cancellationToken)
    {
        if (request.ClassId is null && !User.IsInRole(Roles.Administrator))
        {
            return BadRequest(new { message = "Obaveštenje za celu školu može da objavi samo administrator." });
        }
        if (request.ClassId is int classId && !await access.CanEditClassAsync(User, classId, cancellationToken)) return Forbid();

        var announcement = new Announcement
        {
            ClassId = request.ClassId,
            AuthorUserId = User.GetUserId(),
            AuthorName = User.GetFullName(),
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Announcements.Add(announcement);
        activity.Log("Obaveštenje", announcement.Content);
        await db.SaveChangesAsync(cancellationToken);
        var className = request.ClassId is null
            ? null
            : await db.Classes.Where(item => item.Id == request.ClassId).Select(item => item.Name).SingleAsync(cancellationToken);
        return Created("/api/announcements", new
        {
            announcement.Id,
            announcement.ClassId,
            ClassName = className,
            announcement.AuthorName,
            announcement.Content,
            announcement.CreatedAt,
            CanDelete = true,
        });
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpDelete("{announcementId:int}")]
    public async Task<IActionResult> Delete(int announcementId, CancellationToken cancellationToken)
    {
        var announcement = await db.Announcements.SingleOrDefaultAsync(item => item.Id == announcementId, cancellationToken);
        if (announcement is null) return NotFound(new { message = "Obaveštenje ne postoji." });
        if (!User.IsInRole(Roles.Administrator) && announcement.AuthorUserId != User.GetUserId()) return Forbid();
        db.Announcements.Remove(announcement);
        activity.Log("Obrisano obaveštenje", announcement.Content);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
