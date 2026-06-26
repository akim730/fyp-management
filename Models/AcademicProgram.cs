// Models/AcademicProgram.cs
using System.ComponentModel.DataAnnotations;

namespace fypSystem.Models
{
    public class AcademicProgram
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Academic Program")]
        public string Name { get; set; } // e.g., "Data Engineering", "Software Engineering"

        [Required]
        [StringLength(100)]
        [Display(Name = "Program Code")]
        public string Code { get; set; }
    }
}
