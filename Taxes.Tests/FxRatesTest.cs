﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Taxes.FxRates;

namespace Taxes.Test;

[TestClass]
public class FxRatesTest
{
    private const string CurrencyUSD = "USD";
    private const string CurrencyCHF = "CHF";
    private const string CurrencyGBP = "GBP";
    private const string CurrencyUSDHeader = "Dollars(USD)";
    private const string CurrencyCHFHeader = "Francs(CHF)";
    private const string CurrencyGBPHeader = "Pounds(GBP)";

    private static string[] BuildHeaderLines(params string[] currencies) =>
        [
            $"Titre :;{string.Join(';', currencies)}",
            "    Code série : ... not relevant for the parsing",
            "    Unité : ... not relevant for the parsing",
            "Magnitude : ... not relevant for the parsing",
            "Méthode d'observation : ... not relevant for the parsing",
            "Source : ... not relevant for the parsing",
        ];

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithEmptyContent_ReturnEmptyFxRates()
    {
        var fxRates = ParseSingleCurrencyFromContent(CurrencyUSD, []);
        Assert.AreEqual(1, fxRates.Rates.Count);
        Assert.IsTrue(fxRates.Rates.ContainsKey(CurrencyUSD));
        Assert.AreEqual(0, fxRates.Rates[CurrencyUSD].Count);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithSingleRate_ReturnThatFxRate()
    {
        var fxRates = ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021\t1.23"]);
        Assert.AreEqual(1, fxRates.Rates[CurrencyUSD].Count);
        Assert.AreEqual(1.23m, fxRates.Rates[CurrencyUSD][new(2021, 01, 01)]);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithMultipleRates_ReturnAllFxRates()
    {
        var fxRates = ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021\t1.23", "1/2/2021\t1.24"]);
        Assert.AreEqual(2, fxRates.Rates[CurrencyUSD].Count);
        Assert.AreEqual(1.23m, fxRates.Rates[CurrencyUSD][new(2021, 01, 01)]);
        Assert.AreEqual(1.24m, fxRates.Rates[CurrencyUSD][new(2021, 01, 02)]);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_IgnoresSingleComment()
    {
        var fxRates = ParseSingleCurrencyFromContent(CurrencyUSD, ["// 1/1/2021\t1.23", "1/2/2021\t1.24"]);
        Assert.AreEqual(1, fxRates.Rates[CurrencyUSD].Count);
        Assert.AreEqual(1.24m, fxRates.Rates[CurrencyUSD][new(2021, 01, 02)]);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_IgnoresMultipleComment()
    {
        var fxRates = ParseSingleCurrencyFromContent(CurrencyUSD, ["// 1/1/2021\t1.23", "// 1/2/2021\t1.24"]);
        Assert.AreEqual(0, fxRates.Rates[CurrencyUSD].Count);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithInvalidFormat_RaisesException()
    {
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021\t1.23\t1.24"]));
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021"]));
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021\t1.23\tEUR"]));
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithMultipleFXRatesForTheSameDay_RaisesException()
    {
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021\t1.23", "1/1/2021\t1.24"]));
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WhenFXRateIsDash_SkipsTheLine()
    {
        var fxRates = ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021\t-", "1/2/2021\t1.24"]);
        Assert.AreEqual(1, fxRates.Rates[CurrencyUSD].Count);
        Assert.AreEqual(1.24m, fxRates.Rates[CurrencyUSD][new(2021, 01, 02)]);
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithInvalidDateFormat_RaisesException()
    {
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/21\t1.23"]));
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(CurrencyUSD, ["13/1/21\t1.23"])); // 13th month
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021\t1.23", "1/2/21\t1.24"]));
    }

    [TestMethod]
    public void ParseSingleCurrencyFromContent_WithInvalidFXRateFormat_RaisesException()
    {
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021\t1.23", "1/2/2021\t1,24a"]));
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021\t1.23", "1/2/2021\t1.24.0"]));
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseSingleCurrencyFromContent(CurrencyUSD, ["1/1/2021\t1.23", "1/2/2021\t-1.24"]));
    }

    [TestMethod]
    public void ParseSingleCurrencyFromFile_ReadsFileCorrectly()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, ["1/1/2021\t1.23", "1/2/2021\t1.24"]);

            var fxRates = ParseSingleCurrencyFromFile(CurrencyUSD, path);
            Assert.AreEqual(2, fxRates.Rates[CurrencyUSD].Count);
            Assert.AreEqual(1.23m, fxRates.Rates[CurrencyUSD][new(2021, 01, 01)]);
            Assert.AreEqual(1.24m, fxRates.Rates[CurrencyUSD][new(2021, 01, 02)]);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void ParseMultiCurrenciesFromContent_WithInvalidHeaderLines_RaisesException()
    {
        // Including only the first header line
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseMultiCurrenciesFromContent(BuildHeaderLines(CurrencyUSDHeader).Take(1).ToList()));
        // Including only the second header line
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseMultiCurrenciesFromContent(BuildHeaderLines(CurrencyUSDHeader).Skip(1).Take(1).ToList()));
        // Including only the first three header lines
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseMultiCurrenciesFromContent(BuildHeaderLines(CurrencyUSDHeader).Take(3).ToList()));
        // With one of the header lines wrong (different prefix than expected)
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseMultiCurrenciesFromContent(BuildHeaderLines(CurrencyUSDHeader)
                .Select((s, i) => i != 3 ? s : $"Invalid Prefix{s}").ToList()));
        // With header lines in the wrong order
        AssertExtensions.ThrowsAny<InvalidDataException>(
            () => ParseMultiCurrenciesFromContent(BuildHeaderLines(CurrencyUSDHeader).Reverse().ToList()));

        // With currency with invalid format
        AssertExtensions.ThrowsAny<Exception>(() => ParseMultiCurrenciesFromContent(BuildHeaderLines("")));
        AssertExtensions.ThrowsAny<Exception>(() => ParseMultiCurrenciesFromContent(BuildHeaderLines("Dollar")));
        AssertExtensions.ThrowsAny<Exception>(() => ParseMultiCurrenciesFromContent(BuildHeaderLines("Dollar(USD")));
        AssertExtensions.ThrowsAny<Exception>(() => ParseMultiCurrenciesFromContent(BuildHeaderLines("DollarUSD)")));
        AssertExtensions.ThrowsAny<Exception>(() => ParseMultiCurrenciesFromContent(BuildHeaderLines("(USD)")));
    }

    [TestMethod]
    public void ParseMultiCurrenciesFromContent_WithSingleCurrencyAndNoData_ReturnsDictionaryOfTheEmptyCurrency()
    {
        var fxRates = ParseMultiCurrenciesFromContent(BuildHeaderLines(CurrencyUSDHeader));
        Assert.AreEqual(1, fxRates.Rates.Count);
        Assert.IsTrue(fxRates.Rates.ContainsKey(CurrencyUSD));
        Assert.AreEqual(0, fxRates.Rates[CurrencyUSD].Count);
    }

    [TestMethod]
    public void ParseMultiCurrenciesFromContent_WithMultipleCurrenciesAndNoData_ReturnsDictionaryOfTheEmptyCurrencies()
    {
        var fxRates = ParseMultiCurrenciesFromContent(BuildHeaderLines(CurrencyUSDHeader, CurrencyCHFHeader, CurrencyGBPHeader));
        Assert.AreEqual(3, fxRates.Rates.Count);
        Assert.IsTrue(fxRates.Rates.ContainsKey(CurrencyUSD));
        Assert.IsTrue(fxRates.Rates.ContainsKey(CurrencyCHF));
        Assert.IsTrue(fxRates.Rates.ContainsKey(CurrencyGBP));
        Assert.AreEqual(0, fxRates.Rates[CurrencyUSD].Count);
        Assert.AreEqual(0, fxRates.Rates[CurrencyCHF].Count);
        Assert.AreEqual(0, fxRates.Rates[CurrencyGBP].Count);
    }

    [TestMethod]
    public void ParseMultiCurrenciesFromContent_WithDifferentNumberOfDataFieldsThanCurrencies_RaisesException()
    {
        // Less data than currencies
        AssertExtensions.ThrowsAny<InvalidDataException>(() => ParseMultiCurrenciesFromContent(
            [.. BuildHeaderLines(CurrencyUSDHeader, CurrencyCHFHeader), "01/01/2021;1,23"]));
        // More data than currencies
        AssertExtensions.ThrowsAny<InvalidDataException>(() => ParseMultiCurrenciesFromContent(
            [.. BuildHeaderLines(CurrencyUSDHeader, CurrencyCHFHeader), "01/01/2021;1,23;1,24;1,25"]));
    }

    [TestMethod]
    public void ParseMultiCurrenciesFromContent_WithInvalidDayFormat_RaisesException()
    {
        AssertExtensions.ThrowsAny<FormatException>(() => ParseMultiCurrenciesFromContent(
            [.. BuildHeaderLines(CurrencyUSDHeader, CurrencyCHFHeader), "1/1/2021;1,23;1,24"]));
        AssertExtensions.ThrowsAny<FormatException>(() => ParseMultiCurrenciesFromContent(
            [.. BuildHeaderLines(CurrencyUSDHeader, CurrencyCHFHeader), "01/1/2021;1,23;1,24"]));
        AssertExtensions.ThrowsAny<FormatException>(() => ParseMultiCurrenciesFromContent(
            [.. BuildHeaderLines(CurrencyUSDHeader, CurrencyCHFHeader), "01/01/21;1,23;1,24"]));
    }

    [TestMethod]
    public void ParseMultiCurrenciesFromContent_WithInvalidNumber_RaisesException()
    {
        AssertExtensions.ThrowsAny<FormatException>(() => ParseMultiCurrenciesFromContent(
            [.. BuildHeaderLines(CurrencyUSDHeader), "01/01/2021;1.23"]));
        AssertExtensions.ThrowsAny<FormatException>(() => ParseMultiCurrenciesFromContent(
            [.. BuildHeaderLines(CurrencyUSDHeader), "01/01/2021;1,24.0"]));
        AssertExtensions.ThrowsAny<FormatException>(() => ParseMultiCurrenciesFromContent(
            [.. BuildHeaderLines(CurrencyUSDHeader), "01/01/2021;1,23a"]));
    }

    [TestMethod]
    public void ParseMultiCurrenciesWithRoupieIndienneInHeader()
    {
        var fxRates = ParseMultiCurrenciesFromContent(
            [.. BuildHeaderLines("Roupie indienne(100 paise)"), "01/01/2021;11,2", "02/01/2021;11,3" ]);
        Assert.AreEqual(1, fxRates.Rates.Count);
        Assert.IsTrue(fxRates.Rates.ContainsKey("100 paise"));
        Assert.AreEqual(2, fxRates.Rates["100 paise"].Count);
        Assert.AreEqual(11.2m, fxRates.Rates["100 paise"][new(2021, 01, 01)]);
        Assert.AreEqual(11.3m, fxRates.Rates["100 paise"][new(2021, 01, 02)]);
    }
}