// fypSystem/Models/Supervisor.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace fypSystem.Models
{
    public class Supervisor
    {
        [Key]
        public int Id { get; set; }

        // Foreign Key to Lecturer who is acting as a Supervisor
        [Required]
        [Display(Name = "Lecturer")]
        public int LecturerId { get; set; }

        [ForeignKey("LecturerId")]
        public Lecturer Lecturer { get; set; } // Navigation property to the Lecturer

        [StringLength(100)]
        [Display(Name = "Supervisor Type")]
        public string? SupervisorType { get; set; }

        [Display(Name = "Max Students")]
        [Range(0, 20)]
        public int MaxStudents { get; set; } = 5;

        // Navigation property for StudentSupervisor assignments
        // A Supervisor can have multiple students assigned through StudentSupervisor entries
        public ICollection<StudentSupervisor>? StudentSupervisors { get; set; } = new List<StudentSupervisor>();
    }
}