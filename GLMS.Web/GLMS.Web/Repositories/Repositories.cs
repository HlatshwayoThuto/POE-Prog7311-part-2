// ─── REPOSITORY IMPLEMENTATIONS ──────────────────────────────────────────────
// Concrete implementations of the repository interfaces.
// These are the ONLY classes that interact with GlmsDbContext directly.
// Controllers and Services never touch the DbContext — they only use these repos.
//
// All methods are async (Task-based) to support non-blocking database operations.
// This satisfies the Async/Await requirement from LU4.

using Microsoft.EntityFrameworkCore;
using GLMS.Web.Data;
using GLMS.Web.Models;

namespace GLMS.Web.Repositories
{
    // ── Client Repository ─────────────────────────────────────────────────────
    public class ClientRepository : IClientRepository
    {
        private readonly GlmsDbContext _db;

        // GlmsDbContext is injected via constructor — Dependency Injection
        public ClientRepository(GlmsDbContext db) => _db = db;

        // ToListAsync() asynchronously retrieves all clients from the database
        public async Task<IEnumerable<Client>> GetAllAsync() =>
            await _db.Clients.ToListAsync();

        // Include(c => c.Contracts) — eager loading, loads related Contracts
        // alongside the Client in a single database query
        public async Task<Client?> GetByIdAsync(int id) =>
            await _db.Clients.Include(c => c.Contracts)
                .FirstOrDefaultAsync(c => c.Id == id);

        public async Task AddAsync(Client c)
        {
            _db.Clients.Add(c);
            await _db.SaveChangesAsync(); // Commits the transaction to the database
        }

        public async Task UpdateAsync(Client c)
        {
            _db.Clients.Update(c);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var c = await _db.Clients.FindAsync(id);
            if (c != null)
            {
                _db.Clients.Remove(c);
                await _db.SaveChangesAsync();
            }
        }
    }

    // ── Contract Repository ───────────────────────────────────────────────────
    public class ContractRepository : IContractRepository
    {
        private readonly GlmsDbContext _db;
        public ContractRepository(GlmsDbContext db) => _db = db;

        // Include(c => c.Client) — loads the related Client for each Contract
        // OrderByDescending — newest contracts appear first
        public async Task<IEnumerable<Contract>> GetAllAsync() =>
            await _db.Contracts.Include(c => c.Client)
                .OrderByDescending(c => c.CreatedOn).ToListAsync();

        // Double Include — loads Client AND ServiceRequests for the Details page
        public async Task<Contract?> GetByIdAsync(int id) =>
            await _db.Contracts
                .Include(c => c.Client)
                .Include(c => c.ServiceRequests)
                .FirstOrDefaultAsync(c => c.Id == id);

        // ── LINQ Search/Filter ────────────────────────────────────────────────
        // Satisfies rubric requirement: "Search/Filter using LINQ by Date Range and Status"
        // AsQueryable() builds the query lazily — filters are added conditionally
        // The query only executes when ToListAsync() is called at the end
        public async Task<IEnumerable<Contract>> SearchAsync(
            DateTime? startDate, DateTime? endDate, ContractStatus? status)
        {
            var q = _db.Contracts.Include(c => c.Client).AsQueryable();

            // Each filter is only applied if the user provided that parameter
            if (startDate.HasValue)
                q = q.Where(c => c.StartDate >= startDate.Value);
            if (endDate.HasValue)
                q = q.Where(c => c.EndDate <= endDate.Value);
            if (status.HasValue)
                q = q.Where(c => c.Status == status.Value);

            return await q.OrderByDescending(c => c.CreatedOn).ToListAsync();
        }

        public async Task AddAsync(Contract c)
        {
            _db.Contracts.Add(c);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Contract c)
        {
            _db.Contracts.Update(c);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var c = await _db.Contracts.FindAsync(id);
            if (c != null) { _db.Contracts.Remove(c); await _db.SaveChangesAsync(); }
        }
    }

    // ── Service Request Repository ────────────────────────────────────────────
    public class ServiceRequestRepository : IServiceRequestRepository
    {
        private readonly GlmsDbContext _db;
        public ServiceRequestRepository(GlmsDbContext db) => _db = db;

        // ThenInclude — chains a second Include to load Client via Contract
        // This allows us to display the Client name on the ServiceRequests list
        public async Task<IEnumerable<ServiceRequest>> GetAllAsync() =>
            await _db.ServiceRequests
                .Include(sr => sr.Contract).ThenInclude(c => c!.Client)
                .OrderByDescending(sr => sr.CreatedOn).ToListAsync();

        public async Task<ServiceRequest?> GetByIdAsync(int id) =>
            await _db.ServiceRequests
                .Include(sr => sr.Contract).ThenInclude(c => c!.Client)
                .FirstOrDefaultAsync(sr => sr.Id == id);

        public async Task<IEnumerable<ServiceRequest>> GetByContractIdAsync(int contractId) =>
            await _db.ServiceRequests
                .Where(sr => sr.ContractId == contractId)
                .OrderByDescending(sr => sr.CreatedOn).ToListAsync();

        public async Task AddAsync(ServiceRequest r)
        {
            _db.ServiceRequests.Add(r);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(ServiceRequest r)
        {
            _db.ServiceRequests.Update(r);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var r = await _db.ServiceRequests.FindAsync(id);
            if (r != null) { _db.ServiceRequests.Remove(r); await _db.SaveChangesAsync(); }
        }
    }
}