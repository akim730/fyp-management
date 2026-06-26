// fypSystem/Models/Student.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace fypSystem.Models
{
    [Index(nameof(StudentNo), IsUnique = true)]
    public class Student
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
        [Display(Name = "Matric No.")]
        [StringLength(10)]
        public string StudentNo { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime DateOfBirth { get; set; }

        [Required]
        [StringLength(10)]
        public string Gender { get; set; }

        [Display(Name = "Phone Number")]
        [DataType(DataType.PhoneNumber)]
        [StringLength(20)]
        public string PhoneNumber { get; set; }

        [StringLength(255)]
        public string Address { get; set; }

        [Display(Name = "Academic Program")]
        public int AcademicProgramId { get; set; }
        [ForeignKey("AcademicProgramId")]
        public AcademicProgram AcademicProgram { get; set; }

        [DisplayFormat(DataFormatString = "{0:F2}")]
        public decimal CGPA { get; set; }

        [Display(Name = "Academic Session")]
        [StringLength(20)]
        public string? AcademicSession { get; set; }

        [Display(Name = "Semester")]
        [StringLength(10)]
        public string? Semester { get; set; }

        // =====================================================================
        // Navigation Properties
        // =====================================================================

        // A Student can submit multiple ProjectProposals over time
        public ICollection<ProjectProposal>? ProjectProposals { get; set; } = new List<ProjectProposal>();

        // Navigation property for the many-to-many relationship with Supervisors
        // (via the StudentSupervisor join table)
        // Renamed for clarity from StudentSupervisorRequests to StudentSupervisorAssignments
        public ICollection<StudentSupervisor>? StudentSupervisorAssignments { get; set; } = new List<StudentSupervisor>();
    }
}