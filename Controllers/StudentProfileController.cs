// fypSystem.Controllers/StudentProfileController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

using fypSystem.Data;
using fypSystem.Models;

namespace fypSystem.Controllers
{
    [Authorize(Roles = "Student")]
    public class StudentProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public StudentProfileController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<int?> GetCurrentStudentIdAsync()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return null;

            var userEmail = (await _userManager.FindByIdAsync(userId))?.Email;
            if (userEmail == null) return null;

            return (await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.Email == userEmail))?.Id;
        }

        // GET: StudentProfile/Edit
        public async Task<IActionResult> Edit()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (!studentId.HasValue)
            {
                TempData["ErrorMessage"] = "Student profile not found. Please ensure your account is correctly linked.";
                return RedirectToAction("Login", "Account");
            }

            var student = await _context.Students
                                        .Include(s => s.AcademicProgram)
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(s => s.Id == studentId.Value);

            if (student == null)
            {
                TempData["ErrorMessage"] = "Student profile not found.";
                return NotFound();
            }

            return View(student);
        }

        // POST: StudentProfile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            [Bind("Id,PhoneNumber,Address")] Student student)
        {
            var currentStudentId = await GetCurrentStudentIdAsync();
            if (!currentStudentId.HasValue || currentStudentId.Value != student.Id)
            {
                TempData["ErrorMessage"] = "Unauthorized access attempt.";
                return Unauthorized();
            }

            var studentToUpdate = await _context.Students
                                                .FirstOrDefaultAsync(s => s.Id == student.Id);

            if (studentToUpdate == null)
            {
                TempData["ErrorMessage"] = "Student profile not found for update.";
                return NotFound();
            }

            studentToUpdate.PhoneNumber = student.PhoneNumber;
            studentToUpdate.Address = student.Address;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(studentToUpdate);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Profile updated successfully!";
                    return RedirectToAction(nameof(Edit));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.Id))
                    {
                        TempData["ErrorMessage"] = "Student profile not found during update.";
                        return NotFound();
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "A concurrency error occurred while saving. Please try again.";
                        studentToUpdate = await _context.Students
                                                        .Include(s => s.AcademicProgram)
                                                        .AsNoTracking()
                                                        .FirstOrDefaultAsync(s => s.Id == student.Id);
                        return View(studentToUpdate);
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                    studentToUpdate = await _context.Students
                                                    .Include(s => s.AcademicProgram)
                                                    .AsNoTracking()
                                                    .FirstOrDefaultAsync(s => s.Id == student.Id);
                    return View(studentToUpdate);
                }
            }

            studentToUpdate = await _context.Students
                                            .Include(s => s.AcademicProgram)
                                            .AsNoTracking()
                                            .FirstOrDefaultAsync(s => s.Id == student.Id);
            return View(studentToUpdate);
        }

        private bool StudentExists(int id)
        {
            return _context.Students.Any(e => e.Id == id);
        }
    }
}