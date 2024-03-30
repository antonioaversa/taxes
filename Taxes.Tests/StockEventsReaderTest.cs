﻿namespace Taxes.Test;

[TestClass]
public class StockEventsReaderTest
{
    private static readonly Dictionary<DateTime, decimal> NoFxRates = new() { };
    private static readonly Dictionary<DateTime, decimal> FxRates = new()
    {
        [new(2022, 3, 30)] = 1.11m,
        [new(2022, 5, 2)] = 1.06m,
        [new(2022, 6, 2)] = 1.08m,
        [new(2022, 6, 6)] = 1.08m,
        [new(2022, 6, 10)] = 1.07m,
        [new(2022, 6, 23)] = 1.06m,
        [new(2022, 6, 29)] = 1.05m,
        [new(2022, 7, 7)] = 1.03m,
        [new(2022, 7, 19)] = 1.02m,
        [new(2023, 1, 1)] = 1.059m,
        [new(2023, 11, 26)] = 1.10m,
        [new(2023, 12, 11)] = 1.08m,
        [new(2023, 12, 18)] = 1.10m,
    };

    [TestMethod]
    public void Parse_AllTypesOfEvents_Correctly()
    {
        using var textReader = new StringReader("""
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
            2023-01-01T00:00:00.000000Z,,RESET,,,,USD,1.0584
            2023-11-26T21:54:34.023429Z,,CASH WITHDRAWAL,,,"($3,500)",USD,1.0968
            2023-12-11T14:34:06.497Z,PFE,BUY - LIMIT,17,$28.50 ,$485.71 ,USD,1.0765
            2023-12-18T14:37:36.664Z,ORCL,BUY - MARKET,20,$104.24 ,"$2,084.90 ",USD,1.0947
            """);
        var events = StockEventsReader.Parse(textReader, NoFxRates);
    
        // Assert all data read correctly for all events
        Assert.AreEqual(13, events.Count);
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
    }
}

static class EventExtensions
{
    public static void AssertEvent(
        this Event @event, 
        DateTime? date = null, 
        EventType? type = null, 
        string? ticker = null, 
        decimal? quantity = null, 
        decimal? pricePerShareLocal = null, 
        decimal? totalAmountLocal = null, 
        decimal? feesLocal = null, 
        string? currency = null, 
        decimal? fxRate = null, 
        decimal? portfolioCurrentValueBase = null)
    {
        if (date is not null)
            Assert.AreEqual(date, @event.Date);
        if (type is not null)
            Assert.AreEqual(type, @event.Type);
        if (ticker is not null)
            Assert.AreEqual(ticker, @event.Ticker);
        if (quantity is not null)
            Assert.AreEqual(quantity, @event.Quantity);
        if (pricePerShareLocal is not null)
            Assert.AreEqual(pricePerShareLocal, @event.PricePerShareLocal);
        if (totalAmountLocal is not null)
            Assert.AreEqual(totalAmountLocal, @event.TotalAmountLocal);
        if (feesLocal is not null)
            Assert.AreEqual(feesLocal, @event.FeesLocal);
        if (currency is not null)
            Assert.AreEqual(currency, @event.Currency);
        if (fxRate is not null)
            Assert.AreEqual(fxRate, @event.FXRate);
        if (portfolioCurrentValueBase is not null)
            Assert.AreEqual(portfolioCurrentValueBase, @event.PortfolioCurrentValueBase);
    }
}