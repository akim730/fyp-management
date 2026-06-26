using fypSystem.Data; // Your DbContext namespace (assuming this remains FYPI_System.Data)
using fypSystem.Models; // Updated: Your AcademicProgram model namespace
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace fypSystem.Controllers // Assuming your controllers are still in FYPI_System.Controllers
{
    // This controller provides basic CRUD operations for AcademicProgram entities.
    public class AcademicProgramsController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Constructor: Injects the ApplicationDbContext to interact with the database.
        public AcademicProgramsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AcademicPrograms
        // Displays a list of all academic programs. This is the "Read All" operation.
        public async Task<IActionResult> Index()
        {
            // Retrieves all AcademicProgram entities from the database.
            return View(await _context.AcademicPrograms.ToListAsync());
        }

        // GET: AcademicPrograms/Details/5
        // Displays the details of a specific academic program. This is part of the "Read" operation.
        public async Task<IActionResult> Details(int? id)
        {
            // If no ID is provided, return a 404 Not Found.
            if (id == null)
            {
                return NotFound();
            }

            // Find the academic program by its ID.
            var academicProgram = await _context.AcademicPrograms
                .FirstOrDefaultAsync(m => m.Id == id);

            // If the program is not found, return a 404 Not Found.
            if (academicProgram == null)
            {
                return NotFound();
            }

            // Pass the found program to the view.
            return View(academicProgram);
        }

        // GET: AcademicPrograms/Create
        // Displays the form to create a new academic program. This prepares for the "Create" operation.
        public IActionResult Create()
        {
            // Returns an empty view for the user to fill in program details.
            return View();
        }

        // POST: AcademicPrograms/Create
        // Handles the form submission for creating a new academic program. This is the "Create" operation.
        [HttpPost]
        [ValidateAntiForgeryToken] // Protects against Cross-Site Request Forgery (CSRF) attacks.
        public async Task<IActionResult> Create([Bind("Id,Name,Code")] AcademicProgram academicProgram)
        {
            // Check if the submitted model data is valid based on data annotations in the model.
            if (ModelState.IsValid)
            {
                // Add the new academic program to the database context.
                _context.Add(academicProgram);
                // Save the changes to the database asynchronously.
                await _context.SaveChangesAsync();
                // Redirect the user back to the Index page to see the updated list.
                return RedirectToAction(nameof(Index));
            }
            // If ModelState is not valid, return the view with the entered data and validation errors.
            return View(academicProgram);
        }

        // GET: AcademicPrograms/Edit/5
        // Displays the form to edit an existing academic program. This prepares for the "Update" operation.
        public async Task<IActionResult> Edit(int? id)
        {
            // If no ID is provided, return a 404 Not Found.
            if (id == null)
            {
                return NotFound();
            }

            // Find the academic program by its ID.
            var academicProgram = await _context.AcademicPrograms.FindAsync(id);

            // If the program is not found, return a 404 Not Found.
            if (academicProgram == null)
            {
                return NotFound();
            }
            // Pass the found program to the view for editing.
            return View(academicProgram);
        }

        // POST: AcademicPrograms/Edit/5
        // Handles the form submission for updating an existing academic program. This is the "Update" operation.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Code")] AcademicProgram academicProgram)
        {
            // Check if the ID in the URL matches the ID of the submitted model.
            if (id != academicProgram.Id)
            {
                return NotFound(); // Mismatch indicates a potential tampering or incorrect request.
            }

            // Check if the submitted model data is valid.
            if (ModelState.IsValid)
            {
                try
                {
                    // Update the academic program in the database context.
                    _context.Update(academicProgram);
                    // Save the changes to the database.
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Handle concurrency conflicts (e.g., another user modified the record simultaneously).
                    if (!AcademicProgramExists(academicProgram.Id))
                    {
                        return NotFound(); // If the program no longer exists, return 404.
                    }
                    else
                    {
                        throw; // Re-throw other concurrency exceptions.
                    }
                }
                // Redirect to the Index page after successful update.
                return RedirectToAction(nameof(Index));
            }
            // If ModelState is not valid, return the view with errors.
            return View(academicProgram);
        }

        // GET: AcademicPrograms/Delete/5
        // Displays a confirmation page before deleting an academic program. This prepares for the "Delete" operation.
        public async Task<IActionResult> Delete(int? id)
        {
            // If no ID is provided, return a 404 Not Found.
            if (id == null)
            {
                return NotFound();
            }

            // Find the academic program by its ID.
            var academicProgram = await _context.AcademicPrograms
                .FirstOrDefaultAsync(m => m.Id == id);

            // If the program is not found, return a 404 Not Found.
            if (academicProgram == null)
            {
                return NotFound();
            }

            // Pass the found program to the view for confirmation.
            return View(academicProgram);
        }

        // POST: AcademicPrograms/Delete/5
        // Handles the deletion of an academic program after confirmation. This is the "Delete" operation.
        [HttpPost, ActionName("Delete")] // Maps this POST action to the "Delete" GET action.
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Find the academic program to be deleted.
            var academicProgram = await _context.AcademicPrograms.FindAsync(id);
            if (academicProgram != null)
            {
                // Remove the academic program from the database context.
                _context.AcademicPrograms.Remove(academicProgram);
            }

            // Save changes to the database.
            await _context.SaveChangesAsync();
            // Redirect to the Index page after successful deletion.
            return RedirectToAction(nameof(Index));
        }

        // Helper method to check if an academic program exists (used for concurrency handling).
        private bool AcademicProgramExists(int id)
        {
            return _context.AcademicPrograms.Any(e => e.Id == id);
        }
    }
}
