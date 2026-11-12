using System.ComponentModel.DataAnnotations;

namespace backend.Contracts;

public sealed record RegisterRequest(
    [Required(ErrorMessage = "Unesite ime i prezime."), StringLength(120, MinimumLength = 2, ErrorMessage = "Ime i prezime mora imati od 2 do 120 karaktera.")] string FullName,
    [Required(ErrorMessage = "Unesite email adresu."), EmailAddress(ErrorMessage = "Email adresa nije ispravna."), StringLength(160)] string Email,
    [Required(ErrorMessage = "Unesite lozinku."), StringLength(128, MinimumLength = 10, ErrorMessage = "Lozinka mora imati najmanje 10 karaktera.")] string Password,
    [Required(ErrorMessage = "Unesite kod učenika."), StringLength(32)] string StudentCode);

public sealed record LoginRequest(
    [Required(ErrorMessage = "Unesite email adresu."), EmailAddress(ErrorMessage = "Email adresa nije ispravna.")] string Email,
    [Required(ErrorMessage = "Unesite lozinku.")] string Password);

public sealed record ChangePasswordRequest(
    [Required(ErrorMessage = "Unesite trenutnu lozinku.")] string CurrentPassword,
    [Required(ErrorMessage = "Unesite novu lozinku."), StringLength(128, MinimumLength = 10, ErrorMessage = "Nova lozinka mora imati najmanje 10 karaktera.")] string NewPassword);

public sealed record LinkChildRequest(
    [Required(ErrorMessage = "Unesite kod učenika."), StringLength(32)] string StudentCode);

public sealed record StaffUserRequest(
    [Required(ErrorMessage = "Unesite ime i prezime."), StringLength(120, MinimumLength = 2, ErrorMessage = "Ime i prezime mora imati od 2 do 120 karaktera.")] string FullName,
    [Required(ErrorMessage = "Unesite email adresu."), EmailAddress(ErrorMessage = "Email adresa nije ispravna."), StringLength(160)] string Email,
    [Required(ErrorMessage = "Unesite privremenu lozinku."), StringLength(128, MinimumLength = 10, ErrorMessage = "Lozinka mora imati najmanje 10 karaktera.")] string Password,
    [Required] string Role);

public sealed record UpdateUserRequest(
    [Required(ErrorMessage = "Unesite ime i prezime."), StringLength(120, MinimumLength = 2, ErrorMessage = "Ime i prezime mora imati od 2 do 120 karaktera.")] string FullName,
    [Required(ErrorMessage = "Unesite email adresu."), EmailAddress(ErrorMessage = "Email adresa nije ispravna."), StringLength(160)] string Email,
    [Required] string Role,
    [StringLength(128, MinimumLength = 10, ErrorMessage = "Nova lozinka mora imati najmanje 10 karaktera.")] string? NewPassword);

public sealed record TeacherAssignmentRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Izaberite nastavnika.")] int TeacherUserId,
    [Range(1, int.MaxValue, ErrorMessage = "Izaberite odeljenje.")] int ClassId,
    int? SubjectId,
    bool IsHomeroom);

public sealed record StudentRequest(
    [Required(ErrorMessage = "Unesite ime i prezime učenika."), StringLength(120, MinimumLength = 2, ErrorMessage = "Ime i prezime mora imati od 2 do 120 karaktera.")] string FullName,
    [Range(1, int.MaxValue, ErrorMessage = "Izaberite odeljenje.")] int ClassId);

public sealed record ClassRequest(
    [Required(ErrorMessage = "Unesite naziv odeljenja."), StringLength(20, MinimumLength = 1, ErrorMessage = "Naziv odeljenja može imati najviše 20 karaktera.")] string Name,
    [Range(1, 8, ErrorMessage = "Razred mora biti između 1 i 8.")] int GradeLevel);

public sealed record SubjectRequest(
    [Required(ErrorMessage = "Unesite naziv predmeta."), StringLength(80, MinimumLength = 2, ErrorMessage = "Naziv predmeta mora imati od 2 do 80 karaktera.")] string Name,
    bool UsesDescriptiveScale);

