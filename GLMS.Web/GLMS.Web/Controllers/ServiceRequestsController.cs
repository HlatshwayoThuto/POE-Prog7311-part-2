// ─────────────────────────────────────────────────────────────────────────────
// SERVICE REQUESTS CONTROLLER
// Manages all operations for Service Requests in the GLMS system.
//
// WHAT IS A SERVICE REQUEST?
// A service request is a logistics task raised against an active contract.
// For example: "Ship 500 units from Johannesburg to London"
// Every request has a USD cost that gets automatically converted to ZAR
// using a live exchange rate from an external currency API.
//
// ROLE-BASED ACCESS:
// - Admin:   Full access — view, create, update status
// - Manager: Can view and create service requests
// - Viewer:  Read-only — can only view service requests
//
// KEY FEATURES:
// 1. WORKFLOW VALIDATION — blocks requests on Expired/OnHold contracts
// 2. LIVE CURRENCY CONVERSION — shows ZAR preview as user types USD
// 3. STATUS MANAGEMENT — Admin can update request status
// 4. SEPARATION OF CONCERNS — workflow rules live in WorkflowService
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using GLMS.Web.Models;
using GLMS.Web.Repositories;
using GLMS.Web.Services;

namespace GLMS.Web.Controllers
{
    // Every action in this controller requires the user to be logged in
    [Authorize]
    public class ServiceRequestsController : Controller
    {
        // Repository for ServiceRequest database operations
        // This is the ONLY thing that talks to the database for service requests
        private readonly IServiceRequestRepository _repo;

        // Used to load the parent Contract for workflow validation
        // and to populate the Contract dropdown on the Create form
        private readonly IContractRepository _contractRepo;

        // Handles the external Currency API call and USD to ZAR conversion
        // Calls open.er-api.com to get the live exchange rate
        private readonly ICurrencyService _currencyService;

        // Contains the business rules about when service requests can be raised
        // Separated from the controller to keep this class clean and focused
        private readonly IWorkflowService _workflowService;

        // Constructor — all four dependencies are injected automatically
        // by ASP.NET Core because we registered them in Program.cs
        public ServiceRequestsController(
            IServiceRequestRepository repo,
            IContractRepository contractRepo,
            ICurrencyService currencyService,
            IWorkflowService workflowService)
        {
            _repo = repo;
            _contractRepo = contractRepo;
            _currencyService = currencyService;
            _workflowService = workflowService;
        }

        // ── INDEX — VIEW ALL SERVICE REQUESTS ────────────────────────────────
        // GET: /ServiceRequests
        // All three roles can see the service requests list
        // The list shows contract name, client, description, USD/ZAR costs etc.
        [Authorize(Roles = "Admin,Manager,Viewer")]
        public async Task<IActionResult> Index()
        {
            // GetAllAsync includes Contract and Client via eager loading
            // so the list can show the contract title and client name
            // without needing extra database queries
            return View(await _repo.GetAllAsync());
        }

        // ── DETAILS — VIEW ONE SERVICE REQUEST ───────────────────────────────
        // GET: /ServiceRequests/Details/5
        // All roles can view the full details of a service request
        // The Details page also shows the status update form for Admins
        [Authorize(Roles = "Admin,Manager,Viewer")]
        public async Task<IActionResult> Details(int id)
        {
            // Load the service request with its contract and client details
            var request = await _repo.GetByIdAsync(id);

            // Return 404 if the service request does not exist
            if (request == null) return NotFound();

            return View(request);
        }

        // ── CREATE GET — SHOW THE CREATE FORM ────────────────────────────────
        // GET: /ServiceRequests/Create
        // GET: /ServiceRequests/Create?contractId=5
        //
        // The optional contractId comes from the "+ New Request" button
        // on the Contract Details page — it pre-selects that contract
        // Admin and Manager can create service requests
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create(int? contractId)
        {
            // Only show ACTIVE contracts in the dropdown
            // Expired/OnHold/Draft contracts are filtered out
            await PopulateContractsDropDown(contractId);

            // Fetch the live USD to ZAR exchange rate from the external API
            // This is shown in the view so JavaScript can display
            // the ZAR equivalent as the user types a USD amount
            var rate = await _currencyService.GetUsdToZarRateAsync();
            ViewBag.ExchangeRate = rate;

            // Pre-set the ContractId if it was passed in the URL
            // "?? 0" means use contractId if provided, otherwise use 0
            return View(new ServiceRequest { ContractId = contractId ?? 0 });
        }

