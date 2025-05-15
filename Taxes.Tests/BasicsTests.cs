using Newtonsoft.Json;
using Taxes.Test;

namespace Taxes.Tests;

[TestClass]
public class BasicsTests
{
    private const string TestReportsDir = "TestReports";
    private const string BasicsJsonFileName = "Basics.json";
        
    private static readonly string TestBasicsJsonPath = Path.Combine(TestReportsDir, BasicsJsonFileName);

    [TestInitialize]
    public void TestInitialize()
    {
        Directory.CreateDirectory(TestReportsDir);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(TestReportsDir))
        {
            Directory.Delete(TestReportsDir, true);
        }
    }

    private static void CreateBasicsJsonFile(object content)
    {
        var jsonContent = JsonConvert.SerializeObject(content, Formatting.Indented);
        File.WriteAllText(TestBasicsJsonPath, jsonContent);
    }

    [TestMethod]
    public void Basics_Constructor_ValidFile_InitializesProperties()
    {
        var validBasicsContent = new
        {
            Rounding = "Fixed_2",
            Precision = 0.01m,
            BaseCurrency = "USD",
            BeginTaxPeriodOfInterest = "2023-01-01",
            EndTaxPeriodOfInterest = "2023-12-31",
            FilterTaxFormsByPeriodOfInterest = "true",
            Positions = new Dictionary<string, object>
            {
                ["AAPL"] = new { Country = "US", ISIN = "US0378331005" }
            },
            StockEventsFiles = new List<object>
            {
                new { FilePattern = "stock_events_*.csv", Broker = "Broker1" }
            },
            CryptoEventsFiles = new List<object>
            {
                new { FilePattern = "crypto_events_*.csv", Broker = "Broker2" }
            },
            CryptoPortfolioValuesFilePath = "crypto_portfolio.csv",
            MergeAllCryptos = false,
            FXRatesFilePath = "fx_rates.csv",
            WithholdingTaxes = new Dictionary<string, object>
            {
                ["US"] = new { Dividends = 0.15m, Interests = 0.10m }
            }
        };
        CreateBasicsJsonFile(validBasicsContent);

        var basics = new Basics(TestReportsDir);

        Assert.AreEqual("USD", basics.BaseCurrency);
        Assert.AreEqual(new DateTime(2023, 12, 31, 0, 0, 0, DateTimeKind.Local), basics.EndTaxPeriodOfInterest);
        Assert.IsTrue(basics.FilterTaxFormsByPeriodOfInterest);
        Assert.AreEqual(0.01m, basics.Precision);
        Assert.IsNotNull(basics.Rounding);
        // Test rounding "Fixed_2"
        Assert.AreEqual(12.34m, basics.Rounding(12.345m));
        Assert.AreEqual(12.36m, basics.Rounding(12.355m));
        Assert.AreEqual(12.36m, basics.Rounding(12.365m));
        Assert.AreEqual(1, basics.Positions.Count);
        Assert.AreEqual("US", basics.Positions["AAPL"].Country);
        Assert.AreEqual("US0378331005", basics.Positions["AAPL"].ISIN);
        Assert.AreEqual(1, basics.StockEventsFiles.Count);
        Assert.AreEqual("stock_events_*.csv", basics.StockEventsFiles[0].FilePattern);
        Assert.AreEqual("Broker1", basics.StockEventsFiles[0].Broker);
        Assert.AreEqual(1, basics.CryptoEventsFiles.Count);
        Assert.AreEqual("crypto_events_*.csv", basics.CryptoEventsFiles[0].FilePattern);
        Assert.AreEqual("Broker2", basics.CryptoEventsFiles[0].Broker);
        Assert.AreEqual("crypto_portfolio.csv", basics.CryptoPortfolioValuesFilePath);
        Assert.IsFalse(basics.MergeAllCryptos);
        Assert.AreEqual("fx_rates.csv", basics.FXRatesFilePath);
        Assert.AreEqual(1, basics.WithholdingTaxes.Count);
        Assert.AreEqual(0.15m, basics.WithholdingTaxes["US"].Dividends);
        Assert.AreEqual(0.10m, basics.WithholdingTaxes["US"].Interests);
    }

    [TestMethod]
    public void Basics_Constructor_ReportsDirectoryNotFound_ThrowsException()
    {
        AssertExtensions.ThrowsAny<Exception>(() => 
            _ = new Basics("NonExistentDirectory"));
    }

    [TestMethod]
    public void Basics_Constructor_BasicsFileNotFound_ThrowsException()
    {
        AssertExtensions.ThrowsAny<Exception>(() => 
            new Basics(TestReportsDir, "NonExistentBasics.json"));
    }

    private object GetMinimalValidBasicsContent()
    {
        return new
        {
            Rounding = "Fixed_2",
            Precision = 0.01m,
            BaseCurrency = "USD",
            BeginTaxPeriodOfInterest = "2023-01-01",
            EndTaxPeriodOfInterest = "2023-12-31",
            FilterTaxFormsByPeriodOfInterest = "true",
            Positions = new Dictionary<string, object>(),
            StockEventsFiles = new List<object>(),
            CryptoEventsFiles = new List<object>(),
            CryptoPortfolioValuesFilePath = "path.csv",
            MergeAllCryptos = false,
            FXRatesFilePath = "fx.csv",
            WithholdingTaxes = new Dictionary<string, object>()
        };
    }

    [TestMethod]
    public void Basics_Constructor_MissingRounding_ThrowsException()
    {
        dynamic content = GetMinimalValidBasicsContent();
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(content));
        dict!.Remove("Rounding");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_InvalidRoundingFormat_ThrowsException()
    {
        dynamic content = GetMinimalValidBasicsContent();
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(content));
        dict["Rounding"] = "InvalidFormat";
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingPrecision_ThrowsException()
    {
        dynamic content = GetMinimalValidBasicsContent();
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(content));
        dict!.Remove("Precision");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingBaseCurrency_ThrowsException()
    {
        dynamic content = GetMinimalValidBasicsContent();
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(content));
        dict!.Remove("BaseCurrency");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingPositions_ThrowsException()
    {
        dynamic content = GetMinimalValidBasicsContent();
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(content));
        dict!.Remove("Positions");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_PositionMissingCountry_ThrowsException()
    {
        dynamic content = GetMinimalValidBasicsContent();
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(content));
        dict["Positions"] = new Dictionary<string, object>
        {
            ["AAPL"] = new { ISIN = "US0378331005" } // Missing Country
        };
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_PositionEmptyCountry_ThrowsException()
    {
        dynamic content = GetMinimalValidBasicsContent();
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(content));
        dict["Positions"] = new Dictionary<string, object>
        {
            ["AAPL"] = new { Country = string.Empty, ISIN = "US0378331005" }
        };
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_PositionMissingISIN_ThrowsException()
    {
        dynamic content = GetMinimalValidBasicsContent();
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(content));
        dict["Positions"] = new Dictionary<string, object>
        {
            ["AAPL"] = new { Country = "US" } // Missing ISIN
        };
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_PositionEmptyISIN_ThrowsException()
    {
        dynamic content = GetMinimalValidBasicsContent();
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(content));
        dict["Positions"] = new Dictionary<string, object>
        {
            ["AAPL"] = new { Country = "US", ISIN = string.Empty }
        };
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingBeginTaxPeriodOfInterest_ThrowsException()
    {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(GetMinimalValidBasicsContent()));
        dict!.Remove("BeginTaxPeriodOfInterest");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingEndTaxPeriodOfInterest_ThrowsException()
    {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(GetMinimalValidBasicsContent()));
        dict!.Remove("EndTaxPeriodOfInterest");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }
        
    [TestMethod]
    public void Basics_Constructor_MissingFilterTaxFormsByPeriodOfInterest_ThrowsException()
    {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(GetMinimalValidBasicsContent()));
        dict!.Remove("FilterTaxFormsByPeriodOfInterest");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingStockEventsFiles_ThrowsException()
    {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(GetMinimalValidBasicsContent()));
        dict!.Remove("StockEventsFiles");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingCryptoEventsFiles_ThrowsException()
    {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(GetMinimalValidBasicsContent()));
        dict!.Remove("CryptoEventsFiles");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingCryptoPortfolioValuesFilePath_ThrowsException()
    {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(GetMinimalValidBasicsContent()));
        dict!.Remove("CryptoPortfolioValuesFilePath");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingMergeAllCryptos_ThrowsException()
    {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(GetMinimalValidBasicsContent()));
        dict!.Remove("MergeAllCryptos");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingFXRatesFilePath_ThrowsException()
    {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(GetMinimalValidBasicsContent()));
        dict!.Remove("FXRatesFilePath");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_MissingWithholdingTaxes_ThrowsException()
    {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(GetMinimalValidBasicsContent()));
        dict!.Remove("WithholdingTaxes");
        CreateBasicsJsonFile(dict);
        AssertExtensions.ThrowsAny<Exception>(() => _ = new Basics(TestReportsDir));
    }

    [TestMethod]
    public void Basics_Constructor_RoundingWithResolutionAroundZero_ParsesCorrectly()
    {
        var content = new
        {
            Rounding = "Fixed_2_0.005", // Round to 2 decimal places, values < 0.005 become 0
            Precision = 0.01m,
            BaseCurrency = "EUR",
            BeginTaxPeriodOfInterest = "2023-01-01",
            EndTaxPeriodOfInterest = "2023-12-31",
            FilterTaxFormsByPeriodOfInterest = "true",
            Positions = new Dictionary<string, object>(),
            StockEventsFiles = new List<object>(),
            CryptoEventsFiles = new List<object>(),
            CryptoPortfolioValuesFilePath = "path.csv",
            MergeAllCryptos = false,
            FXRatesFilePath = "fx.csv",
            WithholdingTaxes = new Dictionary<string, object>()
        };
        CreateBasicsJsonFile(content);
        var basics = new Basics(TestReportsDir);
        Assert.IsNotNull(basics.Rounding);
        Assert.AreEqual(0m, basics.Rounding(0.004m));
        Assert.AreEqual(0.01m, basics.Rounding(0.006m));
        Assert.AreEqual(12.35m, basics.Rounding(12.346m));
        Assert.AreEqual(-0.01m, basics.Rounding(-0.006m));
        Assert.AreEqual(0m, basics.Rounding(-0.004m));
    }

    // More tests will be added here
}