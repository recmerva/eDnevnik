namespace backend.Models;

public sealed class Announcement
{
    public int Id { get; set; }
    // null znači da je obaveštenje za celu školu.
    public int? ClassId { get; set; }
    public SchoolClass? Class { get; set; }
    public int? AuthorUserId { get; set; }
    public required string AuthorName { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
}
