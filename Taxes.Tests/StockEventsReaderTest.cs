namespace Taxes.Test;

[TestClass]
public class StockEventsReaderTests
{
    private static readonly Basics Basics = new() { BaseCurrency = "EUR" };
    
    private const string Broker = "THE BROKER";
    private static readonly FxRates NoFxRates = new(Basics, []);
    private static readonly FxRates OnlyUSDFxRates = new(Basics, new()
    {
        ["USD"] = new()
        {
            [(2022, 3, 30).ToUtc()] = 1.11m,
            [(2022, 5, 2).ToUtc()] = 1.06m,
            [(2022, 6, 2).ToUtc()] = 1.08m,
            [(2022, 6, 6).ToUtc()] = 1.08m,
            [(2022, 6, 10).ToUtc()] = 1.07m,
            [(2022, 6, 23).ToUtc()] = 1.06m,
            [(2022, 6, 29).ToUtc()] = 1.05m,
            [(2022, 7, 7).ToUtc()] = 1.03m,
            [(2022, 7, 19).ToUtc()] = 1.02m,
            [(2023, 1, 1).ToUtc()] = 1.059m,
            [(2023, 11, 26).ToUtc()] = 1.10m,
            [(2023, 12, 11).ToUtc()] = 1.08m,
            [(2023, 12, 18).ToUtc()] = 1.10m,
        }
    });
    private static readonly TextWriter NoOut = TextWriter.Null;

    private readonly StockEventsReader Instance = new(Basics);

