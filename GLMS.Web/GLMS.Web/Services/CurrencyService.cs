// ─── CURRENCY SERVICE ─────────────────────────────────────────────────────────
// Handles all currency conversion logic for the GLMS system.
// TechMove operates internationally but reports costs in ZAR (South African Rand).
//
// EXTERNAL API: Uses open.er-api.com (free, no API key required)
// to get the live USD → ZAR exchange rate.
//
// ASYNC/AWAIT: HttpClient calls are fully async — the thread is never blocked
// while waiting for the API response. This satisfies LU4 (Async/Await requirement).
//
// ERROR HANDLING: If the API is unavailable, a fallback rate is used so the
// application continues to function (high availability principle from Part 1).
//
// TESTABILITY: ConvertUsdToZar() is a pure function (no external dependencies)
// making it easy to unit test with known inputs and expected outputs.

using System.Text.Json;

namespace GLMS.Web.Services
{
    public interface ICurrencyService
    {
        // Async method — fetches live rate from external API
        Task<decimal> GetUsdToZarRateAsync();

        // Pure calculation method — no external dependencies, fully unit testable
        decimal ConvertUsdToZar(decimal usdAmount, decimal rate);
    }

    public class CurrencyService : ICurrencyService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CurrencyService> _logger;

        // Free currency API — no API key required
        private const string ApiUrl = "https://open.er-api.com/v6/latest/USD";

        // HttpClient is injected via DI — registered as AddHttpClient in Program.cs
        public CurrencyService(HttpClient httpClient, ILogger<CurrencyService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<decimal> GetUsdToZarRateAsync()
        {
            try
            {
                // Async HTTP call — does not block the thread while waiting
                var response = await _httpClient.GetStringAsync(ApiUrl);

                // Parse JSON response using System.Text.Json
                var doc = JsonDocument.Parse(response);

                // Navigate the JSON structure: { "rates": { "ZAR": 18.50 } }
                var rate = doc.RootElement
                              .GetProperty("rates")
                              .GetProperty("ZAR")
                              .GetDecimal();
                return rate;
            }
            catch (Exception ex)
            {
                // If API is down, log the error and use a fallback rate
                // This prevents the entire application from crashing
                _logger.LogWarning(ex, "Currency API unavailable. Using fallback rate of 18.50.");
                return 18.50m;
            }
        }

        /// <summary>
        /// Converts a USD amount to ZAR using the provided exchange rate.
        /// This method is intentionally kept pure (no side effects) for unit testing.
        /// </summary>
        public decimal ConvertUsdToZar(decimal usdAmount, decimal rate)
        {
            // Validate the rate — a zero or negative rate would produce nonsensical results
            if (rate <= 0)
                throw new ArgumentException("Exchange rate must be greater than zero.", nameof(rate));

            // Round to 2 decimal places for currency display
            return Math.Round(usdAmount * rate, 2);
        }
    }
}