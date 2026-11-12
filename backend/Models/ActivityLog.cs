namespace backend.Models;

// Zapis o važnim radnjama u sistemu, koje administrator pregleda.
public sealed class ActivityLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public required string UserName { get; set; }
    public required string Action { get; set; }
    public required string Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
