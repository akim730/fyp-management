// fypSystem.Controllers/CommitteeMembersController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

using fypSystem.Data;
using fypSystem.Models;
using System.Collections.Generic;
using System.IO;

namespace fypSystem.Controllers
{
    [Authorize(Roles = "Admin, Committee")]
    public class CommitteeMembersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public CommitteeMembersController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Helper to populate available Lecturers (for Evaluators dropdown)
        private async Task PopulateEvaluatorDropdown(int? selectedEvaluator1Id = null, int? selectedEvaluator2Id = null)
        {
            var lecturers = await _context.Lecturers.AsNoTracking().OrderBy(l => l.Name).ToListAsync();

            var selectListItems = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "--- Select Evaluator ---" }
            };
            selectListItems.AddRange(lecturers.Select(l => new SelectListItem { Value = l.Id.ToString(), Text = l.Name }));

            ViewBag.Evaluator1Id = new SelectList(selectListItems, "Value", "Text", selectedEvaluator1Id);
            ViewBag.Evaluator2Id = new SelectList(selectListItems, "Value", "Text", selectedEvaluator2Id);
        }

        // Helper to populate Semester and Academic Session dropdowns
        private void PopulateSemesterAndSessionDropdowns(string? selectedSemester = null, string? selectedAcademicSession = null)
        {
            var semesters = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "All Semesters" },
                new SelectListItem { Value = "Semester 1", Text = "Semester 1" },
                new SelectListItem { Value = "Semester 2", Text = "Semester 2" },
                new SelectListItem { Value = "Special Semester", Text = "Special Semester" }
            };
            ViewBag.Semesters = new SelectList(semesters, "Value", "Text", selectedSemester);

            // Get distinct academic sessions from existing proposals or generate some common ones
            var academicSessions = _context.ProjectProposals
                                           .Select(p => p.AcademicSession)
                                           .Distinct()
                                           .OrderByDescending(s => s)
                                           .ToList();

            // Add some default/future sessions if none exist or to provide options
            if (!academicSessions.Any())
            {
                academicSessions.Add($"{DateTime.Now.Year}/{DateTime.Now.Year + 1}");
                academicSessions.Add($"{DateTime.Now.Year - 1}/{DateTime.Now.Year}");
            }
            else
            {
                // Ensure current/next session is always an option
                var currentSession = $"{DateTime.Now.Year}/{DateTime.Now.Year + 1}";
                if (!academicSessions.Contains(currentSession))
                {
                    academicSessions.Insert(0, currentSession);
                }
            }

            var sessionListItems = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "All Sessions" }
            };
            sessionListItems.AddRange(academicSessions.Select(s => new SelectListItem { Value = s, Text = s }));
            ViewBag.AcademicSessions = new SelectList(sessionListItems, "Value", "Text", selectedAcademicSession);
        }

        // GET: CommitteeMembers/Index
        // This action lists all project proposals for committee review/assignment, with filtering.
        public async Task<IActionResult> Index(string? semesterFilter, string? academicSessionFilter)
        {
            IQueryable<ProjectProposal> proposals = _context.ProjectProposals
                                                          .Include(p => p.Student)
                                                              .ThenInclude(s => s.AcademicProgram)
                                                          .Include(p => p.MainSupervisorLecturer)
                                                          .Include(p => p.Evaluator1)
                                                          .Include(p => p.Evaluator2);

            if (!string.IsNullOrEmpty(semesterFilter))
            {
                proposals = proposals.Where(p => p.Semester == semesterFilter);
            }

            if (!string.IsNullOrEmpty(academicSessionFilter))
            {
                proposals = proposals.Where(p => p.AcademicSession == academicSessionFilter);
            }

            proposals = proposals.OrderByDescending(p => p.SubmissionDate).AsNoTracking();

            PopulateSemesterAndSessionDropdowns(semesterFilter, academicSessionFilter);

            return View(await proposals.ToListAsync());
        }

        // GET: CommitteeMembers/AssignEvaluators/{id}
        public async Task<IActionResult> AssignEvaluators(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Project proposal ID is missing.";
                return RedirectToAction(nameof(Index));
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
                return RedirectToAction(nameof(Index));
            }

            await PopulateEvaluatorDropdown(projectProposal.Evaluator1Id, projectProposal.Evaluator2Id);

            return View(projectProposal);
        }

        // POST: CommitteeMembers/AssignEvaluators/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignEvaluators(int id, int? Evaluator1Id, int? Evaluator2Id)
        {
            var projectProposal = await _context.ProjectProposals
                                                .FirstOrDefaultAsync(p => p.Id == id);

            if (projectProposal == null)
            {
                TempData["ErrorMessage"] = "Project proposal not found for assignment.";
                return NotFound();
            }

            if (!Evaluator1Id.HasValue)
            {
                ModelState.AddModelError("Evaluator1Id", "Evaluator 1 is required.");
            }
            if (!Evaluator2Id.HasValue)
            {
                ModelState.AddModelError("Evaluator2Id", "Evaluator 2 is required.");
            }

            if (Evaluator1Id.HasValue && Evaluator2Id.HasValue && Evaluator1Id.Value == Evaluator2Id.Value)
            {
                ModelState.AddModelError("Evaluator2Id", "Evaluator 1 and Evaluator 2 cannot be the same.");
            }

            if (projectProposal.MainSupervisorLecturerId.HasValue)
            {
                if (Evaluator1Id.HasValue && Evaluator1Id.Value == projectProposal.MainSupervisorLecturerId.Value)
                {
                    ModelState.AddModelError("Evaluator1Id", "Evaluator 1 cannot be the Main Supervisor.");
                }
                if (Evaluator2Id.HasValue && Evaluator2Id.Value == projectProposal.MainSupervisorLecturerId.Value)
                {
                    ModelState.AddModelError("Evaluator2Id", "Evaluator 2 cannot be the Main Supervisor.");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    projectProposal.Evaluator1Id = Evaluator1Id;
                    projectProposal.Evaluator2Id = Evaluator2Id;

                    _context.Update(projectProposal);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Evaluators assigned successfully!";
                    return RedirectToAction(nameof(AssignEvaluators), new { id = projectProposal.Id });
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

            projectProposal = await _context.ProjectProposals
                                                .Include(p => p.Student)
                                                    .ThenInclude(s => s.AcademicProgram)
                                                .Include(p => p.MainSupervisorLecturer)
                                                .Include(p => p.Evaluator1)
                                                .Include(p => p.Evaluator2)
                                                .AsNoTracking()
                                                .FirstOrDefaultAsync(m => m.Id == id);

            await PopulateEvaluatorDropdown(Evaluator1Id, Evaluator2Id);
            return View(projectProposal);
        }

        // GET: CommitteeMembers/CommitteeProjectDetails/{id}
        // Displays detailed information about a specific project for committee members.
        public async Task<IActionResult> CommitteeProjectDetails(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Project proposal ID is missing.";
                return RedirectToAction(nameof(Index));
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
                return RedirectToAction(nameof(Index));
            }

            return View(projectProposal);
        }

        // GET: CommitteeMembers/ReviewProposal/{id}
        // Displays the form for the committee to set the final status and add feedback.
        public async Task<IActionResult> ReviewProposal(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Project proposal ID is missing.";
                return RedirectToAction(nameof(Index));
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
                return RedirectToAction(nameof(Index));
            }

            // Populate ViewBag for the dropdown with final decision statuses
            var finalStatuses = Enum.GetValues(typeof(ProposalStatus))
                                    .Cast<ProposalStatus>()
                                    .Where(s => s == ProposalStatus.Approved ||
                                                s == ProposalStatus.Rejected ||
                                                s == ProposalStatus.ResubmissionRequired)
                                    .Select(s => new SelectListItem
                                    {
                                        Value = s.ToString(),
                                        Text = s.ToString().Replace("ResubmissionRequired", "Resubmission Required")
                                    })
                                    .ToList();

            ViewBag.FinalStatuses = new SelectList(finalStatuses, "Value", "Text", projectProposal.Status.ToString());

            return View(projectProposal);
        }

        // POST: CommitteeMembers/ReviewProposal/{id}
        // Processes the committee's final decision.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewProposal(int id, ProposalStatus finalStatus, string? committeeFeedback)
        {
            var projectProposal = await _context.ProjectProposals
                                                .FirstOrDefaultAsync(p => p.Id == id);

            if (projectProposal == null)
            {
                TempData["ErrorMessage"] = "Project proposal not found for review.";
                return NotFound();
            }

            // Ensure the selected status is one of the valid final decision statuses
            if (!(finalStatus == ProposalStatus.Approved ||
                  finalStatus == ProposalStatus.Rejected ||
                  finalStatus == ProposalStatus.ResubmissionRequired))
            {
                ModelState.AddModelError("finalStatus", "Invalid final status selected.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    projectProposal.Status = finalStatus;
                    projectProposal.CommitteeFeedback = committeeFeedback;
                    projectProposal.ReviewDate = DateTime.Today; // Set committee review date

                    _context.Update(projectProposal);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Proposal review submitted successfully and status updated!";
                    return RedirectToAction(nameof(CommitteeProjectDetails), new { id = projectProposal.Id });
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

            var finalStatusesOptions = Enum.GetValues(typeof(ProposalStatus))
                                            .Cast<ProposalStatus>()
                                            .Where(s => s == ProposalStatus.Approved ||
                                                        s == ProposalStatus.Rejected ||
                                                        s == ProposalStatus.ResubmissionRequired)
                                            .Select(s => new SelectListItem
                                            {
                                                Value = s.ToString(),
                                                Text = s.ToString().Replace("ResubmissionRequired", "Resubmission Required")
                                            })
                                            .ToList();
            ViewBag.FinalStatuses = new SelectList(finalStatusesOptions, "Value", "Text", finalStatus.ToString());

            return View(projectProposal);
        }


        // GET: CommitteeMembers/Download/{id}
        // Allows committee members to download the proposal file
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
