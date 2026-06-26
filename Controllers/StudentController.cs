// fypSystem.Controllers/StudentController.cs
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using fypSystem.Data;
using fypSystem.Models; // Ensure this is present
// Removed: using fypSystem.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using System; // Added for DateTime

namespace fypSystem.Controllers
{
    // Ensure "Student" role is allowed at the controller level
    [Authorize(Roles = "Admin, Committee, Student")]
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public StudentController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Helper method to get the current logged-in student's ID based on their associated Student's email
        private async Task<int?> GetCurrentStudentIdAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return null;

            var userEmail = (await _userManager.FindByIdAsync(userId))?.Email;
            if (userEmail == null) return null;

            // Find the Student associated with this email
            return (await _context.Students.FirstOrDefaultAsync(s => s.Email == userEmail))?.Id;
        }

        // Helper to populate Semester and Academic Session dropdowns
        private void PopulateSemesterAndSessionDropdowns(string? selectedSemester = null, string? selectedAcademicSession = null)
        {
            var semesters = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Semester --" }, // Default empty option
                new SelectListItem { Value = "Semester 1", Text = "Semester 1" },
                new SelectListItem { Value = "Semester 2", Text = "Semester 2" },
                new SelectListItem { Value = "Special Semester", Text = "Special Semester" }
            };
            ViewBag.Semesters = new SelectList(semesters, "Value", "Text", selectedSemester);

            // Generate a list of academic sessions (e.g., current year +/- a few)
            var academicSessions = new List<string>();
            int currentYear = DateTime.Now.Year;
            for (int i = -2; i <= 2; i++) // Generate for current year +/- 2 years
            {
                academicSessions.Add($"{currentYear + i}/{currentYear + i + 1}");
            }
            // Add any existing sessions from students if they are not already in the generated list
            academicSessions.AddRange(_context.Students.Select(s => s.AcademicSession).Distinct().Where(s => !string.IsNullOrEmpty(s) && !academicSessions.Contains(s)));
            academicSessions = academicSessions.OrderByDescending(s => s).ToList(); // Order descending for most recent first

            var sessionListItems = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Academic Session --" } // Default empty option
            };
            sessionListItems.AddRange(academicSessions.Select(s => new SelectListItem { Value = s, Text = s }));
            ViewBag.AcademicSessions = new SelectList(sessionListItems, "Value", "Text", selectedAcademicSession);
        }

        // ─────────────────────────────────────────────────────────────
        // GET: Student (Admin/Committee only)
        [Authorize(Roles = "Admin, Committee")] // Explicitly restrict Index to Admin, Committee
        public async Task<IActionResult> Index()
        {
            var students = await _context.Students
                                         .Include(s => s.AcademicProgram)
                                         .AsNoTracking()
                                         .ToListAsync();
            return View(students);
        }

        // ─────────────────────────────────────────────────────────────
        // GET: Student/Details/5 (Admin/Committee only)
        [Authorize(Roles = "Admin, Committee")] // Explicitly restrict Details to Admin, Committee
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students
                                         .Include(s => s.AcademicProgram)
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(m => m.Id == id);

            return student == null ? NotFound() : View(student);
        }

        // ─────────────────────────────────────────────────────────────
        // GET: Student/Create (Admin/Committee only)
        [Authorize(Roles = "Admin, Committee")] // Explicitly restrict Create to Admin, Committee
        public async Task<IActionResult> Create()
        {
            ViewBag.AcademicProgramId = new SelectList(await _context.AcademicPrograms.ToListAsync(), "Id", "Name");
            PopulateSemesterAndSessionDropdowns(); // Populate for new student
            return View();
        }

        // POST: Student/Create (Admin/Committee only)
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Committee")] // Explicitly restrict Create to Admin, Committee
        public async Task<IActionResult> Create(
            // Ensure AcademicSession and Semester are bound here
            [Bind("Name,Email,StudentNo,DateOfBirth,Gender,PhoneNumber,Address,AcademicProgramId,CGPA,AcademicSession,Semester")]
            Student student)
        {
            if (await _context.Students.AnyAsync(s => s.StudentNo == student.StudentNo))
            {
                ModelState.AddModelError("StudentNo", "This Student No. is already registered.");
            }

            if (await _context.Students.AnyAsync(s => s.Email == student.Email))
            {
                ModelState.AddModelError("Email", "This Email is already registered.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(student);
                await _context.SaveChangesAsync();

                var newUser = new IdentityUser { UserName = student.Email, Email = student.Email };
                var result = await _userManager.CreateAsync(newUser, "StudentDefault@123!"); // !!! CHANGE THIS PASSWORD IN PRODUCTION !!!
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(newUser, "Student");
                }
                else
                {
                    ModelState.AddModelError("", "Could not create associated login account.");
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }

                if (ModelState.IsValid) // Re-check ModelState after Identity operations
                {
                    return RedirectToAction(nameof(Index));
                }
            }
            ViewBag.AcademicProgramId = new SelectList(await _context.AcademicPrograms.ToListAsync(), "Id", "Name", student.AcademicProgramId);
            PopulateSemesterAndSessionDropdowns(student.Semester, student.AcademicSession); // Re-populate if validation fails
            return View(student);
        }

        // ─────────────────────────────────────────────────────────────
        // GET: Student/Edit/5 (Admin/Committee only)
        [Authorize(Roles = "Admin, Committee")] // Explicitly restrict Edit to Admin, Committee
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            ViewBag.AcademicProgramId = new SelectList(await _context.AcademicPrograms.ToListAsync(), "Id", "Name", student.AcademicProgramId);
            PopulateSemesterAndSessionDropdowns(student.Semester, student.AcademicSession); // Populate for existing student
            return View(student);
        }

        // POST: Student/Edit/5 (Admin/Committee only)
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Committee")] // Explicitly restrict Edit to Admin, Committee
        public async Task<IActionResult> Edit(
            int id,
            // Ensure AcademicSession and Semester are bound here
            [Bind("Id,Name,Email,StudentNo,DateOfBirth,Gender,PhoneNumber,Address,AcademicProgramId,CGPA,AcademicSession,Semester")]
            Student student)
        {
            if (id != student.Id) return NotFound();

            if (await _context.Students.AnyAsync(s => s.StudentNo == student.StudentNo && s.Id != student.Id))
            {
                ModelState.AddModelError("StudentNo", "This Student No. is already registered by another student.");
            }

            if (await _context.Students.AnyAsync(s => s.Email == student.Email && s.Id != student.Id))
            {
                ModelState.AddModelError("Email", "This Email is already registered by another student.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.AcademicProgramId = new SelectList(await _context.AcademicPrograms.ToListAsync(), "Id", "Name", student.AcademicProgramId);
            PopulateSemesterAndSessionDropdowns(student.Semester, student.AcademicSession); // Re-populate if validation fails
            return View(student);
        }

        // ─────────────────────────────────────────────────────────────
        // GET: Student/Delete/5 (Admin/Committee only)
        [Authorize(Roles = "Admin, Committee")] // Explicitly restrict Delete to Admin, Committee
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var student = await _context.Students
                                         .Include(s => s.AcademicProgram)
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(m => m.Id == id);

            return student == null ? NotFound() : View(student);
        }

        // POST: Student/Delete/5 (Admin/Committee only)
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin, Committee")] // Explicitly restrict Delete to Admin, Committee
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                var user = await _userManager.FindByEmailAsync(student.Email);
                if (user != null)
                {
                    var result = await _userManager.DeleteAsync(user);
                    if (!result.Succeeded)
                    {
                        TempData["ErrorMessage"] = "Failed to delete associated user account.";
                    }
                }

                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────
        private bool StudentExists(int id) =>
            _context.Students.Any(e => e.Id == id);


        // =====================================================================
        // NEW ACTION FOR STUDENT'S OWN AGREEMENT VIEW (NO VIEWMODEL)
        // =====================================================================

        [Authorize(Roles = "Student")] // Only Student role can access this
        public async Task<IActionResult> MySupervisorAgreement()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (!studentId.HasValue)
            {
                TempData["ErrorMessage"] = "Could not identify your student profile. Please ensure your account is linked to a student record.";
                // Redirect to login or a more informative page if profile isn't found
                return RedirectToAction("Login", "Account", new { Area = "Identity" });
            }

            // Find the approved StudentSupervisor entry for this student
            // We now include all necessary navigation properties for the view
            var studentSupervisor = await _context.StudentSupervisors
                                                  .Include(ss => ss.Student)
                                                      .ThenInclude(s => s.AcademicProgram) // Include student's Academic Program
                                                  .Include(ss => ss.Supervisor)
                                                      .ThenInclude(sv => sv.Lecturer)
                                                          .ThenInclude(l => l.AcademicProgram) // Include supervisor's Academic Program
                                                  .Where(ss => ss.StudentId == studentId.Value && ss.Status == SupervisorAssignmentStatus.Approved)
                                                  .FirstOrDefaultAsync();

            if (studentSupervisor == null)
            {
                ViewBag.Message = "Your supervisor assignment is currently pending approval or has not been assigned yet.";
                return View("NoAgreementYet"); // A dedicated view for "no agreement" scenario
            }

            // Store the dynamic agreement text in ViewBag or ViewData
            // This is the "general agreement" text
            ViewBag.AgreementText = $"This agreement confirms that {studentSupervisor.Student.Name} is officially assigned to {studentSupervisor.Supervisor.Lecturer.Name} as their {studentSupervisor.SupervisorType} for their Final Year Project. This assignment has been reviewed and approved by the committee on {studentSupervisor.ActionDate?.ToString("dd MMMM yyyy") ?? "N/A"}. Both parties agree to adhere to the university's guidelines and regulations regarding supervision and project progress. The student will be responsible for regular progress updates, and the supervisor will provide guidance and feedback as per the established guidelines.";

            // Pass the StudentSupervisor model directly to the view
            return View(studentSupervisor);
        }
    }
}
