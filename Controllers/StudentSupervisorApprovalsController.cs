// fypSystem.Controllers/StudentSupervisorApprovalsController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity; // Needed for UserManager if you want to log committee member actions
using System.Collections.Generic; // For List

using fypSystem.Data;
using fypSystem.Models;

namespace fypSystem.Controllers
{
    [Authorize(Roles = "Admin,Committee")] // Only Admin or Committee members can access this controller
    public class StudentSupervisorApprovalsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager; // Optional: to get current committee member's info

        public StudentSupervisorApprovalsController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: StudentSupervisorApprovals
        // Displays a list of all pending supervisor requests
        public async Task<IActionResult> Index()
        {
            var pendingRequests = await _context.StudentSupervisors
                                                .Include(ss => ss.Student)
                                                    .ThenInclude(s => s.AcademicProgram) // Include student's program
                                                .Include(ss => ss.Supervisor)
                                                    .ThenInclude(sv => sv.Lecturer) // Include supervisor's lecturer details
                                                .Where(ss => ss.Status == SupervisorAssignmentStatus.Pending)
                                                .OrderBy(ss => ss.RequestDate)
                                                .AsNoTracking()
                                                .ToListAsync();

            // Add a TempData message if no pending requests are found
            if (!pendingRequests.Any())
            {
                TempData["InfoMessage"] = "There are no pending supervisor requests at this time.";
            }

            return View(pendingRequests);
        }

        // GET: StudentSupervisorApprovals/Details/5
        // Displays details of a specific request and provides action buttons
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "Request ID not provided.";
                return NotFound();
            }

            var request = await _context.StudentSupervisors
                                        .Include(ss => ss.Student)
                                            .ThenInclude(s => s.AcademicProgram)
                                        .Include(ss => ss.Supervisor)
                                            .ThenInclude(sv => sv.Lecturer)
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(m => m.Id == id);

            if (request == null)
            {
                TempData["ErrorMessage"] = "Supervisor request not found or invalid ID.";
                return NotFound();
            }

            // Get current student count for the selected supervisor
            var supervisor = await _context.Supervisors
                                           .Include(s => s.StudentSupervisors) // Load supervisor's student assignments
                                           .FirstOrDefaultAsync(s => s.Id == request.SupervisorId);

            ViewBag.CurrentStudentCount = supervisor?.StudentSupervisors?.Count(ss => ss.Status == SupervisorAssignmentStatus.Approved) ?? 0;
            ViewBag.MaxStudents = supervisor?.MaxStudents ?? 0;

            return View(request);
        }

        // POST: StudentSupervisorApprovals/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string committeeRemarks)
        {
            var request = await _context.StudentSupervisors
                                        .Include(ss => ss.Student)
                                        .Include(ss => ss.Supervisor)
                                            .ThenInclude(sv => sv.StudentSupervisors) // Load supervisor's student assignments
                                        .FirstOrDefaultAsync(r => r.Id == id && r.Status == SupervisorAssignmentStatus.Pending);

            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found or already processed.";
                return RedirectToAction(nameof(Index));
            }

            // Check supervisor capacity BEFORE approving
            var supervisor = request.Supervisor; // Already loaded via Include
            var currentApprovedStudents = supervisor.StudentSupervisors?.Count(ss => ss.Status == SupervisorAssignmentStatus.Approved) ?? 0;

            if (currentApprovedStudents >= supervisor.MaxStudents)
            {
                TempData["ErrorMessage"] = $"Approval failed: Supervisor '{supervisor.Lecturer.Name}' has reached their maximum student capacity ({supervisor.MaxStudents}).";
                return RedirectToAction(nameof(Details), new { id = request.Id });
            }

            // Begin Transaction (important for multi-step updates like this)
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    request.Status = SupervisorAssignmentStatus.Approved;
                    request.ActionDate = DateTime.Today;
                    request.CommitteeRemarks = committeeRemarks;

                    _context.Update(request);

                    // Reject any other pending requests for the SAME STUDENT
                    var otherPendingRequests = await _context.StudentSupervisors
                                                            .Where(ss => ss.StudentId == request.StudentId &&
                                                                         ss.Id != request.Id &&
                                                                         ss.Status == SupervisorAssignmentStatus.Pending)
                                                            .ToListAsync();

                    foreach (var otherReq in otherPendingRequests)
                    {
                        otherReq.Status = SupervisorAssignmentStatus.Rejected;
                        otherReq.ActionDate = DateTime.Today;
                        otherReq.CommitteeRemarks = "Automatically rejected due to another supervisor request being approved for this student.";
                        _context.Update(otherReq);
                    }

                    // --- CRITICAL STEP: Update ProjectProposal with MainSupervisorLecturerId ---
                    // Find the student's active project proposal (assuming one active proposal per student for FYP)
                    var projectProposal = await _context.ProjectProposals
                                                        .FirstOrDefaultAsync(pp => pp.StudentId == request.StudentId);

                    if (projectProposal != null)
                    {
                        projectProposal.MainSupervisorLecturerId = request.Supervisor.LecturerId;
                        _context.Update(projectProposal);
                    }
                    else
                    {
                        // Log a warning if no project proposal is found for the student
                        // This might indicate a workflow issue: student has supervisor but no project.
                        TempData["WarningMessage"] = $"Warning: No active project proposal found for student '{request.Student.Name}'. Main supervisor link not updated.";
                    }
                    // --- END CRITICAL STEP ---


                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Supervisor request approved successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    TempData["ErrorMessage"] = $"An error occurred during approval: {ex.Message}";
                    // Log the exception for debugging
                    // _logger.LogError(ex, "Error approving student supervisor request.");
                    return RedirectToAction(nameof(Details), new { id = request.Id });
                }
            }
        }

        // POST: StudentSupervisorApprovals/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string committeeRemarks)
        {
            var request = await _context.StudentSupervisors
                                        .FirstOrDefaultAsync(r => r.Id == id && r.Status == SupervisorAssignmentStatus.Pending);

            if (request == null)
            {
                TempData["ErrorMessage"] = "Request not found or already processed.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                request.Status = SupervisorAssignmentStatus.Rejected;
                request.ActionDate = DateTime.Today;
                request.CommitteeRemarks = committeeRemarks;

                _context.Update(request);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Supervisor request rejected successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An error occurred during rejection: {ex.Message}";
                // Log the exception
                // _logger.LogError(ex, "Error rejecting student supervisor request.");
                return RedirectToAction(nameof(Details), new { id = request.Id });
            }
        }
    }
}