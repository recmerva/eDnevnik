using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace backend.Data;

// Početni podaci jedne osnovne škole: odeljenja od I do VIII razreda, nastavni plan po razredima,
// raspored, učenici sa ocenama i izostancima. Nastavnici i primer-roditelji se prave samo ako je
// zadata lozinka SEED_DEMO_PASSWORD, da se na pravoj instalaciji ne bi pojavili nalozi sa poznatom lozinkom.
// Namerno nije sve popunjeno (npr. I-2 nema učitelja, VII-2 razrednog, a Informatika i Muzička kultura
// u starijim razredima nastavnika), da bi administrator mogao da isproba dodele.
public static class DatabaseSeeder
{
    private static readonly string[] ClassNames = ["I-1", "I-2", "II-1", "III-1", "IV-1", "V-1", "VI-1", "VII-1", "VII-2", "VIII-1"];

    private static readonly string[] FirstNames = [
        "Amina", "Faris", "Lejla", "Harun", "Emina", "Amar", "Sara", "Adnan", "Nikola", "Džejla",
        "Emir", "Merjem", "Kenan", "Ajla", "Stefan", "Tarik", "Lamija", "Mirza", "Milica", "Hana",
        "Ismail", "Edina", "Hamza", "Jelena", "Nejla", "Anel", "Medina", "Luka", "Ajdin", "Esma",
    ];
    private static readonly string[] LastNames = [
        "Hadžić", "Delić", "Karić", "Ibrahimović", "Osmanović", "Mujkić", "Jovanović", "Hasić", "Begović",
        "Selimović", "Petrović", "Mahmutović", "Šehić", "Čengić", "Nikolić", "Kurtović", "Ramović",
    ];

    // Naziv predmeta i da li se ocenjuje opisno (ne ulazi u prosek).
    private static readonly (string Name, bool Descriptive)[] SubjectList = [
        ("Srpski jezik", false), ("Bosanski jezik", false), ("Matematika", false), ("Svet oko nas", false),
        ("Priroda i društvo", false), ("Engleski jezik", false), ("Nemački jezik", false), ("Istorija", false),
        ("Geografija", false), ("Fizika", false), ("Hemija", false), ("Biologija", false),
        ("Tehnika i tehnologija", false), ("Informatika i računarstvo", false),
        ("Likovna kultura", false), ("Muzička kultura", false), ("Fizičko i zdravstveno vaspitanje", false),
        ("Veronauka", true),
    ];

    // Nedeljni fond časova po razredu (prema nastavnom planu za osnovnu školu).
    private static (string Subject, int Lessons)[] WeeklyPlan(int grade) => grade switch
    {
        1 or 2 => [
            ("Srpski jezik", 5), ("Matematika", 5), ("Svet oko nas", 2), ("Engleski jezik", 2), ("Likovna kultura", grade),
            ("Muzička kultura", 1), ("Fizičko i zdravstveno vaspitanje", 3), ("Informatika i računarstvo", 1), ("Veronauka", 1),
        ],
        3 or 4 => [
            ("Srpski jezik", 5), ("Matematika", 5), ("Priroda i društvo", 2), ("Engleski jezik", 2), ("Likovna kultura", 2),
            ("Muzička kultura", 1), ("Fizičko i zdravstveno vaspitanje", 3), ("Informatika i računarstvo", 1), ("Veronauka", 1),
        ],
        5 => [
            ("Srpski jezik", 5), ("Bosanski jezik", 2), ("Matematika", 4), ("Engleski jezik", 2), ("Nemački jezik", 2),
            ("Istorija", 1), ("Geografija", 1), ("Biologija", 2), ("Tehnika i tehnologija", 2), ("Informatika i računarstvo", 1),
            ("Likovna kultura", 2), ("Muzička kultura", 2), ("Fizičko i zdravstveno vaspitanje", 3), ("Veronauka", 1),
        ],
        6 => [
            ("Srpski jezik", 4), ("Bosanski jezik", 2), ("Matematika", 4), ("Engleski jezik", 2), ("Nemački jezik", 2),
            ("Istorija", 2), ("Geografija", 2), ("Fizika", 2), ("Biologija", 2), ("Tehnika i tehnologija", 2),
            ("Informatika i računarstvo", 1), ("Likovna kultura", 1), ("Muzička kultura", 1), ("Fizičko i zdravstveno vaspitanje", 2), ("Veronauka", 1),
        ],
        _ => [
            ("Srpski jezik", 4), ("Bosanski jezik", 2), ("Matematika", 4), ("Engleski jezik", 2), ("Nemački jezik", 2),
            ("Istorija", 2), ("Geografija", 2), ("Fizika", 2), ("Hemija", 2), ("Biologija", 2), ("Tehnika i tehnologija", 2),
            ("Informatika i računarstvo", 1), ("Likovna kultura", 1), ("Muzička kultura", 1), ("Fizičko i zdravstveno vaspitanje", 2), ("Veronauka", 1),
        ],
    };

