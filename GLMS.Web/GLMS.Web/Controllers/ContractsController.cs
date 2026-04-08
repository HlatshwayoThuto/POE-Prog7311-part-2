// ─── CONTRACTS CONTROLLER 
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
// - Admin:   Full access — search/filter, create, update status
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using GLMS.Web.Factories;
using GLMS.Web.Models;
using GLMS.Web.Repositories;
using GLMS.Web.Services;

namespace GLMS.Web.Controllers
{
    [Authorize] // All actions require login
    public class ContractsController : Controller
    {
        // IContractService wraps the repository AND handles Observer notifications
        // When a status changes, it automatically notifies AuditLog and Email observers
        private readonly IContractService _contractService;

        // Used only to populate the Client dropdown on the Create form
        private readonly IClientRepository _clientRepo;

        // Factory Pattern — creates Contract objects with correct business rules applied
        private readonly IContractFactory _factory;

        // Handles PDF validation and saving to the server file system
        private readonly IFileService _fileService;

        // All dependencies injected via constructor — registered in Program.cs
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

        // ── INDEX — Search and Filter Contracts 
        // GET: /Contracts or /Contracts?startDate=2026-01-01&status=Active
        //
        // ADMIN ONLY — this is the key requirement from the assignment brief:
        // "Implement a Search/Filter mechanism using LINQ to allow Admins to
        // find contracts by Date Range and Status"
        //
        // The three optional parameters come from the search form query string
        // If no filters are provided, ALL contracts are returned
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index(
            DateTime? startDate, DateTime? endDate, ContractStatus? status)
        {
            // Store the filter values in ViewBag so the search form
            // can repopulate itself with the user's last search terms
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;

            // SearchAsync uses LINQ to filter contracts
            // Passing null for a parameter means "don't filter by this"
            // So calling SearchAsync(null, null, null) returns ALL contracts
            var contracts = await _contractService.SearchAsync(startDate, endDate, status);
            return View(contracts);
        }

        // ── DETAILS — View a Single Contract 
        // GET: /Contracts/Details/5
        //
        // All roles can view contract details
        // The Details view shows the contract info, service requests,
        // and the status update form (only visible to Admins in the view)
        [Authorize(Roles = "Admin,Manager,Viewer")]
        public async Task<IActionResult> Details(int id)
        {
            // GetByIdAsync includes related Client and ServiceRequests
            // so we can display them all on the Details page
            var contract = await _contractService.GetByIdAsync(id);

            // Return 404 if contract ID doesn't exist
            if (contract == null) return NotFound();
            return View(contract);
        }

        // ── CREATE (GET) — Show the Create Form 
        // GET: /Contracts/Create
        // Admin and Manager can create new contracts
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create()
        {
            // Populate the Client dropdown so user can select which client
            // this contract belongs to
            await PopulateClientsDropDown();
            return View();
        }

        // ── CREATE (POST) — Save the New Contract 
        // POST: /Contracts/Create
        // enctype="multipart/form-data" in the view allows file uploads
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create(Contract vm, IFormFile? signedAgreement)
        {
            // Validate all required fields before proceeding
            if (!ModelState.IsValid)
            {
                await PopulateClientsDropDown();
                return View(vm);
            }

            // ── Factory Pattern 
            // Instead of "new Contract { ... }" we use the Factory
            // The Factory applies business rules:
            // - Standard ServiceLevel → Status starts as Draft
            // - Premium/Enterprise ServiceLevel → Status starts as Active
            // This keeps business logic out of the controller
            var contract = _factory.CreateContract(
                vm.ClientId, vm.Title, vm.StartDate, vm.EndDate, vm.ServiceLevel);

            // ── File Handling 
            // Check if the user uploaded a signed agreement PDF
            if (signedAgreement != null && signedAgreement.Length > 0)
            {
                try
                {
                    // FileService validates it's a PDF and saves it to the server
                    // Returns the server path and original filename
                    var (path, name) = await _fileService.SaveContractFileAsync(signedAgreement);

                    // Store the path in the database so we can retrieve it later
                    contract.SignedAgreementPath = path;
                    // Store original name so the download has a meaningful filename
                    contract.SignedAgreementOriginalName = name;
                }
                catch (InvalidOperationException ex)
                {
                    // FileService threw because the file was not a PDF
                    // Add the error to ModelState so it shows in the view
                    ModelState.AddModelError("SignedAgreement", ex.Message);
                    await PopulateClientsDropDown();
                    return View(vm);
                }
            }

            // Save the new contract to the database via the service
            await _contractService.CreateAsync(contract);
            TempData["Success"] = "Contract created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // ── UPDATE STATUS — Change a Contract's Status 
        // POST: /Contracts/UpdateStatus
        //
        // ADMIN ONLY — only Admins can change contract statuses
        //
        // This is the key action that triggers the Observer Pattern:
        // UpdateStatusAsync() in ContractService saves the new status AND
        // automatically notifies all registered observers:
        // 1. AuditLogObserver — logs the change
        // 2. EmailNotificationObserver — simulates sending an email
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id, ContractStatus newStatus)
        {
            try
            {
                // This single call does THREE things:
                // 1. Finds the contract in the database
                // 2. Updates the status
                // 3. Notifies all observers of the change
                await _contractService.UpdateStatusAsync(id, newStatus);
                TempData["Success"] = $"Contract status updated to {newStatus}.";
            }
            catch (KeyNotFoundException)
            {
                // Contract ID not found in the database
                return NotFound();
            }

            // Redirect back to the same contract's Details page
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── DOWNLOAD AGREEMENT — Stream PDF to Browser 
        // GET: /Contracts/DownloadAgreement/5
        //
        // All roles can download signed agreements
        // Reads the file from the server disk and streams it to the browser
        // The browser will prompt the user to open or save the PDF
        [Authorize(Roles = "Admin,Manager,Viewer")]
        public async Task<IActionResult> DownloadAgreement(int id)
        {
            var contract = await _contractService.GetByIdAsync(id);

            // Check that the contract exists AND has a file attached
            if (contract == null || contract.SignedAgreementPath == null)
                return NotFound();

            // Check the file still physically exists on the server disk
            // It could have been manually deleted from the file system
            if (!System.IO.File.Exists(contract.SignedAgreementPath))
                return NotFound("The file no longer exists on the server.");

            // Read the file bytes from disk asynchronously
            var bytes = await System.IO.File.ReadAllBytesAsync(contract.SignedAgreementPath);

            // Return the file as a downloadable PDF
            // "application/pdf" tells the browser what type of file it is
            // The original filename is used so the download has a meaningful name
            return File(bytes, "application/pdf",
                contract.SignedAgreementOriginalName ?? "agreement.pdf");
        }

        // ── PRIVATE HELPER — Populate Clients Dropdown 
        // Used by Create() to populate the Client selection dropdown
        // Fetches all clients from the database and converts them to a SelectList
        // SelectList is the ASP.NET MVC format for dropdown options
        private async Task PopulateClientsDropDown(int? selectedId = null)
        {
            var clients = await _clientRepo.GetAllAsync();

            // "Id" = the value stored when selected
            // "Name" = the text displayed in the dropdown
            ViewBag.Clients = new SelectList(clients, "Id", "Name", selectedId);
        }
    }
}