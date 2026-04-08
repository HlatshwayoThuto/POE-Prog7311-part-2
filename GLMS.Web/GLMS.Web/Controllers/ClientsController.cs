// ─── CLIENTS CONTROLLER 
// Manages all CRUD operations for Clients in the GLMS system.
//
// WHAT IS A CLIENT?
// A Client is a company or individual that TechMove has a logistics relationship
// with. Clients own Contracts, and Contracts own ServiceRequests.
// So Client is the top-level entity in the system hierarchy:
// Client → Contract → ServiceRequest
//
// ROLE-BASED ACCESS:
// - Admin:   Full access — can view, create, edit and delete clients
// - Manager: Can only VIEW clients (no modifications allowed)
// - Viewer:  Can only VIEW clients (no modifications allowed)
//
// DESIGN PATTERN USED:
// Repository Pattern — this controller never touches GlmsDbContext directly.
// All database operations go through IClientRepository.
// This keeps the controller focused on HTTP handling only.
//
// AUTHENTICATION:
// [Authorize] at the class level means every single action in this controller
// requires the user to be logged in. If not logged in, they get redirected
// to the Login page automatically by ASP.NET Core Identity.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GLMS.Web.Models;
using GLMS.Web.Repositories;

namespace GLMS.Web.Controllers
{
    // [Authorize] applies to ALL actions in this controller
    // No one can access ANY client page without being logged in
    [Authorize]
    public class ClientsController : Controller
    {
        // The repository interface — hides all database logic from this controller
        // The actual implementation (ClientRepository) is injected by the DI container
        private readonly IClientRepository _repo;

        // Constructor Dependency Injection
        // ASP.NET Core automatically provides the IClientRepository implementation
        // that was registered in Program.cs as AddScoped<IClientRepository, ClientRepository>()
        public ClientsController(IClientRepository repo) => _repo = repo;

        // ── INDEX — View All Clients 
        // GET: /Clients
        // All three roles can see the client list
        // The view shows Edit/Delete buttons only if the user is Admin (handled in the view)
        [Authorize(Roles = "Admin,Manager,Viewer")]
        public async Task<IActionResult> Index()
        {
            // GetAllAsync() calls the repository which queries the database
            // Returns all clients as a list and passes them to the Index view
            var clients = await _repo.GetAllAsync();
            return View(clients);
        }

        // ── CREATE (GET) — Show the Create Form 
        // GET: /Clients/Create
        // Only Admin can create new clients
        // Returns an empty form for the user to fill in
        [Authorize(Roles = "Admin")]
        public IActionResult Create() => View();

        // ── CREATE (POST) — Save the New Client 
        // POST: /Clients/Create
        // Receives the form data submitted by the user
        // [ValidateAntiForgeryToken] prevents Cross-Site Request Forgery (CSRF) attacks
        // This is a security measure — it checks a hidden token in the form
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Client client)
        {
            // ModelState.IsValid checks all data annotations on the Client model
            // e.g. [Required], [EmailAddress], [Phone] etc.
            // If any validation fails, we return the form with error messages
            if (!ModelState.IsValid) return View(client);

            // Validation passed — save the new client to the database
            await _repo.AddAsync(client);

            // TempData stores a message that survives one redirect
            // The _Layout.cshtml displays this as a green success alert
            TempData["Success"] = "Client added successfully.";

            // Redirect to the Index page to show the updated client list
            return RedirectToAction(nameof(Index));
        }

        // ── EDIT (GET) — Show the Edit Form 
        // GET: /Clients/Edit/5
        // Loads the existing client data and pre-populates the form
        // Only Admin can edit clients
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            // Try to find the client by their ID in the database
            var client = await _repo.GetByIdAsync(id);

            // If the client doesn't exist, return a 404 Not Found response
            // This handles cases where someone manually types an invalid URL
            if (client == null) return NotFound();

            // Client found — return the Edit view with the client data pre-filled
            return View(client);
        }

        // ── EDIT (POST) — Save the Updated Client 
        // POST: /Clients/Edit/5
        // Receives the updated form data and saves it to the database
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(Client client)
        {
            // Validate the updated data against the model annotations
            if (!ModelState.IsValid) return View(client);

            // Save the updated client — EF Core tracks which fields changed
            await _repo.UpdateAsync(client);
            TempData["Success"] = "Client updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── DELETE (GET) — Show Confirmation Page 
        // GET: /Clients/Delete/5
        // Shows the client details with a confirmation message before deleting
        // This prevents accidental deletions — user must explicitly confirm
        // Only Admin can delete clients
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var client = await _repo.GetByIdAsync(id);
            if (client == null) return NotFound();

            // Return the Delete confirmation view showing the client's details
            return View(client);
        }

        // ── DELETE (POST) — Actually Delete the Client 
        // POST: /Clients/Delete/5
        // [ActionName("Delete")] maps this POST action to the "Delete" route
        // even though the method is named DeleteConfirmed
        // This is needed because you can't have two methods with the same name
        // and the same HTTP verb (GET Delete and POST Delete)
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Permanently remove the client from the database
            // Note: DeleteBehavior.Restrict in GlmsDbContext means this will
            // FAIL if the client has contracts — protecting data integrity
            await _repo.DeleteAsync(id);
            TempData["Success"] = "Client deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}