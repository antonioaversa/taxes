﻿using System.Globalization;
using static Taxes.Tests.AssertExtensions;

namespace Taxes.Tests;

[TestClass]
public class CryptoEventsReaderTest
{
    private static readonly Basics Basics = new() { BaseCurrency = "EUR" };

    private const string Broker = "THE BROKER";

    private const string HeaderLinePre2025 =
        "Type,Product,Started Date,Completed Date,Description,Amount,Currency,Fiat amount,Fiat amount (inc. fees),Fee,Base currency,State,Balance";

    private const string HeaderLine2025 = "Symbol,Type,Quantity,Price,Value,Fees,Date";

    private static readonly FxRates NoFxRates = new(Basics, []);

    private readonly CryptoEventsReader Instance = new(Basics);
    private readonly TextWriter NoOut = TextWriter.Null;

    [TestInitialize]
    public void TestInitialize()
    {
        // Set the culture to InvariantCulture since some assertions are on logged text, and
        // those logs are done via a simple interpolated string, so they are culture-specific.
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    }

    [TestMethod]
    public void ParseFile_WithTemporaryFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"""
                {HeaderLinePre2025}
                EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
                EXCHANGE,Current,2022-06-27 10:32:23,2022-06-27 10:32:23,Exchanged to EUR,-1000.0000000000,ZRX,-324.0898000000,-319.2298000000,4.8600000000,EUR,COMPLETED,0.0000000000
                """);

            var events = Instance.ParseFile(path, NoFxRates, Broker, NoOut);
            Assert.AreEqual(2, events.Count);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithEmptyContent_ThrowException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent(string.Empty, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithOnlyHeader_ReturnEmptyList()
    {
        var content = HeaderLinePre2025;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithBlankLines_IgnoresThem()
    {
        const string content = $"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000

            EXCHANGE,Current,2022-06-27 10:32:23,2022-06-27 10:32:23,Exchanged to EUR,-1000.0000000000,ZRX,-324.0898000000,-319.2298000000,4.8600000000,EUR,COMPLETED,0.0000000000            
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(2, events.Count);
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithInvalidType_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            INVALID,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithTransferType_IgnoresTheRecord()
    {
        const string content = $"""
            {HeaderLinePre2025}
            TRANSFER,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithRewardType_ReadsTheRecord()
    {
        const string content = $"""
            {HeaderLinePre2025}
            REWARD,Crypto Staking,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(1, events.Count);
        var firstEvent = events[0];
        Assert.AreEqual(new DateTime(2022, 6, 25, 13, 29, 3, DateTimeKind.Utc), firstEvent.Date);
        Assert.AreEqual(EventType.Reward, firstEvent.Type);
        Assert.AreEqual("CRYPTO", firstEvent.Ticker);
        Assert.AreEqual(1000m, firstEvent.Quantity);
        Assert.IsNotNull(firstEvent.PricePerShareLocal);
        Assert.AreEqual(293.9067439m / 1000, firstEvent.PricePerShareLocal.Value, 0.000001m);
        Assert.AreEqual(298.3167439m, firstEvent.TotalAmountLocal, 0.000001m);
        Assert.IsNotNull(firstEvent.FeesLocal);
        Assert.AreEqual(4.41m, firstEvent.FeesLocal.Value, 0.000001m);
        Assert.AreEqual("EUR", firstEvent.Currency);
        Assert.AreEqual(1m, firstEvent.FXRate);
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithRewardTypeAndCurrentProduct_RaisesExceptionForInconsistency()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            REWARD,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithNonCurrentProduct_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,NonCurrent,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithInvalidDateTime_RaisesException()
    {
        // Missing part of time in Started Date
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
        // Missing part of time in Completed Date
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
        // dd-MM-yyyy instead of yyyy-MM-dd in Started Date
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,25-06-2022 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
        // dd-MM-yyyy instead of yyyy-MM-dd in Completed Date
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,25-06-2022 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
        // 31 of April does not exist in Started Date
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-04-31 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
        // 31 of April does not exist in Completed Date
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-04-31 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [DataTestMethod]
    [DataRow("2022-06-25 13:29:03.123", DisplayName = "Milliseconds not supported")]
    [DataRow("2022-06-25", DisplayName = "Short datetime format not supported")]
    [DataRow("2022-06-25 13:29:03+02:00", DisplayName = "Timezone not supported")]
    [DataRow("2022-06-25T13:29:03", DisplayName = "T between date and time not supported")]
    public void ParseContent_Pre2025_WithUnsupportedDateTimeFormats(string startedDate)
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,{startedDate},2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithStartedDateDifferentThanCompletedDate_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-24 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithStateDifferentThanCompleted_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,INVALID,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithRecordBaseCurrencyDifferentThanBasicsBaseCurrency_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,USD,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithReset_ReturnsResetEvent()
    {
        const string content = $"""
            {HeaderLinePre2025}
            RESET,,2023-01-01 00:00:00,2023-01-01 00:00:00,,,,,,,EUR,,
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(1, events.Count);
        var resetEvent = events[0];
        Assert.AreEqual(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc), resetEvent.Date);
        Assert.AreEqual(EventType.Reset, resetEvent.Type);
        Assert.IsNull(resetEvent.Ticker);
        Assert.IsNull(resetEvent.Quantity);
        Assert.IsNull(resetEvent.PricePerShareLocal);
        Assert.AreEqual(0, resetEvent.TotalAmountLocal);
        Assert.IsNull(resetEvent.FeesLocal);
        Assert.AreEqual("EUR", resetEvent.Currency);
        Assert.AreEqual(1m, resetEvent.FXRate);
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithExchange_ReturnsEventsWithCorrectProperties()
    {
        const string content = $"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            EXCHANGE,Current,2022-06-27 10:32:23,2022-06-27 10:32:23,Exchanged to EUR,-1000.0000000000,ZRX,-324.0898000000,-319.2298000000,4.8600000000,EUR,COMPLETED,0.0000000000
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(2, events.Count);

        var delta = 0.000001m;
        var firstEvent = events[0];
        Assert.AreEqual(new DateTime(2022, 6, 25, 13, 29, 3, DateTimeKind.Utc), firstEvent.Date);
        Assert.AreEqual(EventType.BuyMarket, firstEvent.Type);
        // The ticker is not ZRX, but a generic CRYPTO ticker
        Assert.AreEqual("CRYPTO", firstEvent.Ticker);
        // The quantity is positive for a buy event, but taken in absolute value anyway
        Assert.AreEqual(1000m, firstEvent.Quantity);
        Assert.IsNotNull(firstEvent.PricePerShareLocal);
        Assert.AreEqual(293.9067439m / 1000, firstEvent.PricePerShareLocal.Value, delta);
        // For a buy event, the total amount is higher than the price per share * quantity due to fees, that are added
        Assert.AreEqual(298.3167439m, firstEvent.TotalAmountLocal, delta);
        Assert.IsNotNull(firstEvent.FeesLocal);
        Assert.AreEqual(4.41m, firstEvent.FeesLocal.Value, delta);
        Assert.AreEqual("EUR", firstEvent.Currency);
        Assert.AreEqual(1m, firstEvent.FXRate);

        var secondEvent = events[1];
        Assert.AreEqual(new DateTime(2022, 6, 27, 10, 32, 23, DateTimeKind.Utc), secondEvent.Date);
        Assert.AreEqual(EventType.SellMarket, secondEvent.Type);
        // The ticker is not ZRX, but a generic CRYPTO ticker
        Assert.AreEqual("CRYPTO", secondEvent.Ticker);
        // The quantity is negative for a sell event, but taken in absolute value
        Assert.AreEqual(1000m, secondEvent.Quantity);
        Assert.IsNotNull(secondEvent.PricePerShareLocal);
        Assert.AreEqual(324.0898m / 1000m, secondEvent.PricePerShareLocal.Value, delta);
        // For a sell event, the total amount is lower than the price per share * quantity due to fees, that are subtracted
        Assert.AreEqual(319.2298m, secondEvent.TotalAmountLocal, delta);
        Assert.IsNotNull(secondEvent.FeesLocal);
        Assert.AreEqual(4.86m, secondEvent.FeesLocal.Value, delta);
        Assert.AreEqual("EUR", secondEvent.Currency);
        Assert.AreEqual(1m, secondEvent.FXRate);
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithZeroQuantity_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,0.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,0.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithAmountAndFiatAmountHavingDifferentSigns_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,-1000.0000000000,ZRX,293.9067439000,-298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithFiatAmountsHavingDifferentSigns_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,-298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithNegativeFees_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,-4.4100000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithZeroFees_IsOk()
    {
        var events = Instance.ParseContent($"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,0.0000000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut);
        Assert.AreEqual(1, events.Count);
    }

    [TestMethod]
    public void ParseContent_Pre2025_With2025Header_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLine2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,0.0000000000,EUR,COMPLETED,1000.0000000000
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_Pre2025_WithMergeAllCryptosFalse_KeepsOriginalCryptoCurrency()
    {
        const string content = $"""
            {HeaderLinePre2025}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            EXCHANGE,Current,2022-06-27 10:32:23,2022-06-27 10:32:23,Exchanged to EUR,-1000.0000000000,ZRX,-324.0898000000,-319.2298000000,4.8600000000,EUR,COMPLETED,0.0000000000
            """;
        var basics = new Basics() { MergeAllCryptos = false };
        var instance = new CryptoEventsReader(basics);
        var events = instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(2, events.Count);
        var firstEvent = events[0];
        Assert.AreEqual("ZRX", firstEvent.Ticker);
        var secondEvent = events[1];
        Assert.AreEqual("ZRX", secondEvent.Ticker);
    }

    [TestMethod]
    public void ParseContent_2025_WithValidReset_ReturnsResetEvent()
    {
        const string content = $"""
            {HeaderLine2025}
            ,Reset,,,,,"Jan 1, 2024, 12:00:00 AM"
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(1, events.Count);
        var resetEvent = events[0];
        Assert.AreEqual(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), resetEvent.Date);
        Assert.AreEqual(EventType.Reset, resetEvent.Type);
        Assert.IsNull(resetEvent.Ticker);
        Assert.IsNull(resetEvent.Quantity);
        Assert.IsNull(resetEvent.PricePerShareLocal);
        Assert.AreEqual(0, resetEvent.TotalAmountLocal);
        Assert.IsNull(resetEvent.FeesLocal);
        Assert.AreEqual(Instance.Basics.BaseCurrency, resetEvent.Currency);
        Assert.AreEqual(1m, resetEvent.FXRate);
    }

    [TestMethod]
    public void ParseContent_2025_WithResetWithNoDate_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLine2025}
            ,Reset,,,,,,
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_2025_WithResetWithTicker_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLine2025}
            BTC,Reset,,,,,"Jan 1, 2024, 12:00:00 AM"
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_2025_WithResetWithQuantity_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLine2025}
            ,Reset,0.01,,,,,"Jan 1, 2024, 12:00:00 AM"
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_2025_WithResetWithPrice_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ParseContent($"""
            {HeaderLine2025}
            ,Reset,,0.01,,,,"Jan 1, 2024, 12:00:00 AM"
            """, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_2025_WithValidSell_ReturnsSellEvent()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Sell,0.02,"EUR 62,671.63","EUR 1,253.43",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(1, events.Count);
        var firstEvent = events[0];
        Assert.AreEqual(new DateTime(2024, 3, 8, 21, 32, 39, DateTimeKind.Utc), firstEvent.Date);
        Assert.AreEqual(EventType.SellMarket, firstEvent.Type);
        Assert.AreEqual("CRYPTO", firstEvent.Ticker);
        Assert.AreEqual(0.02m, firstEvent.Quantity);
        Assert.IsNotNull(firstEvent.PricePerShareLocal);
        Assert.AreEqual(62671.63m, firstEvent.PricePerShareLocal.Value);
        Assert.AreEqual(1253.43m - 12.41m, firstEvent.TotalAmountLocal);
        Assert.IsNotNull(firstEvent.FeesLocal);
        Assert.AreEqual(12.41m, firstEvent.FeesLocal.Value);
        Assert.AreEqual("EUR", firstEvent.Currency);
        Assert.AreEqual(1m, firstEvent.FXRate);
    }

    [TestMethod]
    public void ParseContent_2025_WithValidBuy_ReturnsBuyEvent()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Buy,0.02,"EUR 62,654.96","EUR 1,253.10",EUR 12.41,"Mar 15, 2024, 12:05:32 PM"
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(1, events.Count);
        var firstEvent = events[0];
        Assert.AreEqual(new DateTime(2024, 3, 15, 12, 5, 32, DateTimeKind.Utc), firstEvent.Date);
        Assert.AreEqual(EventType.BuyMarket, firstEvent.Type);
        Assert.AreEqual("CRYPTO", firstEvent.Ticker);
        Assert.AreEqual(0.02m, firstEvent.Quantity);
        Assert.IsNotNull(firstEvent.PricePerShareLocal);
        Assert.AreEqual(62654.96m, firstEvent.PricePerShareLocal.Value);
        Assert.AreEqual(1253.10m + 12.41m, firstEvent.TotalAmountLocal);
        Assert.IsNotNull(firstEvent.FeesLocal);
        Assert.AreEqual(12.41m, firstEvent.FeesLocal.Value);
        Assert.AreEqual("EUR", firstEvent.Currency);
        Assert.AreEqual(1m, firstEvent.FXRate);
    }

    [TestMethod]
    public void ParseContent_2025_WithCurrencyBeforeAmount_ParsesCurrencyCorrectly()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Sell,0.02,"EUR 62,671.63","EUR 1,253.43",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(1, events.Count);
        var firstEvent = events[0];
        Assert.AreEqual(new DateTime(2024, 3, 8, 21, 32, 39, DateTimeKind.Utc), firstEvent.Date);
        Assert.AreEqual(EventType.SellMarket, firstEvent.Type);
        Assert.AreEqual("CRYPTO", firstEvent.Ticker);
        Assert.AreEqual(0.02m, firstEvent.Quantity);
        Assert.IsNotNull(firstEvent.PricePerShareLocal);
        Assert.AreEqual(62671.63m, firstEvent.PricePerShareLocal.Value);
        Assert.AreEqual(1253.43m - 12.41m, firstEvent.TotalAmountLocal);
        Assert.IsNotNull(firstEvent.FeesLocal);
        Assert.AreEqual(12.41m, firstEvent.FeesLocal.Value);
        Assert.AreEqual("EUR", firstEvent.Currency);
        Assert.AreEqual(1m, firstEvent.FXRate);
    }

    [TestMethod]
    public void ParseContent_2025_WithCurrencyAfterAmount_ParsesCurrencyCorrectly()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Sell,0.02,"EUR 62,671.63","1,253.43 EUR",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(1, events.Count);
        var firstEvent = events[0];
        Assert.AreEqual(new DateTime(2024, 3, 8, 21, 32, 39, DateTimeKind.Utc), firstEvent.Date);
        Assert.AreEqual(EventType.SellMarket, firstEvent.Type);
        Assert.AreEqual("CRYPTO", firstEvent.Ticker);
        Assert.AreEqual(0.02m, firstEvent.Quantity);
        Assert.IsNotNull(firstEvent.PricePerShareLocal);
        Assert.AreEqual(62671.63m, firstEvent.PricePerShareLocal.Value);
        Assert.AreEqual(1253.43m - 12.41m, firstEvent.TotalAmountLocal);
        Assert.IsNotNull(firstEvent.FeesLocal);
        Assert.AreEqual(12.41m, firstEvent.FeesLocal.Value);
        Assert.AreEqual("EUR", firstEvent.Currency);
        Assert.AreEqual(1m, firstEvent.FXRate);
    }

    [TestMethod]
    public void ParseContent_2025_WithMultipleEventsWithDifferentCurrencies_ParsesEachCurrencyCorrectly()
    {
        var fxRates = new FxRates(Basics, new()
        {
            ["USD"] = new() { [(2024, 03, 15).ToUtc()] = 1.09m }
        });
        const string content = $"""
            {HeaderLine2025}
            BTC,Sell,0.02,"EUR 62,671.63","1,253.43 EUR",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
            BTC,Buy,0.02,"USD 62,654.96","1,253.10 USD",USD 12.41,"Mar 15, 2024, 12:05:32 PM"
            """;
        var events = Instance.ParseContent(content, fxRates, Broker, NoOut);
        Assert.AreEqual(2, events.Count);
        var firstEvent = events[0];
        Assert.AreEqual("EUR", firstEvent.Currency);
        var secondEvent = events[1];
        Assert.AreEqual("EUR", secondEvent.Currency);
        Assert.AreEqual(1.09m, secondEvent.FXRate);
    }

    [TestMethod]
    public void ParseContent_2025_WithSingleEventWithMultipleCurrencies_ThrowsException()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Sell,0.02,"EUR 62,671.63","1,253.43 EUR",USD 12.41,"Mar 8, 2024, 9:32:39 PM"
            """;
        ThrowsAny<Exception>(() => Instance.ParseContent(content, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_2025_WithValidBuyAndSell_ReturnsEvents()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Sell,0.02,"EUR 62,671.63","EUR 1,253.43",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
            BTC,Buy,0.02,"EUR 62,654.96","EUR 1,253.10",EUR 12.41,"Mar 15, 2024, 12:05:32 PM"
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(2, events.Count);
        var sellEvent = events[0];
        Assert.AreEqual(new DateTime(2024, 3, 8, 21, 32, 39, DateTimeKind.Utc), sellEvent.Date);
        Assert.AreEqual(EventType.SellMarket, sellEvent.Type);
        Assert.AreEqual("CRYPTO", sellEvent.Ticker);
        Assert.AreEqual(0.02m, sellEvent.Quantity);
        Assert.IsNotNull(sellEvent.PricePerShareLocal);
        Assert.AreEqual(62671.63m, sellEvent.PricePerShareLocal.Value);
        Assert.AreEqual(1253.43m - 12.41m, sellEvent.TotalAmountLocal);
        Assert.IsNotNull(sellEvent.FeesLocal);
        Assert.AreEqual(12.41m, sellEvent.FeesLocal.Value);
        Assert.AreEqual("EUR", sellEvent.Currency);
        Assert.AreEqual(1m, sellEvent.FXRate);
        var buyEvent = events[1];
        Assert.AreEqual(new DateTime(2024, 3, 15, 12, 5, 32, DateTimeKind.Utc), buyEvent.Date);
        Assert.AreEqual(EventType.BuyMarket, buyEvent.Type);
        Assert.AreEqual("CRYPTO", buyEvent.Ticker);
        Assert.AreEqual(0.02m, buyEvent.Quantity);
        Assert.IsNotNull(buyEvent.PricePerShareLocal);
        Assert.AreEqual(62654.96m, buyEvent.PricePerShareLocal.Value);
        Assert.AreEqual(1253.10m + 12.41m, buyEvent.TotalAmountLocal);
        Assert.IsNotNull(buyEvent.FeesLocal);
        Assert.AreEqual(12.41m, buyEvent.FeesLocal.Value);
        Assert.AreEqual("EUR", buyEvent.Currency);
        Assert.AreEqual(1m, buyEvent.FXRate);
    }

    [TestMethod]
    public void ParseContent_2025_WithPre2025Header_RaisesException()
    {
        const string content = $"""
            {HeaderLinePre2025}
            BTC,Sell,0.02,"EUR 62,671.63","EUR 1,253.43",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
            """;
        ThrowsAny<Exception>(() => Instance.ParseContent(content, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_2025_WithEmptyLines_IgnoresThem()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Sell,0.02,"EUR 62,671.63","EUR 1,253.43",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"

            BTC,Buy,0.02,"EUR 62,654.96","EUR 1,253.10",EUR 12.41,"Mar 15, 2024, 12:05:32 PM"
            """;
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(2, events.Count);
    }

    [TestMethod]
    public void ParseContent_2025_WithInvalidType_RaisesException()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Invalid,0.02,"EUR 62,671.63","EUR 1,253.43",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
            """;
        ThrowsAny<Exception>(() => Instance.ParseContent(content, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_2025_WithRewardType_RaisesException()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Reward,0.02,"EUR 62,671.63","EUR 1,253.43",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
            """;
        ThrowsAny<Exception>(() => Instance.ParseContent(content, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_2025_WithDeltaAbovePrecision_RaisesException()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Sell,0.02,"EUR 62,671.63","EUR 1,254.44",EUR 12.411,"Mar 8, 2024, 9:32:39 PM"
            """;
        // 62,671.63 * 0.02 = 1,253.4326, abs(1,254.44 - 1,253.4326) > 0.01
        ThrowsAny<Exception>(() => Instance.ParseContent(content, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void ParseContent_2025_WithDeltaExactlyEqualsToPrecision_DoesNotRaiseException()
    {
        const string content = $"""
            {HeaderLine2025}
            BTC,Sell,0.02,"EUR 62,671.63","EUR 1,253.4426",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
            """;
        // 62,671.63 * 0.02 = 1,253.4326, abs(1,253.4426 - 1,253.4326) == 0.01
        var events = Instance.ParseContent(content, NoFxRates, Broker, NoOut);
        Assert.AreEqual(1, events.Count);
    }
}