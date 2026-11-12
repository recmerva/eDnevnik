namespace backend.Models;

public sealed class Subject
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool UsesDescriptiveScale { get; set; }
}
