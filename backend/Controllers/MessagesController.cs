using backend.Contracts;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

// Razgovor uvek vode roditelj i jedan nastavnik, u vezi sa jednim učenikom.
// Roditelj može da piše svakom nastavniku koji predaje u odeljenju njegovog deteta,
// a nastavnik roditeljima učenika iz svojih odeljenja.
[ApiController]
[Authorize]
public sealed class MessagesController(SchoolDbContext db, AccessService access) : ControllerBase
{
    [HttpGet("api/conversations")]
    public async Task<IActionResult> GetConversations(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (User.IsInRole(Roles.Parent)) return Ok(await ParentConversationsAsync(userId, cancellationToken));

        var studentIds = access.VisibleStudentIds(User);
        var threads = await db.Messages
            .Where(item => item.TeacherUserId == userId && studentIds.Contains(item.StudentId))
            .GroupBy(item => item.StudentId)
            .Select(group => new
            {
                StudentId = group.Key,
                LastAt = group.Max(item => item.CreatedAt),
                Unread = group.Count(item => item.SentByParent && item.ReadAt == null),
            })
            .ToListAsync(cancellationToken);
        var ids = threads.Select(item => item.StudentId).ToList();
        var students = await db.Students.Where(item => ids.Contains(item.Id))
            .Select(item => new { item.Id, item.FullName, item.ClassId, ClassName = item.Class!.Name, ParentName = item.Parent != null ? item.Parent.FullName : null })
            .ToListAsync(cancellationToken);
        var lastMessages = await LastMessagesAsync(db.Messages.Where(item => item.TeacherUserId == userId && ids.Contains(item.StudentId)), cancellationToken);

        return Ok(threads.OrderByDescending(item => item.LastAt).Select(thread =>
        {
            var student = students.Single(item => item.Id == thread.StudentId);
            var last = lastMessages.GetValueOrDefault((thread.StudentId, (int?)userId));
            return new
            {
                Key = $"{thread.StudentId}",
                thread.StudentId,
                StudentName = student.FullName,
                student.ClassId,
                student.ClassName,
                TeacherId = (int?)userId,
                CounterpartName = student.ParentName ?? "Roditelj više nije povezan",
                CounterpartRole = $"Roditelj · {student.FullName}, {student.ClassName}",
                LastMessage = last?.Content,
                LastFromMe = last is not null && !last.SentByParent,
                LastAt = (DateTime?)thread.LastAt,
                thread.Unread,
                CanWrite = student.ParentName is not null,
            };
        }));
    }

    // Roditelju se prikazuju svi nastavnici odeljenja (razredni prvi), i oni sa kojima još nije pisao.
    private async Task<List<object>> ParentConversationsAsync(int parentId, CancellationToken cancellationToken)
    {
        var children = await db.Students.Where(item => item.ParentUserId == parentId)
            .Select(item => new { item.Id, item.FullName, item.ClassId, ClassName = item.Class!.Name })
            .ToListAsync(cancellationToken);
        var classIds = children.Select(item => item.ClassId).ToList();
        var childIds = children.Select(item => item.Id).ToList();
        var assignments = await db.TeacherAssignments.Where(item => classIds.Contains(item.ClassId))
            .Select(item => new { item.ClassId, item.TeacherUserId, item.Teacher!.FullName, item.IsHomeroom, Subject = item.Subject != null ? item.Subject.Name : null })
            .ToListAsync(cancellationToken);
        var threads = await db.Messages.Where(item => childIds.Contains(item.StudentId))
            .GroupBy(item => new { item.StudentId, item.TeacherUserId, item.TeacherName })
            .Select(group => new
            {
                group.Key.StudentId,
                group.Key.TeacherUserId,
                group.Key.TeacherName,
                LastAt = group.Max(item => item.CreatedAt),
                Unread = group.Count(item => !item.SentByParent && item.ReadAt == null),
            })
            .ToListAsync(cancellationToken);
        var lastMessages = await LastMessagesAsync(db.Messages.Where(item => childIds.Contains(item.StudentId)), cancellationToken);

        var result = new List<(object Item, bool Homeroom, DateTime? LastAt, string Name)>();
        foreach (var child in children)
        {
            var teachers = assignments.Where(item => item.ClassId == child.ClassId).GroupBy(item => new { item.TeacherUserId, item.FullName });
            foreach (var teacher in teachers)
            {
                var homeroom = teacher.Any(item => item.IsHomeroom);
                var subjects = teacher.Where(item => item.Subject is not null).Select(item => item.Subject!).Order().ToList();
                // Učitelj u mlađim razredima predaje gotovo sve predmete, pa se umesto spiska piše "razredna nastava".
                var taught = subjects.Count > 3 ? "razredna nastava" : subjects.Count > 0 ? string.Join(", ", subjects) : null;
                var role = string.Join(" · ", new[] { homeroom ? "Razredni starešina" : null, taught }.Where(part => part is not null));
                var thread = threads.FirstOrDefault(item => item.StudentId == child.Id && item.TeacherUserId == teacher.Key.TeacherUserId);
                result.Add((Conversation(child.Id, child.FullName, child.ClassId, child.ClassName, teacher.Key.TeacherUserId, teacher.Key.FullName, role,
                    lastMessages.GetValueOrDefault((child.Id, teacher.Key.TeacherUserId)), thread?.LastAt, thread?.Unread ?? 0, true),
                    homeroom, thread?.LastAt, teacher.Key.FullName));
            }
            // Ranije poruke sa nastavnikom koji više ne predaje odeljenju ostaju dostupne za čitanje.
            foreach (var thread in threads.Where(item => item.StudentId == child.Id && !assignments.Any(a => a.ClassId == child.ClassId && a.TeacherUserId == item.TeacherUserId)))
            {
                result.Add((Conversation(child.Id, child.FullName, child.ClassId, child.ClassName, thread.TeacherUserId, thread.TeacherName, "Više ne predaje odeljenju",
                    lastMessages.GetValueOrDefault((child.Id, thread.TeacherUserId)), thread.LastAt, thread.Unread, false), false, thread.LastAt, thread.TeacherName));
            }
        }
        return result
            .OrderByDescending(item => item.LastAt is not null).ThenByDescending(item => item.Homeroom).ThenByDescending(item => item.LastAt).ThenBy(item => item.Name)
            .Select(item => item.Item).ToList();
    }

