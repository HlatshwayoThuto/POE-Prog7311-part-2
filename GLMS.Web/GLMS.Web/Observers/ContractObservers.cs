// ─── OBSERVER PATTERN ─────────────────────────────────────────────────────────
// The Observer Pattern (Gang of Four — Behavioural Pattern) is used here to
// automatically notify interested parties when a Contract's status changes.
//
// WHY OBSERVER?
// Without this, the ContractService would need to know about every system that
// cares about status changes (audit logs, email, billing etc). This creates
// tight coupling. With Observer, new "listeners" can be added without touching
// the ContractService at all.
//
// HOW IT WORKS:
// 1. IContractObserver defines what an observer must implement
// 2. IContractSubject defines how observers register and get notified
// 3. ContractService implements IContractSubject
// 4. AuditLogObserver and EmailNotificationObserver implement IContractObserver
// 5. When a status changes, NotifyObserversAsync() calls all registered observers

using GLMS.Web.Models;

namespace GLMS.Web.Observers
{
    // Any class that wants to be notified of contract status changes
    // must implement this interface
    public interface IContractObserver
    {
        Task OnStatusChangedAsync(Contract contract, ContractStatus oldStatus, ContractStatus newStatus);
    }

    // The "subject" that observers subscribe to
    public interface IContractSubject
    {
        void RegisterObserver(IContractObserver observer);
        Task NotifyObserversAsync(Contract contract, ContractStatus oldStatus, ContractStatus newStatus);
    }

    // ── Observer 1: Audit Log ─────────────────────────────────────────────────
    // Records every status change to the application log
    // In production this would write to a database audit table
    public class AuditLogObserver : IContractObserver
    {
        private readonly ILogger<AuditLogObserver> _logger;
        public AuditLogObserver(ILogger<AuditLogObserver> logger) => _logger = logger;

        public Task OnStatusChangedAsync(Contract contract,
            ContractStatus oldStatus, ContractStatus newStatus)
        {
            // Logs to the Visual Studio Output window / application logs
            _logger.LogInformation(
                "[AUDIT] Contract #{Id} '{Title}' status changed: {Old} → {New} at {Time}",
                contract.Id, contract.Title, oldStatus, newStatus, DateTime.UtcNow);
            return Task.CompletedTask;
        }
    }

    // ── Observer 2: Email Notification ───────────────────────────────────────
    // Simulates sending an email when a contract status changes
    // In production this would use SMTP, SendGrid, or similar
    public class EmailNotificationObserver : IContractObserver
    {
        private readonly ILogger<EmailNotificationObserver> _logger;
        public EmailNotificationObserver(ILogger<EmailNotificationObserver> logger) => _logger = logger;

        public Task OnStatusChangedAsync(Contract contract,
            ContractStatus oldStatus, ContractStatus newStatus)
        {
            _logger.LogInformation(
                "[EMAIL] Notification sent to {Email}: Contract '{Title}' is now {Status}.",
                contract.Client?.ContactEmail ?? "unknown", contract.Title, newStatus);
            return Task.CompletedTask;
        }
    }
}