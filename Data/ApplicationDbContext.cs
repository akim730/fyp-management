using fypSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace fypSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // --- DbSet Properties ---
        public DbSet<Student> Students { get; set; }
        public DbSet<Lecturer> Lecturers { get; set; }
        public DbSet<AcademicProgram> AcademicPrograms { get; set; }
        public DbSet<CommitteeMember> CommitteeMembers { get; set; }
        public DbSet<Supervisor> Supervisors { get; set; }
        public DbSet<StudentSupervisor> StudentSupervisors { get; set; }
        public DbSet<ProjectProposal> ProjectProposals { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- Configure Relationships ---

            // AcademicProgram and Lecturer (One-to-Many)
            modelBuilder.Entity<Lecturer>()
                .HasOne(l => l.AcademicProgram)
                .WithMany()
                .HasForeignKey(l => l.AcademicProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            // AcademicProgram and Student (One-to-Many)
            modelBuilder.Entity<Student>()
                .HasOne(s => s.AcademicProgram)
                .WithMany()
                .HasForeignKey(s => s.AcademicProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            // AcademicProgram and CommitteeMember (One-to-Many)
            modelBuilder.Entity<CommitteeMember>()
                .HasOne(cm => cm.AcademicProgram)
                .WithMany()
                .HasForeignKey(cm => cm.AcademicProgramId)
                .OnDelete(DeleteBehavior.Restrict);

            // Lecturer and CommitteeMember (One-to-Many from Lecturer's perspective, handled by InverseProperty attribute)
            modelBuilder.Entity<CommitteeMember>()
                .HasOne(cm => cm.Lecturer)
                .WithMany(l => l.CommitteeMembers)
                .HasForeignKey(cm => cm.LecturerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Lecturer and Supervisor (One-to-Zero-or-One)
            modelBuilder.Entity<Supervisor>()
                .HasOne(s => s.Lecturer)
                .WithOne(l => l.SupervisorDetails)
                .HasForeignKey<Supervisor>(s => s.LecturerId)
                .OnDelete(DeleteBehavior.Cascade); // Keep Cascade here if Supervisor entry is dependent on Lecturer

            // StudentSupervisor (Join Table for Many-to-Many between Student and Supervisor)
            modelBuilder.Entity<StudentSupervisor>()
                .HasOne(ss => ss.Student)
                .WithMany(s => s.StudentSupervisorAssignments)
                .HasForeignKey(ss => ss.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentSupervisor>()
                .HasOne(ss => ss.Supervisor)
                .WithMany(s => s.StudentSupervisors)
                .HasForeignKey(ss => ss.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentSupervisor>()
                .Property(s => s.Status)
                .HasConversion<string>();

            // ProjectProposal and Student (One-to-Many)
            modelBuilder.Entity<ProjectProposal>()
                .HasOne(p => p.Student)
                .WithMany(s => s.ProjectProposals)
                .HasForeignKey(p => p.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ProjectProposal and MainSupervisorLecturer
            modelBuilder.Entity<ProjectProposal>()
                .HasOne(p => p.MainSupervisorLecturer)
                .WithMany(l => l.SupervisedProposals)
                .HasForeignKey(p => p.MainSupervisorLecturerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict); // <--- CHANGED THIS TO RESTRICT

            // ProjectProposal and Evaluator1
            modelBuilder.Entity<ProjectProposal>()
                .HasOne(p => p.Evaluator1)
                .WithMany(l => l.EvaluatedProposals1)
                .HasForeignKey(p => p.Evaluator1Id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict); // <--- THIS WAS ALREADY RESTRICT, CONFIRMED

            // ProjectProposal and Evaluator2
            modelBuilder.Entity<ProjectProposal>()
                .HasOne(p => p.Evaluator2)
                .WithMany(l => l.EvaluatedProposals2)
                .HasForeignKey(p => p.Evaluator2Id)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict); // <--- THIS WAS ALREADY RESTRICT, CONFIRMED

            modelBuilder.Entity<ProjectProposal>()
                .Property(p => p.Status)
                .HasConversion<string>();
        }
    }
}