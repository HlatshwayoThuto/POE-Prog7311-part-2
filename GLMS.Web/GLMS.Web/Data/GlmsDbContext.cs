// ─── DATABASE CONTEXT 
// GlmsDbContext inherits from IdentityDbContext instead of DbContext.
// This automatically adds all ASP.NET Core Identity tables to the database:
// AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims etc.
//
// IdentityDbContext<IdentityUser> includes everything DbContext has PLUS
// all the Identity-related DbSets needed for authentication and roles.

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GLMS.Web.Models;

namespace GLMS.Web.Data
{
    // Changed from DbContext to IdentityDbContext<IdentityUser>
    // This is the only structural change — everything else stays the same
    public class GlmsDbContext : IdentityDbContext<IdentityUser>
    {
        public GlmsDbContext(DbContextOptions<GlmsDbContext> options) : base(options) { }

        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Contract> Contracts => Set<Contract>();
        public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // MUST call base first — Identity needs this to set up its own tables
            base.OnModelCreating(modelBuilder);

            // ── Relationship: Client → Contracts (One-to-Many) ────────────────
            // One Client can have many Contracts
            // DeleteBehavior.Restrict prevents deleting a Client that has Contracts
            modelBuilder.Entity<Contract>()
                .HasOne(c => c.Client)
                .WithMany(cl => cl.Contracts)
                .HasForeignKey(c => c.ClientId)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Relationship: Contract → ServiceRequests (One-to-Many) ────────
            // One Contract can have many ServiceRequests
            // DeleteBehavior.Cascade automatically deletes ServiceRequests
            // when their parent Contract is deleted
            modelBuilder.Entity<ServiceRequest>()
                .HasOne(sr => sr.Contract)
                .WithMany(c => c.ServiceRequests)
                .HasForeignKey(sr => sr.ContractId)
                .OnDelete(DeleteBehavior.Cascade);

            // ── Decimal precision configuration ───────────────────────────────
            modelBuilder.Entity<ServiceRequest>()
                .Property(sr => sr.CostUsd).HasPrecision(18, 2);
            modelBuilder.Entity<ServiceRequest>()
                .Property(sr => sr.CostZar).HasPrecision(18, 2);
            modelBuilder.Entity<ServiceRequest>()
                .Property(sr => sr.ExchangeRateUsed).HasPrecision(18, 6);

            // ── Seed Data ─────────────────────────────────────────────────────
            modelBuilder.Entity<Client>().HasData(
                new Client { Id = 1, Name = "Global Freight SA", ContactEmail = "ops@globalfreight.co.za", ContactPhone = "+27115551234", Region = "Southern Africa" },
                new Client { Id = 2, Name = "Transatlantic Cargo Ltd", ContactEmail = "info@tac.com", ContactPhone = "+12125557890", Region = "North America" }
            );
        }
    }
}