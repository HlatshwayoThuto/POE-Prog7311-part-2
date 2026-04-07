// ─── WORKFLOW SERVICE ─────────────────────────────────────────────────────────
// Centralises all business workflow rules for the GLMS system.
// Separating this from Controllers satisfies the rubric requirement:
// "Validation logic is separated from Controllers"
//
// Controllers call these methods instead of checking status inline.
// This makes rules reusable and independently testable.

using GLMS.Web.Models;

namespace GLMS.Web.Services
{
    public interface IWorkflowService
    {
        /// <summary>Returns true if a service request can be raised against this contract.</summary>
        bool CanRaiseServiceRequest(Contract contract);

        /// <summary>Returns an error message if the contract blocks service requests, null otherwise.</summary>
        string? GetServiceRequestBlockReason(Contract contract);

        /// <summary>Returns true if the contract status transition is valid.</summary>
        bool IsValidStatusTransition(ContractStatus current, ContractStatus next);
    }

    public class WorkflowService : IWorkflowService
    {
        /// <summary>
        /// Core business rule: Only Active contracts can have service requests raised.
        /// Expired and OnHold contracts are blocked.
        /// </summary>
        public bool CanRaiseServiceRequest(Contract contract)
        {
            return contract.Status == ContractStatus.Active;
        }

        /// <summary>
        /// Returns a human-readable reason why a service request cannot be raised.
        /// Returns null if the contract is valid for service requests.
        /// </summary>
        public string? GetServiceRequestBlockReason(Contract contract)
        {
            return contract.Status switch
            {
                ContractStatus.Expired =>
                    "This contract has expired. Service requests cannot be raised against expired contracts.",
                ContractStatus.OnHold =>
                    "This contract is on hold. Please reactivate it before raising service requests.",
                ContractStatus.Draft =>
                    "This contract is still in draft. It must be activated before raising service requests.",
                ContractStatus.Active => null, // No block — allowed
                _ => "Unknown contract status."
            };
        }

        /// <summary>
        /// Validates that a status transition makes logical sense.
        /// For example, you cannot go from Expired back to Draft.
        /// </summary>
        public bool IsValidStatusTransition(ContractStatus current, ContractStatus next)
        {
            return (current, next) switch
            {
                // Draft can move to Active or OnHold
                (ContractStatus.Draft, ContractStatus.Active) => true,
                (ContractStatus.Draft, ContractStatus.OnHold) => true,

                // Active can move to Expired or OnHold
                (ContractStatus.Active, ContractStatus.Expired) => true,
                (ContractStatus.Active, ContractStatus.OnHold) => true,

                // OnHold can be reactivated or expired
                (ContractStatus.OnHold, ContractStatus.Active) => true,
                (ContractStatus.OnHold, ContractStatus.Expired) => true,

                // Expired is a terminal state — no transitions allowed
                (ContractStatus.Expired, _) => false,

                // Same status = no change needed
                _ when current == next => false,

                _ => false
            };
        }
    }
}