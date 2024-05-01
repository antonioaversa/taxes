using Taxes.Test;

namespace Taxes.Tests;

[TestClass]
public class CryptoPortfolioValuesTest
{
    private const string CHF = "CHF";
    private const string USD = "USD";

    private Basics Basics { get; } = new();

    [TestMethod]
    public void ReadsValuesCorrectly()
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

        var cryptoPortfolioValues = new CryptoPortfolioValues(Basics, fxRates, USD, new StringReader("""
            Date,PortfolioValue
            2021-01-01,0
            2021-01-02,1234
            2021-01-03,5.67
            """));

        AssertEq(0m, cryptoPortfolioValues[(2021, 1, 1).ToUtc()]);
        AssertEq(1234m / 1.1m, cryptoPortfolioValues[(2021, 1, 2).ToUtc()]);
        AssertEq(5.67m / 1.2m, cryptoPortfolioValues[(2021, 1, 3).ToUtc()]);
    }

    [AssertionMethod]
    private void AssertEq(decimal expected, decimal actual, string message = "") =>
        Assert.AreEqual(expected, actual, Basics.Precision, message);
}
