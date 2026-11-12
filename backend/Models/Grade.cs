namespace backend.Models;

public sealed class Grade
{
    public static readonly string[] Types = ["Usmeno odgovaranje", "Aktivnost na času", "Kontrolni zadatak", "Pismeni zadatak"];
    public static readonly string[] DescriptiveScale = ["Nezadovoljavajući", "Zadovoljavajući", "Dobar", "Vrlo dobar", "Odličan"];

    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public int Value { get; set; }
    public required string GradeType { get; set; }
    public DateOnly GradedOn { get; set; }
}