        // ── CREATE POST — SAVE THE NEW SERVICE REQUEST ────────────────────────
        // POST: /ServiceRequests/Create
        //
        // TWO key things happen here:
        // 1. WORKFLOW VALIDATION — check the contract allows requests
        // 2. CURRENCY CONVERSION — convert USD to ZAR using live rate
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create(ServiceRequest model)
        {
            // ── STEP 1: WORKFLOW VALIDATION ───────────────────────────────────
            // Load the parent contract to check if it allows service requests
            // We check this BEFORE ModelState because it is a business rule
            var contract = await _contractRepo.GetByIdAsync(model.ContractId);

            if (contract == null)
            {
                // The selected contract does not exist in the database
                ModelState.AddModelError("ContractId",
                    "Selected contract does not exist.");
            }
            else
            {
                // Ask WorkflowService if this contract blocks service requests
                // Returns null if allowed (Active contract)
                // Returns an error message if blocked (Expired/OnHold/Draft)
                var blockReason = _workflowService
                    .GetServiceRequestBlockReason(contract);

                if (blockReason != null)
                {
                    // Contract is blocked — show the reason as a form error
                    ModelState.AddModelError("ContractId", blockReason);
                }
            }

            // If any validation failed return the form with error messages
            if (!ModelState.IsValid)
            {
                await PopulateContractsDropDown(model.ContractId);
                ViewBag.ExchangeRate = await _currencyService.GetUsdToZarRateAsync();
                return View(model);
            }

            // ── STEP 2: CURRENCY CONVERSION ───────────────────────────────────
            // Get the live exchange rate from the external API
            var exchangeRate = await _currencyService.GetUsdToZarRateAsync();

            // Calculate ZAR = USD × rate, rounded to 2 decimal places
            model.CostZar = _currencyService.ConvertUsdToZar(
                model.CostUsd, exchangeRate);

            // Save the rate that was used — important for audit purposes
            // If the rate changes tomorrow we still know what was used today
            model.ExchangeRateUsed = exchangeRate;
            model.CreatedOn = DateTime.UtcNow;

            // Save the service request to the database
            await _repo.AddAsync(model);

            // Show a success message with the converted ZAR amount
            // ":N2" formats the number with 2 decimal places and commas
            TempData["Success"] =
                $"Service request created. ZAR Cost: R{model.CostZar:N2}";

            return RedirectToAction(nameof(Index));
        }

        // ── UPDATE STATUS GET — SHOW THE STATUS UPDATE FORM ──────────────────
        // GET: /ServiceRequests/UpdateStatus/5
        // Admin only — only Admins can change service request status
        // Shows the current request details with a status dropdown
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatus(int id)
        {
            // Load the service request from the database
            var request = await _repo.GetByIdAsync(id);

            // Return 404 if it does not exist
            if (request == null) return NotFound();

            return View(request);
        }

        // ── UPDATE STATUS POST — SAVE THE NEW STATUS ─────────────────────────
        // POST: /ServiceRequests/UpdateStatusConfirmed
        // Admin only — saves the new status to the database
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateStatusConfirmed(
            int id, ServiceRequestStatus newStatus)
        {
            // Load the service request from the database
            var request = await _repo.GetByIdAsync(id);
            if (request == null) return NotFound();

            // Update the status to the new value
            request.Status = newStatus;

            // Save the updated request to the database
            await _repo.UpdateAsync(request);

            TempData["Success"] =
                $"Service request status updated to {newStatus}.";

            // Go back to the service requests list
            return RedirectToAction(nameof(Index));
        }

        // ── GET RATE — RETURN LIVE EXCHANGE RATE AS JSON ──────────────────────
        // GET: /ServiceRequests/GetRate
        // Called by the JavaScript on the Create form
        // Returns the current USD to ZAR rate as JSON
        // The JavaScript reads this and updates the ZAR preview in real time
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetRate()
        {
            var rate = await _currencyService.GetUsdToZarRateAsync();

            // Json() converts the C# object to JSON format
            // The browser JavaScript reads: { "rate": 18.50 }
            return Json(new { rate });
        }

        // ── PRIVATE HELPER — POPULATE CONTRACTS DROPDOWN ──────────────────────
        // Only shows ACTIVE contracts in the dropdown
        // This prevents users from accidentally selecting an invalid contract
        // The server-side WorkflowService validation also catches this
        private async Task PopulateContractsDropDown(int? selectedId = null)
        {
            // Get all contracts then filter to Active only
            var contracts = (await _contractRepo.GetAllAsync())
                .Where(c => c.Status == ContractStatus.Active);

            // Convert to SelectList for the HTML dropdown
            // "Id" = the value stored when an option is selected
            // "Title" = the text displayed in the dropdown
            ViewBag.Contracts = new SelectList(
                contracts, "Id", "Title", selectedId);
        }
    }
}