public sealed record SchoolYearRequest(
    [Required(ErrorMessage = "Unesite naziv školske godine."), StringLength(20, MinimumLength = 4)] string Name,
    DateOnly FirstTermStart,
    DateOnly FirstTermEnd,
    DateOnly SecondTermStart,
    DateOnly SecondTermEnd);

public sealed record FinalGradeRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Izaberite predmet.")] int SubjectId,
    [Range(1, 2, ErrorMessage = "Polugodište mora biti 1 ili 2.")] int Term,
    [Range(1, 5, ErrorMessage = "Ocena mora biti između 1 i 5.")] int Value);

public sealed record ScheduleRequest(
    [Range(1, 5, ErrorMessage = "Dan mora biti od ponedeljka do petka.")] int Day,
    [Range(1, 7, ErrorMessage = "Čas mora biti između 1 i 7.")] int LessonNumber,
    int? SubjectId);

public sealed record HomeworkRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Izaberite predmet.")] int SubjectId,
    [Required(ErrorMessage = "Unesite naslov zadatka."), StringLength(160, MinimumLength = 3, ErrorMessage = "Naslov mora imati od 3 do 160 karaktera.")] string Title,
    [StringLength(2000, ErrorMessage = "Opis može imati najviše 2000 karaktera.")] string? Description,
    DateOnly DueDate);

public sealed record NoteRequest(
    [Required(ErrorMessage = "Unesite belešku."), StringLength(2000, MinimumLength = 3, ErrorMessage = "Beleška mora imati od 3 do 2000 karaktera.")] string Content);

public sealed record HomeroomRequest(bool IsHomeroom);

// Nastavnik za predmet ili razredno starešinstvo; null uklanja dodelu.
public sealed record StaffChangeRequest(int? TeacherUserId);

public sealed record GradeRequest(
    [Range(1, int.MaxValue, ErrorMessage = "Izaberite predmet.")] int SubjectId,
    [Range(1, 5, ErrorMessage = "Ocena mora biti između 1 i 5.")] int Value,
    [Required(ErrorMessage = "Izaberite vrstu ocene.")] string GradeType,
    DateOnly? GradedOn);

public sealed record AbsenceRequest(
    DateOnly Date,
    [Range(1, 7, ErrorMessage = "Broj časova mora biti između 1 i 7.")] int LessonHours,
    bool IsExcused,
    [StringLength(500, ErrorMessage = "Napomena može imati najviše 500 karaktera.")] string? Note);

public sealed record TestRequest(
    [Required(ErrorMessage = "Unesite naziv testa."), StringLength(160, MinimumLength = 3, ErrorMessage = "Naziv testa mora imati od 3 do 160 karaktera.")] string Title,
    [Range(1, int.MaxValue, ErrorMessage = "Izaberite predmet.")] int SubjectId,
    DateOnly HeldOn);

public sealed record MessageRequest(
    [Required(ErrorMessage = "Poruka ne može biti prazna."), StringLength(4000, MinimumLength = 1, ErrorMessage = "Poruka može imati najviše 4000 karaktera.")] string Content,
    // Roditelj bira kom nastavniku piše; nastavniku se ovo polje ne šalje.
    int? TeacherUserId);

public sealed record ComplaintRequest(
    [Required(ErrorMessage = "Unesite tekst žalbe."), StringLength(4000, MinimumLength = 5, ErrorMessage = "Žalba mora imati najmanje 5 karaktera.")] string Content);

public sealed record ComplaintResponseRequest(
    [Required(ErrorMessage = "Unesite odgovor."), StringLength(4000, MinimumLength = 2, ErrorMessage = "Odgovor mora imati najmanje 2 karaktera.")] string Response);

public sealed record AnnouncementRequest(
    int? ClassId,
    [Required(ErrorMessage = "Unesite tekst obaveštenja."), StringLength(2000, MinimumLength = 5, ErrorMessage = "Obaveštenje mora imati od 5 do 2000 karaktera.")] string Content);
