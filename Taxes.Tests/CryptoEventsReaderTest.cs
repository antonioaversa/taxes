using System.Globalization;
using static Taxes.Test.AssertExtensions;

namespace Taxes.Tests;

[TestClass]
public class CryptoEventsReaderTest
{
    private const string Broker = "THE BROKER";
    private const string HeaderLine = "Type,Product,Started Date,Completed Date,Description,Amount,Currency,Fiat amount,Fiat amount (inc. fees),Fee,Base currency,State,Balance";

    private readonly CryptoEventsReader Instance = new(new());
    private readonly TextWriter NoOut = TextWriter.Null;

    [TestInitialize]
    public void TestInitialize()
    {
        // Set the culture to InvariantCulture since some assertions are on logged text, and
        // those logs are done via a simple interpolated string, so they are culture-specific.
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; 
    }
    
    [TestMethod]
    public void Parse_WithTemporaryFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"""
                {HeaderLine}
                EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
                EXCHANGE,Current,2022-06-27 10:32:23,2022-06-27 10:32:23,Exchanged to EUR,-1000.0000000000,ZRX,-324.0898000000,-319.2298000000,4.8600000000,EUR,COMPLETED,0.0000000000
                """);

            var events = Instance.Parse(path, Broker, NoOut);
            Assert.AreEqual(2, events.Count);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void Parse_WithEmptyContent_ReturnEmptyList()
    {
        using var textReader = new StringReader(string.Empty);
        var events = Instance.Parse(textReader, Broker, NoOut);
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public void Parse_WithOnlyHeader_ReturnEmptyList()
    {
        using var textReader = new StringReader(
            HeaderLine);
        var events = Instance.Parse(textReader, Broker, NoOut);
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public void Parse_WithBlankLines_IgnoresThem()
    {
        using var textReader = new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000

            EXCHANGE,Current,2022-06-27 10:32:23,2022-06-27 10:32:23,Exchanged to EUR,-1000.0000000000,ZRX,-324.0898000000,-319.2298000000,4.8600000000,EUR,COMPLETED,0.0000000000            
            """);
        var events = Instance.Parse(textReader, Broker, NoOut);
        Assert.AreEqual(2, events.Count);
    }

    [TestMethod]
    public void Parse_WithInvalidType_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            INVALID,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut));
    }

    [TestMethod]
    public void Parse_WithTransferType_IgnoresTheRecord()
    {
        using var textReader = new StringReader($"""
            {HeaderLine}
            TRANSFER,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """);
        var events = Instance.Parse(textReader, Broker, NoOut);
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public void Parse_WithRewardType_IgnoresTheRecord() // TODO: to be fixed
    {
        using var textReader = new StringReader($"""
            {HeaderLine}
            REWARD,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """);
        var events = Instance.Parse(textReader, Broker, NoOut);
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public void Parse_WithNonCurrentProduct_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,NonCurrent,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut));
    }

    [TestMethod]
    public void Parse_WithInvalidDateTime_RaisesException()
    {
        // Missing part of time in Started Date
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut));
        // Missing part of time in Completed Date
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut));
        // dd-MM-yyyy instead of yyyy-MM-dd in Started Date
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,25-06-2022 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut));
        // dd-MM-yyyy instead of yyyy-MM-dd in Completed Date
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,25-06-2022 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut));
        // 31 of April does not exist in Started Date
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-04-31 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut));
        // 31 of April does not exist in Completed Date
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-04-31 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut));
    }

    [DataTestMethod]
    [DataRow("2022-06-25 13:29:03.123", DisplayName = "Milliseconds not supported")]
    [DataRow("2022-06-25", DisplayName = "Short datetime format not supported")]
    [DataRow("2022-06-25 13:29:03+02:00", DisplayName = "Timezone not supported")]
    [DataRow("2022-06-25T13:29:03", DisplayName = "T between date and time not supported")]
    public void Parse_WithUnsupportedDateTimeFormats(string startedDate)
    {
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,{startedDate},2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut).ToList());
    }

    [TestMethod]
    public void Parse_WithStartedDateDifferentThanCompletedDate_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-24 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut).ToList());
    }

    [TestMethod]
    public void Parse_WithStateDifferentThanCompleted_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,INVALID,1000.0000000000
            """), Broker, NoOut).ToList());
    }

    [TestMethod]
    public void Parse_WithRecordBaseCurrencyDifferentThanBasicsBaseCurrency_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,USD,COMPLETED,1000.0000000000
            """), Broker, NoOut).ToList());
    }

    [TestMethod]
    public void Parse_WithReset_ReturnsResetEvent()
    {
        using var textReader = new StringReader($"""
            {HeaderLine}
            RESET,,2023-01-01 00:00:00,2023-01-01 00:00:00,,,,,,,EUR,,
            """);
        var events = Instance.Parse(textReader, Broker, NoOut);
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
    public void Parse_WithExchange_ReturnsEventsWithCorrectProperties()
    {
        using var textReader = new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            EXCHANGE,Current,2022-06-27 10:32:23,2022-06-27 10:32:23,Exchanged to EUR,-1000.0000000000,ZRX,-324.0898000000,-319.2298000000,4.8600000000,EUR,COMPLETED,0.0000000000
            """);
        var events = Instance.Parse(textReader, Broker, NoOut);
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
    public void Parse_WithZeroQuantity_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,0.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,0.0000000000
            """), Broker, NoOut).ToList());
    }

    [TestMethod]
    public void Parse_WithAmountAndFiatAmountHavingDifferentSigns_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,-1000.0000000000,ZRX,293.9067439000,-298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut).ToList());
    }
    
    [TestMethod]
    public void Parse_WithFiatAmountsHavingDifferentSigns_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,-298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut).ToList());
    }

    [TestMethod]
    public void Parse_WithNegativeFees_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,-4.4100000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut).ToList());
    }

    [TestMethod]
    public void Parse_WithZeroFees_IsOk()
    {
        var events = Instance.Parse(new StringReader($"""
            {HeaderLine}
            EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,0.0000000000,EUR,COMPLETED,1000.0000000000
            """), Broker, NoOut);
        Assert.AreEqual(1, events.Count);

    }
}
