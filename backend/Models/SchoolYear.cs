namespace backend.Models;

// Podešavanja školske godine; u bazi postoji jedan red koji administrator menja.
public sealed class SchoolYear
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateOnly FirstTermStart { get; set; }
    public DateOnly FirstTermEnd { get; set; }
    public DateOnly SecondTermStart { get; set; }
    public DateOnly SecondTermEnd { get; set; }

    public int TermOf(DateOnly date) => date >= SecondTermStart ? 2 : 1;

    public static SchoolYear Default(DateOnly today)
    {
        var startYear = today.Month >= 9 ? today.Year : today.Year - 1;
        return new SchoolYear
        {
            Name = $"{startYear}/{(startYear + 1) % 100:00}.",
            FirstTermStart = new DateOnly(startYear, 9, 1),
            FirstTermEnd = new DateOnly(startYear, 12, 31),
            SecondTermStart = new DateOnly(startYear + 1, 1, 15),
            SecondTermEnd = new DateOnly(startYear + 1, 6, 20),
        };
    }
}
