// fypSystem.Controllers/LecturerController.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity; // Needed for UserManager

using fypSystem.Data;
using fypSystem.Models;
using System.Collections.Generic; // For List

namespace fypSystem.Controllers
{
    // Allow Admin and Committee to manage lecturers in general.
    // Specific actions can have more granular control.
    [Authorize(Roles = "Admin, Committee")]
    public class LecturerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;

        public LecturerController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Lecturer/Index (General Lecturer List - accessible by Admin, Committee)
        public async Task<IActionResult> Index()
        {
            var lecturers = await _context.Lecturers
                                          .Include(l => l.AcademicProgram)
                                          .AsNoTracking()
                                          .ToListAsync();
            return View(lecturers);
        }

        // GET: Lecturer/CommitteeMembers (Specific list for Committee Members - accessible by Admin)
        [Authorize(Roles = "Admin")] // Only Admin can view/manage the list of Committee Members specifically
        public async Task<IActionResult> CommitteeMembers()
        {
            // UPDATED: Include AcademicProgram when fetching lecturers
            var allLecturers = await _context.Lecturers
                                             .Include(l => l.AcademicProgram) // <--- ADDED THIS LINE
                                             .AsNoTracking()
                                             .ToListAsync();
            var committeeLecturers = new List<Lecturer>();

            foreach (var lecturer in allLecturers)
            {
                // Find the associated IdentityUser
                var identityUser = await _userManager.FindByEmailAsync(lecturer.Email);
                if (identityUser != null && await _userManager.IsInRoleAsync(identityUser, "Committee"))
                {
                    committeeLecturers.Add(lecturer);
                }
            }

            return View(committeeLecturers.OrderBy(l => l.Name).ToList());
        }


        // GET: Lecturer/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var lecturer = await _context.Lecturers
                                         .Include(l => l.AcademicProgram)
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(m => m.Id == id);

            return lecturer == null ? NotFound() : View(lecturer);
        }

        // GET: Lecturer/Create
        [Authorize(Roles = "Admin")] // Only Admin can create new lecturers (and assign roles later)
        public async Task<IActionResult> Create()
        {
            ViewBag.AcademicProgramId = new SelectList(await _context.AcademicPrograms.ToListAsync(), "Id", "Name");
            return View();
        }

        // POST: Lecturer/Create
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Name,Email,StaffNo,DateOfBirth,Gender,PhoneNumber,Address,AcademicProgramId")] Lecturer lecturer)
        {
            if (await _context.Lecturers.AnyAsync(l => l.StaffNo == lecturer.StaffNo))
            {
                ModelState.AddModelError("StaffNo", "This Staff No. is already registered.");
            }

            if (await _context.Lecturers.AnyAsync(l => l.Email == lecturer.Email))
            {
                ModelState.AddModelError("Email", "This Email is already registered.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(lecturer);
                await _context.SaveChangesAsync();

                // Create associated IdentityUser account
                var newUser = new IdentityUser { UserName = lecturer.Email, Email = lecturer.Email };
                var result = await _userManager.CreateAsync(newUser, "LecturerDefault@123!"); // !!! CHANGE THIS PASSWORD IN PRODUCTION !!!
                if (result.Succeeded)
                {
                    // Optionally assign a default role, e.g., "Supervisor" or "Evaluator"
                    // Admin would then manually assign "Committee" role via Identity management tools if needed.
                    // For now, let's assume they are just "Lecturer" by default, or you can pick one.
                    await _userManager.AddToRoleAsync(newUser, "Supervisor"); // Default role for new lecturers
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
                    TempData["SuccessMessage"] = "Lecturer created successfully!";
                    return RedirectToAction(nameof(Index));
                }
            }
            ViewBag.AcademicProgramId = new SelectList(await _context.AcademicPrograms.ToListAsync(), "Id", "Name", lecturer.AcademicProgramId);
            return View(lecturer);
        }

        // GET: Lecturer/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var lecturer = await _context.Lecturers.FindAsync(id);
            if (lecturer == null) return NotFound();

            ViewBag.AcademicProgramId = new SelectList(await _context.AcademicPrograms.ToListAsync(), "Id", "Name", lecturer.AcademicProgramId);
            return View(lecturer);
        }

        // POST: Lecturer/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Email,StaffNo,DateOfBirth,Gender,PhoneNumber,Address,AcademicProgramId")] Lecturer lecturer)
        {
            if (id != lecturer.Id) return NotFound();

            // Check for duplicate StaffNo or Email, excluding the current lecturer being edited
            if (await _context.Lecturers.AnyAsync(l => l.StaffNo == lecturer.StaffNo && l.Id != lecturer.Id))
            {
                ModelState.AddModelError("StaffNo", "This Staff No. is already registered by another lecturer.");
            }
            if (await _context.Lecturers.AnyAsync(l => l.Email == lecturer.Email && l.Id != lecturer.Id))
            {
                ModelState.AddModelError("Email", "This Email is already registered by another lecturer.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(lecturer);
                    await _context.SaveChangesAsync();

                    // Update associated IdentityUser email if it changed
                    var identityUser = await _userManager.FindByIdAsync(await _userManager.GetUserIdAsync(await _userManager.FindByEmailAsync(lecturer.Email))); // Find by old email if it changed
                    if (identityUser != null && identityUser.Email != lecturer.Email)
                    {
                        var setUserNameResult = await _userManager.SetUserNameAsync(identityUser, lecturer.Email);
                        var setEmailResult = await _userManager.SetEmailAsync(identityUser, lecturer.Email);
                        if (!setUserNameResult.Succeeded || !setEmailResult.Succeeded)
                        {
                            TempData["ErrorMessage"] = "Failed to update associated login account email.";
                            // Log errors
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LecturerExists(lecturer.Id)) return NotFound();
                    throw;
                }
                TempData["SuccessMessage"] = "Lecturer updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.AcademicProgramId = new SelectList(await _context.AcademicPrograms.ToListAsync(), "Id", "Name", lecturer.AcademicProgramId);
            return View(lecturer);
        }

        // GET: Lecturer/Delete/5
        [Authorize(Roles = "Admin")] // Only Admin can delete lecturers
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var lecturer = await _context.Lecturers
                                         .Include(l => l.AcademicProgram)
                                         .AsNoTracking()
                                         .FirstOrDefaultAsync(m => m.Id == id);

            return lecturer == null ? NotFound() : View(lecturer);
        }

        // POST: Lecturer/Delete/5
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var lecturer = await _context.Lecturers.FindAsync(id);
            if (lecturer != null)
            {
                // Delete associated IdentityUser account
                var user = await _userManager.FindByEmailAsync(lecturer.Email);
                if (user != null)
                {
                    var result = await _userManager.DeleteAsync(user);
                    if (!result.Succeeded)
                    {
                        TempData["ErrorMessage"] = "Failed to delete associated user account.";
                        // Log errors
                    }
                }

                _context.Lecturers.Remove(lecturer);
                await _context.SaveChangesAsync();
            }
            TempData["SuccessMessage"] = "Lecturer deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        private bool LecturerExists(int id) =>
            _context.Lecturers.Any(e => e.Id == id);
    }
}
