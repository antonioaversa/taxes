namespace Taxes.Test;

using static TickerProcessing;
using static EventType;
using static AssertExtensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TickerProcessingTest
{
    private const string EUR = "EUR";
    private const string USD = "USD";
    private const string Ticker = "AAPL";
    private const string Isin = "US0378331005";
    private static readonly DateTime T0 = new(2022, 01, 01, 00, 00, 00);
    private static readonly TimeSpan D = TimeSpan.FromDays(1);

    [TestMethod]
    public void ProcessTicker_NoEvents()
    {
        ProcessTicker(Ticker, []).AssertZeroExceptFor();
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit()
    {
        // Quantity, PricePerShareLocal, TotalAmountLocal, FeesLocal
        List<Event> e = [new(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1)];
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 3, totalAmountBase: 303, portfolioAcquisitionValueBase: 303);
        e.Add(new(T0 + D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1, -1));
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 5, totalAmountBase: 525, portfolioAcquisitionValueBase: 525);
        e.Add(new(T0 + 2 * D, BuyLimit, Ticker, 1, 90, 91, 1, EUR, 1, -1));
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 6, totalAmountBase: 616, portfolioAcquisitionValueBase: 616);
        e.Add(new(T0 + 2 * D, BuyLimit, Ticker, 1, 90, 92, 1, EUR, 1, -1));
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 7, totalAmountBase: 708, portfolioAcquisitionValueBase: 708);
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit_DifferentCurrency_ConvertsToBase()
    {
        List<Event> e = [new(T0, BuyLimit, Ticker, 3, 100, 303, 3, USD, 1.2m, -1)];
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 3, totalAmountBase: 303 * 1.2m, portfolioAcquisitionValueBase: 303 * 1.2m);
    }

    [TestMethod]
    public void ProcessTicker_BuyMarket_Fees()
    {
        // Assert fees after 2022-12-28T20:12:29.182442Z,AAPL,BUY - MARKET,5,$126.21,$632.62,EUR,1
        List<Event> e = [new(T0, BuyMarket, Ticker, 5, 126.21m, 632.62m, 1.58m, EUR, 1, -1)];
        // TODO: continue
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit_NonPositiveQuantity_RaisesException()
    {
        ThrowsAny<Exception>(() => ProcessTicker(Ticker, [new(T0, BuyLimit, Ticker, -3, 100, 303, 3, EUR, 1, -1)]));
        ThrowsAny<Exception>(() => ProcessTicker(Ticker, [new(T0, BuyLimit, Ticker, 0, 100, 303, 3, EUR, 1, -1)]));
    }

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellWithoutBuying() => 
        ProcessTicker(Ticker, [new(T0, SellLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1)]);

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellMoreThanBuying() =>
         ProcessTicker(Ticker, [
             new(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1,  -1),
             new(T0, SellLimit, Ticker, 4, 100, 404, 4, EUR, 1, -1)]);

    [TestMethod]
    public void ProcessTicker_SellExactQuantityBought_GeneratingPlusValue()
    {
        List<Event> e = [
            new(T0, BuyLimit, Ticker, 
                Quantity: 3, PricePerShareLocal: 100, TotalAmountLocal: 303, FeesLocal: 3, 
                EUR, 1, -1),
            new(T0 + D, SellLimit, Ticker, 
                Quantity: 3, PricePerShareLocal: 110, TotalAmountLocal: 330, FeesLocal: 3, 
                EUR, 1, -1)];
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            plusValueCumpBase: 27, plusValuePepsBase: 27, plusValueCryptoBase: 0);
    }

    [TestMethod]
    public void ProcessSell_WhenPassingNotSupportedType_RaisesException()
    {
        // Custody fees for 12 EUR
        var tickerEvent = new Event(T0, CustodyFee, Ticker, 0, 0, 0, 12.0m, EUR, 1, -1);
        // While owning no shares
        var tickerState = new TickerState(Ticker, Isin, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenTickerNameIsNull_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, null, 1, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, 0, 0, 0, 0, 0, 0, 3, 5.5m, 0);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenPricePerShareLocalIsNull_RaisesException()
    {
        // Selling 1 share at NULL, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, null, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, 0, 0, 0, 0, 0, 0, 3, 5.5m, 0);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenQuantityIsNull_RaisesException()
    {
        // Selling NULL shares at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, null, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, 0, 0, 0, 0, 0, 0, 3, 5.5m, 0);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));

    }

    [TestMethod]
    public void ProcessSell_WhenCurrenciesAreEtherogenous_RaisesException()
    {
        // Selling 1 share at 2.0 USD, with fees of 0.2 USD => Total Amount Local of 1.8 USD
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, USD, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, 0, 0, 0, 0, 0, 0, 3, 5.5m, 0);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 0, 0, 0, 0, EUR, 1, -1) };
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, tickerEvents, 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenSellingMoreThanOwned_RaisesException()
    {
        // Selling 4 shares at 2.0 EUR, with fees of 0.8 EUR => Total Amount Local of 7.2 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 4, 8.0m, 7.2m, 0.8m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, 0, 0, 0, 0, 0, 0, 3, 5.5m, 0);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 3, 0, 0, 0, EUR, 1, -1) };
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, tickerEvents, 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_PassingCustomTextWrites_WritesOnTotalSellPriceOnThatTextWriter()
    {
        // Selling 3 shares at 2.1 EUR, with fees of 0.3 EUR => Total Amount Local of 6.0 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 3, 2.1m, 6.0m, 0.3m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, 0, 0, 0, 0, 0, 0, 3, 5.5m, 0);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 3, 0, 0, 0, EUR, 1, -1) };
        var outWriter = new StringWriter();
        ProcessSell(tickerEvent, tickerEvents, 0, tickerState, outWriter);
        var output = outWriter.ToString();
        Assert.IsTrue(output.Contains($"Total Sell Price ({Basics.BaseCurrency}) = 6.0"));
    }

}