    // Učitelji: predaju sve predmete u svom odeljenju osim engleskog jezika i veronauke.
    private static readonly (string Name, string Class)[] ClassTeachers = [
        ("Emina Hodžić", "I-1"), ("Jasmina Petrović", "II-1"), ("Senada Mujović", "III-1"), ("Alma Kurtović", "IV-1"),
    ];

    // Predmetni nastavnici i odeljenja u kojima predaju (null = sva odeljenja u kojima se predmet uči).
    // Engleski i veronauka se uče od I razreda, ostali predmeti predmetne nastave od V do VIII.
    // Predmeti sa više časova imaju dva nastavnika koji dele starija odeljenja.
    private static readonly string[] FirstShift = ["V-1", "VI-1", "VII-1"];
    private static readonly string[] SecondShift = ["VII-2", "VIII-1"];
    private static readonly string[] LowerClasses = ["I-1", "I-2", "II-1", "III-1", "IV-1"];
    private static readonly (string Name, string Subject, string[]? Classes)[] SubjectTeachers = [
        ("Mirela Dazdarević", "Srpski jezik", FirstShift), ("Milica Stojanović", "Srpski jezik", SecondShift),
        ("Edin Ličina", "Bosanski jezik", null),
        ("Senad Hamidović", "Matematika", FirstShift), ("Adnan Kačar", "Matematika", SecondShift),
        ("Lejla Fetahović", "Engleski jezik", [.. FirstShift, .. SecondShift]), ("Ajla Rastoder", "Engleski jezik", LowerClasses),
        ("Dragana Jovanović", "Nemački jezik", FirstShift), ("Mersiha Ćatović", "Nemački jezik", SecondShift),
        ("Nermin Kolašinac", "Istorija", null), ("Aida Zukorlić", "Geografija", null),
        ("Admir Bihorac", "Fizika", null), ("Snežana Nikolić", "Hemija", null),
        ("Merima Gegić", "Biologija", FirstShift), ("Edina Muminović", "Biologija", SecondShift),
        ("Hajrudin Mehmedović", "Tehnika i tehnologija", null),
        ("Kenan Mušović", "Informatika i računarstvo", FirstShift),
        ("Vesna Đurović", "Likovna kultura", null),
        ("Elvir Muratović", "Fizičko i zdravstveno vaspitanje", FirstShift), ("Damir Ademović", "Fizičko i zdravstveno vaspitanje", SecondShift),
        ("Fuad Hadžibulić", "Veronauka", null),
    ];
    private static readonly string[] AllGradesSubjects = ["Engleski jezik", "Veronauka"];

    // Zaposleni koji još nemaju dodelu (npr. nova učiteljica za I-2, nastavnica muzičke kulture),
    // da bi administrator mogao da ih rasporedi kroz Administracija → Dodele.
    private static readonly string[] UnassignedTeachers = ["Amela Šabotić", "Selma Kalač"];

