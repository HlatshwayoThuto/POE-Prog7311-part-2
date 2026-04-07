// ─── DATABASE CONTEXT ─────────────────────────────────────────────────────────
// GlmsDbContext is the Entity Framework Core "bridge" between C# objects and
// the SQL Server database. It inherits from DbContext which provides all
// the database operations (queries, inserts, updates, deletes).
//
// Every DbSet<T> maps to a table in the database.
// The OnModelCreating method configures relationships and constraints
// using the Fluent API — a more explicit alternative to data annotations.

using GLMS.Web.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace GLMS.Web.Data
{
    public class GlmsDbContext : DbContext
    {
        // Constructor — receives options (connection string etc.) via Dependency Injection
        // configured in Program.cs
        public GlmsDbContext(DbContextOptions<GlmsDbContext> options) : base(options) { }

        // Each DbSet maps to a SQL table
        // EF Core automatically names tables based on the DbSet property name
        public DbSet<Client> Clients => Set<Client>();
        public DbSet<Contract> Contracts => Set<Contract>();
        public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

        // Fluent API configuration — runs when EF Core builds the database model
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
            // SQL Server needs explicit precision for decimal columns
            // (18,2) = up to 18 digits total, 2 after decimal point
            // (18,6) = 6 decimal places for the exchange rate (more precision needed)
            modelBuilder.Entity<ServiceRequest>()
                .Property(sr => sr.CostUsd).HasPrecision(18, 2);
            modelBuilder.Entity<ServiceRequest>()
                .Property(sr => sr.CostZar).HasPrecision(18, 2);
            modelBuilder.Entity<ServiceRequest>()
                .Property(sr => sr.ExchangeRateUsed).HasPrecision(18, 6);

            // ── Seed Data ─────────────────────────────────────────────────────
            // Pre-populates the database with sample clients when migrations run
            modelBuilder.Entity<Client>().HasData(
                new Client { Id = 1, Name = "Global Freight SA", ContactEmail = "ops@globalfreight.co.za", ContactPhone = "+27115551234", Region = "Southern Africa" },
                new Client { Id = 2, Name = "Transatlantic Cargo Ltd", ContactEmail = "info@tac.com", ContactPhone = "+12125557890", Region = "North America" }
            );
        }
    }
}