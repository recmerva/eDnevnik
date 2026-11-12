namespace backend.Models;

// Razgovor vode roditelj i jedan nastavnik, uvek u vezi sa konkretnim učenikom.
public sealed class Message
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student? Student { get; set; }
    // Nastavnik u razgovoru; ostaje null ako je njegov nalog obrisan, a ime se čuva u TeacherName.
    public int? TeacherUserId { get; set; }
    public required string TeacherName { get; set; }
    public int? SenderUserId { get; set; }
    public required string SenderName { get; set; }
    public bool SentByParent { get; set; }
    public required string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
