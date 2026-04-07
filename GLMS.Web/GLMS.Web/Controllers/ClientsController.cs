// ─── CLIENTS CONTROLLER ───────────────────────────────────────────────────────
// Handles all HTTP requests related to Client management.
// Uses the Repository Pattern — never accesses GlmsDbContext directly.
// All database operations go through IClientRepository.

using Microsoft.AspNetCore.Mvc;
using GLMS.Web.Models;
using GLMS.Web.Repositories;

namespace GLMS.Web.Controllers
{
    public class ClientsController : Controller
    {
        private readonly IClientRepository _repo;

        // IClientRepository injected via constructor Dependency Injection
        public ClientsController(IClientRepository repo) => _repo = repo;

        // GET: /Clients
        // Retrieves and displays all clients
        public async Task<IActionResult> Index()
            => View(await _repo.GetAllAsync());

        // GET: /Clients/Create
        // Returns the empty Create form
        public IActionResult Create() => View();

        // POST: /Clients/Create
        // Receives the form data, validates it, and saves to database
        [HttpPost, ValidateAntiForgeryToken] // Anti-forgery token prevents CSRF attacks
        public async Task<IActionResult> Create(Client client)
        {
            // ModelState.IsValid checks all [Required], [EmailAddress] etc. annotations
            if (!ModelState.IsValid) return View(client);
            await _repo.AddAsync(client);
            TempData["Success"] = "Client added successfully."; // Shows green alert in layout
            return RedirectToAction(nameof(Index));
        }

        // GET: /Clients/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var client = await _repo.GetByIdAsync(id);
            if (client == null) return NotFound(); // Returns 404 if client doesn't exist
            return View(client);
        }

        // POST: /Clients/Edit/5
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Client client)
        {
            if (!ModelState.IsValid) return View(client);
            await _repo.UpdateAsync(client);
            TempData["Success"] = "Client updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Clients/Delete/5
        // Shows confirmation page before deleting
        public async Task<IActionResult> Delete(int id)
        {
            var client = await _repo.GetByIdAsync(id);
            if (client == null) return NotFound();
            return View(client);
        }

        // POST: /Clients/Delete/5
        // ActionName("Delete") maps this POST to the Delete route
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _repo.DeleteAsync(id);
            TempData["Success"] = "Client deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}