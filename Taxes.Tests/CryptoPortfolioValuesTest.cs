using Taxes.Test;
using static Taxes.Test.AssertExtensions;

namespace Taxes.Tests;

[TestClass]
public class CryptoPortfolioValuesTest
{
    private const string CHF = "CHF";
    private const string USD = "USD";
    private const string GBP = "GBP";

    private Basics Basics { get; } = new();

    [TestMethod]
    public void Parse_ReadsTheRightCurrency()
    {
        var fxRates = new FxRates(new() 
            {
                [USD] = new()
                    {
                        [(2021, 1, 1).ToUtc()] = 1.0m,
                        [(2021, 1, 2).ToUtc()] = 1.1m,
                        [(2021, 1, 3).ToUtc()] = 1.2m,
                    },
                [CHF] = new()
                    {
                        [(2021, 1, 1).ToUtc()] = 0.9m,
                        [(2021, 1, 2).ToUtc()] = 0.8m,
                        [(2021, 1, 3).ToUtc()] = 0.7m,
                    },
            });

        static StringReader portfolioValuesReader() => new StringReader("""
            Date,PortfolioValue
            2021-01-01,0
            2021-01-02,1234
            2021-01-03,5.67
            """);

        var cryptoPortfolioValuesUSD = new CryptoPortfolioValues(Basics, fxRates, USD, portfolioValuesReader());
        AssertEq(0m / 1.0m, cryptoPortfolioValuesUSD[(2021, 1, 1).ToUtc()]);
        AssertEq(1234m / 1.1m, cryptoPortfolioValuesUSD[(2021, 1, 2).ToUtc()]);
        AssertEq(5.67m / 1.2m, cryptoPortfolioValuesUSD[(2021, 1, 3).ToUtc()]);
        AssertEq(-1m, cryptoPortfolioValuesUSD[(2021, 1, 4).ToUtc()]); // No portfolio value for this date

        var cryptoPortfolioValuesCHF = new CryptoPortfolioValues(Basics, fxRates, CHF, portfolioValuesReader());
        AssertEq(0m / 0.9m, cryptoPortfolioValuesCHF[(2021, 1, 1).ToUtc()]);
        AssertEq(1234m / 0.8m, cryptoPortfolioValuesCHF[(2021, 1, 2).ToUtc()]);
        AssertEq(5.67m / 0.7m, cryptoPortfolioValuesCHF[(2021, 1, 3).ToUtc()]);
        AssertEq(-1m, cryptoPortfolioValuesCHF[(2021, 1, 4).ToUtc()]); // No portfolio value for this date

        // No FX Rates for GBP
        var cryptoPortfolioValuesGBP = new CryptoPortfolioValues(Basics, fxRates, GBP, portfolioValuesReader());
        ThrowsAny<InvalidDataException>(() => _ = cryptoPortfolioValuesGBP[(2021, 1, 1).ToUtc()]);
        ThrowsAny<InvalidDataException>(() => _ = cryptoPortfolioValuesGBP[(2021, 1, 2).ToUtc()]);
        ThrowsAny<InvalidDataException>(() => _ = cryptoPortfolioValuesGBP[(2021, 1, 3).ToUtc()]);
    }

    [AssertionMethod]
    private void AssertEq(decimal expected, decimal actual, string message = "") =>
        Assert.AreEqual(expected, actual, Basics.Precision, message);
}