    // Razredni starešine starijih razreda predaju svoj predmet u tom odeljenju.
    private static readonly (string Class, string Teacher)[] UpperHomerooms = [
        ("V-1", "Mirela Dazdarević"), ("VI-1", "Senad Hamidović"), ("VII-1", "Merima Gegić"), ("VIII-1", "Lejla Fetahović"),
    ];

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SchoolDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseSeeder));

        // MySQL kontejner se ponekad podiže sporije od API-ja, zato pokušavamo više puta.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.EnsureCreatedAsync();
                break;
            }
            catch (MySqlException exception) when (attempt < 20)
            {
                logger.LogWarning("Baza još nije dostupna ({Message}). Pokušaj {Attempt}/20.", exception.Message, attempt);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        if (!await db.SchoolYears.AnyAsync())
        {
            db.SchoolYears.Add(SchoolYear.Default(DateOnly.FromDateTime(DateTime.UtcNow)));
            await db.SaveChangesAsync();
        }

        if (!await db.Classes.AnyAsync())
        {
            await SeedSchoolAsync(db, configuration["SEED_DEMO_PASSWORD"], logger);
            logger.LogInformation("Baza je popunjena početnim podacima.");
        }

        await SeedAdministratorAsync(db, configuration, logger);
    }

    private static async Task SeedSchoolAsync(SchoolDbContext db, string? demoPassword, ILogger logger)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var classes = ClassNames.Select(name => new SchoolClass { Name = name, GradeLevel = GradeOf(name) }).ToArray();
        var subjects = SubjectList.Select(item => new Subject { Name = item.Name, UsesDescriptiveScale = item.Descriptive }).ToDictionary(item => item.Name);
        db.Classes.AddRange(classes);
        db.Subjects.AddRange(subjects.Values);
        await db.SaveChangesAsync();

        // Učenici: mlađa odeljenja imaju nešto manje dece od starijih.
        var students = new List<Student>();
        var seed = 0;
        foreach (var (schoolClass, classIndex) in classes.Select((item, index) => (item, index)))
        {
            var count = (schoolClass.GradeLevel <= 4 ? 21 : 24) + classIndex * 3 % 5;
            for (var seat = 0; seat < count; seat++, seed++)
            {
                students.Add(new Student
                {
                    FullName = $"{FirstNames[seed % FirstNames.Length]} {LastNames[seed * 7 % LastNames.Length]}",
                    ClassId = schoolClass.Id,
                    RegistrationCode = RegistrationCodes.Generate(),
                });
            }
        }
        db.Students.AddRange(students);
        await db.SaveChangesAsync();

        // Raspored: uzastopni časovi istog predmeta padaju u različite dane, a svako odeljenje ima pomeren raspored.
        var plans = classes.ToDictionary(item => item.Id, item => WeeklyPlan(item.GradeLevel));
        foreach (var (schoolClass, classIndex) in classes.Select((item, index) => (item, index)))
        {
            var lessons = plans[schoolClass.Id].SelectMany(item => Enumerable.Repeat(subjects[item.Subject].Id, item.Lessons)).ToArray();
            db.Schedule.AddRange(lessons.Select((_, slot) => new ScheduleEntry
            {
                ClassId = schoolClass.Id,
                Day = 1 + slot % ScheduleEntry.Days,
                LessonNumber = 1 + slot / ScheduleEntry.Days,
                SubjectId = lessons[(slot + classIndex * 7) % lessons.Length],
            }));
        }

        // Ocene: svaki učenik ima svoj nivo znanja (2–5), a ocene variraju oko njega.
        foreach (var (student, studentIndex) in students.Select((item, index) => (item, index)))
        {
            var level = 2 + studentIndex * 7 % 4 + (studentIndex % 3 == 0 ? 1 : 0);
            var classSubjects = plans[student.ClassId].Select(item => subjects[item.Subject]).ToList();
            db.Grades.AddRange(classSubjects.SelectMany((subject, subjectIndex) =>
                Enumerable.Range(0, Grade.Types.Length).Select(typeIndex => new Grade
                {
                    StudentId = student.Id,
                    SubjectId = subject.Id,
                    Value = Math.Clamp(level + (studentIndex + subjectIndex * 3 + typeIndex * 2) % 3 - 1, 1, 5),
                    GradeType = Grade.Types[typeIndex],
                    GradedOn = today.AddDays(-((studentIndex * 3 + subjectIndex * 5 + typeIndex * 11) % 70 + 1)),
                })));
        }

        // Kontrolni i pismeni zadaci samo iz predmeta gde se zaista pišu, raspoređeni po danima.
        string[] testSubjects = ["Srpski jezik", "Matematika", "Engleski jezik", "Priroda i društvo", "Fizika", "Hemija", "Istorija", "Geografija", "Biologija"];
        foreach (var (schoolClass, classIndex) in classes.Select((item, index) => (item, index)))
        {
            var tested = plans[schoolClass.Id].Where(item => testSubjects.Contains(item.Subject)).Select(item => subjects[item.Subject]).ToList();
            db.Tests.AddRange(tested.SelectMany((subject, index) => new[]
            {
                new SchoolTest { ClassId = schoolClass.Id, SubjectId = subject.Id, Title = $"Kontrolni zadatak – {subject.Name}", HeldOn = today.AddDays(-6 - index * 4 - classIndex % 3) },
                new SchoolTest { ClassId = schoolClass.Id, SubjectId = subject.Id, Title = $"Pismeni zadatak – {subject.Name}", HeldOn = today.AddDays(3 + index * 5 + classIndex % 4) },
            }));
        }

        db.Absences.AddRange(students.Where((_, index) => index % 4 == 0).Select((student, index) => new Absence
        {
            StudentId = student.Id,
            Date = today.AddDays(-(index * 3 % 45) - 1),
            LessonHours = index % 4 + 1,
            IsExcused = index % 3 != 0,
            Note = index % 3 != 0 ? "Roditelj dostavio lekarsko opravdanje." : null,
        }));
        await db.SaveChangesAsync();

        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogInformation("SEED_DEMO_PASSWORD nije zadat, pa nastavnici i primer-roditelji nisu kreirani.");
            return;
        }
        await SeedPeopleAsync(db, classes, subjects, plans, students, demoPassword, today);
    }

    private static async Task SeedPeopleAsync(SchoolDbContext db, SchoolClass[] classes, Dictionary<string, Subject> subjects,
        Dictionary<int, (string Subject, int Lessons)[]> plans, List<Student> students, string password, DateOnly today)
    {
        var hasher = new PasswordHasher<ApplicationUser>();
        ApplicationUser Account(string name, string role, string domain)
        {
            var user = new ApplicationUser { FullName = name, Email = $"{EmailName(name)}@{domain}", PasswordHash = string.Empty, Role = role };
            user.PasswordHash = hasher.HashPassword(user, password);
            return user;
        }

        var teachers = ClassTeachers.Select(item => item.Name).Concat(SubjectTeachers.Select(item => item.Name)).Concat(UnassignedTeachers)
            .Distinct()
            .ToDictionary(name => name, name => Account(name, Roles.Teacher, "skola.local"));
        db.Users.AddRange(teachers.Values);
        await db.SaveChangesAsync();

        var classByName = classes.ToDictionary(item => item.Name);
        var assignments = new List<TeacherClassAssignment>();
        foreach (var (name, className) in ClassTeachers)
        {
            var schoolClass = classByName[className];
            assignments.AddRange(plans[schoolClass.Id].Where(item => !AllGradesSubjects.Contains(item.Subject)).Select((item, index) => new TeacherClassAssignment
            {
                TeacherUserId = teachers[name].Id, ClassId = schoolClass.Id, SubjectId = subjects[item.Subject].Id, IsHomeroom = index == 0,
            }));
        }
        foreach (var (name, subjectName, onlyClasses) in SubjectTeachers)
        {
            var taught = classes.Where(item => (onlyClasses?.Contains(item.Name) ?? true)
                && (item.GradeLevel >= 5 || AllGradesSubjects.Contains(subjectName))
                && plans[item.Id].Any(plan => plan.Subject == subjectName));
            assignments.AddRange(taught.Select(schoolClass => new TeacherClassAssignment
            {
                TeacherUserId = teachers[name].Id,
                ClassId = schoolClass.Id,
                SubjectId = subjects[subjectName].Id,
                IsHomeroom = UpperHomerooms.Contains((schoolClass.Name, name)),
            }));
        }
        db.TeacherAssignments.AddRange(assignments);

        // Domaći zadaci sa rokovima u narednim danima, zadaje ih nastavnik predmeta.
        foreach (var schoolClass in classes)
        {
            foreach (var (subjectName, title, days) in new[] { ("Matematika", "Zadaci iz zbirke, strana 34 (1–8)", 2), ("Srpski jezik", "Sastav: Moj najlepši dan", 5) })
            {
                var teacher = assignments.FirstOrDefault(item => item.ClassId == schoolClass.Id && item.SubjectId == subjects[subjectName].Id);
                if (teacher is null) continue;
                var author = teachers.Values.Single(item => item.Id == teacher.TeacherUserId);
                db.Homework.Add(new Homework
                {
                    ClassId = schoolClass.Id, SubjectId = subjects[subjectName].Id, Title = title, DueDate = today.AddDays(days),
                    Description = "Uraditi u svesci i doneti na sledeći čas.", AuthorUserId = author.Id, AuthorName = author.FullName, CreatedAt = DateTime.UtcNow.AddDays(-1),
                });
            }
        }

        // Dva primer-roditelja: jedan sa dvoje dece (mlađi i stariji razred), jedan sa jednim detetom.
        var older = students.First(item => item.ClassId == classByName["VI-1"].Id);
        var younger = students.First(item => item.ClassId == classByName["II-1"].Id);
        var eighth = students.First(item => item.ClassId == classByName["VIII-1"].Id);
        older.FullName = "Tarik Hadžić";
        younger.FullName = "Lamija Hadžić";
        eighth.FullName = "Nikola Jovanović";
        var amira = Account("Amira Hadžić", Roles.Parent, "primer.rs");
        var marko = Account("Marko Jovanović", Roles.Parent, "primer.rs");
        db.Users.AddRange(amira, marko);
        older.Parent = amira;
        younger.Parent = amira;
        eighth.Parent = marko;
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        Message Write(Student student, ApplicationUser teacher, bool fromParent, string text, double hoursAgo, bool read) => new()
        {
            StudentId = student.Id, TeacherUserId = teacher.Id, TeacherName = teacher.FullName,
            SenderUserId = fromParent ? student.ParentUserId : teacher.Id, SenderName = fromParent ? student.Parent!.FullName : teacher.FullName,
            SentByParent = fromParent, Content = text, CreatedAt = now.AddHours(-hoursAgo), ReadAt = read ? now.AddHours(-hoursAgo + 1) : null,
        };
        var hamidovic = teachers["Senad Hamidović"];
        var petrovic = teachers["Jasmina Petrović"];
        var fetahovic = teachers["Lejla Fetahović"];
        db.Messages.AddRange(
            Write(older, hamidovic, true, "Poštovani, Tarik je juče bio bolestan. Da li je bilo nešto važno na času matematike?", 30, true),
            Write(older, hamidovic, false, "Poštovana, radili smo razlomke. Neka uradi zadatke iz zbirke na strani 34, rado ću mu pomoći na dopunskoj.", 28, true),
            Write(older, hamidovic, true, "Hvala vam. Kada je dopunska nastava?", 3, false),
            Write(younger, petrovic, false, "Lamija je ove nedelje bila veoma aktivna na času. Molim vas da za petak pripremi materijal za likovno.", 20, false),
            Write(eighth, fetahovic, true, "Dobar dan, da li Nikola može da odgovara engleski naknadno zbog takmičenja?", 50, true),
            Write(eighth, fetahovic, false, "Može, dogovorićemo termin naredne nedelje.", 46, true));
        await db.SaveChangesAsync();
    }

    private static int GradeOf(string className) => className.Split('-')[0] switch
    {
        "I" => 1, "II" => 2, "III" => 3, "IV" => 4, "V" => 5, "VI" => 6, "VII" => 7, _ => 8,
    };

    // "Mirela Dazdarević" → "mirela.dazdarevic"
    private static string EmailName(string fullName)
    {
        var map = new Dictionary<char, string> { ['č'] = "c", ['ć'] = "c", ['š'] = "s", ['ž'] = "z", ['đ'] = "dj" };
        var ascii = string.Concat(fullName.ToLowerInvariant().Select(ch => map.TryGetValue(ch, out var value) ? value : ch.ToString()));
        return ascii.Replace(' ', '.');
    }

    private static async Task SeedAdministratorAsync(SchoolDbContext db, IConfiguration configuration, ILogger logger)
    {
        var email = configuration["SEED_ADMIN_EMAIL"]?.Trim().ToLowerInvariant();
        var password = configuration["SEED_ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (!await db.Users.AnyAsync(user => user.Role == Roles.Administrator))
            {
                logger.LogWarning("Ne postoji nijedan administrator. Postavi SEED_ADMIN_EMAIL i SEED_ADMIN_PASSWORD.");
            }
            return;
        }
        if (await db.Users.AnyAsync(user => user.Email == email)) return;

        var admin = new ApplicationUser
        {
            FullName = "Administrator škole",
            Email = email,
            PasswordHash = string.Empty,
            Role = Roles.Administrator,
        };
        admin.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(admin, password);
        db.Users.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Kreiran administratorski nalog {Email}.", email);
    }
}
