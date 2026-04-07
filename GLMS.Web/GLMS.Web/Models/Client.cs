// ─── CLIENT MODEL ─────────────────────────────────────────────────────────────
// Represents a TechMove client (shipping company or individual).
// This is the top-level entity — Clients own Contracts.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;

namespace GLMS.Web.Models
{
    public class Client
    {
        // Primary key — EF Core automatically makes this the identity column
        public int Id { get; set; }

        // [Required] triggers server-side validation — field cannot be empty
        // [StringLength] sets the max characters allowed in the database column
        [Required(ErrorMessage = "Client name is required.")]
        [StringLength(200)]
        [Display(Name = "Client Name")] // Controls the label text in Razor views
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact email is required.")]
        [EmailAddress] // Validates that the value is a valid email format
        [Display(Name = "Contact Email")]
        public string ContactEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Contact phone is required.")]
        [Phone] // Validates phone number format
        [Display(Name = "Contact Phone")]
        public string ContactPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Region is required.")]
        [StringLength(100)]
        public string Region { get; set; } = string.Empty;

        // Navigation property — EF Core uses this to load related contracts
        // ICollection allows one client to have many contracts (One-to-Many)
        public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
    }
}