namespace backend.Models;

public sealed class Homework
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public SchoolClass? Class { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateOnly DueDate { get; set; }
    public int? AuthorUserId { get; set; }
    public required string AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
}
