using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public sealed class SchoolDbContext(DbContextOptions<SchoolDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<TeacherClassAssignment> TeacherAssignments => Set<TeacherClassAssignment>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<SchoolTest> Tests => Set<SchoolTest>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Absence> Absences => Set<Absence>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();
    public DbSet<FinalGrade> FinalGrades => Set<FinalGrade>();
    public DbSet<ScheduleEntry> Schedule => Set<ScheduleEntry>();
    public DbSet<Homework> Homework => Set<Homework>();
    public DbSet<StudentNote> StudentNotes => Set<StudentNote>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolYear>().Property(item => item.Name).HasMaxLength(20);

        modelBuilder.Entity<FinalGrade>(finalGrade =>
        {
            finalGrade.Property(item => item.RecordedByName).HasMaxLength(120);
            finalGrade.HasIndex(item => new { item.StudentId, item.SubjectId, item.Term }).IsUnique();
            finalGrade.HasOne(item => item.Student).WithMany()
                .HasForeignKey(item => item.StudentId).OnDelete(DeleteBehavior.Cascade);
            finalGrade.HasOne(item => item.Subject).WithMany()
                .HasForeignKey(item => item.SubjectId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ScheduleEntry>(entry =>
        {
            entry.HasIndex(item => new { item.ClassId, item.Day, item.LessonNumber }).IsUnique();
            entry.HasOne(item => item.Class).WithMany()
                .HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Cascade);
            entry.HasOne(item => item.Subject).WithMany()
                .HasForeignKey(item => item.SubjectId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Homework>(homework =>
        {
            homework.Property(item => item.Title).HasMaxLength(160);
            homework.Property(item => item.Description).HasMaxLength(2000);
            homework.Property(item => item.AuthorName).HasMaxLength(120);
            homework.HasIndex(item => new { item.ClassId, item.DueDate });
            homework.HasOne(item => item.Class).WithMany()
                .HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Cascade);
            homework.HasOne(item => item.Subject).WithMany()
                .HasForeignKey(item => item.SubjectId).OnDelete(DeleteBehavior.Restrict);
            homework.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.AuthorUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StudentNote>(note =>
        {
            note.Property(item => item.AuthorName).HasMaxLength(120);
            note.Property(item => item.Content).HasMaxLength(2000);
            note.HasIndex(item => new { item.StudentId, item.CreatedAt });
            note.HasOne(item => item.Student).WithMany()
                .HasForeignKey(item => item.StudentId).OnDelete(DeleteBehavior.Cascade);
            note.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.AuthorUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ActivityLog>(log =>
        {
            log.Property(item => item.UserName).HasMaxLength(120);
            log.Property(item => item.Action).HasMaxLength(60);
            log.Property(item => item.Details).HasMaxLength(500);
            log.HasIndex(item => item.CreatedAt);
            log.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApplicationUser>(user =>
        {
            user.Property(item => item.FullName).HasMaxLength(120);
            user.Property(item => item.Email).HasMaxLength(160);
            user.Property(item => item.Role).HasMaxLength(20);
            user.HasIndex(item => item.Email).IsUnique();
        });

        modelBuilder.Entity<SchoolClass>(schoolClass =>
        {
            schoolClass.Property(item => item.Name).HasMaxLength(20);
            schoolClass.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<Subject>(subject =>
        {
            subject.Property(item => item.Name).HasMaxLength(80);
            subject.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<Student>(student =>
        {
            student.Property(item => item.FullName).HasMaxLength(120);
            student.Property(item => item.RegistrationCode).HasMaxLength(32);
            student.HasIndex(item => item.RegistrationCode).IsUnique();
            student.HasIndex(item => item.FullName);
            student.HasOne(item => item.Class).WithMany(item => item.Students)
                .HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Restrict);
            student.HasOne(item => item.Parent).WithMany(item => item.Children)
                .HasForeignKey(item => item.ParentUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TeacherClassAssignment>(assignment =>
        {
            assignment.HasIndex(item => new { item.TeacherUserId, item.ClassId, item.SubjectId }).IsUnique();
            assignment.HasOne(item => item.Subject).WithMany()
                .HasForeignKey(item => item.SubjectId).OnDelete(DeleteBehavior.Cascade);
            assignment.HasOne(item => item.Teacher).WithMany(item => item.Assignments)
                .HasForeignKey(item => item.TeacherUserId).OnDelete(DeleteBehavior.Cascade);
            assignment.HasOne(item => item.Class).WithMany(item => item.Assignments)
                .HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Grade>(grade =>
        {
            grade.Property(item => item.GradeType).HasMaxLength(40);
            grade.HasIndex(item => new { item.StudentId, item.GradedOn });
            grade.HasOne(item => item.Student).WithMany()
                .HasForeignKey(item => item.StudentId).OnDelete(DeleteBehavior.Cascade);
            grade.HasOne(item => item.Subject).WithMany()
                .HasForeignKey(item => item.SubjectId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SchoolTest>(test =>
        {
            test.Property(item => item.Title).HasMaxLength(160);
            test.HasIndex(item => new { item.ClassId, item.HeldOn });
            test.HasOne(item => item.Class).WithMany()
                .HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Cascade);
            test.HasOne(item => item.Subject).WithMany()
                .HasForeignKey(item => item.SubjectId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Absence>(absence =>
        {
            absence.Property(item => item.Note).HasMaxLength(500);
            absence.HasIndex(item => new { item.StudentId, item.Date });
            absence.HasOne(item => item.Student).WithMany()
                .HasForeignKey(item => item.StudentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(message =>
        {
            message.Property(item => item.SenderName).HasMaxLength(120);
            message.Property(item => item.Content).HasMaxLength(4000);
            message.Property(item => item.TeacherName).HasMaxLength(120);
            message.HasIndex(item => new { item.StudentId, item.TeacherUserId, item.CreatedAt });
            message.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.TeacherUserId).OnDelete(DeleteBehavior.SetNull);
            message.HasOne(item => item.Student).WithMany()
                .HasForeignKey(item => item.StudentId).OnDelete(DeleteBehavior.Cascade);
            message.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.SenderUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Complaint>(complaint =>
        {
            complaint.Property(item => item.ParentName).HasMaxLength(120);
            complaint.Property(item => item.Content).HasMaxLength(4000);
            complaint.Property(item => item.Status).HasMaxLength(20);
            complaint.Property(item => item.Response).HasMaxLength(4000);
            complaint.Property(item => item.RespondedByName).HasMaxLength(120);
            complaint.HasIndex(item => new { item.StudentId, item.Status });
            complaint.HasOne(item => item.Student).WithMany()
                .HasForeignKey(item => item.StudentId).OnDelete(DeleteBehavior.Cascade);
            complaint.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.ParentUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Announcement>(announcement =>
        {
            announcement.Property(item => item.AuthorName).HasMaxLength(120);
            announcement.Property(item => item.Content).HasMaxLength(2000);
            announcement.HasIndex(item => item.CreatedAt);
            announcement.HasOne(item => item.Class).WithMany()
                .HasForeignKey(item => item.ClassId).OnDelete(DeleteBehavior.Cascade);
            announcement.HasOne<ApplicationUser>().WithMany()
                .HasForeignKey(item => item.AuthorUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
