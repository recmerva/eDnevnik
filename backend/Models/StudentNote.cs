namespace backend.Models;

// Beleška nastavnika o učeniku (vladanje, napredak, zapažanja); vidi je i roditelj.
public sealed class StudentNote
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int? AuthorUserId { get; set; }
    public required string AuthorName { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
}