    [TestMethod]
    public void Parse_WithTemporaryFile()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, 
                """
                Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
                2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"$3,000",USD,1.12
                """);

            var events = Instance.Parse(path, NoFxRates, Broker, NoOut);
            Assert.AreEqual(1, events.Count);
            events[0].AssertEvent(
                type: EventType.CashTopUp, totalAmountLocal: 3000, currency: "USD", fxRate: 1.12m);
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
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public void Parse_WithOnlyHeader_ReturnEmptyList()
    {
        using var textReader = new StringReader(
            "Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate");
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.AreEqual(0, events.Count);
    }

    [TestMethod]
    public void Parse_WithBlankLines_IgnoresThem()
    {
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"$3,000",USD,1.12
            
            2022-05-02T13:32:24.217636Z,TSLA,BUY - MARKET,1.018999,$861.63,$878,USD,01.06
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.AreEqual(2, events.Count);
    }

    [TestMethod]
    public void Parse_WithInvalidDateTime_RaisesException()
    {
        // Missing part of time
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-29T00,,CASH TOP-UP,,,"$3,000",USD,1.12
            """), NoFxRates, Broker, NoOut));
        // dd-MM-yyyy instead of yyyy-MM-dd
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            29-03-2022T00:00:00.000Z,,CASH TOP-UP,,,"$3,000",USD,1.12
            """), NoFxRates, Broker, NoOut));
        // 31 of April does not exist
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-04-31T00:00:00.000Z,,CASH TOP-UP,,,"$3,000",USD,1.12
            """), NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void Parse_SupportAllDateTimeFormats()
    {
        // Revolut old format
        using var textReader1 = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"$3,000",USD,1.12
            """);
        Assert.AreEqual(new(2022,03,30,23,48,44,882,381, DateTimeKind.Utc), Instance.Parse(textReader1, NoFxRates, Broker, NoOut)[0].Date);
        // Revolut new format
        using var textReader2 = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882Z,,CASH TOP-UP,,,"$3,000",USD,1.12
            """);
        Assert.AreEqual(new(2022,03,30,23,48,44,882, DateTimeKind.Utc), Instance.Parse(textReader2, NoFxRates, Broker, NoOut)[0].Date);
        // IBKR Trades format
        using var textReader3 = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            "2022-03-30, 23:48:44",,CASH TOP-UP,,,"$3,000",USD,1.12
            """);
        Assert.AreEqual(new(2022,03,30,23,48,44, DateTimeKind.Utc), Instance.Parse(textReader3, NoFxRates, Broker, NoOut)[0].Date);
        // IBKR Dividends and Withholding Tax format
        using var textReader4 = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30,,CASH TOP-UP,,,"$3,000",USD,1.12
            """);
        Assert.AreEqual(new(2022,03,30,0,0,0, DateTimeKind.Utc), Instance.Parse(textReader4, NoFxRates, Broker, NoOut)[0].Date);
    }

    [TestMethod]
    public void Parse_WithInvalidType_RaisesException()
    {
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,INVALID,,,"$3,000",USD,1.12
            """);
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(textReader, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void Parse_WithTotalAmountNull_RaisesException()
    {
        // With CASH TOP-UP: the Total Amount is significant as it's the amount of money added to the account
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,,USD,1.12
            """);
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(textReader, NoFxRates, Broker, NoOut));
        
        // With RESET: even though it's a synthetic event, it should have a Total Amount equal to 0
        using var textReader2 = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2023-01-01T00:00:00.000000Z,,RESET,,,,USD,1.0584
            """);
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(textReader2, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void Parse_WithPricePerShareNull()
    {
        // A cash top-up event has no price per share
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"$3,000",USD,1.12
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.IsNull(events[0].PricePerShareLocal);
    }

    [TestMethod]
    public void Parse_WithPricePerShareAndQuantityNonNull_AndNonBuyNorSell_RaisesException()
    {
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CUSTODY FEE,1,"$3,000","$3,000",USD,1.12
            """);
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(textReader, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void Parse_WithoutThousandsSeparatorOrDollarSymbol()
    {
        // Without thousands separator and with dollar symbol
        using var textReader1 = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"$3000",USD,1.12
            """);
        var events1 = Instance.Parse(textReader1, NoFxRates, Broker, NoOut);
        Assert.AreEqual(3000m, events1[0].TotalAmountLocal);
        // With thousands separator and without dollar symbol
        using var textReader2 = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"3,000",USD,1.12
            """);
        var events2 = Instance.Parse(textReader2, NoFxRates, Broker, NoOut);
        Assert.AreEqual(3000m, events2[0].TotalAmountLocal);
        // Without thousands separator nor dollar symbol
        using var textReader3 = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"3000",USD,1.12
            """);
        var events3 = Instance.Parse(textReader3, NoFxRates, Broker, NoOut);
        Assert.AreEqual(3000m, events3[0].TotalAmountLocal);
    }

    [TestMethod]
    public void Parse_WithCurrencySymbols()
    {
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"€3,000.00",EUR,1
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"£3,000.00",GBP,1.12
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"EUR 3,000.00",EUR,1
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"CHF 3,000.00",CHF,1.12
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"GBP 3,000.00",GBP,1.12
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"JPY 3,000.00",JPY,1.12
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"(EUR 3,000.00)",EUR,1
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.AreEqual(7, events.Count);
        foreach (var @event in events)
            Assert.AreEqual(3000m, @event.TotalAmountLocal);
    }

    [TestMethod]
    public void Parse_WithCurrency_NoFxRatesAvailableForTheCurrency_AndNonNegativeEventFxRate_UsesThatFxRate()
    {
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"3000",IDR,17153.90
            """);
        var fxRates = new FxRates(Basics, new()
        {
            ["USD"] = new() { [(2022, 03, 30).ToUtc()] = 1.1126m },
        });
        // A FX Rate is provided for the date of the event. However, it's not for the right currency.
        // So, the value provided in the input file should be used, that is expressed as Base/Local.
        var events = Instance.Parse(textReader, fxRates, Broker, NoOut);
        Assert.AreEqual("IDR", events[0].Currency);
        Assert.AreEqual(17153.90m, events[0].FXRate);
    }

    [TestMethod]
    public void Parse_WithCurrency_NoFxRatesAvailableForTheCurrency_AndNegativeEventFXRate_ThrowsException()
    {
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"3000",IDR,-1
            """);
        var fxRates = new FxRates(Basics, new()
        {
            ["USD"] = new() { [(2022, 03, 30).ToUtc()] = 1.1126m },
        });
        // A FX Rate is provided for the date of the event. However, it's not for the right currency.
        // Moreover, the value provided in the input file is negative, so an exception is raised, for the lack of a
        // valid FX rate.
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(textReader, fxRates, Broker, NoOut));
    }

    [TestMethod]
    public void Parse_WithCurrency_FxRatesAvailableForThatDate()
    {
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"3000",IDR,17153.90
            """);
        var fxRates = new FxRates(Basics, new()
        {
            ["IDR"] = new() { [(2022, 03, 30).ToUtc()] = 17250.00m },
        });
        // FX Rates are expressed in Base/Local, that is the same as in the input file. So, no need to invert them.
        // FX Rates provides a slightly different value for that currency that day, that is 17250m.
        // So, the value to be used is the one provided by fxRates.
        var events = Instance.Parse(textReader, fxRates, Broker, NoOut);
        Assert.AreEqual("IDR", events[0].Currency);
        Assert.AreEqual(17250m, events[0].FXRate);
    }

    [TestMethod]
    public void Parse_WithCurrency_FxRatesAvailableForTheCurrencyButNotThatDate()
    {
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"3000",IDR,17153.90
            """);
        var fxRates = new FxRates(Basics, new()
        {
            ["IDR"] = new() { [(2022, 03, 29).ToUtc()] = 17250.00m },
        });
        // A FX Rate is provided for the currency of the event. However, it's not for the right date.
        // So, the value provided in the input file should be used.
        var events = Instance.Parse(textReader, fxRates, Broker, NoOut);
        Assert.AreEqual("IDR", events[0].Currency);
        Assert.AreEqual(17153.90m, events[0].FXRate);
    }

    [TestMethod]
    public void Parse_WithMultipleCurrencies_PicksTheRightFxRate()
    {
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"3000",IDR,17153.90
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"3000",USD,1.12
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"3000",CHF,1.10
            """);
        var fxRates = new FxRates(Basics, new()
        {
            ["IDR"] = new() { [(2022, 03, 30).ToUtc()] = 17250.00m },
            ["USD"] = new() { [(2022, 03, 30).ToUtc()] = 1.09m }
        });
        // The first event is in IDR, so the FX Rate for that currency should be used.
        var events = Instance.Parse(textReader, fxRates, Broker, NoOut);
        Assert.AreEqual("IDR", events[0].Currency);
        Assert.AreEqual(17250.00m, events[0].FXRate); // Instead of 17153.90m
        // The second event is in USD, so the FX Rate for that currency should be used.
        Assert.AreEqual("USD", events[1].Currency);
        Assert.AreEqual(1.09m, events[1].FXRate); // Instead of 1.10m
        // The third event is in CHF, but there's no FX Rate for that currency, so the value provided in the input
        // file should be used.
        Assert.AreEqual("CHF", events[2].Currency);
        Assert.AreEqual(1.10m, events[2].FXRate);
    }

    [TestMethod]
    public void Parse_WithBaseCurrencyAndFxRateDifferentThanOne_RaisesException()
    {
        using var textReader = new StringReader(
            $"""
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"3000",{Instance.Basics.BaseCurrency},1.12
            """);
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(textReader, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void Parse_AllTypesOfEvents_Correctly()
    {
        using var textReader = new StringReader(
            """
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"$3,000",USD,1.12
            2022-05-02T13:32:24.217636Z,TSLA,BUY - MARKET,1.018999,$861.63,$878,USD,01.06
            2022-06-02T06:41:50.336664Z,,CUSTODY FEE,,,($1.35),USD,01.07
            2022-06-06T05:20:46.594417Z,AMZN,STOCK SPLIT,11.49072,,$0,USD,01.08
            2022-06-10T04:28:02.657456Z,MSFT,DIVIDEND,,,$10.54,USD,01.07
            2022-06-23T14:30:37.660417Z,CVNA,SELL - LIMIT,15,$26.62,$398.23,USD,01.06
            2022-06-29T13:45:38.161874Z,CVNA,BUY - LIMIT,10,$23.02,$231.25,USD,01.05
            2022-07-07T15:37:02.604183Z,QCOM,SELL - MARKET,8,$132.29,"$1,055.65",USD,01.02
            2022-07-19T06:01:55.845058Z,GSK,STOCK SPLIT,-1.8,,$0,USD,01.02
            2023-01-01T00:00:00.000000Z,,RESET,,,$0,USD,1.0584
            2023-11-26T21:54:34.023429Z,,CASH WITHDRAWAL,,,"($3,500)",USD,1.0968
            2023-12-11T14:34:06.497Z,PFE,BUY - LIMIT,17,$28.50 ,$485.71 ,USD,1.0765
            2023-12-18T14:37:36.664Z,ORCL,BUY - MARKET,20,$104.24 ,"$2,084.90 ",USD,1.0947
            2023-12-20,INTEREST_IBKR,INTEREST,,,"1,234.56 ",CHF,0.98
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
    
        Assert.AreEqual(14, events.Count);
        events[0].AssertEvent(
            type: EventType.CashTopUp, totalAmountLocal: 3000, currency: "USD", fxRate: 1.12m);
        events[1].AssertEvent(
            type: EventType.BuyMarket, ticker: "TSLA", quantity: 1.018999m, pricePerShareLocal: 861.63m, 
            totalAmountLocal: 878, currency: "USD", fxRate: 1.06m);
        events[2].AssertEvent(
            type: EventType.CustodyFee, totalAmountLocal: 1.35m, currency: "USD", fxRate: 1.07m);
        events[3].AssertEvent(
            type: EventType.StockSplit, ticker: "AMZN", quantity: 11.49072m, currency: "USD", fxRate: 1.08m);
        events[4].AssertEvent(
            type: EventType.Dividend, ticker: "MSFT", totalAmountLocal: 10.54m, currency: "USD", fxRate: 1.07m);
        events[5].AssertEvent(
            type: EventType.SellLimit, ticker: "CVNA", quantity: 15, pricePerShareLocal: 26.62m, 
            totalAmountLocal: 398.23m, currency: "USD", fxRate: 1.06m);
        events[6].AssertEvent(
            type: EventType.BuyLimit, ticker: "CVNA", quantity: 10, pricePerShareLocal: 23.02m, 
            totalAmountLocal: 231.25m, currency: "USD", fxRate: 1.05m);
        events[7].AssertEvent(
            type: EventType.SellMarket, ticker: "QCOM", quantity: 8, pricePerShareLocal: 132.29m, 
            totalAmountLocal: 1055.65m, currency: "USD", fxRate: 1.02m);
        events[8].AssertEvent(
            type: EventType.StockSplit, ticker: "GSK", quantity: -1.8m, currency: "USD", fxRate: 1.02m);
        events[9].AssertEvent(
            type: EventType.Reset, currency: "USD", fxRate: 1.0584m);
        events[10].AssertEvent(
            type: EventType.CashWithdrawal, totalAmountLocal: 3500, currency: "USD", fxRate: 1.0968m);
        events[11].AssertEvent(
            type: EventType.BuyLimit, ticker: "PFE", quantity: 17, pricePerShareLocal: 28.50m, 
            totalAmountLocal: 485.71m, currency: "USD", fxRate: 1.0765m);
        events[12].AssertEvent(
            type: EventType.BuyMarket, ticker: "ORCL", quantity: 20, pricePerShareLocal: 104.24m, 
            totalAmountLocal: 2084.90m, currency: "USD", fxRate: 1.0947m);
        events[13].AssertEvent(
            type: EventType.Interest, totalAmountLocal: 1234.56m, currency: "CHF", fxRate: 0.98m);
    }

    [TestMethod]
    public void Parse_FeesLocal_AutocalculatedOrNullDependingOnEventType()
    {
        using var textReader = new StringReader("""
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"$3,000",USD,1.12
            2022-05-02T13:32:24.217636Z,TSLA,BUY - MARKET,1.018999,$861.63,$878,USD,01.06
            2022-06-02T06:41:50.336664Z,,CUSTODY FEE,,,($1.35),USD,01.07
            2022-06-06T05:20:46.594417Z,AMZN,STOCK SPLIT,11.49072,,$0,USD,01.08
            2022-06-23T14:30:37.660417Z,CVNA,SELL - LIMIT,15,$26.62,$398.23,USD,01.06
            2022-06-29T13:45:38.161874Z,CVNA,BUY - LIMIT,10,$23.02,$231.25,USD,01.05
            2022-06-10T04:28:02.657456Z,MSFT,DIVIDEND,,,$10.54,USD,01.07
            2023-12-20,INTEREST_IBKR,INTEREST,,,"1,234.56 ",CHF,0.98
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.IsNull(events[0].FeesLocal);
        Assert.IsNotNull(events[1].FeesLocal);
        Assert.IsNull(events[2].FeesLocal);
        Assert.IsNull(events[3].FeesLocal);
        Assert.IsNotNull(events[4].FeesLocal);
        Assert.IsNotNull(events[5].FeesLocal);
        Assert.IsNull(events[6].FeesLocal);
        Assert.IsNull(events[7].FeesLocal);
    }

    [TestMethod]
    public void Parse_FeesLocal_AutocalculatedForBuy()
    {
        // FeesLocal = TotalAmountLocal - Quantity * PricePerShareLocal
        using var textReader = new StringReader("""
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-12-28T20:12:29.182442Z,AAPL,BUY - MARKET,5,$126.21,$632.62,USD,01.07
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.IsNotNull(events[0].FeesLocal);
        events[0].AssertEvent(feesLocal: 1.57m); // 632.62 - 5 * 126.21
    }

    [TestMethod]
    public void Parse_FeesLocal_AutocalculatedForSell()
    {
        // FeesLocal =  Quantity * PricePerShareLocal - TotalAmountLocal
        using var textReader = new StringReader("""
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2023-03-23T14:01:01.396Z,AAPL,SELL - LIMIT,5,$159.92 ,$797.58 ,USD,1.0897
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.IsNotNull(events[0].FeesLocal);
        events[0].AssertEvent(feesLocal: 2.02m); // 5 * 159.92 - 797.58
    }

    [TestMethod]
    public void Parse_FeesLocal_NotAvailableForDividends()
    {
        using var textReader = new StringReader("""
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-06-10T04:28:02.657456Z,MSFT,DIVIDEND,,,$10.54,USD,01.07
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.IsNull(events[0].FeesLocal);
    }

    [TestMethod]
    public void Parse_FeesLocal_NotAvailableForInterests()
    {
        using var textReader = new StringReader("""
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2023-12-20,INTEREST_IBKR,INTEREST,,,"1,234.56 ",CHF,0.98
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.IsNull(events[0].FeesLocal);
    }

    [TestMethod]
    public void Parse_FeesLocal_NotAvailableForStockSplits()
    {
        using var textReader = new StringReader("""
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-06-06T05:20:46.594417Z,AMZN,STOCK SPLIT,11.49072,,$0,USD,01.08
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.IsNull(events[0].FeesLocal);
    }

    [TestMethod]
    public void Parse_TakesIntoAccountProvidedFxRate_WhenAvailable()
    {
        using var textReader = new StringReader("""
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-03-29T00:00:00.000Z,,CASH TOP-UP,,,"$3,000",USD,1.12
            2022-03-30T00:00:00.000Z,,CASH TOP-UP,,,"$3,000",USD,1.12
            2022-05-02T00:00:00.000Z,TSLA,BUY - MARKET,1.018999,$861.63,$878,USD,01.06
            2022-06-02T00:00:00.000Z,,CUSTODY FEE,,,($1.35),USD,01.07
            """);
        var events = Instance.Parse(textReader, OnlyUSDFxRates, Broker, NoOut);
        events[0].AssertEvent(
            date: (2022, 3, 29).ToUtc(), type: EventType.CashTopUp, totalAmountLocal: 3000, currency: "USD", fxRate: 1.12m);
        events[1].AssertEvent(
            date: (2022, 3, 30).ToUtc(), type: EventType.CashTopUp, totalAmountLocal: 3000, currency: "USD", fxRate: 1.11m);
        events[2].AssertEvent(
            date: (2022, 5, 2).ToUtc(), type: EventType.BuyMarket, ticker: "TSLA", quantity: 1.018999m, 
            pricePerShareLocal: 861.63m, totalAmountLocal: 878, currency: "USD", fxRate: 1.06m);
        events[3].AssertEvent(
            date: (2022, 6, 2).ToUtc(), type: EventType.CustodyFee, totalAmountLocal: 1.35m, currency: "USD", fxRate: 1.08m);
    }

    [TestMethod]
    public void Parse_CustodyFee_ReturnsPositiveTotalAmount_EvenThoughItsNegativeInTheInput()
    {
        // The total amount is negative in the input file, but it should be positive in the Event
        using var textReader = new StringReader("""
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-06-02T06:41:50.336664Z,,CUSTODY FEE,,,($1.35),USD,01.07
            """);
        var events = Instance.Parse(textReader, NoFxRates, Broker, NoOut);
        Assert.AreEqual(1, events.Count);
        events[0].AssertEvent(type: EventType.CustodyFee, totalAmountLocal: 1.35m, currency: "USD", fxRate: 1.07m);
    }

    [TestMethod]
    public void Parse_WithoutHeader_RaisesException()
    {
        using var textReader = new StringReader("""
            2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,""$3,000"",USD,1.12
            """);
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(textReader, NoFxRates, Broker, NoOut));
    }

    [TestMethod]
    public void Parse_WithInvalidFields_RaisesExceptio()
    {
        using var textReader = new StringReader("""
            Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
            2022-05-02T13:32:24.217636Z,TSLA,BUY - MARKET,1.018999,$861.63,$878,USD,01.06
            2022-06-02T06:41:50.336664Z,,CUSTODY FEE,,,($1.35),USD,01.07
            2022-06-06T05:20:46.594417Z,AMZN,STOCK SPLIT,11.49072,,$0,USD,01.08,NONEXISTANT
            """);
        AssertExtensions.ThrowsAny<Exception>(() => Instance.Parse(textReader, NoFxRates, Broker, NoOut));
    }
}
