// fypSystem/Models/ProjectProposal.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fypSystem.Models
{
    public enum ProjectType
    {
        Research,
        Development
    }

    public enum ProposalStatus
    {
        Pending,
        Approved,
        Rejected,
        ResubmissionRequired,
        AcceptedWithConditions
    }

    public class ProjectProposal
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "Project Title")]
        [StringLength(200, MinimumLength = 10, ErrorMessage = "Title must be between 10 and 200 characters.")]
        public string Title { get; set; }

        [Required]
        [Display(Name = "Project Type")]
        public ProjectType ProjectType { get; set; }

        [Display(Name = "Uploaded Proposal File (PDF)")]
        [StringLength(255)]
        public string? FilePath { get; set; }

        [Display(Name = "Submission Date")]
        [DataType(DataType.Date)]
        public DateTime SubmissionDate { get; set; } = DateTime.Today;

        // Foreign Key to link to the Student who submitted the proposal
        [Required]
        public int StudentId { get; set; }
        [ForeignKey("StudentId")]
        public Student Student { get; set; } // Navigation property to the Student

        [Display(Name = "Status")]
        public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

        [StringLength(500)]
        [Display(Name = "Committee Feedback")]
        public string? CommitteeFeedback { get; set; }

        [Display(Name = "Review Date")]
        [DataType(DataType.Date)]
        public DateTime? ReviewDate { get; set; }

        // --- IMPORTANT ADDITION: Link to the Lecturer who is the Main Supervisor ---
        [Display(Name = "Main Supervisor")]
        public int? MainSupervisorLecturerId { get; set; }
        [ForeignKey("MainSupervisorLecturerId")]
        public Lecturer? MainSupervisorLecturer { get; set; } // Navigation property to the Lecturer who is the supervisor

        // --- EVALUATOR PROPERTIES ---
        [Display(Name = "Evaluator 1")]
        public int? Evaluator1Id { get; set; }
        [ForeignKey("Evaluator1Id")]
        public Lecturer? Evaluator1 { get; set; } // Navigation property for Evaluator 1

        [Display(Name = "Evaluator 1 Feedback")]
        [StringLength(500)]
        public string? Evaluator1Feedback { get; set; }

        [Display(Name = "Evaluator 1 Review Date")]
        [DataType(DataType.Date)]
        public DateTime? Evaluator1ReviewDate { get; set; }

        [Display(Name = "Evaluator 1 Recommendation")]
        public ProposalStatus? Evaluator1Recommendation { get; set; } // Nullable, as it might not be set yet

        [Display(Name = "Evaluator 2")]
        public int? Evaluator2Id { get; set; }
        [ForeignKey("Evaluator2Id")]
        public Lecturer? Evaluator2 { get; set; } // Navigation property for Evaluator 2

        [Display(Name = "Evaluator 2 Feedback")]
        [StringLength(500)]
        public string? Evaluator2Feedback { get; set; }

        [Display(Name = "Evaluator 2 Review Date")]
        [DataType(DataType.Date)]
        public DateTime? Evaluator2ReviewDate { get; set; }

        [Display(Name = "Evaluator 2 Recommendation")]
        public ProposalStatus? Evaluator2Recommendation { get; set; } // Nullable, as it might not be set yet

        // --- NEW PROPERTIES FOR SEMESTER AND ACADEMIC SESSION ---
        [Required]
        [StringLength(50)]
        [Display(Name = "Academic Session")]
        public string AcademicSession { get; set; } = DateTime.Now.Year + "/" + (DateTime.Now.Year + 1); // Default to current/next year

        [Required]
        [StringLength(50)]
        [Display(Name = "Semester")]
        public string Semester { get; set; } = "Semester 1"; // Default to Semester 1
    }
}
