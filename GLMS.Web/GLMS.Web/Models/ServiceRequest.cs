// ─── SERVICE REQUEST MODEL ────────────────────────────────────────────────────
// Represents a logistics service request raised against an active contract.
// WORKFLOW RULE: Cannot be created if the parent Contract is Expired or OnHold.
// CURRENCY: Cost is entered in USD and automatically converted to ZAR via API.

using System.ComponentModel.DataAnnotations;

namespace GLMS.Web.Models
{
    public enum ServiceRequestStatus
    {
        Pending,    // Just created, awaiting processing
        InProgress, // Being worked on
        Completed,  // Finished
        Cancelled   // Cancelled before completion
    }

    public class ServiceRequest
    {
        public int Id { get; set; }

        // Foreign key — links this request to its parent Contract
        [Required]
        public int ContractId { get; set; }

        // Navigation property — loads the related Contract (and Client via Contract)
        [Display(Name = "Contract")]
        public Contract? Contract { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        // Amount entered by the user in US Dollars
        [Required(ErrorMessage = "USD cost is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Cost must be greater than zero.")]
        [Display(Name = "Cost (USD)")]
        public decimal CostUsd { get; set; }

        // Converted ZAR amount — calculated and saved at time of creation
        // Uses the live exchange rate from the external Currency API
        [Display(Name = "Cost (ZAR)")]
        public decimal CostZar { get; set; }

        // The exchange rate used at the time of creation — stored for audit purposes
        [Display(Name = "Exchange Rate (USD→ZAR)")]
        public decimal ExchangeRateUsed { get; set; }

        public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Pending;

        [DataType(DataType.DateTime)]
        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    }
}