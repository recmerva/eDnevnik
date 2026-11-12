namespace backend.Models;

// Zaključna ocena iz predmeta za jedno polugodište (1 ili 2).
public sealed class FinalGrade
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public int Term { get; set; }
    public int Value { get; set; }
    public required string RecordedByName { get; set; }
    public DateTime RecordedAt { get; set; }
}
