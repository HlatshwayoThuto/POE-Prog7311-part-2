// ─── CONTRACTS CONTROLLER ─────────────────────────────────────────────────────
// Handles all HTTP requests for Contract management.
//
// DESIGN PATTERNS USED:
// 1. Repository Pattern — data access via IContractService/IClientRepository
// 2. Factory Pattern — contract creation via IContractFactory
// 3. Observer Pattern — status changes trigger notifications via IContractService
//
// FILE HANDLING — PDF upload and download via IFileService
// SEARCH/FILTER — LINQ-based filtering passed to ContractService.SearchAsync()

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using GLMS.Web.Factories;
using GLMS.Web.Models;
using GLMS.Web.Repositories;
using GLMS.Web.Services;

namespace GLMS.Web.Controllers
{
    public class ContractsController : Controller
    {
        private readonly IContractService _contractService;
        private readonly IClientRepository _clientRepo;
        private readonly IContractFactory _factory;
        private readonly IFileService _fileService;

        public ContractsController(IContractService contractService,
            IClientRepository clientRepo, IContractFactory factory, IFileService fileService)
        {
            _contractService = contractService;
            _clientRepo = clientRepo;
            _factory = factory;
            _fileService = fileService;
        }

        // GET: /Contracts
        // Supports optional query parameters for filtering: ?startDate=&endDate=&status=
        public async Task<IActionResult> Index(
            DateTime? startDate, DateTime? endDate, ContractStatus? status)
        {
            // Pass filter values back to the view to repopulate the search form
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;

            // SearchAsync with null parameters returns ALL contracts (no filter applied)
            var contracts = await _contractService.SearchAsync(startDate, endDate, status);
            return View(contracts);
        }

        // GET: /Contracts/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var contract = await _contractService.GetByIdAsync(id);
            if (contract == null) return NotFound();
            return View(contract);
        }

        // GET: /Contracts/Create
        public async Task<IActionResult> Create()
        {
            await PopulateClientsDropDown();
            return View();
        }

        // POST: /Contracts/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Contract vm, IFormFile? signedAgreement)
        {
            if (!ModelState.IsValid)
            {
                await PopulateClientsDropDown();
                return View(vm);
            }

            // ── Factory Pattern ───────────────────────────────────────────────
            // Use ContractFactory instead of "new Contract { ... }"
            // Factory applies business rules (e.g. Premium starts Active)
            var contract = _factory.CreateContract(
                vm.ClientId, vm.Title, vm.StartDate, vm.EndDate, vm.ServiceLevel);

            // ── File Handling ─────────────────────────────────────────────────
            // If a file was uploaded, validate and save it via FileService
            if (signedAgreement != null && signedAgreement.Length > 0)
            {
                try
                {
                    var (path, name) = await _fileService.SaveContractFileAsync(signedAgreement);
                    contract.SignedAgreementPath = path;         // Server path stored in DB
                    contract.SignedAgreementOriginalName = name; // Original name for download
                }
                catch (InvalidOperationException ex)
                {
                    // FileService threw because file was not a PDF
                    ModelState.AddModelError("SignedAgreement", ex.Message);
                    await PopulateClientsDropDown();
                    return View(vm);
                }
            }

            await _contractService.CreateAsync(contract);
            TempData["Success"] = "Contract created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Contracts/UpdateStatus
        // Updates contract status and triggers Observer notifications
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, ContractStatus newStatus)
        {
            try
            {
                // UpdateStatusAsync saves the new status AND notifies observers
                // (AuditLogObserver logs it, EmailNotificationObserver simulates email)
                await _contractService.UpdateStatusAsync(id, newStatus);
                TempData["Success"] = $"Contract status updated to {newStatus}.";
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: /Contracts/DownloadAgreement/5
        // Reads the PDF from disk and streams it to the browser
        public async Task<IActionResult> DownloadAgreement(int id)
        {
            var contract = await _contractService.GetByIdAsync(id);
            if (contract == null || contract.SignedAgreementPath == null)
                return NotFound();

            if (!System.IO.File.Exists(contract.SignedAgreementPath))
                return NotFound("The file no longer exists on the server.");

            var bytes = await System.IO.File.ReadAllBytesAsync(contract.SignedAgreementPath);

            // Returns the file as a downloadable PDF with the original filename
            return File(bytes, "application/pdf",
                contract.SignedAgreementOriginalName ?? "agreement.pdf");
        }

        // Populates the Client dropdown on the Create form
        private async Task PopulateClientsDropDown(int? selectedId = null)
        {
            var clients = await _clientRepo.GetAllAsync();
            ViewBag.Clients = new SelectList(clients, "Id", "Name", selectedId);
        }
    }
}