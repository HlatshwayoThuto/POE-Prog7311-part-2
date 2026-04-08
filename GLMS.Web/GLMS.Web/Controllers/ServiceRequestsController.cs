// ─── SERVICE REQUESTS CONTROLLER 
// Manages all operations for Service Requests in the GLMS system.
//
// WHAT IS A SERVICE REQUEST?
// A Service Request is a logistics task raised against an Active contract.
// For example: "Ship 500 units from Johannesburg to London"
// Each request has a USD cost that gets automatically converted to ZAR
// using a live exchange rate from an external currency API.
//
// ROLE-BASED ACCESS:
// - Admin:   Full access — view and create service requests
// - Manager: Can view and create service requests
// - Viewer:  Read-only — can only view service requests
//
// KEY FEATURES:
// 1. WORKFLOW VALIDATION — Cannot create requests on Expired/OnHold contracts
// 2. CURRENCY CONVERSION — Live USD→ZAR rate from external API
// 3. SEPARATION OF CONCERNS — Workflow logic delegated to IWorkflowService
//
// DESIGN PATTERN:
// Repository Pattern — data access via IServiceRequestRepository
// The workflow validation logic lives in WorkflowService, not here

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using GLMS.Web.Models;
using GLMS.Web.Repositories;
using GLMS.Web.Services;

namespace GLMS.Web.Controllers
{
    [Authorize] // All actions require login
    public class ServiceRequestsController : Controller
    {
        // Repository for ServiceRequest data access
        private readonly IServiceRequestRepository _repo;

        // Used to load the parent Contract for workflow validation
        // and to populate the Contract dropdown on the Create form
        private readonly IContractRepository _contractRepo;

        // Handles the external Currency API call and USD→ZAR conversion math
        private readonly ICurrencyService _currencyService;

        // Contains the business rules about when service requests can be raised
        // Separated from the controller to keep this class focused on HTTP only
        private readonly IWorkflowService _workflowService;

        // All four dependencies are injected by the DI container
        // They were all registered in Program.cs
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

        // ── INDEX — View All Service Requests 
        // GET: /ServiceRequests
        //
        // All three roles can see service requests
        // The list shows contract name, client, description, USD/ZAR costs etc.
        [Authorize(Roles = "Admin,Manager,Viewer")]
        public async Task<IActionResult> Index()
        {
            // GetAllAsync includes Contract and Client via eager loading
            // so we can display the contract title and client name in the list
            return View(await _repo.GetAllAsync());
        }

        // ── CREATE (GET) — Show the Create Form 
        // GET: /ServiceRequests/Create or /ServiceRequests/Create?contractId=5
        //
        // The optional contractId parameter pre-selects a contract in the dropdown
        // This is used when clicking "+ New Request" from a contract's Details page
        // Admin and Manager can create service requests
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create(int? contractId)
        {
            // Only show Active contracts in the dropdown
            // Expired/OnHold/Draft contracts are filtered out in PopulateContractsDropDown
            await PopulateContractsDropDown(contractId);

            // Fetch the live USD→ZAR exchange rate from the external API
            // This is passed to the view so the JavaScript preview can display
            // the ZAR equivalent as the user types a USD amount
            var rate = await _currencyService.GetUsdToZarRateAsync();
            ViewBag.ExchangeRate = rate;

            // Pre-set the ContractId if it was passed in the URL
            return View(new ServiceRequest { ContractId = contractId ?? 0 });
        }

        // ── CREATE (POST) — Save the New Service Request 
        // POST: /ServiceRequests/Create
        //
        // This action has two key responsibilities:
        // 1. Validate that the contract allows service requests (workflow rule)
        // 2. Convert the USD cost to ZAR using the live exchange rate
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create(ServiceRequest model)
        {
            // ── STEP 1: WORKFLOW VALIDATION 
            // Load the parent contract to check if it allows service requests
            // We must do this BEFORE checking ModelState because it's a
            // business rule, not just a data validation rule
            var contract = await _contractRepo.GetByIdAsync(model.ContractId);

            if (contract == null)
            {
                // Contract ID doesn't exist in the database
                ModelState.AddModelError("ContractId", "Selected contract does not exist.");
            }
            else
            {
                // Ask WorkflowService if this contract blocks service requests
                // WorkflowService.GetServiceRequestBlockReason() returns:
                // - null if the contract is Active (allowed)
                // - An error message string if Expired, OnHold or Draft (blocked)
                var blockReason = _workflowService.GetServiceRequestBlockReason(contract);
                if (blockReason != null)
                {
                    // Add the block reason as a validation error on the ContractId field
                    // This shows the message next to the Contract dropdown in the form
                    ModelState.AddModelError("ContractId", blockReason);
                }
            }

            // If any validation failed (model annotations OR workflow rule)
            // return the form with all the error messages displayed
            if (!ModelState.IsValid)
            {
                await PopulateContractsDropDown(model.ContractId);
                ViewBag.ExchangeRate = await _currencyService.GetUsdToZarRateAsync();
                return View(model);
            }

            // ── STEP 2: CURRENCY CONVERSION 
            // Fetch the live exchange rate from the external API (open.er-api.com)
            // This is an async call — the thread is not blocked while waiting
            var exchangeRate = await _currencyService.GetUsdToZarRateAsync();

            // Convert the USD amount to ZAR using the pure calculation method
            // ConvertUsdToZar() is testable independently (see unit tests)
            model.CostZar = _currencyService.ConvertUsdToZar(model.CostUsd, exchangeRate);

            // Store the rate that was used — important for audit purposes
            // If the rate changes tomorrow, we still know what rate was used today
            model.ExchangeRateUsed = exchangeRate;
            model.CreatedOn = DateTime.UtcNow;

            // Save the completed service request to the database
            await _repo.AddAsync(model);

            // Show the converted ZAR amount in the success message
            TempData["Success"] = $"Service request created. ZAR Cost: R{model.CostZar:N2}";
            return RedirectToAction(nameof(Index));
        }

        // ── GET RATE — Live Exchange Rate Endpoint 
        // GET: /ServiceRequests/GetRate
        //
        // Returns the current USD→ZAR rate as JSON
        // Called by the JavaScript on the Create form to update the ZAR preview
        // as the user types — gives real-time currency conversion feedback
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetRate()
        {
            var rate = await _currencyService.GetUsdToZarRateAsync();

            // Json() serializes the object to JSON format
            // The JavaScript in the view reads this response
            return Json(new { rate });
        }

        // ── PRIVATE HELPER — Populate Contracts Dropdown 
        // Fetches only ACTIVE contracts for the dropdown
        // This is a UI guard — the workflow validation also runs server-side
        // but filtering the dropdown prevents users from even selecting
        // invalid contracts in the first place
        private async Task PopulateContractsDropDown(int? selectedId = null)
        {
            // Filter to only Active contracts — Expired/OnHold/Draft are excluded
            var contracts = (await _contractRepo.GetAllAsync())
                .Where(c => c.Status == ContractStatus.Active);

            // "Id" = value stored, "Title" = text displayed in dropdown
            ViewBag.Contracts = new SelectList(contracts, "Id", "Title", selectedId);
        }
    }
}