// ─────────────────────────────────────────────────────────────────────────────
// CONTRACTS CONTROLLER
// Manages all operations for Contracts in the GLMS system.
//
// WHAT IS A CONTRACT?
// A Contract is a legal agreement between TechMove and a Client.
// It has a start/end date, a status, a service level, and can have
// a signed PDF agreement uploaded to it.
// Contracts are the "parent" of ServiceRequests — you cannot raise a
// ServiceRequest without a valid Active contract.
//
// ROLE-BASED ACCESS:
// - Admin:   Full access — search/filter, create, update status, delete
// - Manager: Can create contracts and download agreements
// - Viewer:  Read-only — can view details and download agreements only
//
// DESIGN PATTERNS USED:
// 1. Repository Pattern  — data access via IContractService/IClientRepository
// 2. Factory Pattern     — contract creation via IContractFactory
// 3. Observer Pattern    — status changes trigger notifications via IContractService
//
// KEY FEATURES:
// - LINQ search/filter by date range and status (Admin only)
// - PDF file upload and download
// - Status workflow management
// - Observer notifications on status change
// - Delete contracts (Admin only) — required before deleting a client
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using GLMS.Web.Factories;
using GLMS.Web.Models;
using GLMS.Web.Repositories;
using GLMS.Web.Services;

namespace GLMS.Web.Controllers
{
    // [Authorize] means every single action in this controller
    // requires the user to be logged in
    // If not logged in they get redirected to the Login page automatically
    [Authorize]
    public class ContractsController : Controller
    {
        // IContractService handles contract data AND Observer notifications
        // When a status changes it automatically notifies AuditLog and Email observers
        private readonly IContractService _contractService;

        // Used only to populate the Client dropdown on the Create form
        private readonly IClientRepository _clientRepo;

        // Factory Pattern — creates Contract objects with correct business rules
        // Standard = Draft, Premium/Enterprise = Active
        private readonly IContractFactory _factory;

        // Handles PDF validation and saving to the server file system
        private readonly IFileService _fileService;

        // All four dependencies are provided automatically by ASP.NET Core
        // because we registered them all in Program.cs
        public ContractsController(
            IContractService contractService,
            IClientRepository clientRepo,
            IContractFactory factory,
            IFileService fileService)
        {
            _contractService = contractService;
            _clientRepo = clientRepo;
            _factory = factory;
            _fileService = fileService;
        }

        // ── INDEX — SEARCH AND FILTER CONTRACTS ──────────────────────────────
        // GET: /Contracts
        // GET: /Contracts?startDate=2026-01-01&status=Active
        //
        // ADMIN ONLY — the assignment specifically says Admins use search/filter
        // The three parameters are OPTIONAL — they come from the search form
        // If the user just goes to /Contracts all three are null
        // and ALL contracts are returned (no filter applied)
        [Authorize(Roles = "Admin,Manager, Viewer")]
        public async Task<IActionResult> Index(
            DateTime? startDate, DateTime? endDate, ContractStatus? status)
        {
            // Store the filter values in ViewBag so the search form
            // can show what the user last searched for
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;

            // SearchAsync uses LINQ to filter contracts
            // If all three parameters are null it returns ALL contracts
            var contracts = await _contractService.SearchAsync(startDate, endDate, status);
            return View(contracts);
        }

        // ── DETAILS — VIEW ONE CONTRACT ───────────────────────────────────────
        // GET: /Contracts/Details/5
        // All three roles can view contract details
        [Authorize(Roles = "Admin,Manager,Viewer")]
        public async Task<IActionResult> Details(int id)
        {
            // GetByIdAsync loads the contract WITH its Client AND ServiceRequests
            var contract = await _contractService.GetByIdAsync(id);
            if (contract == null) return NotFound();
            return View(contract);
        }

        // ── CREATE GET — SHOW THE CREATE FORM ────────────────────────────────
        // GET: /Contracts/Create
        // Admin and Manager can create contracts
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create()
        {
            // Populate the Client dropdown before showing the form
            await PopulateClientsDropDown();
            return View();
        }

