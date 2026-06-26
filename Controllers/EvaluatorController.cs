// fypSystem.Controllers/EvaluatorController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity; // Needed for UserManager
using Microsoft.AspNetCore.Mvc.Rendering; // For SelectList

using fypSystem.Data;
using fypSystem.Models;
using System.Collections.Generic; // For List
using System.IO; // For FileStream and Path

namespace fypSystem.Controllers
{
    [Authorize(Roles = "Evaluator")] // Only users with the "Evaluator" role can access this controller
    public class EvaluatorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public EvaluatorController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Helper to get the current logged-in Lecturer's ID (who is an Evaluator)
        private async Task<int?> GetCurrentLecturerIdAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null;

            var lecturer = await _context.Lecturers.AsNoTracking().FirstOrDefaultAsync(l => l.Email == user.Email);
            return lecturer?.Id;
        }

        // GET: Evaluator/ProjectsToEvaluate
        // This action lists all project proposals assigned to the logged-in evaluator.
        public async Task<IActionResult> ProjectsToEvaluate()
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
                                         .Include(p => p.Evaluator1)
                                         .Include(p => p.Evaluator2)
                                         .Where(p => p.Evaluator1Id == currentLecturerId.Value || p.Evaluator2Id == currentLecturerId.Value)
                                         .OrderByDescending(p => p.SubmissionDate)
                                         .AsNoTracking()
                                         .ToListAsync();

            return View(projects);
        }

        // GET: Evaluator/SubmitEvaluation/{id}
        // Displays the form for an evaluator to submit their feedback for a specific proposal.
        public async Task<IActionResult> SubmitEvaluation(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Project proposal ID is missing.";
                return RedirectToAction(nameof(ProjectsToEvaluate));
            }

            var projectProposal = await _context.ProjectProposals
                                                .Include(p => p.Student)
                                                    .ThenInclude(s => s.AcademicProgram)
                                                .Include(p => p.MainSupervisorLecturer)
                                                .Include(p => p.Evaluator1)
                                                .Include(p => p.Evaluator2)
                                                .FirstOrDefaultAsync(m => m.Id == id);

            if (projectProposal == null)
            {
                TempData["ErrorMessage"] = "Project proposal not found.";
                return RedirectToAction(nameof(ProjectsToEvaluate));
            }

            var currentLecturerId = await GetCurrentLecturerIdAsync();
            if (!currentLecturerId.HasValue ||
                (projectProposal.Evaluator1Id != currentLecturerId.Value && projectProposal.Evaluator2Id != currentLecturerId.Value))
            {
                TempData["ErrorMessage"] = "You are not authorized to evaluate this proposal.";
                return RedirectToAction(nameof(ProjectsToEvaluate));
            }

            // Populate ViewBag for the dropdown
            // Filter the enum values to only include relevant evaluation statuses
            var evaluationStatuses = Enum.GetValues(typeof(ProposalStatus))
                                         .Cast<ProposalStatus>()
                                         .Where(s => s == ProposalStatus.Approved ||
                                                     s == ProposalStatus.AcceptedWithConditions ||
                                                     s == ProposalStatus.Rejected)
                                         .Select(s => new SelectListItem
                                         {
                                             Value = s.ToString(),
                                             Text = s.ToString().Replace("WithConditions", " With Conditions") // Make it more readable
                                         })
                                         .ToList();

            // Pre-select the current evaluator's recommendation if it exists
            ProposalStatus? currentRecommendation = null;
            string? currentFeedback = null;

            if (projectProposal.Evaluator1Id == currentLecturerId.Value)
            {
                currentRecommendation = projectProposal.Evaluator1Recommendation;
                currentFeedback = projectProposal.Evaluator1Feedback;
            }
            else if (projectProposal.Evaluator2Id == currentLecturerId.Value)
            {
                currentRecommendation = projectProposal.Evaluator2Recommendation;
                currentFeedback = projectProposal.Evaluator2Feedback;
            }

            ViewBag.EvaluationStatuses = new SelectList(evaluationStatuses, "Value", "Text", currentRecommendation?.ToString());
            ViewBag.CurrentFeedback = currentFeedback; // Pass current feedback to pre-fill textarea

            return View(projectProposal);
        }

        // POST: Evaluator/SubmitEvaluation/{id}
        // Processes the evaluator's feedback submission and updates the main proposal status.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitEvaluation(int id, ProposalStatus evaluationStatus, string comments)
        {
            var projectProposal = await _context.ProjectProposals
                                                .FirstOrDefaultAsync(p => p.Id == id);

            if (projectProposal == null)
            {
                TempData["ErrorMessage"] = "Project proposal not found for evaluation.";
                return NotFound();
            }

            var currentLecturerId = await GetCurrentLecturerIdAsync();
            if (!currentLecturerId.HasValue ||
                (projectProposal.Evaluator1Id != currentLecturerId.Value && projectProposal.Evaluator2Id != currentLecturerId.Value))
            {
                TempData["ErrorMessage"] = "You are not authorized to evaluate this proposal.";
                return Unauthorized();
            }

            // Basic validation for comments (optional, but good practice)
            if (string.IsNullOrWhiteSpace(comments))
            {
                ModelState.AddModelError("comments", "Comments are required for the evaluation.");
            }

            // Ensure the selected status is one of the valid evaluation statuses
            if (!(evaluationStatus == ProposalStatus.Approved ||
                  evaluationStatus == ProposalStatus.AcceptedWithConditions ||
                  evaluationStatus == ProposalStatus.Rejected))
            {
                ModelState.AddModelError("evaluationStatus", "Invalid evaluation status selected.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (projectProposal.Evaluator1Id == currentLecturerId.Value)
                    {
                        projectProposal.Evaluator1Recommendation = evaluationStatus;
                        projectProposal.Evaluator1Feedback = comments;
                        projectProposal.Evaluator1ReviewDate = DateTime.Today;
                    }
                    else if (projectProposal.Evaluator2Id == currentLecturerId.Value)
                    {
                        projectProposal.Evaluator2Recommendation = evaluationStatus;
                        projectProposal.Evaluator2Feedback = comments;
                        projectProposal.Evaluator2ReviewDate = DateTime.Today;
                    }

                    // --- NEW LOGIC: Update the main proposal status based on evaluator's recommendation ---
                    // This assumes that the evaluator's decision is final for the overall status.
                    // If you have two evaluators and need consensus, this logic would need to be more complex.
                    // For now, it directly sets the main status to the evaluator's recommendation.
                    projectProposal.Status = evaluationStatus;
                    // --- END NEW LOGIC ---

                    _context.Update(projectProposal);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Your evaluation has been submitted successfully and proposal status updated!";
                    return RedirectToAction(nameof(ProjectsToEvaluate));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProjectProposalExists(projectProposal.Id))
                    {
                        TempData["ErrorMessage"] = "Project proposal not found during update.";
                        return NotFound();
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "A concurrency error occurred while saving. Please try again.";
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                }
            }

            // If ModelState is not valid, re-populate dropdowns and return view
            projectProposal = await _context.ProjectProposals
                                                .Include(p => p.Student)
                                                    .ThenInclude(s => s.AcademicProgram)
                                                .Include(p => p.MainSupervisorLecturer)
                                                .Include(p => p.Evaluator1)
                                                .Include(p => p.Evaluator2)
                                                .AsNoTracking()
                                                .FirstOrDefaultAsync(m => m.Id == id);

            // Re-populate ViewBag for dropdown and comments
            var evaluationStatusesOptions = Enum.GetValues(typeof(ProposalStatus))
                                         .Cast<ProposalStatus>()
                                         .Where(s => s == ProposalStatus.Approved ||
                                                     s == ProposalStatus.AcceptedWithConditions ||
                                                     s == ProposalStatus.Rejected)
                                         .Select(s => new SelectListItem
                                         {
                                             Value = s.ToString(),
                                             Text = s.ToString().Replace("WithConditions", " With Conditions")
                                         })
                                         .ToList();
            ViewBag.EvaluationStatuses = new SelectList(evaluationStatusesOptions, "Value", "Text", evaluationStatus.ToString());
            ViewBag.CurrentFeedback = comments; // Use the submitted comments for re-display

            return View(projectProposal);
        }

        // GET: Evaluator/Download/{id}
        // Allows evaluators to download the proposal file
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
