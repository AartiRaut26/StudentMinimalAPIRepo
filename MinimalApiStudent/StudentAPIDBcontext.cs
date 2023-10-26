using Microsoft.EntityFrameworkCore;
using MinimalApiStudent.Models;

namespace MinimalApiStudent
{
    public class StudentAPIDBcontext : DbContext
    {
        public StudentAPIDBcontext(DbContextOptions<StudentAPIDBcontext> options) : base(options) { }

        public DbSet<Student> Students { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<Attendance> Attendances { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Attendance>()
                .HasKey(a => a.AttendanceId);

            modelBuilder.Entity<Attendance>()
             .Property(a => a.AttendanceId)
             .UseIdentityColumn(); // Set AttendanceId to be an identity column

            modelBuilder.Entity<Attendance>()
                .HasKey(a => new { a.StudentId, a.ClassId });

            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.Student)
                .WithMany(s => s.Attendances)
                .HasForeignKey(a => a.StudentId);

            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.Class)
                .WithMany(c => c.Attendances)
                .HasForeignKey(a => a.ClassId);

            base.OnModelCreating(modelBuilder);

        }
    }
}
