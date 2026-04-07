// ─── FACTORY PATTERN ──────────────────────────────────────────────────────────
// The Factory Pattern (Gang of Four — Creational Pattern) is used here to
// centralise the creation of Contract objects.
//
// WHY FACTORY?
// Without this, every controller that creates a Contract would need to know
// the business rules (e.g. Premium contracts start Active). If the rules change,
// you'd need to update every controller. With Factory, you update ONE place.
//
// HOW IT WORKS:
// The ContractsController calls IContractFactory.CreateContract() instead of
// using "new Contract { ... }" directly. The factory applies all business rules
// and returns a fully configured Contract object.

using GLMS.Web.Models;

namespace GLMS.Web.Factories
{
    // Interface — controllers depend on this, not the concrete class
    public interface IContractFactory
    {
        Contract CreateContract(int clientId, string title, DateTime startDate,
                                DateTime endDate, ServiceLevel serviceLevel);
    }

    public class ContractFactory : IContractFactory
    {
        /// <summary>
        /// Creates a Contract with the correct initial status based on ServiceLevel.
        /// Standard = Draft (requires approval)
        /// Premium/Enterprise = Active immediately (pre-approved)
        /// </summary>
        public Contract CreateContract(int clientId, string title, DateTime startDate,
                                       DateTime endDate, ServiceLevel serviceLevel)
        {
            var contract = new Contract
            {
                ClientId = clientId,
                Title = title,
                StartDate = startDate,
                EndDate = endDate,
                ServiceLevel = serviceLevel,
                CreatedOn = DateTime.UtcNow,

                // ── Factory Business Rule ─────────────────────────────────────
                // Standard contracts start as Draft and need manual approval.
                // Premium and Enterprise contracts are pre-approved and start Active.
                Status = serviceLevel == ServiceLevel.Standard
                         ? ContractStatus.Draft
                         : ContractStatus.Active
            };

            return contract;
        }
    }
}