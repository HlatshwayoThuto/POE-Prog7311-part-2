// ─── CURRENCY CALCULATION UNIT TESTS ──────────────────────────────────────────
// Tests the ConvertUsdToZar() method in CurrencyService.
//
// WHY UNIT TESTS?
// Enterprise systems cannot rely on manual testing alone. Automated tests:
// 1. Prove the business logic (currency math) is correct
// 2. Run automatically — catch regressions when code changes
// 3. Are required by the rubric for full marks
//
// WHAT WE TEST:
// - Normal conversion (known USD and rate → expected ZAR)
// - Rounding (2 decimal places)
// - Edge cases (zero amount, zero rate, negative rate, very large/small amounts)
// - Multiple scenarios via [Theory] and [InlineData]
//
// NullLogger is used because CurrencyService requires a logger
// but we don't care about log output in tests

using System;
using System.Net.Http;
using GLMS.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GLMS.Tests
{
    public class CurrencyCalculationTests
    {
        private readonly CurrencyService _sut; // SUT = System Under Test

        public CurrencyCalculationTests()
        {
            // We only test ConvertUsdToZar() which is a pure method
            // HttpClient is passed but never called in these tests
            _sut = new CurrencyService(new HttpClient(), NullLogger<CurrencyService>.Instance);
        }

        [Fact]
        public void ConvertUsdToZar_CorrectResult_GivenKnownRate()
        {
            // Arrange: 100 USD at rate 18.50
            // Act
            decimal result = _sut.ConvertUsdToZar(100m, 18.50m);
            // Assert: 100 × 18.50 = 1850.00
            Assert.Equal(1850.00m, result);
        }

        [Fact]
        public void ConvertUsdToZar_RoundsToTwoDecimalPlaces()
        {
            // 1 × 18.5678 = 18.5678 → should round to 18.57
            decimal result = _sut.ConvertUsdToZar(1m, 18.5678m);
            Assert.Equal(18.57m, result);
        }

        [Fact]
        public void ConvertUsdToZar_ZeroAmount_ReturnsZero()
        {
            // 0 USD should always return 0 ZAR regardless of rate
            decimal result = _sut.ConvertUsdToZar(0m, 18.50m);
            Assert.Equal(0m, result);
        }

        [Fact]
        public void ConvertUsdToZar_ZeroRate_ThrowsArgumentException()
        {
            // A zero rate is invalid — should throw ArgumentException
            Assert.Throws<ArgumentException>(() => _sut.ConvertUsdToZar(100m, 0m));
        }

        [Fact]
        public void ConvertUsdToZar_NegativeRate_ThrowsArgumentException()
        {
            // A negative rate is invalid — should throw ArgumentException
            Assert.Throws<ArgumentException>(() => _sut.ConvertUsdToZar(100m, -5m));
        }

        // [Theory] runs the same test with multiple sets of inputs
        // [InlineData] provides each set of test values
        [Theory]
        [InlineData(500, 18.00, 9000.00)]
        [InlineData(1000, 19.50, 19500.00)]
        [InlineData(0.01, 20.00, 0.20)]
        public void ConvertUsdToZar_MultipleScenarios(decimal usd, decimal rate, decimal expected)
        {
            Assert.Equal(expected, _sut.ConvertUsdToZar(usd, rate));
        }

        [Fact]
        public void ConvertUsdToZar_LargeAmount_CalculatesCorrectly()
        {
            // Tests that large numbers (e.g. after TechMove acquires a competitor)
            // are handled correctly without overflow
            decimal result = _sut.ConvertUsdToZar(1_000_000m, 18.50m);
            Assert.Equal(18_500_000.00m, result);
        }

        [Fact]
        public void ConvertUsdToZar_VerySmallAmount_CalculatesCorrectly()
        {
            // 0.01 × 18.50 = 0.185 → rounds to 0.18 (banker's rounding)
            decimal result = _sut.ConvertUsdToZar(0.01m, 18.50m);
            Assert.Equal(0.18m, result);
        }
    }
}