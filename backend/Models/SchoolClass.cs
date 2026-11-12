namespace backend.Models;

public sealed class SchoolClass
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int GradeLevel { get; set; }

    public List<Student> Students { get; set; } = [];
    public List<TeacherClassAssignment> Assignments { get; set; } = [];
}
