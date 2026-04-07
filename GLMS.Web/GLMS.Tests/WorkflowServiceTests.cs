// ─── WORKFLOW SERVICE UNIT TESTS ──────────────────────────────────────────────
// Tests the WorkflowService business rules in isolation.
// These tests demonstrate TDD principles — testing logic separately from
// controllers and database access.

using GLMS.Web.Models;
using GLMS.Web.Services;
using Xunit;

namespace GLMS.Tests
{
    public class WorkflowServiceTests
    {
        private readonly WorkflowService _sut = new WorkflowService();

        private static Contract MakeContract(ContractStatus status) => new Contract
        {
            Id = 1,
            Title = "Test",
            Status = status,
            ClientId = 1,
            StartDate = DateTime.Today.AddDays(-10),
            EndDate = DateTime.Today.AddDays(30),
            ServiceLevel = ServiceLevel.Standard
        };

        // ── CanRaiseServiceRequest ────────────────────────────────────────────

        [Fact]
        public void CanRaiseServiceRequest_ReturnsTrue_ForActiveContract()
        {
            Assert.True(_sut.CanRaiseServiceRequest(MakeContract(ContractStatus.Active)));
        }

        [Fact]
        public void CanRaiseServiceRequest_ReturnsFalse_ForExpiredContract()
        {
            Assert.False(_sut.CanRaiseServiceRequest(MakeContract(ContractStatus.Expired)));
        }

        [Fact]
        public void CanRaiseServiceRequest_ReturnsFalse_ForOnHoldContract()
        {
            Assert.False(_sut.CanRaiseServiceRequest(MakeContract(ContractStatus.OnHold)));
        }

        [Fact]
        public void CanRaiseServiceRequest_ReturnsFalse_ForDraftContract()
        {
            Assert.False(_sut.CanRaiseServiceRequest(MakeContract(ContractStatus.Draft)));
        }

        // ── GetServiceRequestBlockReason ──────────────────────────────────────

        [Fact]
        public void GetServiceRequestBlockReason_ReturnsNull_ForActiveContract()
        {
            var reason = _sut.GetServiceRequestBlockReason(MakeContract(ContractStatus.Active));
            Assert.Null(reason); // No block for active contracts
        }

        [Fact]
        public void GetServiceRequestBlockReason_ReturnsMessage_ForExpiredContract()
        {
            var reason = _sut.GetServiceRequestBlockReason(MakeContract(ContractStatus.Expired));
            Assert.NotNull(reason);
            Assert.Contains("expired", reason.ToLower());
        }

        [Fact]
        public void GetServiceRequestBlockReason_ReturnsMessage_ForOnHoldContract()
        {
            var reason = _sut.GetServiceRequestBlockReason(MakeContract(ContractStatus.OnHold));
            Assert.NotNull(reason);
            Assert.Contains("hold", reason.ToLower());
        }

        // ── IsValidStatusTransition ───────────────────────────────────────────

        [Fact]
        public void IsValidStatusTransition_ReturnsTrue_DraftToActive()
        {
            Assert.True(_sut.IsValidStatusTransition(
                ContractStatus.Draft, ContractStatus.Active));
        }

        [Fact]
        public void IsValidStatusTransition_ReturnsTrue_ActiveToExpired()
        {
            Assert.True(_sut.IsValidStatusTransition(
                ContractStatus.Active, ContractStatus.Expired));
        }

        [Fact]
        public void IsValidStatusTransition_ReturnsFalse_ExpiredToAny()
        {
            // Expired is a terminal state — no transitions allowed
            Assert.False(_sut.IsValidStatusTransition(
                ContractStatus.Expired, ContractStatus.Active));
            Assert.False(_sut.IsValidStatusTransition(
                ContractStatus.Expired, ContractStatus.Draft));
        }

        [Fact]
        public void IsValidStatusTransition_ReturnsFalse_SameStatus()
        {
            Assert.False(_sut.IsValidStatusTransition(
                ContractStatus.Active, ContractStatus.Active));
        }
    }
}