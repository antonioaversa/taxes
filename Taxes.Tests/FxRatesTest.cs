using static Taxes.FxRates;

namespace Taxes.Test;

[TestClass]
public class FxRatesTest
{
    private const string Currency = "USD";

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithEmptyContent_ReturnEmptyFxRates()
    {
        var fxRates = ParseSingleCurrencyFromContent(Currency, []);
        Assert.AreEqual(1, fxRates.Rates.Count);
        Assert.IsTrue(fxRates.Rates.ContainsKey(Currency));
        Assert.AreEqual(0, fxRates.Rates[Currency].Count);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithSingleRate_ReturnThatFxRate()
    {
        var fxRates = ParseSingleCurrencyFromContent(Currency, ["1/1/2021\t1.23"]);
        Assert.AreEqual(1, fxRates.Rates[Currency].Count);
        Assert.AreEqual(1.23m, fxRates.Rates[Currency][new(2021, 01, 01)]);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithMultipleRates_ReturnAllFxRates()
    {
        var fxRates = ParseSingleCurrencyFromContent(Currency, ["1/1/2021\t1.23", "1/2/2021\t1.24"]);
        Assert.AreEqual(2, fxRates.Rates[Currency].Count);
        Assert.AreEqual(1.23m, fxRates.Rates[Currency][new(2021, 01, 01)]);
        Assert.AreEqual(1.24m, fxRates.Rates[Currency][new(2021, 01, 02)]);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_IgnoresSingleComment()
    {
        var fxRates = ParseSingleCurrencyFromContent(Currency, ["// 1/1/2021\t1.23", "1/2/2021\t1.24"]);
        Assert.AreEqual(1, fxRates.Rates[Currency].Count);
        Assert.AreEqual(1.24m, fxRates.Rates[Currency][new(2021, 01, 02)]);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_IgnoresMultipleComment()
    {
        var fxRates = ParseSingleCurrencyFromContent(Currency, ["// 1/1/2021\t1.23", "// 1/2/2021\t1.24"]);
        Assert.AreEqual(0, fxRates.Rates[Currency].Count);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithInvalidFormat_RaisesException()
    {
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(Currency, ["1/1/2021\t1.23\t1.24"]));
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(Currency, ["1/1/2021"]));
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(Currency, ["1/1/2021\t1.23\tEUR"]));
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithMultipleFXRatesForTheSameDay_RaisesException()
    {
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(Currency, ["1/1/2021\t1.23", "1/1/2021\t1.24"]));
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WhenFXRateIsDash_SkipsTheLine()
    {
        var fxRates = ParseSingleCurrencyFromContent(Currency, ["1/1/2021\t-", "1/2/2021\t1.24"]);
        Assert.AreEqual(1, fxRates.Rates[Currency].Count);
        Assert.AreEqual(1.24m, fxRates.Rates[Currency][new(2021, 01, 02)]);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithInvalidDateFormat_RaisesException()
    {
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(Currency, ["1/1/21\t1.23"]));
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(Currency, ["13/1/21\t1.23"])); // 13th month
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(Currency, ["1/1/2021\t1.23", "1/2/21\t1.24"]));
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithInvalidFXRateFormat_RaisesException()
    {
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(Currency, ["1/1/2021\t1.23", "1/2/2021\t1,24a"]));
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(Currency, ["1/1/2021\t1.23", "1/2/2021\t1.24.0"]));
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(Currency, ["1/1/2021\t1.23", "1/2/2021\t-1.24"]));
    }

    [TestMethod]
    public void ParseSingleCurrencyFromFile_ReadsFileCorrectly()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, ["1/1/2021\t1.23", "1/2/2021\t1.24"]);

            var fxRates = ParseSingleCurrencyFromFile(Currency, path);
            Assert.AreEqual(2, fxRates.Rates[Currency].Count);
            Assert.AreEqual(1.23m, fxRates.Rates[Currency][new(2021, 01, 01)]);
            Assert.AreEqual(1.24m, fxRates.Rates[Currency][new(2021, 01, 02)]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}