namespace backend.Models;

public sealed class Student
{
    public int Id { get; set; }
    public required string FullName { get; set; }
    public int ClassId { get; set; }
    public SchoolClass? Class { get; set; }
    public required string RegistrationCode { get; set; }
    public int? ParentUserId { get; set; }
    public ApplicationUser? Parent { get; set; }
}
