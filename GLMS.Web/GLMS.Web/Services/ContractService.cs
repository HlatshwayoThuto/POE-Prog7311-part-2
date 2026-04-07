// ─── CONTRACT SERVICE ─────────────────────────────────────────────────────────
// The ContractService sits between the Controllers and the Repository.
// It handles business logic that goes beyond simple data access.
//
// KEY RESPONSIBILITIES:
// 1. Delegates data access to IContractRepository (Repository Pattern)
// 2. Implements IContractSubject (Observer Pattern)
// 3. Notifies registered observers when a contract status changes
//
// This class demonstrates the combination of two design patterns working together:
// Repository Pattern (data access) + Observer Pattern (event notification)

using GLMS.Web.Models;
using GLMS.Web.Observers;
using GLMS.Web.Repositories;

namespace GLMS.Web.Services
{
    // Inherits from IContractSubject to support the Observer Pattern
    public interface IContractService : IContractSubject
    {
        Task<IEnumerable<Contract>> GetAllAsync();
        Task<Contract?> GetByIdAsync(int id);
        Task<IEnumerable<Contract>> SearchAsync(DateTime? start, DateTime? end, ContractStatus? status);
        Task CreateAsync(Contract contract);
        Task UpdateStatusAsync(int contractId, ContractStatus newStatus);
        Task DeleteAsync(int id);
    }

    public class ContractService : IContractService
    {
        private readonly IContractRepository _repo;

        // List of registered observers — they get notified on status changes
        private readonly List<IContractObserver> _observers = new();

        // IContractRepository injected — ContractService never touches DbContext directly
        public ContractService(IContractRepository repo) => _repo = repo;

        // ── Observer Pattern: Registration ────────────────────────────────────
        // Observers register themselves here — called in Program.cs during setup
        public void RegisterObserver(IContractObserver observer) => _observers.Add(observer);

        // Calls OnStatusChangedAsync on every registered observer
        public async Task NotifyObserversAsync(Contract contract,
            ContractStatus oldStatus, ContractStatus newStatus)
        {
            foreach (var observer in _observers)
                await observer.OnStatusChangedAsync(contract, oldStatus, newStatus);
        }

        // ── Standard CRUD via Repository ──────────────────────────────────────
        public Task<IEnumerable<Contract>> GetAllAsync() => _repo.GetAllAsync();
        public Task<Contract?> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
        public Task<IEnumerable<Contract>> SearchAsync(DateTime? s, DateTime? e, ContractStatus? st) =>
            _repo.SearchAsync(s, e, st);
        public Task CreateAsync(Contract contract) => _repo.AddAsync(contract);
        public Task DeleteAsync(int id) => _repo.DeleteAsync(id);

        // ── Status Update with Observer Notification ──────────────────────────
        // This is the key method — it updates the status AND notifies all observers
        public async Task UpdateStatusAsync(int contractId, ContractStatus newStatus)
        {
            var contract = await _repo.GetByIdAsync(contractId)
                ?? throw new KeyNotFoundException($"Contract #{contractId} not found.");

            var oldStatus = contract.Status; // Save old status for observer notification
            contract.Status = newStatus;
            await _repo.UpdateAsync(contract);

            // Notify all observers (AuditLog, EmailNotification) of the change
            await NotifyObserversAsync(contract, oldStatus, newStatus);
        }
    }
}