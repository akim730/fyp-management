// fypSystem.Controllers/SupervisorController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity; // Needed for UserManager

using fypSystem.Data;
using fypSystem.Models;
using System.Collections.Generic; // For List
using System.IO; // For FileStream and Path

namespace fypSystem.Controllers
{
    [Authorize(Roles = "Supervisor")] // Only users with the "Supervisor" role can access this controller
    public class SupervisorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public SupervisorController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Helper to get the current logged-in Lecturer's ID (who is a Supervisor)
        private async Task<int?> GetCurrentLecturerIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            var lecturer = await _context.Lecturers.AsNoTracking().FirstOrDefaultAsync(l => l.Email == user.Email);
            return lecturer?.Id;
        }

        // GET: Supervisor/AssignedProjects
        // Lists all project proposals where the current lecturer is assigned as Main Supervisor.
        public async Task<IActionResult> AssignedProjects()
        {
            var currentLecturerId = await GetCurrentLecturerIdAsync();
            if (!currentLecturerId.HasValue)
            {
                TempData["ErrorMessage"] = "Your lecturer profile could not be found. Please contact administration.";
                return RedirectToAction("Index", "Home");
            }

            var projects = await _context.ProjectProposals
                                         .Include(p => p.Student)
                                             .ThenInclude(s => s.AcademicProgram)
                                         .Include(p => p.MainSupervisorLecturer)
                                         .Include(p => p.Evaluator1) // Include Evaluator1
                                         .Include(p => p.Evaluator2) // Include Evaluator2
                                         .Where(p => p.MainSupervisorLecturerId == currentLecturerId.Value)
                                         .OrderByDescending(p => p.SubmissionDate)
                                         .AsNoTracking()
                                         .ToListAsync();

            return View(projects);
        }

        // GET: Supervisor/SupervisorProjectDetails/{id}
        // Displays detailed information about a specific project, including evaluator comments.
        public async Task<IActionResult> SupervisorProjectDetails(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Project proposal ID is missing.";
                return RedirectToAction(nameof(AssignedProjects));
            }

            var projectProposal = await _context.ProjectProposals
                                                .Include(p => p.Student)
                                                    .ThenInclude(s => s.AcademicProgram)
                                                .Include(p => p.MainSupervisorLecturer)
                                                .Include(p => p.Evaluator1)
                                                .Include(p => p.Evaluator2)
                                                .AsNoTracking()
                                                .FirstOrDefaultAsync(m => m.Id == id);

            if (projectProposal == null)
            {
                TempData["ErrorMessage"] = "Project proposal not found.";
                return RedirectToAction(nameof(AssignedProjects));
            }

            var currentLecturerId = await GetCurrentLecturerIdAsync();
            // Security check: Ensure the logged-in supervisor is the main supervisor for this project
            if (!currentLecturerId.HasValue || projectProposal.MainSupervisorLecturerId != currentLecturerId.Value)
            {
                TempData["ErrorMessage"] = "You are not authorized to view details for this project.";
                return Unauthorized();
            }

            return View(projectProposal);
        }


        // GET: Supervisor/Download/{id}
        // Allows supervisors to download the proposal file
        public async Task<IActionResult> Download(int id)
        {
            var projectProposal = await _context.ProjectProposals.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

            if (projectProposal == null || string.IsNullOrEmpty(projectProposal.FilePath))
            {
                TempData["ErrorMessage"] = "File not found for this proposal.";
                return NotFound();
            }

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", projectProposal.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
            {
                TempData["ErrorMessage"] = "The file does not exist on the server.";
                return NotFound();
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var fileName = Path.GetFileName(projectProposal.FilePath);

            return File(fileBytes, "application/pdf", fileName);
        }

        private bool ProjectProposalExists(int id)
        {
            return _context.ProjectProposals.Any(e => e.Id == id);
        }
    }
}
