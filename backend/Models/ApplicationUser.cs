namespace backend.Models;

public sealed class ApplicationUser
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Student> Children { get; set; } = [];
    public List<TeacherClassAssignment> Assignments { get; set; } = [];
}
