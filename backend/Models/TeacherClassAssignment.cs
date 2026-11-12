namespace backend.Models;

public sealed class TeacherClassAssignment
{
    public int Id { get; set; }
    public int TeacherUserId { get; set; }
    public ApplicationUser? Teacher { get; set; }
    public int ClassId { get; set; }
    public SchoolClass? Class { get; set; }
    // Predmet koji nastavnik predaje u odeljenju; null kada je nastavnik samo razredni starešina.
    public int? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public bool IsHomeroom { get; set; }
}
