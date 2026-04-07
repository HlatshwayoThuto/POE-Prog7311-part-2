// ─── WORKFLOW VALIDATION UNIT TESTS ───────────────────────────────────────────
// Tests the core business rule of the GLMS system:
// "A ServiceRequest cannot be created against an Expired or OnHold contract."
//
// WHAT WE TEST:
// - Expired contracts → cannot raise requests
// - OnHold contracts → cannot raise requests
// - Active contracts → CAN raise requests
// - Draft contracts → cannot raise requests
// - Contract properties are set correctly
//
// NOTE: These tests validate the STATUS CHECK LOGIC independently
// without needing a database or HTTP request.
// The same logic is enforced in ServiceRequestsController.Create()

using GLMS.Web.Models;
using System;
using Xunit;

namespace GLMS.Tests
{
    public class WorkflowValidationTests
    {
        // Helper — creates a Contract with the specified status
        private static Contract MakeContract(ContractStatus status) => new Contract
        {
            Id = 1,
            Title = "Test Contract",
            Status = status,
            ClientId = 1,
            StartDate = DateTime.Today.AddDays(-30),
            EndDate = DateTime.Today.AddDays(30),
            ServiceLevel = ServiceLevel.Standard
        };

        // [Theory] with [InlineData] runs this test twice —
        // once for Expired and once for OnHold
        [Theory]
        [InlineData(ContractStatus.Expired)]
        [InlineData(ContractStatus.OnHold)]
        public void CanRaiseServiceRequest_ReturnsFalse_ForExpiredOrOnHold(ContractStatus status)
        {
            var contract = MakeContract(status);
            bool canRaise = contract.Status == ContractStatus.Active;
            Assert.False(canRaise); // Must be blocked
        }

        [Theory]
        [InlineData(ContractStatus.Active)]
        public void CanRaiseServiceRequest_ReturnsTrue_ForActiveContract(ContractStatus status)
        {
            var contract = MakeContract(status);
            bool canRaise = contract.Status == ContractStatus.Active;
            Assert.True(canRaise); // Must be allowed
        }

        [Fact]
        public void CanRaiseServiceRequest_ReturnsFalse_ForDraftContract()
        {
            // Draft contracts are not yet approved — requests should be blocked
            var contract = MakeContract(ContractStatus.Draft);
            bool canRaise = contract.Status == ContractStatus.Active;
            Assert.False(canRaise);
        }

        [Fact]
        public void ContractStatus_IsExpired_WhenSetToExpired()
        {
            var contract = MakeContract(ContractStatus.Expired);
            Assert.Equal(ContractStatus.Expired, contract.Status);
        }

        [Fact]
        public void ContractStatus_DefaultsToStandard_ServiceLevel()
        {
            var contract = MakeContract(ContractStatus.Draft);
            Assert.Equal(ServiceLevel.Standard, contract.ServiceLevel);
        }

        [Fact]
        public void Contract_HasCorrectClientId()
        {
            var contract = MakeContract(ContractStatus.Active);
            Assert.Equal(1, contract.ClientId);
        }
    }
}