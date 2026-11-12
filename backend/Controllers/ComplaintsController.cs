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
public sealed class ComplaintsController(SchoolDbContext db, AccessService access, ActivityService activity) : ControllerBase
{
    [HttpGet("api/students/{studentId:int}/complaints")]
    public async Task<IActionResult> GetComplaints(int studentId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewStudentAsync(User, studentId, cancellationToken)) return Forbid();
        var complaints = await db.Complaints.Where(item => item.StudentId == studentId)
            .OrderByDescending(item => item.SubmittedAt)
            .Select(item => new
            {
                item.Id,
                item.ParentName,
                item.Content,
                item.SubmittedAt,
                item.Status,
                item.Response,
                item.RespondedByName,
                item.RespondedAt,
            })
            .ToListAsync(cancellationToken);
        return Ok(complaints);
    }

    [Authorize(Roles = Roles.Parent)]
    [HttpPost("api/students/{studentId:int}/complaints")]
    public async Task<IActionResult> SubmitComplaint(int studentId, ComplaintRequest request, CancellationToken cancellationToken)
    {
        if (!await access.CanViewStudentAsync(User, studentId, cancellationToken)) return Forbid();
        var complaint = new Complaint
        {
            StudentId = studentId,
            ParentUserId = User.GetUserId(),
            ParentName = User.GetFullName(),
            Content = request.Content.Trim(),
            SubmittedAt = DateTime.UtcNow,
        };
        db.Complaints.Add(complaint);
        activity.Log("Žalba roditelja", complaint.Content);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/students/{studentId}/complaints", new
        {
            complaint.Id,
            complaint.ParentName,
            complaint.Content,
            complaint.SubmittedAt,
            complaint.Status,
            complaint.Response,
            complaint.RespondedByName,
            complaint.RespondedAt,
        });
    }

    [Authorize(Roles = Roles.Staff)]
    [HttpPut("api/complaints/{complaintId:int}")]
    public async Task<IActionResult> Respond(int complaintId, ComplaintResponseRequest request, CancellationToken cancellationToken)
    {
        var complaint = await db.Complaints.SingleOrDefaultAsync(item => item.Id == complaintId, cancellationToken);
        if (complaint is null) return NotFound(new { message = "Žalba ne postoji." });
        if (!await access.IsHomeroomOfStudentAsync(User, complaint.StudentId, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Na žalbe roditelja odgovara razredni starešina odeljenja." });
        }

        complaint.Response = request.Response.Trim();
        complaint.RespondedByName = User.GetFullName();
        complaint.RespondedAt = DateTime.UtcNow;
        complaint.Status = Complaint.Resolved;
        activity.Log("Odgovor na žalbu", complaint.Response);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            complaint.Id,
            complaint.ParentName,
            complaint.Content,
            complaint.SubmittedAt,
            complaint.Status,
            complaint.Response,
            complaint.RespondedByName,
            complaint.RespondedAt,
        });
    }
}
