using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AttendenceManagementSystem.Models;

namespace AttendenceManagementSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Initialize DbSets with nullable types to fix non-nullable warnings
        public DbSet<Department> Departments => Set<Department>();
        public DbSet<Batch> Batches => Set<Batch>();
        public DbSet<Section> Sections => Set<Section>();
        public DbSet<Course> Courses => Set<Course>();
        public DbSet<Teacher> Teachers => Set<Teacher>();
        public DbSet<Student> Students => Set<Student>();
        public DbSet<Timetable> Timetables => Set<Timetable>();
        public DbSet<Attendance> Attendances => Set<Attendance>();
        public DbSet<Enrollment> Enrollments => Set<Enrollment>();
        public DbSet<UploadLog> UploadLogs => Set<UploadLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure relationships and constraints
            builder.Entity<Enrollment>()
                .HasIndex(e => new { e.StudentId, e.CourseId })
                .IsUnique();

            builder.Entity<Attendance>()
                .HasIndex(a => new { a.StudentId, a.CourseId, a.Date })
                .IsUnique();

            builder.Entity<Timetable>()
                .HasIndex(t => new { t.BatchId, t.SectionId, t.DayOfWeek, t.StartTime })
                .IsUnique();

            builder.Entity<Student>()
                .HasIndex(s => s.RollNumber)
                .IsUnique();

            builder.Entity<Teacher>()
                .HasIndex(t => t.EmployeeId)
                .IsUnique();

            builder.Entity<Course>()
                .HasIndex(c => c.Code)
                .IsUnique();

            // Configure cascading behavior
            builder.Entity<Batch>()
                .HasOne(b => b.Department)
                .WithMany(d => d.Batches)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Section>()
                .HasOne(s => s.Batch)
                .WithMany(b => b.Sections)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Student>()
                .HasOne(s => s.Department)
                .WithMany(d => d.Students)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Timetable relationships using the collection navigations
            builder.Entity<Timetable>()
                .HasOne(t => t.Teacher)
                .WithMany(tc => tc.Timetables)
                .HasForeignKey(t => t.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Timetable>()
                .HasOne(t => t.Course)
                .WithMany(c => c.Timetables)
                .HasForeignKey(t => t.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Timetable>()
                .HasOne(t => t.Batch)
                .WithMany(b => b.Timetables)
                .HasForeignKey(t => t.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Timetable>()
                .HasOne(t => t.Section)
                .WithMany(s => s.Timetables)
                .HasForeignKey(t => t.SectionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Ensure no cascade delete is applied anywhere to avoid SQL Server multiple cascade paths
            foreach (var foreignKey in builder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }
    }
}