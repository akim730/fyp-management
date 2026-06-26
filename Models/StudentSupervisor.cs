// fypSystem/Models/StudentSupervisor.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fypSystem.Models
{
    public enum SupervisorAssignmentStatus
    {
        Pending,
        Approved,
        Rejected
    }

    public class StudentSupervisor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Student")]
        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Student Student { get; set; } // Navigation property to the Student

        [Required]
        [Display(Name = "Supervisor")]
        public int SupervisorId { get; set; }
        [ForeignKey("SupervisorId")]
        public Supervisor Supervisor { get; set; } // Navigation property to the Supervisor

        [Required]
        [StringLength(50)]
        [Display(Name = "Supervisor Type")]
        public string SupervisorType { get; set; } // e.g., "Main Supervisor", "Co-Supervisor"

        [Required]
        [Display(Name = "Status")]
        public SupervisorAssignmentStatus Status { get; set; } = SupervisorAssignmentStatus.Pending;

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Request Date")]
        public DateTime RequestDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        [Display(Name = "Action Date")]
        public DateTime? ActionDate { get; set; }

        [StringLength(500)]
        [Display(Name = "Committee Remarks")]
        public string? CommitteeRemarks { get; set; }
    }
}