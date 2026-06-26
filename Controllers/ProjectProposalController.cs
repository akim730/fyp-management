// fypSystem.Controllers/ProjectProposalController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO; // Required for file operations
using System.Linq;
using System.Threading.Tasks;
using fypSystem.Data;
using fypSystem.Models;
namespace fypSystem.Controllers
{
    [Authorize(Roles = "Student")] // Only students can submit proposals
    public class ProjectProposalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public ProjectProposalController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Helper method to get the current logged-in student's ID
        private async Task<int?> GetCurrentStudentIdAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return null;

            var userEmail = (await _userManager.FindByIdAsync(userId))?.Email;
            if (userEmail == null) return null;

            return (await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Email == userEmail))?.Id;
        }

        // Helper to populate available supervisors for the dropdown
        private async Task PopulateSupervisorDropdown(int? currentSupervisorId = null)
        {
            var supervisors = await _context.Supervisors
                                            .Include(s => s.Lecturer)
                                                .ThenInclude(l => l.AcademicProgram)
                                            .Include(s => s.StudentSupervisors)
                                            .AsNoTracking()
                                            .ToListAsync();

            var availableSupervisors = supervisors
                .Where(s =>
                {
                    var currentApprovedStudents = s.StudentSupervisors?.Count(ss => ss.Status == SupervisorAssignmentStatus.Approved) ?? 0;
                    return currentApprovedStudents < s.MaxStudents || s.Id == currentSupervisorId;
                })
                .OrderBy(s => s.Lecturer.Name)
                .Select(s => new
                {
                    s.Id,
                    Name = $"{s.Lecturer?.Name ?? "Unknown Lecturer"} ({s.Lecturer?.AcademicProgram?.Code ?? "N/A"}) - (Students: {s.StudentSupervisors?.Count(ss => ss.Status == SupervisorAssignmentStatus.Approved) ?? 0}/{s.MaxStudents})"
                })
                .ToList();

            var selectListItems = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "--- Select Preferred Supervisor ---", Selected = !currentSupervisorId.HasValue }
            };
            selectListItems.AddRange(availableSupervisors.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name, Selected = (s.Id == currentSupervisorId) }));

            ViewBag.PreferredSupervisorId = new SelectList(selectListItems, "Value", "Text", currentSupervisorId);
        }


        // GET: ProjectProposals/Create
        public async Task<IActionResult> Create()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (!studentId.HasValue)
            {
                TempData["ErrorMessage"] = "Student profile not found. Please ensure your account is correctly linked.";
                return RedirectToAction("Login", "Account"); // Or appropriate error page
            }

            // Check if student already has an active project proposal (optional, depending on your rules)
            var existingProposal = await _context.ProjectProposals
                                                 .AsNoTracking()
                                                 .FirstOrDefaultAsync(p => p.StudentId == studentId.Value &&
                                                                           (p.Status == ProposalStatus.Pending || p.Status == ProposalStatus.Approved));
            if (existingProposal != null)
            {
                TempData["WarningMessage"] = "You already have an active project proposal. You cannot submit another one at this time.";
                return RedirectToAction(nameof(Index)); // Redirect to their list of proposals
            }

            // Fetch current student's supervisor request status for display
            var student = await _context.Students
                                        .Include(s => s.StudentSupervisorAssignments)
                                            .ThenInclude(ssa => ssa.Supervisor)
                                                .ThenInclude(sv => sv.Lecturer)
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(s => s.Id == studentId.Value);

            var currentActiveRequest = student?.StudentSupervisorAssignments?
                                              .FirstOrDefault(ssa => ssa.Status == SupervisorAssignmentStatus.Approved || ssa.Status == SupervisorAssignmentStatus.Pending);

            ViewBag.CurrentSupervisorRequest = currentActiveRequest;

            // Populate supervisor dropdown, pre-selecting if there's an active request
            await PopulateSupervisorDropdown(currentActiveRequest?.SupervisorId);

            // Initialize a new ProjectProposal model for the form
            var proposal = new ProjectProposal
            {
                StudentId = studentId.Value,
                SubmissionDate = DateTime.Today,
                Status = ProposalStatus.Pending // Default status
            };

            return View(proposal);
        }

        // POST: ProjectProposals/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Id,Title,ProjectType,FilePath,SubmissionDate,StudentId,Status,PreferredSupervisorId,AcademicSession,Semester")] ProjectProposal projectProposal, // ADDED AcademicSession, Semester
            int? PreferredSupervisorId, IFormFile? proposalFile) // Add IFormFile for file upload
        {
            // Ensure StudentId is correctly set for security (from logged-in user, not form)
            var studentId = await GetCurrentStudentIdAsync();
            if (!studentId.HasValue || projectProposal.StudentId != studentId.Value)
            {
                TempData["ErrorMessage"] = "Unauthorized submission attempt.";
                return Unauthorized();
            }

            // Set default values that shouldn't come from the form directly
            projectProposal.SubmissionDate = DateTime.Today;
            projectProposal.Status = ProposalStatus.Pending;

            // --- Handle File Upload ---
            if (proposalFile != null && proposalFile.Length > 0)
            {
                // Basic validation: Check file type (e.g., PDF only)
                if (proposalFile.ContentType != "application/pdf")
                {
                    ModelState.AddModelError("proposalFile", "Only PDF files are allowed for proposal submission.");
                }
                else
                {
                    // Define the path to save the file
                    // Example: wwwroot/proposals/{studentId}/{filename.pdf}
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "proposals", studentId.Value.ToString());
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + proposalFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await proposalFile.CopyToAsync(fileStream);
                    }
                    projectProposal.FilePath = $"/proposals/{studentId.Value}/{uniqueFileName}"; // Save relative path
                }
            }
            else
            {
                ModelState.AddModelError("proposalFile", "Please upload a proposal file (PDF).");
            }
            // --- End File Upload ---


            // --- Supervisor Selection Logic ---
            StudentSupervisor newStudentSupervisorRequest = null;
            var studentEntity = await _context.Students
                                              .Include(s => s.StudentSupervisorAssignments)
                                              .FirstOrDefaultAsync(s => s.Id == studentId.Value);

            var existingActiveRequest = studentEntity?.StudentSupervisorAssignments?
                                                     .FirstOrDefault(ssa => ssa.Status == SupervisorAssignmentStatus.Approved || ssa.Status == SupervisorAssignmentStatus.Pending);

            if (PreferredSupervisorId.HasValue)
            {
                var selectedSupervisor = await _context.Supervisors
                                                       .Include(s => s.Lecturer)
                                                       .Include(s => s.StudentSupervisors)
                                                       .FirstOrDefaultAsync(s => s.Id == PreferredSupervisorId.Value);

                if (selectedSupervisor == null)
                {
                    ModelState.AddModelError("PreferredSupervisorId", "Selected supervisor not found. Please choose a valid supervisor.");
                }
                else
                {
                    if (existingActiveRequest != null)
                    {
                        // If student changes supervisor while one is active, this is an error
                        if (existingActiveRequest.SupervisorId != PreferredSupervisorId.Value)
                        {
                            ModelState.AddModelError("PreferredSupervisorId", "You already have a pending or approved supervisor request. Please wait for committee action or contact them to change.");
                        }
                        // If they select the same supervisor, no new request is created, existing one is fine.
                    }
                    else // No existing active request, so create a new one
                    {
                        var currentApprovedStudents = selectedSupervisor.StudentSupervisors?.Count(ss => ss.Status == SupervisorAssignmentStatus.Approved) ?? 0;
                        if (currentApprovedStudents >= selectedSupervisor.MaxStudents)
                        {
                            ModelState.AddModelError("PreferredSupervisorId", $"Supervisor {selectedSupervisor.Lecturer.Name} has reached their maximum student capacity ({selectedSupervisor.MaxStudents}). Please choose another.");
                        }
                        else
                        {
                            newStudentSupervisorRequest = new StudentSupervisor
                            {
                                StudentId = studentId.Value,
                                SupervisorId = PreferredSupervisorId.Value,
                                SupervisorType = "Main Supervisor", // Assuming this is for Main Supervisor
                                Status = SupervisorAssignmentStatus.Pending,
                                RequestDate = DateTime.Today
                            };
                            _context.StudentSupervisors.Add(newStudentSupervisorRequest);
                        }
                    }
                }
            }
            else // No supervisor selected
            {
                if (existingActiveRequest == null) // If no active request and no selection, it's an error
                {
                    ModelState.AddModelError("PreferredSupervisorId", "Please select a preferred supervisor to submit your proposal.");
                }
            }
            // --- End Supervisor Selection Logic ---


            if (ModelState.IsValid)
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        _context.Add(projectProposal);
                        await _context.SaveChangesAsync(); // Save proposal and supervisor request (if new)

                        // If a new supervisor request was successfully created and saved,
                        // and if the proposal needs to link to the *approved* supervisor,
                        // this link happens in StudentSupervisorApprovalsController when the request is approved.
                        // For now, projectProposal.MainSupervisorLecturerId will remain null until approved.

                        await transaction.CommitAsync();
                        TempData["SuccessMessage"] = "Project proposal submitted successfully and supervisor request sent!";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = $"An error occurred during submission: {ex.Message}";
                        // Log the exception: _logger.LogError(ex, "Error submitting project proposal.");
                    }
                }
            }

            // If ModelState is not valid or an error occurred, re-populate dropdowns and return view
            ViewBag.CurrentSupervisorRequest = existingActiveRequest; // Re-use the fetched request
            await PopulateSupervisorDropdown(PreferredSupervisorId ?? existingActiveRequest?.SupervisorId);
            return View(projectProposal);
        }

        // GET: ProjectProposals
        public async Task<IActionResult> Index()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (!studentId.HasValue)
            {
                TempData["ErrorMessage"] = "Student profile not found. Please ensure your account is correctly linked.";
                return RedirectToAction("Login", "Account");
            }

            var proposals = await _context.ProjectProposals
                                          .Include(p => p.Student)
                                          .Include(p => p.MainSupervisorLecturer)
                                          .Where(p => p.StudentId == studentId.Value)
                                          .OrderByDescending(p => p.SubmissionDate)
                                          .AsNoTracking()
                                          .ToListAsync();
            return View(proposals);
        }

        // GET: ProjectProposals/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
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
                return NotFound();
            }

            // Security check: Ensure the logged-in student owns this proposal
            var studentId = await GetCurrentStudentIdAsync();
            if (!studentId.HasValue || projectProposal.StudentId != studentId.Value)
            {
                TempData["ErrorMessage"] = "Unauthorized access to proposal details.";
                return Unauthorized();
            }

            return View(projectProposal);
        }

        // GET: ProjectProposals/Edit/5
        // Students can only edit proposals that are "ResubmissionRequired", "Pending", or "AcceptedWithConditions"
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var projectProposal = await _context.ProjectProposals
                .Include(p => p.Student)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (projectProposal == null)
            {
                return NotFound();
            }

            // Security check
            var studentId = await GetCurrentStudentIdAsync();
            if (!studentId.HasValue || projectProposal.StudentId != studentId.Value)
            {
                TempData["ErrorMessage"] = "Unauthorized access to edit proposal.";
                return Unauthorized();
            }

            // --- UPDATED: Allow editing for AcceptedWithConditions status ---
            if (projectProposal.Status != ProposalStatus.Pending &&
                projectProposal.Status != ProposalStatus.ResubmissionRequired &&
                projectProposal.Status != ProposalStatus.AcceptedWithConditions) // Added AcceptedWithConditions
            {
                TempData["ErrorMessage"] = $"Proposal cannot be edited in '{projectProposal.Status}' status.";
                return RedirectToAction(nameof(Details), new { id = projectProposal.Id });
            }

            // Fetch current student's supervisor request status for display
            var student = await _context.Students
                                        .Include(s => s.StudentSupervisorAssignments)
                                            .ThenInclude(ssa => ssa.Supervisor)
                                                .ThenInclude(sv => sv.Lecturer)
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(s => s.Id == studentId.Value);

            var currentActiveRequest = student?.StudentSupervisorAssignments?
                                              .FirstOrDefault(ssa => ssa.Status == SupervisorAssignmentStatus.Approved || ssa.Status == SupervisorAssignmentStatus.Pending);

            ViewBag.CurrentSupervisorRequest = currentActiveRequest;
            await PopulateSupervisorDropdown(currentActiveRequest?.SupervisorId);


            return View(projectProposal);
        }

        // POST: ProjectProposals/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,Title,ProjectType,FilePath,SubmissionDate,StudentId,Status,PreferredSupervisorId,AcademicSession,Semester")] ProjectProposal projectProposal, // ADDED AcademicSession, Semester
            int? PreferredSupervisorId, IFormFile? proposalFile)
        {
            if (id != projectProposal.Id)
            {
                return NotFound();
            }

            var studentId = await GetCurrentStudentIdAsync();
            if (!studentId.HasValue || projectProposal.StudentId != studentId.Value)
            {
                TempData["ErrorMessage"] = "Unauthorized submission attempt.";
                return Unauthorized();
            }

            var proposalToUpdate = await _context.ProjectProposals
                .Include(p => p.Student)
                .Include(p => p.Student.StudentSupervisorAssignments) // Include for supervisor logic
                .FirstOrDefaultAsync(m => m.Id == id);

            if (proposalToUpdate == null)
            {
                TempData["ErrorMessage"] = "Proposal not found for update.";
                return NotFound();
            }

            // --- UPDATED: Allow editing for AcceptedWithConditions status ---
            if (proposalToUpdate.Status != ProposalStatus.Pending &&
                proposalToUpdate.Status != ProposalStatus.ResubmissionRequired &&
                proposalToUpdate.Status != ProposalStatus.AcceptedWithConditions) // Added AcceptedWithConditions
            {
                TempData["ErrorMessage"] = $"Proposal cannot be edited in '{proposalToUpdate.Status}' status.";
                return RedirectToAction(nameof(Details), new { id = proposalToUpdate.Id });
            }

            // Preserve original status if not explicitly changing it here.
            // When a student resubmits, the status should revert to Pending.
            var originalStatus = proposalToUpdate.Status;

            proposalToUpdate.Title = projectProposal.Title;
            proposalToUpdate.ProjectType = projectProposal.ProjectType;
            proposalToUpdate.SubmissionDate = DateTime.Today; // Update submission date on resubmission
            proposalToUpdate.Status = ProposalStatus.Pending; // Status reverts to Pending on resubmission
            proposalToUpdate.AcademicSession = projectProposal.AcademicSession; // ADDED
            proposalToUpdate.Semester = projectProposal.Semester; // ADDED


            if (proposalFile != null && proposalFile.Length > 0)
            {
                if (proposalFile.ContentType != "application/pdf")
                {
                    ModelState.AddModelError("proposalFile", "Only PDF files are allowed for proposal submission.");
                }
                else
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "proposals", studentId.Value.ToString());
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Delete old file if exists
                    if (!string.IsNullOrEmpty(proposalToUpdate.FilePath))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", proposalToUpdate.FilePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + proposalFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await proposalFile.CopyToAsync(fileStream);
                    }
                    proposalToUpdate.FilePath = $"/proposals/{studentId.Value}/{uniqueFileName}";
                }
            }
            // If no new file is uploaded, keep the existing FilePath. No ModelState error if it's an edit and file exists.
            else if (string.IsNullOrEmpty(proposalToUpdate.FilePath)) // If no file exists AND no new file uploaded
            {
                ModelState.AddModelError("proposalFile", "Please upload a proposal file (PDF).");
            }


            // --- Supervisor Selection Logic for Edit ---
            // This logic is similar to Create, but needs to handle existing requests carefully.
            var studentEntity = proposalToUpdate.Student; // Already included
            var existingActiveRequest = studentEntity?.StudentSupervisorAssignments?
                                                     .FirstOrDefault(ssa => ssa.Status == SupervisorAssignmentStatus.Approved || ssa.Status == SupervisorAssignmentStatus.Pending);

            if (PreferredSupervisorId.HasValue)
            {
                var selectedSupervisor = await _context.Supervisors
                                                       .Include(s => s.Lecturer)
                                                       .Include(s => s.StudentSupervisors)
                                                       .FirstOrDefaultAsync(s => s.Id == PreferredSupervisorId.Value);

                if (selectedSupervisor == null)
                {
                    ModelState.AddModelError("PreferredSupervisorId", "Selected supervisor not found. Please choose a valid supervisor.");
                }
                else
                {
                    if (existingActiveRequest != null)
                    {
                        if (existingActiveRequest.SupervisorId != PreferredSupervisorId.Value)
                        {
                            ModelState.AddModelError("PreferredSupervisorId", "You already have a pending or approved supervisor request. Please wait for committee action or contact them to change.");
                        }
                    }
                    else // No existing active request, create a new one
                    {
                        var currentApprovedStudents = selectedSupervisor.StudentSupervisors?.Count(ss => ss.Status == SupervisorAssignmentStatus.Approved) ?? 0;
                        if (currentApprovedStudents >= selectedSupervisor.MaxStudents)
                        {
                            ModelState.AddModelError("PreferredSupervisorId", $"Supervisor {selectedSupervisor.Lecturer.Name} has reached their maximum student capacity ({selectedSupervisor.MaxStudents}). Please choose another.");
                        }
                        else
                        {
                            var newRequest = new StudentSupervisor
                            {
                                StudentId = studentId.Value,
                                SupervisorId = PreferredSupervisorId.Value,
                                SupervisorType = "Main Supervisor",
                                Status = SupervisorAssignmentStatus.Pending,
                                RequestDate = DateTime.Today
                            };
                            _context.StudentSupervisors.Add(newRequest);
                        }
                    }
                }
            }
            else // No supervisor selected
            {
                if (existingActiveRequest == null)
                {
                    ModelState.AddModelError("PreferredSupervisorId", "Please select a preferred supervisor.");
                }
            }
            // --- End Supervisor Selection Logic for Edit ---


            if (ModelState.IsValid)
            {
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        _context.Update(proposalToUpdate);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        TempData["SuccessMessage"] = "Project proposal updated successfully!";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        await transaction.RollbackAsync();
                        if (!ProjectProposalExists(projectProposal.Id))
                        {
                            TempData["ErrorMessage"] = "Proposal not found during update.";
                            return NotFound();
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "A concurrency error occurred while saving. Please try again.";
                        }
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        TempData["ErrorMessage"] = $"An error occurred during update: {ex.Message}";
                    }
                }
            }

            // If ModelState is not valid or an error occurred, re-populate dropdowns and return view
            var studentForView = await _context.Students
                                               .Include(s => s.StudentSupervisorAssignments)
                                                   .ThenInclude(ssa => ssa.Supervisor)
                                                       .ThenInclude(sv => sv.Lecturer)
                                               .AsNoTracking()
                                               .FirstOrDefaultAsync(s => s.Id == studentId.Value);

            ViewBag.CurrentSupervisorRequest = studentForView?.StudentSupervisorAssignments?
                                                          .FirstOrDefault(ssa => ssa.Status == SupervisorAssignmentStatus.Approved || ssa.Status == SupervisorAssignmentStatus.Pending);
            await PopulateSupervisorDropdown(PreferredSupervisorId ?? ViewBag.CurrentSupervisorRequest?.SupervisorId);

            // Important: Restore the original status for display if validation fails,
            // as the form expects the model's current status for rendering.
            proposalToUpdate.Status = originalStatus;
            return View(proposalToUpdate);
        }

        // GET: ProjectProposals/Delete/5
        [Authorize(Roles = "Admin,Student")] // Allow admin and student to delete their own proposals
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var projectProposal = await _context.ProjectProposals
                .Include(p => p.Student)
                .Include(p => p.MainSupervisorLecturer)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (projectProposal == null)
            {
                return NotFound();
            }

            // Security check: Only the owner or Admin can delete
            var studentId = await GetCurrentStudentIdAsync();
            if (User.IsInRole("Student") && (!studentId.HasValue || projectProposal.StudentId != studentId.Value))
            {
                TempData["ErrorMessage"] = "Unauthorized access to delete proposal.";
                return Unauthorized();
            }

            return View(projectProposal);
        }

        // POST: ProjectProposals/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Student")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var projectProposal = await _context.ProjectProposals.FindAsync(id);
            if (projectProposal == null)
            {
                TempData["ErrorMessage"] = "Proposal not found.";
                return RedirectToAction(nameof(Index));
            }

            // Security check: Only the owner or Admin can delete
            var studentId = await GetCurrentStudentIdAsync();
            if (User.IsInRole("Student") && (!studentId.HasValue || projectProposal.StudentId != studentId.Value))
            {
                TempData["ErrorMessage"] = "Unauthorized action.";
                return Unauthorized();
            }

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Optionally, delete the associated file from wwwroot
                    if (!string.IsNullOrEmpty(projectProposal.FilePath))
                    {
                        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", projectProposal.FilePath.TrimStart('/'));
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }

                    _context.ProjectProposals.Remove(projectProposal);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Project proposal deleted successfully.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"An error occurred during deletion: {ex.Message}";
                    // Log the exception
                    return RedirectToAction(nameof(Index));
                }
            }
        }

        private bool ProjectProposalExists(int id)
        {
            return _context.ProjectProposals.Any(e => e.Id == id);
        }
    }
}
