namespace backend.Models;

public sealed class SchoolTest
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public SchoolClass? Class { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public required string Title { get; set; }
    public DateOnly HeldOn { get; set; }
}
