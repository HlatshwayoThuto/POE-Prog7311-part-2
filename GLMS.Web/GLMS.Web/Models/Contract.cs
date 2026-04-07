// ─── CONTRACT MODEL ───────────────────────────────────────────────────────────
// Represents a legal agreement between TechMove and a Client.
// Contracts are the central entity — they link Clients to ServiceRequests.
// The Factory Pattern (ContractFactory.cs) is responsible for creating these objects.

using System.ComponentModel.DataAnnotations;

namespace GLMS.Web.Models
{
    // Enum stored as INT in the database (0=Draft, 1=Active, 2=Expired, 3=OnHold)
    // This drives the workflow logic — ServiceRequests can only be raised on Active contracts
    public enum ContractStatus
    {
        Draft,    // Newly created, not yet approved
        Active,   // Approved and currently running
        Expired,  // Past end date or manually expired
        OnHold    // Temporarily suspended
    }

    // Enum for the type of service agreement — used by the Factory Pattern
    // to determine the initial status of a contract
    public enum ServiceLevel
    {
        Standard,   // Starts as Draft — requires manual approval
        Premium,    // Starts as Active immediately
        Enterprise  // Starts as Active immediately
    }

    public class Contract
    {
        public int Id { get; set; }

        // Foreign key linking this contract to a Client
        [Required]
        public int ClientId { get; set; }

        // Navigation property — EF Core loads the related Client object
        [Display(Name = "Client")]
        public Client? Client { get; set; }

        [Required(ErrorMessage = "Contract title is required.")]
        [StringLength(300)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        // Current status — controlled by the workflow logic and Observer Pattern
        [Required]
        public ContractStatus Status { get; set; } = ContractStatus.Draft;

        // Service level — used by ContractFactory to set initial status
        [Required(ErrorMessage = "Service level is required.")]
        [Display(Name = "Service Level")]
        public ServiceLevel ServiceLevel { get; set; } = ServiceLevel.Standard;

        // File path stored on the server disk (not in the database as a blob)
        // UUID-based naming prevents file overwrites (handled in FileService)
        [Display(Name = "Signed Agreement")]
        public string? SignedAgreementPath { get; set; }

        // Original filename shown to the user when downloading
        [Display(Name = "Original File Name")]
        public string? SignedAgreementOriginalName { get; set; }

        [DataType(DataType.DateTime)]
        [Display(Name = "Created On")]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        // Navigation property — one Contract has many ServiceRequests
        public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
    }
}