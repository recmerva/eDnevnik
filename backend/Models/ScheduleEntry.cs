namespace backend.Models;

// Jedan čas u nedeljnom rasporedu odeljenja: dan (1 = ponedeljak ... 5 = petak) i redni broj časa.
public sealed class ScheduleEntry
{
    public const int Days = 5;
    public const int LessonsPerDay = 7;

    public int Id { get; set; }
    public int ClassId { get; set; }
    public SchoolClass? Class { get; set; }
    public int Day { get; set; }
    public int LessonNumber { get; set; }
    public int SubjectId { get; set; }
    public Subject? Subject { get; set; }
}
