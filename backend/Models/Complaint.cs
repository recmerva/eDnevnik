namespace backend.Models;

public sealed class Complaint
{
    public const string Pending = "Na čekanju";
    public const string Resolved = "Rešeno";

    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int? ParentUserId { get; set; }
    public required string ParentName { get; set; }
    public required string Content { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string Status { get; set; } = Pending;
    public string? Response { get; set; }
    public string? RespondedByName { get; set; }
    public DateTime? RespondedAt { get; set; }
}
