// ─── SERVICE REQUESTS CONTROLLER ─────────────────────────────────────────────
// Handles creation and listing of Service Requests.
//
// WORKFLOW RULE (Critical business logic):
// A ServiceRequest CANNOT be created if its parent Contract is Expired or OnHold.
// This is validated server-side before saving.
//
// CURRENCY INTEGRATION:
// Uses ICurrencyService to fetch the live USD→ZAR rate from an external API.
// The converted ZAR amount and rate used are saved to the database.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using GLMS.Web.Models;
using GLMS.Web.Repositories;
using GLMS.Web.Services;

namespace GLMS.Web.Controllers
{
    public class ServiceRequestsController : Controller
    {
        private readonly IServiceRequestRepository _repo;
        private readonly IContractRepository _contractRepo;
        private readonly ICurrencyService _currencyService;

        public ServiceRequestsController(IServiceRequestRepository repo,
            IContractRepository contractRepo, ICurrencyService currencyService)
        {
            _repo = repo;
            _contractRepo = contractRepo;
            _currencyService = currencyService;
        }

        // GET: /ServiceRequests
        public async Task<IActionResult> Index()
            => View(await _repo.GetAllAsync());

        // GET: /ServiceRequests/Create
        // Fetches the live exchange rate to display in the UI for preview
        public async Task<IActionResult> Create(int? contractId)
        {
            await PopulateContractsDropDown(contractId);

            // Fetch live rate — shown in the view for the USD→ZAR preview
            var rate = await _currencyService.GetUsdToZarRateAsync();
            ViewBag.ExchangeRate = rate;

            return View(new ServiceRequest { ContractId = contractId ?? 0 });
        }

        // POST: /ServiceRequests/Create
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceRequest model)
        {
            // ── WORKFLOW VALIDATION ───────────────────────────────────────────
            // Load the parent contract to check its status before allowing creation
            var contract = await _contractRepo.GetByIdAsync(model.ContractId);

            if (contract == null)
            {
                ModelState.AddModelError("ContractId", "Selected contract does not exist.");
            }
            else if (contract.Status == ContractStatus.Expired ||
                     contract.Status == ContractStatus.OnHold)
            {
                // BLOCK the request — this is the core workflow rule
                ModelState.AddModelError("ContractId",
                    $"Service requests cannot be raised against a contract " +
                    $"with status '{contract.Status}'.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateContractsDropDown(model.ContractId);
                ViewBag.ExchangeRate = await _currencyService.GetUsdToZarRateAsync();
                return View(model);
            }

            // ── CURRENCY CONVERSION ───────────────────────────────────────────
            // Fetch the live rate and convert USD to ZAR
            var exchangeRate = await _currencyService.GetUsdToZarRateAsync();
            model.CostZar = _currencyService.ConvertUsdToZar(model.CostUsd, exchangeRate);
            model.ExchangeRateUsed = exchangeRate; // Saved for audit purposes
            model.CreatedOn = DateTime.UtcNow;

            await _repo.AddAsync(model);
            TempData["Success"] = $"Service request created. ZAR Cost: R{model.CostZar:N2}";
            return RedirectToAction(nameof(Index));
        }

        // GET: /ServiceRequests/GetRate
        // Returns the current exchange rate as JSON for the live preview on the form
        [HttpGet]
        public async Task<IActionResult> GetRate()
        {
            var rate = await _currencyService.GetUsdToZarRateAsync();
            return Json(new { rate });
        }

        // Only shows Active contracts in the dropdown
        // Expired/OnHold contracts are excluded to guide the user
        private async Task PopulateContractsDropDown(int? selectedId = null)
        {
            var contracts = (await _contractRepo.GetAllAsync())
                .Where(c => c.Status == ContractStatus.Active);
            ViewBag.Contracts = new SelectList(contracts, "Id", "Title", selectedId);
        }
    }
}