using backend.Data;
using backend.Models;

namespace backend.Services;

// Dodaje zapis u dnevnik aktivnosti; čuva se zajedno sa sledećim SaveChanges pozivom.
public sealed class ActivityService(SchoolDbContext db, IHttpContextAccessor httpContextAccessor)
{
    public void Log(string action, string details)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var isSignedIn = user?.Identity?.IsAuthenticated == true;
        Log(action, details, isSignedIn ? user!.GetUserId() : null, isSignedIn ? user!.GetFullName() : "Sistem");
    }

    public void Log(string action, string details, int? userId, string userName) =>
        db.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            Details = details.Length > 500 ? details[..500] : details,
            CreatedAt = DateTime.UtcNow,
        });
}
