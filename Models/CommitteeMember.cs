// fypSystem.Models.CommitteeMember.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema; // Required for [ForeignKey]

namespace fypSystem.Models
{
    public class CommitteeMember
    {
        public int Id { get; set; }

        // Foreign Key to Lecturer
        [Required]
        [Display(Name = "Lecturer")]
        public int LecturerId { get; set; }

        [ForeignKey("LecturerId")]
        public Lecturer Lecturer { get; set; } // Navigation property to the Lecturer

        // Properties specific to the committee role
        [StringLength(100)]
        [Display(Name = "Committee Role")]
        public string? CommitteeRole { get; set; } // e.g., "Chairperson", "Secretary", "Member"

        [DataType(DataType.Date)]
        [Display(Name = "Appointment Date")]
        public DateTime? AppointmentDate { get; set; } // Nullable if not always set

        // *** THESE LINES MUST BE PRESENT AND UNCOMMENTED ***
        [Required] // This makes it mandatory for every committee member to be associated with an Academic Program
        [Display(Name = "Academic Program")]
        public int AcademicProgramId { get; set; }

        [ForeignKey("AcademicProgramId")]
        public AcademicProgram AcademicProgram { get; set; } // Navigation property to the AcademicProgram
        // ***************************************************
    }
}