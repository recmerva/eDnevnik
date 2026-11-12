namespace backend.Models;

public sealed class Absence
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public DateOnly Date { get; set; }
    public int LessonHours { get; set; }
    public bool IsExcused { get; set; }
    public string? Note { get; set; }
}
