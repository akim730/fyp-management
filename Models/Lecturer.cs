// fypSystem/Models/Lecturer.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore; // Required for [Index]

namespace fypSystem.Models
{
    [Index(nameof(Email), IsUnique = true)]
    [Index(nameof(StaffNo), IsUnique = true)]
    public class Lecturer
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Staff No.")]
        public string StaffNo { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date Of Birth")]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [StringLength(10)]
        public string Gender { get; set; }

        [Display(Name = "Phone Number")]
        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [StringLength(255)]
        public string? Address { get; set; }

        [Display(Name = "Academic Program")]
        public int AcademicProgramId { get; set; }
        [ForeignKey("AcademicProgramId")]
        public AcademicProgram AcademicProgram { get; set; } // Navigation property

        [Required]
        [StringLength(50)]
        [Display(Name = "Academic Rank")]
        public string AcademicRank { get; set; }

        [StringLength(50)]
        [Display(Name = "Domain")]
        public string? Domain { get; set; }

        // =====================================================================
        // Navigation Properties
        // =====================================================================

        // A Lecturer can be a Supervisor (one-to-zero-or-one relationship with Supervisor entity)
        // This is the correct way to link a Lecturer to their single Supervisor profile.
        public Supervisor? SupervisorDetails { get; set; }

        // REMOVED: public ICollection<Supervisor>? Supervisors { get; set; }
        // This was logically redundant with SupervisorDetails for a 1-1 relationship.

        // A Lecturer can be part of multiple CommitteeMember entries (many-to-many through CommitteeMember)
        [InverseProperty("Lecturer")]
        public ICollection<CommitteeMember>? CommitteeMembers { get; set; } = new List<CommitteeMember>();

        // A Lecturer can be an Evaluator for multiple ProjectProposals (as Evaluator1 or Evaluator2)
        [InverseProperty("Evaluator1")]
        public ICollection<ProjectProposal>? EvaluatedProposals1 { get; set; } = new List<ProjectProposal>();

        [InverseProperty("Evaluator2")]
        public ICollection<ProjectProposal>? EvaluatedProposals2 { get; set; } = new List<ProjectProposal>();

        // A Lecturer can be a MainSupervisorLecturer for multiple ProjectProposals
        // RENAMED for consistency with ApplicationDbContext mapping (l => l.SupervisedProposals)
        [InverseProperty("MainSupervisorLecturer")]
        public ICollection<ProjectProposal>? SupervisedProposals { get; set; } = new List<ProjectProposal>();
    }
}