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
public sealed class NotesController(SchoolDbContext db, AccessService access, ActivityService activity) : ControllerBase
{
    [HttpGet("api/students/{studentId:int}/notes")]
    public async Task<IActionResult> GetNotes(int studentId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewStudentAsync(User, studentId, cancellationToken)) return Forbid();
        var userId = User.GetUserId();
        var notes = await db.StudentNotes.Where(item => item.StudentId == studentId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new { item.Id, item.AuthorName, item.Content, item.CreatedAt, CanDelete = item.AuthorUserId == userId })
            .ToListAsync(cancellationToken);
        return Ok(notes);
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpPost("api/students/{studentId:int}/notes")]
    public async Task<IActionResult> AddNote(int studentId, NoteRequest request, CancellationToken cancellationToken)
    {
        if (!await access.CanEditStudentAsync(User, studentId, cancellationToken)) return Forbid();
        var note = new StudentNote
        {
            StudentId = studentId,
            AuthorUserId = User.GetUserId(),
            AuthorName = User.GetFullName(),
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.StudentNotes.Add(note);
        var studentName = await db.Students.Where(item => item.Id == studentId).Select(item => item.FullName).SingleAsync(cancellationToken);
        activity.Log("Beleška o učeniku", $"{studentName}: {note.Content}");
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/students/{studentId}/notes", new { note.Id, note.AuthorName, note.Content, note.CreatedAt, CanDelete = true });
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpDelete("api/notes/{noteId:int}")]
    public async Task<IActionResult> DeleteNote(int noteId, CancellationToken cancellationToken)
    {
        var note = await db.StudentNotes.SingleOrDefaultAsync(item => item.Id == noteId, cancellationToken);
        if (note is null) return NotFound(new { message = "Beleška ne postoji." });
        if (note.AuthorUserId != User.GetUserId()) return Forbid();
        db.StudentNotes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
