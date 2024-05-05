using Taxes.Test;
using static Taxes.Test.AssertExtensions;

namespace Taxes.Tests;

[TestClass]
public class CryptoPortfolioValuesTest
{
    private const string CHF = "CHF";
    private const string USD = "USD";
    private const string GBP = "GBP";

    // The 1st of January 2021 was a Friday
    private readonly FxRates FxRates = new(new() { [USD] = new() { [(2021, 1, 1).ToUtc()] = 1.0m } });

    private Basics Basics { get; } = new();

    [TestMethod]
    public void Parse_File_ReadsTheRightValues()
    {
        // The 1st of January 2024 was a Monday
        var fxRates = new FxRates(new()
        {
            [USD] = new()
            {
                [(2024, 1, 1).ToUtc()] = 1/1m,
                [(2024, 1, 2).ToUtc()] = 1/2m,
                [(2024, 1, 3).ToUtc()] = 1/3m,
            },
        });

        var portfolioValuesPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(portfolioValuesPath, """
                Date,PortfolioValue
                2024-01-01,0
                2024-01-02,1234
                2024-01-03,5.67
                """);

            var cryptoPortfolioValues = new CryptoPortfolioValues(Basics, fxRates, USD, portfolioValuesPath);
            AssertEq(0000m * 1m, cryptoPortfolioValues[(2024, 1, 1).ToUtc()]);
            AssertEq(1234m * 2m, cryptoPortfolioValues[(2024, 1, 2).ToUtc()]);
            AssertEq(5.67m * 3m, cryptoPortfolioValues[(2024, 1, 3).ToUtc()]);
            AssertEq(-1m, cryptoPortfolioValues[(2024, 1, 4).ToUtc()]); // No portfolio value for this date
        }
        finally
        {
            if (File.Exists(portfolioValuesPath))
                File.Delete(portfolioValuesPath);
        }
    }

    [TestMethod]
    public void Parse_ReadsTheRightCurrency()
    {
        // The 1st of January 2024 was a Monday
        var fxRates = new FxRates(new() 
            {
                [USD] = new()
                    {
                        [(2024, 1, 1).ToUtc()] = 1.0m,
                        [(2024, 1, 2).ToUtc()] = 1.1m,
                        [(2024, 1, 3).ToUtc()] = 1.2m,
                    },
                [CHF] = new()
                    {
                        [(2024, 1, 1).ToUtc()] = 0.9m,
                        [(2024, 1, 2).ToUtc()] = 0.8m,
                        [(2024, 1, 3).ToUtc()] = 0.7m,
                    },
            });

        static StringReader portfolioValuesReader() => new("""
            Date,PortfolioValue
            2024-01-01,0
            2024-01-02,1234
            2024-01-03,5.67
            """);

        var cryptoPortfolioValuesUSD = new CryptoPortfolioValues(Basics, fxRates, USD, portfolioValuesReader());
        AssertEq(0000m / 1.0m, cryptoPortfolioValuesUSD[(2024, 1, 1).ToUtc()]);
        AssertEq(1234m / 1.1m, cryptoPortfolioValuesUSD[(2024, 1, 2).ToUtc()]);
        AssertEq(5.67m / 1.2m, cryptoPortfolioValuesUSD[(2024, 1, 3).ToUtc()]);
        AssertEq(-1m, cryptoPortfolioValuesUSD[(2024, 1, 4).ToUtc()]); // No portfolio value for this date

        var cryptoPortfolioValuesCHF = new CryptoPortfolioValues(Basics, fxRates, CHF, portfolioValuesReader());
        AssertEq(0000m / 0.9m, cryptoPortfolioValuesCHF[(2024, 1, 1).ToUtc()]);
        AssertEq(1234m / 0.8m, cryptoPortfolioValuesCHF[(2024, 1, 2).ToUtc()]);
        AssertEq(5.67m / 0.7m, cryptoPortfolioValuesCHF[(2024, 1, 3).ToUtc()]);
        AssertEq(-1m, cryptoPortfolioValuesCHF[(2024, 1, 4).ToUtc()]); // No portfolio value for this date

        // No FX Rates for GBP
        var cryptoPortfolioValuesGBP = new CryptoPortfolioValues(Basics, fxRates, GBP, portfolioValuesReader());
        ThrowsAny<InvalidDataException>(() => _ = cryptoPortfolioValuesGBP[(2024, 1, 1).ToUtc()]);
        ThrowsAny<InvalidDataException>(() => _ = cryptoPortfolioValuesGBP[(2024, 1, 2).ToUtc()]);
        ThrowsAny<InvalidDataException>(() => _ = cryptoPortfolioValuesGBP[(2024, 1, 3).ToUtc()]);
    }

    [TestMethod]
    public void Parse_WhenHeaderColumnNameIsEmpty_RaisesException()
    {
        ThrowsAny<Exception>(() => _ = new CryptoPortfolioValues(Basics, FxRates, USD, new StringReader("""
            Date,
            2021-01-01,0.4
            """)));
    }

    [TestMethod]
    public void Parse_WhenHeaderColumnNameIsMissing_RaisesException()
    {
        ThrowsAny<Exception>(() => _ = new CryptoPortfolioValues(Basics, FxRates, USD, new StringReader("""
            Date
            2021-01-01,0.4
            """)));
    }

    [TestMethod]
    public void Parse_WhenHeaderContainsInvalidColumns_RaisesException()
    {
        ThrowsAny<Exception>(() => _ = new CryptoPortfolioValues(Basics, FxRates, USD, new StringReader("""
            Date,PortfolioValue,SomethingElse
            2021-01-01,0.4
            """)));
    }

    [TestMethod]
    public void Parse_WhenDataColumnIsMissing_RaisesException()
    {
        ThrowsAny<Exception>(() => _ = new CryptoPortfolioValues(Basics, FxRates, USD, new StringReader("""
            Date,PortfolioValue
            2021-01-01
            """)));
    }

    [TestMethod]
    public void Parse_WhenDataColumnIsInvalid_RaisesException()
    {
        ThrowsAny<Exception>(() => _ = new CryptoPortfolioValues(Basics, FxRates, USD, new StringReader("""
            Date,PortfolioValue
            2021-01-01,0.4,Extra
            """)));
    }

    [TestMethod]
    public void Parse_WhenDateFormatIsInvalid_RaisesException()
    {
        ThrowsAny<Exception>(() => _ = new CryptoPortfolioValues(Basics, FxRates, USD, new StringReader("""
            Date,PortfolioValue
            01-01-2021,0.4
            """)));
    }

    [TestMethod]
    public void Parse_WhenNumericFormatIsInvalid_RaisesException()
    {
        ThrowsAny<Exception>(() => _ = new CryptoPortfolioValues(Basics, FxRates, USD, new StringReader("""
            Date,PortfolioValue
            2021-01-01,0,4
            """)));
    }

    [TestMethod]
    public void Indexer_WhenSelectingAWeekendDay_ReturnsPortfolioValueNextMonday()
    {
        // The 1st of January 2021 was a Friday
        var fxRates = new FxRates(new() 
        { 
            [USD] = new() 
            { 
                [(2021, 1, 1).ToUtc()] = 1/2.0m,
                [(2021, 1, 4).ToUtc()] = 1/5.0m,
                [(2021, 1, 5).ToUtc()] = 1/6.0m,
            } 
        });

        static StringReader portfolioValuesReader() => new("""
            Date,PortfolioValue
            2021-01-01,1000
            2021-01-02,1001
            2021-01-03,1002
            2021-01-04,1003
            2021-01-05,1004
            """);

        var cryptoPortfolioValues = new CryptoPortfolioValues(Basics, fxRates, USD, portfolioValuesReader());
        AssertEq(2000m, cryptoPortfolioValues[(2021, 1, 1).ToUtc()]); // Friday
        AssertEq(5005m, cryptoPortfolioValues[(2021, 1, 2).ToUtc()]); // Saturday
        AssertEq(5010m, cryptoPortfolioValues[(2021, 1, 3).ToUtc()]); // Sunday
        AssertEq(5015m, cryptoPortfolioValues[(2021, 1, 4).ToUtc()]); // Monday
        AssertEq(6024m, cryptoPortfolioValues[(2021, 1, 5).ToUtc()]); // Tuesday
    }

    [TestMethod]
    public void Indexer_WhenSelectingANonWeekendDayWithMissingFXRate_ThrowsException()
    {
        // The 1st of January 2021 was a Friday
        var fxRates = new FxRates(new() { [USD] = new() { [(2021, 1, 1).ToUtc()] = 1.0m } });

        static StringReader portfolioValuesReader() => new("""
            Date,PortfolioValue
            2021-01-01,1000
            2021-01-06,1005
            """);

        var cryptoPortfolioValues = new CryptoPortfolioValues(Basics, fxRates, USD, portfolioValuesReader());
        ThrowsAny<InvalidDataException>(() => _ = cryptoPortfolioValues[(2021, 1, 6).ToUtc()]);
    }

    [AssertionMethod]
    private void AssertEq(decimal expected, decimal actual, string message = "") =>
        Assert.AreEqual(expected, actual, Basics.Precision, message);
}