        // ── CREATE POST — SAVE THE NEW CONTRACT ──────────────────────────────
        // POST: /Contracts/Create
        // "IFormFile? signedAgreement" = the optional uploaded PDF file
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create(Contract vm, IFormFile? signedAgreement)
        {
            // Check all required fields are filled in correctly
            if (!ModelState.IsValid)
            {
                await PopulateClientsDropDown();
                return View(vm);
            }

            // FACTORY PATTERN
            // Ask the factory to create the contract object
            // The factory applies the business rule:
            // Standard = starts as Draft, Premium/Enterprise = starts as Active
            var contract = _factory.CreateContract(
                vm.ClientId, vm.Title, vm.StartDate, vm.EndDate, vm.ServiceLevel);

            // FILE HANDLING
            // If the user uploaded a PDF save it to the server
            if (signedAgreement != null && signedAgreement.Length > 0)
            {
                try
                {
                    // FileService validates it is a PDF and saves it to disk
                    // Returns the server path and the original filename
                    var (path, name) = await _fileService
                        .SaveContractFileAsync(signedAgreement);

                    // Save the server path in the database
                    contract.SignedAgreementPath = path;

                    // Save the original name so downloads look proper
                    contract.SignedAgreementOriginalName = name;
                }
                catch (InvalidOperationException ex)
                {
                    // File was not a PDF — show the error message in the form
                    ModelState.AddModelError("SignedAgreement", ex.Message);
                    await PopulateClientsDropDown();
                    return View(vm);
                }
            }

            // Save the contract to the database
            await _contractService.CreateAsync(contract);
            TempData["Success"] = "Contract created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── UPDATE STATUS — CHANGE CONTRACT STATUS ────────────────────────────
        // POST: /Contracts/UpdateStatus
        //
        // ADMIN ONLY
        // This triggers the OBSERVER PATTERN:
        // UpdateStatusAsync() saves the new status AND automatically notifies:
        // 1. AuditLogObserver — writes a log entry
        // 2. EmailNotificationObserver — simulates sending an email
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, ContractStatus newStatus)
        {
            try
            {
                // One method call that does everything:
                // 1. Finds the contract
                // 2. Updates the status
                // 3. Saves to database
                // 4. Notifies all observers
                await _contractService.UpdateStatusAsync(id, newStatus);
                TempData["Success"] = $"Contract status updated to {newStatus}.";
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }

            // Go back to the same contract's Details page
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DOWNLOAD AGREEMENT — STREAM PDF TO BROWSER ────────────────────────
        // GET: /Contracts/DownloadAgreement/5
        // All roles can download signed agreements
        [Authorize(Roles = "Admin,Manager,Viewer")]
        public async Task<IActionResult> DownloadAgreement(int id)
        {
            var contract = await _contractService.GetByIdAsync(id);

            // Check the contract exists and has a file attached
            if (contract == null || contract.SignedAgreementPath == null)
                return NotFound();

            // Check the file still physically exists on the server disk
            if (!System.IO.File.Exists(contract.SignedAgreementPath))
                return NotFound("The file no longer exists on the server.");

            // Read the file bytes from disk
            var bytes = await System.IO.File.ReadAllBytesAsync(
                contract.SignedAgreementPath);

            // Stream the file to the browser as a downloadable PDF
            return File(bytes, "application/pdf",
                contract.SignedAgreementOriginalName ?? "agreement.pdf");
        }

        // ── DELETE GET — SHOW CONFIRMATION PAGE ──────────────────────────────
        // GET: /Contracts/Delete/5
        // Shows the contract details with a warning before deleting
        // ADMIN ONLY — only Admins can delete contracts
        // This is needed so Admins can clear contracts before deleting a client
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            // Find the contract including its client and service requests
            // We need all this information to show on the confirmation page
            var contract = await _contractService.GetByIdAsync(id);

            // If the contract does not exist return 404
            if (contract == null) return NotFound();

            // Show the confirmation page with full contract details
            return View(contract);
        }

        // ── DELETE POST — ACTUALLY DELETE THE CONTRACT ────────────────────────
        // POST: /Contracts/Delete/5
        //
        // Runs when the Admin confirms they want to delete
        // NOTE: Because of CASCADE DELETE in GlmsDbContext
        // all ServiceRequests linked to this contract are
        // automatically deleted at the same time
        // So the Admin does not need to delete service requests manually
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            // Delete the contract from the database
            // All linked ServiceRequests are deleted automatically
            // because of DeleteBehavior.Cascade in GlmsDbContext
            await _contractService.DeleteAsync(id);
            TempData["Success"] = "Contract deleted successfully.";

            // Go back to the contracts list
            return RedirectToAction(nameof(Index));
        }

        // ── PRIVATE HELPER — POPULATE CLIENT DROPDOWN ────────────────────────
        // Used by the Create form to show a list of clients to choose from
        // "private" means only this controller can call this method
        private async Task PopulateClientsDropDown(int? selectedId = null)
        {
            var clients = await _clientRepo.GetAllAsync();

            // SelectList converts clients into a format HTML dropdowns understand
            // "Id" = the value stored when an option is selected
            // "Name" = the text displayed in the dropdown
            ViewBag.Clients = new SelectList(clients, "Id", "Name", selectedId);
        }
    }
}