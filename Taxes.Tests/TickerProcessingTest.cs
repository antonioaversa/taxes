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
    private const string AnotherTicker = "GOOGL";
    private const string AnotherIsin = "US02079K3059";
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

    /////////////////////////////////////////////////////////////////////////////////////////

    [DataTestMethod]
    [DataRow(CashTopUp)]
    [DataRow(CashWithdrawal)]
    [DataRow(CustodyFee)]
    [DataRow(CustodyChange)]
    public void ProcessTicker_Event_DoesntChangeTickerState(EventType eventType)
    {
        var tickerState = new TickerState(Ticker, Isin);
        var tickerEvent = new Event(T0, eventType, Ticker, null, null, 100, null, EUR, 1, -1);
        var tickerStateAfterTopUp = ProcessTicker(Ticker, [tickerEvent], TextWriter.Null);
        Assert.AreEqual(tickerState, tickerStateAfterTopUp);
    }

    [TestMethod]
    public void ProcessReset_KeepsTickerAndIsin()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, null, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerStateAfterReset = ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(Ticker, tickerStateAfterReset.Ticker);
        Assert.AreEqual(Isin, tickerStateAfterReset.Isin);
    }

    [TestMethod]
    public void ProcessReset_WhenPassingNotSupportedType_RaisesException()
    {
        var tickerEvent = new Event(T0, CustodyFee, Ticker, 0, 0, 0, 12.0m, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessReset_WhenPassingTransactionRelatedInfo_RaisesException()
    {
        var tickerState = new TickerState(Ticker, Isin);
        var tickerEvent = new Event(T0, Reset, Ticker, Quantity: 4, null, null, null, EUR, 1, -1);
        ThrowsAny<Exception>(() => ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null));
        tickerEvent = new Event(T0, Reset, Ticker, null, PricePerShareLocal: 100, null, null, EUR, 1, -1);
        ThrowsAny<Exception>(() => ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null));
        tickerEvent = new Event(T0, Reset, Ticker, null, null, TotalAmountLocal: 303, null, EUR, 1, -1);
        ThrowsAny<Exception>(() => ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null));
        tickerEvent = new Event(T0, Reset, Ticker, null, null, null, FeesLocal: 3, EUR, 1, -1);
        ThrowsAny<Exception>(() => ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessReset_PreservesTotalQuantityAndAmountBase()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, null, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerStateAfterReset = ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(3, tickerStateAfterReset.TotalQuantity);
        Assert.AreEqual(5.5m, tickerStateAfterReset.TotalAmountBase);
    }

    [TestMethod]
    public void ProcessReset_PreservesPepsIndexes()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, null, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, PepsCurrentIndex: 3, PepsCurrentIndexBoughtQuantity: 5.5m);
        var tickerStateAfterReset = ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(3, tickerStateAfterReset.PepsCurrentIndex);
        Assert.AreEqual(5.5m, tickerStateAfterReset.PepsCurrentIndexBoughtQuantity);
    }

    [TestMethod]
    public void ProcessReset_PreservesPortfolioAcquisitionValueBaseAndCryptoFractionOfInitialCapital()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, null, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
            PortfolioAcquisitionValueBase: 5.5m, CryptoFractionOfInitialCapital: 0.75m);
        var tickerStateAfterReset = ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(5.5m, tickerStateAfterReset.PortfolioAcquisitionValueBase);
        Assert.AreEqual(0.75m, tickerStateAfterReset.CryptoFractionOfInitialCapital);
    }

    [TestMethod]
    public void ProcessReset_ResetsPlusValues()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, null, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
            PlusValueCumpBase: 5.5m, PlusValuePepsBase: 5.5m, PlusValueCryptoBase: 5.5m);
        var tickerStateAfterReset = ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(0, tickerStateAfterReset.PlusValueCumpBase);
        Assert.AreEqual(0, tickerStateAfterReset.PlusValuePepsBase);
        Assert.AreEqual(0, tickerStateAfterReset.PlusValueCryptoBase);
    }

    [TestMethod]
    public void ProcessReset_ResetsMinusValues()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, null, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
                       MinusValueCumpBase: 5.5m, MinusValuePepsBase: 5.5m, MinusValueCryptoBase: 5.5m);
        var tickerStateAfterReset = ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(0, tickerStateAfterReset.MinusValueCumpBase);
        Assert.AreEqual(0, tickerStateAfterReset.MinusValuePepsBase);
        Assert.AreEqual(0, tickerStateAfterReset.MinusValueCryptoBase);
    }

    [TestMethod]
    public void ProcessReset_ResetsDividends()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, null, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
                       NetDividendsBase: 5.5m, WhtDividendsBase: 5.5m, GrossDividendsBase: 5.5m);
        var tickerStateAfterReset = ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(0, tickerStateAfterReset.NetDividendsBase);
        Assert.AreEqual(0, tickerStateAfterReset.WhtDividendsBase);
        Assert.AreEqual(0, tickerStateAfterReset.GrossDividendsBase);
    }

    [TestMethod]
    public void ProcessNoop_LeavesTickerStateUnchanged()
    {
        var tickerEvent = new Event(T0, CashTopUp, Ticker, null, null, null, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
            PlusValueCumpBase: 5.5m, PlusValuePepsBase: 5.6m, PlusValueCryptoBase: 13.22m,
            MinusValueCumpBase: 3.2m, MinusValuePepsBase: 0m, MinusValueCryptoBase: 3.9m,
            TotalQuantity: 3, TotalAmountBase: 25.46m,
            NetDividendsBase: 4.5m, WhtDividendsBase: 0.2m, GrossDividendsBase: 4.7m,
            PepsCurrentIndex: 3, PepsCurrentIndexBoughtQuantity: 2.1m,
            PortfolioAcquisitionValueBase: 23.3m, CryptoFractionOfInitialCapital: 0.75m);
        var tickerStateAfterNoop = ProcessNoop(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(tickerState, tickerStateAfterNoop);
    }

    [TestMethod]
    public void ProcessSell_WhenPassingNotSupportedType_RaisesException()
    {
        // Custody fees for 12 EUR
        var tickerEvent = new Event(T0, CustodyFee, Ticker, 0, 0, 0, 12.0m, EUR, 1, -1);
        // While owning no shares
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenTickerNameIsNull_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, null, 1, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenTotalAmountLocalIsNull_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of NULL EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, null, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenFeesLocalIsNull_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of NULL EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, null, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenTickersMismatch_RaisesException()
    {
        // Selling 1 share of A at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares of B for a total of 5.5 EUR
        var tickerState = new TickerState(AnotherTicker, AnotherIsin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenPricePerShareLocalIsNull_RaisesException()
    {
        // Selling 1 share at NULL, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, null, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenQuantityIsNull_RaisesException()
    {
        // Selling NULL shares at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, null, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));

    }

    [TestMethod]
    public void ProcessSell_WhenCurrenciesAreEtherogenous_RaisesException()
    {
        // Selling 1 share at 2.0 USD, with fees of 0.2 USD => Total Amount Local of 1.8 USD
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, USD, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 0, 0, 0, 0, EUR, 1, -1) };
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, tickerEvents, 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenSellingMoreThanOwned_RaisesException()
    {
        // Selling 4 shares at 2.0 EUR, with fees of 0.8 EUR => Total Amount Local of 7.2 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 4, 8.0m, 7.2m, 0.8m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 3, 0, 0, 0, EUR, 1, -1) };
        ThrowsAny<Exception>(() => ProcessSell(tickerEvent, tickerEvents, 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_PassingCustomTextWrites_WritesOnTotalSellPriceOnThatTextWriter()
    {
        // Selling 3 shares at 2.1 EUR, with fees of 0.3 EUR => Total Amount Local of 6.0 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 3, 2.1m, 6.0m, 0.3m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 3, 0, 0, 0, EUR, 1, -1) };
        var outWriter = new StringWriter();
        ProcessSell(tickerEvent, tickerEvents, 0, tickerState, outWriter);
        var output = outWriter.ToString();
        Assert.IsTrue(output.Contains($"Total Sell Price ({Basics.BaseCurrency}) = 6.0"));
    }

}