    private static object Conversation(int studentId, string studentName, int classId, string className, int? teacherId, string teacherName, string role,
        Message? last, DateTime? lastAt, int unread, bool canWrite) => new
    {
        Key = $"{studentId}-{teacherId?.ToString() ?? "x"}",
        StudentId = studentId,
        StudentName = studentName,
        ClassId = classId,
        ClassName = className,
        TeacherId = teacherId,
        CounterpartName = teacherName,
        CounterpartRole = role,
        LastMessage = last?.Content,
        LastFromMe = last?.SentByParent ?? false,
        LastAt = lastAt,
        Unread = unread,
        CanWrite = canWrite,
    };

    private static async Task<Dictionary<(int, int?), Message>> LastMessagesAsync(IQueryable<Message> messages, CancellationToken cancellationToken)
    {
        var latestIds = await messages.GroupBy(item => new { item.StudentId, item.TeacherUserId })
            .Select(group => group.Max(item => item.Id)).ToListAsync(cancellationToken);
        var latest = await messages.Where(item => latestIds.Contains(item.Id)).ToListAsync(cancellationToken);
        return latest.ToDictionary(item => (item.StudentId, item.TeacherUserId));
    }

    [HttpGet("api/students/{studentId:int}/messages")]
    public async Task<IActionResult> GetMessages(int studentId, [FromQuery] int? teacherId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewStudentAsync(User, studentId, cancellationToken)) return Forbid();
        var userId = User.GetUserId();
        var partnerId = User.IsInRole(Roles.Parent) ? teacherId : userId;
        var messages = await db.Messages.Where(item => item.StudentId == studentId && item.TeacherUserId == partnerId)
            .OrderBy(item => item.CreatedAt).ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.SenderName, item.SentByParent, item.Content, item.CreatedAt, item.ReadAt, IsMine = item.SenderUserId == userId })
            .ToListAsync(cancellationToken);
        return Ok(messages);
    }

    [HttpPost("api/students/{studentId:int}/messages")]
    public async Task<IActionResult> SendMessage(int studentId, MessageRequest request, CancellationToken cancellationToken)
    {
        if (!await access.CanViewStudentAsync(User, studentId, cancellationToken)) return Forbid();
        // Roditeljima pišu nastavnici; administrator ne vodi razgovore o učenicima.
        if (User.IsInRole(Roles.Administrator)) return StatusCode(StatusCodes.Status403Forbidden, new { message = "Poruke roditeljima šalju nastavnici odeljenja." });
        var isParent = User.IsInRole(Roles.Parent);
        var student = await db.Students.Where(item => item.Id == studentId)
            .Select(item => new { item.ClassId, HasParent = item.ParentUserId != null })
            .SingleAsync(cancellationToken);

        string teacherName;
        int teacherId;
        if (isParent)
        {
            var teacher = await db.TeacherAssignments
                .Where(item => item.ClassId == student.ClassId && item.TeacherUserId == request.TeacherUserId)
                .Select(item => new { item.TeacherUserId, item.Teacher!.FullName })
                .FirstOrDefaultAsync(cancellationToken);
            if (teacher is null) return BadRequest(new { message = "Izaberite nastavnika koji predaje u odeljenju vašeg deteta." });
            (teacherId, teacherName) = (teacher.TeacherUserId, teacher.FullName);
        }
        else
        {
            if (!student.HasParent) return Conflict(new { message = "Učenik još nema povezan roditeljski nalog, pa poruka ne bi bila pročitana." });
            (teacherId, teacherName) = (User.GetUserId(), User.GetFullName());
        }

        var message = new Message
        {
            StudentId = studentId,
            TeacherUserId = teacherId,
            TeacherName = teacherName,
            SenderUserId = User.GetUserId(),
            SenderName = User.GetFullName(),
            SentByParent = isParent,
            Content = request.Content.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/students/{studentId}/messages", new
        {
            message.Id,
            message.SenderName,
            message.SentByParent,
            message.Content,
            message.CreatedAt,
            message.ReadAt,
            IsMine = true,
        });
    }

    // Otvaranjem razgovora označavaju se kao pročitane poruke druge strane.
    [HttpPost("api/students/{studentId:int}/messages/read")]
    public async Task<IActionResult> MarkAsRead(int studentId, [FromQuery] int? teacherId, CancellationToken cancellationToken)
    {
        if (!await access.CanViewStudentAsync(User, studentId, cancellationToken)) return Forbid();
        var readerIsParent = User.IsInRole(Roles.Parent);
        var partnerId = readerIsParent ? teacherId : User.GetUserId();
        var now = DateTime.UtcNow;
        var updated = await db.Messages
            .Where(item => item.StudentId == studentId && item.TeacherUserId == partnerId && item.ReadAt == null && item.SentByParent != readerIsParent)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, now), cancellationToken);
        return Ok(new { updated });
    }
}
