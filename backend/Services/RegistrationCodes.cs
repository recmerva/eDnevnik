using System.Security.Cryptography;

namespace backend.Services;

// Kod učenika koji škola usmeno saopštava roditelju: 8 znakova, bez znakova koji se lako pomešaju (0/O, 1/I/L).
// Čuva se bez crtice, a prikazuje kao "K7MR-Q4TB".
public static class RegistrationCodes
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static string Generate() => RandomNumberGenerator.GetString(Alphabet, 8);

    public static string Normalize(string code) => code.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
}
