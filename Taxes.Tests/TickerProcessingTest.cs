namespace Taxes.Test;

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

    private readonly TickerProcessing Instance = new(new());

    [TestMethod]
    public void ProcessTicker_NoEvents()
    {
        Instance.ProcessTicker(Ticker, []).AssertZeroExceptFor();
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit()
    {
        // Quantity, PricePerShareLocal, TotalAmountLocal, FeesLocal
        List<Event> e = [new(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1)];
        Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 3, totalAmountBase: 303, portfolioAcquisitionValueBase: 303);
        e.Add(new(T0 + D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1, -1));
        Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 5, totalAmountBase: 525, portfolioAcquisitionValueBase: 525);
        e.Add(new(T0 + 2 * D, BuyLimit, Ticker, 1, 90, 91, 1, EUR, 1, -1));
        Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 6, totalAmountBase: 616, portfolioAcquisitionValueBase: 616);
        e.Add(new(T0 + 2 * D, BuyLimit, Ticker, 1, 90, 92, 2, EUR, 1, -1));
        Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 7, totalAmountBase: 708, portfolioAcquisitionValueBase: 708);
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit_DifferentCurrency_ConvertsToBase()
    {
        List<Event> e = [new(T0, BuyLimit, Ticker, 3, 100, 303, 3, USD, 1.2m, -1)];
        Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 3, totalAmountBase: 303 / 1.2m, portfolioAcquisitionValueBase: 303 / 1.2m);
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit_NonPositiveQuantity_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ProcessTicker(Ticker, [new(T0, BuyLimit, Ticker, -3, 100, 303, 3, EUR, 1, -1)]));
        ThrowsAny<Exception>(() => Instance.ProcessTicker(Ticker, [new(T0, BuyLimit, Ticker, 0, 100, 303, 3, EUR, 1, -1)]));
    }

    [TestMethod]
    public void ProcessTicker_BuyMarket_Fees()
    {
        // Assert fees after 2022-12-28T20:12:29.182442Z,AAPL,BUY - MARKET,5,$126.21,$632.62,EUR,1
        List<Event> e = [new(T0, BuyMarket, Ticker, 5, 126.21m, 632.62m, 1.58m, EUR, 1, -1)];
        // TODO: continue
    }

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellWithoutBuying() => 
        Instance.ProcessTicker(Ticker, [new(T0, SellLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1)]);

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellMoreThanBuying() =>
         Instance.ProcessTicker(Ticker, [
             new(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1,  -1),
             new(T0, SellLimit, Ticker, 4, 100, 404, 4, EUR, 1, -1)]);

    // TODO: fix
    // [TestMethod]
    // public void ProcessTicker_SellExactQuantityBought_GeneratingPlusValue()
    // {
    //     List<Event> e = [
    //         new(T0, BuyLimit, Ticker, 
    //             Quantity: 3, PricePerShareLocal: 100, TotalAmountLocal: 303, FeesLocal: 3, 
    //             EUR, 1, -1),
    //         new(T0 + D, SellLimit, Ticker, 
    //             Quantity: 3, PricePerShareLocal: 110, TotalAmountLocal: 330, FeesLocal: 3, 
    //             EUR, 1, -1)];
    //     Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
    //         plusValueCumpBase: 27, plusValuePepsBase: 27, plusValueCryptoBase: 0);
    // }

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
        var tickerStateAfterTopUp = Instance.ProcessTicker(Ticker, [tickerEvent], TextWriter.Null);
        Assert.AreEqual(tickerState, tickerStateAfterTopUp);
    }

    [TestMethod]
    public void ProcessReset_KeepsTickerAndIsin()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(Ticker, tickerStateAfterReset.Ticker);
        Assert.AreEqual(Isin, tickerStateAfterReset.Isin);
    }

    [TestMethod]
    public void ProcessReset_WhenTickerEventAndIndexAreInconsistent_RaisesException()
    {
        var cashWithdrawalEvent = new Event(T0, CashWithdrawal, Ticker, null, null, 100, null, EUR, 1, -1);
        var resetEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessReset(resetEvent, [cashWithdrawalEvent, resetEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessReset_WhenPassingNotSupportedType_RaisesException()
    {
        var tickerEvent = new Event(T0, CustodyFee, Ticker, 0, 0, 0, 12.0m, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessReset_WhenPassingANonZeroTotalAmount_RaisesException()
    {
        var tickerState = new TickerState(Ticker, Isin);
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, TotalAmountLocal: 303, null, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessReset_WhenPassingTransactionRelatedInfo_RaisesException()
    {
        var tickerState = new TickerState(Ticker, Isin);
        var tickerEvent = new Event(T0, Reset, Ticker, Quantity: 4, null, 0m, null, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
        tickerEvent = new Event(T0, Reset, Ticker, null, PricePerShareLocal: 100, 0m, null, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
        tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, FeesLocal: 3, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessReset_PreservesTotalQuantityAndAmountBase()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(3, tickerStateAfterReset.TotalQuantity);
        Assert.AreEqual(5.5m, tickerStateAfterReset.TotalAmountBase);
    }

    [TestMethod]
    public void ProcessReset_PreservesPepsIndexes()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, PepsCurrentIndex: 3, PepsCurrentIndexSoldQuantity: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(3, tickerStateAfterReset.PepsCurrentIndex);
        Assert.AreEqual(5.5m, tickerStateAfterReset.PepsCurrentIndexSoldQuantity);
    }

    [TestMethod]
    public void ProcessReset_PreservesPortfolioAcquisitionValueBaseAndCryptoFractionOfInitialCapital()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
            PortfolioAcquisitionValueBase: 5.5m, CryptoFractionOfInitialCapital: 0.75m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(5.5m, tickerStateAfterReset.PortfolioAcquisitionValueBase);
        Assert.AreEqual(0.75m, tickerStateAfterReset.CryptoFractionOfInitialCapital);
    }

    [TestMethod]
    public void ProcessReset_ResetsPlusValues()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
            PlusValueCumpBase: 5.5m, PlusValuePepsBase: 5.5m, PlusValueCryptoBase: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(0, tickerStateAfterReset.PlusValueCumpBase);
        Assert.AreEqual(0, tickerStateAfterReset.PlusValuePepsBase);
        Assert.AreEqual(0, tickerStateAfterReset.PlusValueCryptoBase);
    }

    [TestMethod]
    public void ProcessReset_ResetsMinusValues()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
                       MinusValueCumpBase: 5.5m, MinusValuePepsBase: 5.5m, MinusValueCryptoBase: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(0, tickerStateAfterReset.MinusValueCumpBase);
        Assert.AreEqual(0, tickerStateAfterReset.MinusValuePepsBase);
        Assert.AreEqual(0, tickerStateAfterReset.MinusValueCryptoBase);
    }

    [TestMethod]
    public void ProcessReset_ResetsDividends()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
                       NetDividendsBase: 5.5m, WhtDividendsBase: 5.5m, GrossDividendsBase: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(0, tickerStateAfterReset.NetDividendsBase);
        Assert.AreEqual(0, tickerStateAfterReset.WhtDividendsBase);
        Assert.AreEqual(0, tickerStateAfterReset.GrossDividendsBase);
    }

    [TestMethod]
    public void ProcessNoop_LeavesTickerStateUnchanged()
    {
        var tickerEvent = new Event(T0, CashTopUp, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
            PlusValueCumpBase: 5.5m, PlusValuePepsBase: 5.6m, PlusValueCryptoBase: 13.22m,
            MinusValueCumpBase: 3.2m, MinusValuePepsBase: 0m, MinusValueCryptoBase: 3.9m,
            TotalQuantity: 3, TotalAmountBase: 25.46m,
            NetDividendsBase: 4.5m, WhtDividendsBase: 0.2m, GrossDividendsBase: 4.7m,
            PepsCurrentIndex: 3, PepsCurrentIndexSoldQuantity: 2.1m,
            PortfolioAcquisitionValueBase: 23.3m, CryptoFractionOfInitialCapital: 0.75m);
        var tickerStateAfterNoop = Instance.ProcessNoop(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(tickerState, tickerStateAfterNoop);
    }

    [TestMethod]
    public void ProcessNoop_WhenTickerEventAndIndexAreInconsistent_RaisesException()
    {
        var cashWithdrawalEvent = new Event(T0, CashWithdrawal, Ticker, null, null, 100, null, EUR, 1, -1);
        var cashTopUpEvent = new Event(T0, CashTopUp, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessNoop(cashTopUpEvent, [cashWithdrawalEvent, cashTopUpEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenTickerEventAndIndexAreInconsistent_RaisesException()
    {
        var cashWithdrawalEvent = new Event(T0, CashWithdrawal, Ticker, null, null, 100, null, EUR, 1, -1);
        var buyLimitEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buyLimitEvent, [cashWithdrawalEvent, buyLimitEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenPassingNotSupportedType_RaisesException()
    {
        var tickerEvent = new Event(T0, CustodyFee, Ticker, null, null, 12.0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenTickerNameIsNull_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, null, 3, 100, 303, 3, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenPricePerShareLocalIsNull_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, null, 303, 3, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenQuantityIsNull_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, Ticker, null, 100, 303, 3, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenTotalAmountLocalIsNonPositive_RaisesException()
    {
        // Buying 3 shares at 100 EUR, with fees of 3 EUR => Total Amount Local of 0 EUR
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 0m, 3, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));

        // Buying 3 shares at 100 EUR, with fees of 3 EUR => Total Amount Local of -1 EUR
        tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, -1m, 3, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenFeesLocalIsNull_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenCurrenciesDontMatch_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, USD, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 0, 0, 0, 0, EUR, 1, -1), tickerEvent };
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, tickerEvents, 1, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenTickersDontMatch_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1);
        var tickerState = new TickerState(AnotherTicker, AnotherIsin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenFeesDontMatch_RaisesException()
    {
        var tickerState = new TickerState(Ticker, Isin);
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 2, EUR, 1, -1); // Fees should be 3
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_IncreasesTotalQuantityByTheQuantityInTheEvent()
    {
        var tickerState = new TickerState(Ticker, Isin);
        // First buy of 3 shares
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1);
        var tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(3, tickerStateAfterBuy.TotalQuantity);
        // Second buy of 2 shares
        tickerEvent = new Event(T0 + D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerStateAfterBuy, TextWriter.Null);
        Assert.AreEqual(5, tickerStateAfterBuy.TotalQuantity);
        // Third buy of 2.5 shares
        tickerEvent = new Event(T0 + 2 * D, BuyLimit, Ticker, 2.5m, 90, 227.5m, 2.5m, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerStateAfterBuy, TextWriter.Null);
        Assert.AreEqual(7.5m, tickerStateAfterBuy.TotalQuantity);
    }

    [TestMethod]
    public void ProcessBuy_IncreasesTotalAmountBase_BySharesAmountPlusFees()
    {
        var tickerState = new TickerState(Ticker, Isin);
        // First buy of 3 shares at 100 EUR, with fees of 3 EUR => Total Amount Local of 303 EUR
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1);
        var tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(303, tickerStateAfterBuy.TotalAmountBase);
        // Second buy of 2 shares at 110 EUR, with fees of 2 EUR => Total Amount Local of 222 EUR
        tickerEvent = new Event(T0 + D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerStateAfterBuy, TextWriter.Null);
        Assert.AreEqual(525, tickerStateAfterBuy.TotalAmountBase);
        // Third buy of 2.5 shares at 90 EUR, with fees of 2.5 EUR => Total Amount Local of 227.5 EUR
        tickerEvent = new Event(T0 + 2 * D, BuyLimit, Ticker, 2.5m, 90, 227.5m, 2.5m, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerStateAfterBuy, TextWriter.Null);
        Assert.AreEqual(752.5m, tickerStateAfterBuy.TotalAmountBase);
    }

    [TestMethod]
    public void ProcessBuy_CalculatesAndPrintsSteps()
    {
        var tickerProcessing = new TickerProcessing(new Basics() { Rounding = x => decimal.Round(x, 2) });
        var writer = new StringWriter();
        var localCurrency = USD; // FX Rate is 2 USD for 1 EUR
        var initialState = new TickerState(Ticker, Isin);

        // First buy 3 shares at 100.10002 USD, with fees of 3.20003 USD => Total Amount Local of 300.30006 USD + 3.20003 USD = 303.50009 USD
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100.10002m, 303.50009m, 3.20003m, localCurrency, 2m, -1);
        tickerProcessing.ProcessBuy(tickerEvent, [tickerEvent], 0, initialState, writer);
        var output = writer.ToString();

        // Prints Total Buy Price in local currency as rounded value
        Assert.IsTrue(output.Contains($"Total Buy Price ({localCurrency}) = 303.50")); // Given by the event
        // Prints Total Buy Price in base currency as rounded value
        Assert.IsTrue(output.Contains($"Total Buy Price ({Instance.Basics.BaseCurrency}) = 151.75")); // ~ (303.50 USD) / (2 USD/EUR)
        // Prints Shares Buy Price in local currency as rounded value
        Assert.IsTrue(output.Contains($"Shares Buy Price ({localCurrency}) = 300.30")); // ~ (3 shares) * (100.10 USD/share)
        // Prints Shares Buy Price in base currency as rounded value
        Assert.IsTrue(output.Contains($"Shares Buy Price ({Instance.Basics.BaseCurrency}) = 150.15")); // ~ (300.30 USD) / (2 USD/EUR)
        // Prints PerShare Buy Price in local currency as rounded value
        Assert.IsTrue(output.Contains($"PerShare Buy Price ({localCurrency}) = 100.10")); // Given by the event
        // Prints PerShare Buy Price in base currency as rounded value
        Assert.IsTrue(output.Contains($"PerShare Buy Price ({Instance.Basics.BaseCurrency}) = 50.05")); // ~ (100.10 USD/share) / (2 USD/EUR)
        // Prints Buy Fees in local currency as rounded value
        Assert.IsTrue(output.Contains($"Buy Fees ({localCurrency}) = 3.20")); // Given by the event
        // Prints Buy Fees in base currency as rounded value
        Assert.IsTrue(output.Contains($"Buy Fees ({Instance.Basics.BaseCurrency}) = 1.60")); // ~ (3.20 USD) / (2 USD/EUR)
    }

    [TestMethod]
    public void ProcessSell_WhenTickerEventAndIndexAreInconsistent_RaisesException()
    {
        var cashWithdrawalEvent = new Event(T0, CashWithdrawal, Ticker, null, null, 100, null, EUR, 1, -1);
        var sellLimitEvent = new Event(T0, SellLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sellLimitEvent, [cashWithdrawalEvent, sellLimitEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenPassingNotSupportedType_RaisesException()
    {
        // Custody fees for 12 EUR
        var tickerEvent = new Event(T0, CustodyFee, Ticker, 0, 0, 0, 12.0m, EUR, 1, -1);
        // While owning no shares
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenTickerNameIsNull_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, null, 1, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenTotalAmountLocalIsNonPositive_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 0 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 0m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));

        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of -1 EUR
        tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, -1m, 0.2m, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenFeesLocalIsNull_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of NULL EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, null, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenTickersMismatch_RaisesException()
    {
        // Selling 1 share of A at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares of B for a total of 5.5 EUR
        var tickerState = new TickerState(AnotherTicker, AnotherIsin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenPricePerShareLocalIsNull_RaisesException()
    {
        // Selling 1 share at NULL, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, null, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenQuantityIsNull_RaisesException()
    {
        // Selling NULL shares at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, null, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null));

    }

    [TestMethod]
    public void ProcessSell_WhenCurrenciesAreEtherogenous_RaisesException()
    {
        // Selling 1 share at 2.0 USD, with fees of 0.2 USD => Total Amount Local of 1.8 USD
        var sellEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, USD, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 0, 0, 0, 0, EUR, 1, -1), sellEvent };
        ThrowsAny<Exception>(() => Instance.ProcessSell(sellEvent, tickerEvents, 1, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenSellingMoreThanOwned_RaisesException()
    {
        // Selling 4 shares at 2.0 EUR, with fees of 0.8 EUR => Total Amount Local of 7.2 EUR
        var sellEvent = new Event(T0, SellLimit, Ticker, 4, 8.0m, 7.2m, 0.8m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 3, 0, 0, 0, EUR, 1, -1), sellEvent };
        ThrowsAny<Exception>(() => Instance.ProcessSell(sellEvent, tickerEvents, 1, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenFeesAreInconsistent_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.4 EUR => Total Amount Local of 1.7 EUR (fees should be 0.3 EUR)
        var sellEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.7m, 0.4m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 3, 2, 5.5m, 0.5m, EUR, 1, -1), sellEvent };
        ThrowsAny<Exception>(() => Instance.ProcessSell(sellEvent, tickerEvents, 1, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenPastBuyEventDoesntHaveQuantity_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var sellEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, null, 2, 5.5m, 0.5m, EUR, 1, -1), sellEvent };
        ThrowsAny<Exception>(() => Instance.ProcessSell(sellEvent, tickerEvents, 1, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_CalculatesAndPrintsSteps()
    {
        var tickerProcessing = new TickerProcessing(new Basics() { Rounding = x => decimal.Round(x, 2) });
        var localCurrency = USD; // FX rate between USD and EUR stays stable at 4 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin);
        
        // First buy 3 shares at 100.10002 USD, with fees of 3.20003 USD => Total Amount Local of 303.50009 USD
        var buyEvent = new Event(T0, BuyLimit, Ticker, 3, 100.10002m, 303.50009m, 3.20003m, localCurrency, 4m, -1);
        var tickerStateAfterBuy = tickerProcessing.ProcessBuy(buyEvent, [buyEvent], 0, initialState, new StringWriter());
        // Then sell 2 shares at 150.15003 USD, with fees of 2.50005 USD => Total Amount Local of 300.30006 USD - 2.50005 USD = 297.80001 USD
        var sellEvent = new Event(T0 + D, SellLimit, Ticker, 2, 150.15003m, 297.80001m, 2.50005m, localCurrency, 4m, -1);
        var writer = new StringWriter();
        var tickerStateAfterSell = tickerProcessing.ProcessSell(sellEvent, [buyEvent, sellEvent], 1, tickerStateAfterBuy, writer);
        var output = writer.ToString();

        // Prints Total Sell Price in local currency as rounded value
        Assert.IsTrue(output.Contains($"Total Sell Price ({localCurrency}) = 297.80")); // Given by the event
        // Prints Total Sell Price in base currency as rounded value
        Assert.IsTrue(output.Contains($"Shares Sell Price ({localCurrency}) = 300.30")); // ~ (2 shares) * (150.15 USD/share)
        // Prints PerShare Average Buy Price in base currency as rounded value
        // All shares have been bought at once at ~ 100.10 USD/share with fees of 3.20 USD = ~ (303.50 USD) / (3 shares) / (4 USD/EUR) in base currency
        Assert.IsTrue(output.Contains($"PerShare Average Buy Price ({Instance.Basics.BaseCurrency}) = 25.29"));
        // Prints Total Average Buy Price in base currency as rounded value (relevant for CUMP)
        Assert.IsTrue(output.Contains($"Total Average Buy Price ({Instance.Basics.BaseCurrency}) = 50.58")); // ~ (25.29 USD/share) * (2 shares)

        // Prints PerShare Sell Price in base currency as rounded value
        Assert.IsTrue(output.Contains($"PerShare Sell Price ({Instance.Basics.BaseCurrency}) = 37.54")); // ~ (150.15 USD/share) / (4 USD/EUR)
        // Prints Shares Sell Price in local currency as rounded value
        Assert.IsTrue(output.Contains($"Shares Sell Price ({Instance.Basics.BaseCurrency}) = 75.08")); // ~ (300.30 USD) / (4 USD/EUR)
        // Prints Shares Sell Price in base currency as rounded value
        // Lower than Shares Sell Price because of the fees
        Assert.IsTrue(output.Contains($"Total Sell Price ({Instance.Basics.BaseCurrency}) = 74.45")); // ~ (297.80 USD) / (4 USD/EUR)

        // Prints Sell Fees in local currency as rounded value
        Assert.IsTrue(output.Contains($"Sell Fees ({Instance.Basics.BaseCurrency}) = 0.63")); // ~ (2.50 USD) / (4 USD/EUR)
    }

    [TestMethod]
    public void ProcessBuyAndSell_UpdateTickerStateCorrectly()
    {
        var tickerProcessing = new TickerProcessing(new Basics() { Rounding = x => decimal.Round(x, 2) });
        var localCurrency = USD; // FX rate between USD and EUR stays stable at 4 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin);

        // -------
        // First buy 3 shares at 100 USD, with fees of 3.20 USD => Total Amount Local of 300 USD + 3.20 USD = 303.20 USD
        var buyEvent1 = new Event(T0, BuyLimit, Ticker, 3, 100m, 303.20m, 3.20m, localCurrency, 4m, -1);
        var tickerStateAfterBuy1 = tickerProcessing.ProcessBuy(
            buyEvent1, [buyEvent1], 0, initialState, new StringWriter());
        
        // 3 shares available
        Assert.AreEqual(3, tickerStateAfterBuy1.TotalQuantity);
        // The total amount corresponds to the total amount of the buy event 1
        var totalAmountBaseAfterBuy1 = tickerStateAfterBuy1.TotalAmountBase;
        Assert.AreEqual(303.20m / 4m, totalAmountBaseAfterBuy1);

        // -------
        // Then sell 2 shares at 150 USD, with fees of 2.50 USD => Total Amount Local of 300 USD - 2.50 USD = 297.50 USD
        var sellEvent1 = new Event(T0 + D, SellLimit, Ticker, 2, 150m, 297.50m, 2.50m, localCurrency, 4m, -1);
        var tickerStateAfterSell1 = tickerProcessing.ProcessSell(
            sellEvent1, [buyEvent1, sellEvent1], 1, tickerStateAfterBuy1, new StringWriter());
        
        // Only 1 share left
        Assert.AreEqual(1, tickerStateAfterSell1.TotalQuantity);
        // The share left has the same average buy price as before
        var totalAmountBaseAfterSell1 = totalAmountBaseAfterBuy1 / 3;
        Assert.AreEqual(totalAmountBaseAfterSell1, tickerStateAfterSell1.TotalAmountBase, Instance.Basics.Precision);
        
        // The plus value CUMP is the difference between the sell price and the average buy price for the two shares sold
        // The minus value CUMP is 0, since no minus value has been realized
        var averageBuyPriceTwoShares = totalAmountBaseAfterSell1 * 2;
        var plusValueCumpSell1 = 297.50m / 4m - averageBuyPriceTwoShares;
        Assert.AreEqual(plusValueCumpSell1, tickerStateAfterSell1.PlusValueCumpBase, Instance.Basics.Precision);
        Assert.AreEqual(0, tickerStateAfterSell1.MinusValueCumpBase);
        // The plus value PEPS is the difference between the sell price and the buy price of the first two shares bought
        // The minus value PEPS is 0, since no minus value has been realized
        var buyPriceFirstTwoShares = (2 * 303.20m / 3) / 4m;
        var plusValuePepsSell1 = 297.50m / 4m - buyPriceFirstTwoShares;
        Assert.AreEqual(plusValuePepsSell1, tickerStateAfterSell1.PlusValuePepsBase, Instance.Basics.Precision);
        Assert.AreEqual(0, tickerStateAfterSell1.MinusValuePepsBase);

        // TODO: calculate the plus value and minus value crypto

        // The PEPS current index is 0, since only 2 shares out of 3 have been sold
        Assert.AreEqual(0, tickerStateAfterSell1.PepsCurrentIndex);
        // The PEPS current index sold quantity is 2, since 2 shares out of 3 have been sold
        Assert.AreEqual(2, tickerStateAfterSell1.PepsCurrentIndexSoldQuantity);

        // -------
        // Buy 3 shares at 110 USD, with fees of 4 USD => Total Amount Local of 330 USD + 4 USD = 334 USD
        var buyEvent2 = new Event(T0 + 2 * D, BuyLimit, Ticker, 3, 110m, 334m, 4m, localCurrency, 4m, -1);
        var tickerStateAfterBuy2 = tickerProcessing.ProcessBuy(
            buyEvent2, [buyEvent1, sellEvent1, buyEvent2], 2, tickerStateAfterSell1, new StringWriter());

        // The total quantity is increased by 3
        Assert.AreEqual(4, tickerStateAfterBuy2.TotalQuantity);
        // The total amount is increased by the total amount of the buy event 2
        var totalAmountBaseAfterBuy2 = totalAmountBaseAfterSell1 + 334m / 4m;
        Assert.AreEqual(totalAmountBaseAfterBuy2, tickerStateAfterBuy2.TotalAmountBase);

        // -------
        // Buy 2 shares at 120 USD, with fees of 3 USD => Total Amount Local of 240 USD + 3 USD = 243 USD
        var buyEvent3 = new Event(T0 + 3 * D, BuyLimit, Ticker, 2, 120m, 243m, 3m, localCurrency, 4m, -1);
        var tickerStateAfterBuy3 = tickerProcessing.ProcessBuy(
            buyEvent3, [buyEvent1, sellEvent1, buyEvent2, buyEvent3], 3, tickerStateAfterBuy2, new StringWriter());

        // The total quantity is increased by 2
        Assert.AreEqual(6, tickerStateAfterBuy3.TotalQuantity);
        // The total amount is increased by the total amount of the buy event 3
        var totalAmountBaseAfterBuy3 = totalAmountBaseAfterBuy2 + 243m / 4m;
        Assert.AreEqual(totalAmountBaseAfterBuy3, tickerStateAfterBuy3.TotalAmountBase);

        // -------
        // Sell 3 shares at 130 USD, with fees of 3 USD => Total Amount Local of 390 USD - 3 USD = 387 USD
        var sellEvent2 = new Event(T0 + 4 * D, SellLimit, Ticker, 3, 130m, 387m, 3m, localCurrency, 4m, -1);
        var tickerStateAfterSell2 = tickerProcessing.ProcessSell(
            sellEvent2, [buyEvent1, sellEvent1, buyEvent2, buyEvent3, sellEvent2], 4, tickerStateAfterBuy3, new StringWriter());

        // Only 3 shares left
        Assert.AreEqual(3, tickerStateAfterSell2.TotalQuantity);
        // The total amount is decreased by half (3 shares out of 6 sold), and not by the total amount of the sell event 2
        var totalAmountBaseAfterSell2 = totalAmountBaseAfterBuy3 * (3m / 6m);
        Assert.AreEqual(totalAmountBaseAfterSell2, tickerStateAfterSell2.TotalAmountBase);
        
        // The plus value CUMP for the event is the difference between the sell price and the average buy price for the three shares sold
        // This value is added to the plus value CUMP accumulated so far (first sell event)
        // The minus value CUMP is 0, since no minus value has been realized
        var averageBuyPriceThreeShares = totalAmountBaseAfterBuy3 - totalAmountBaseAfterSell2;
        var plusValueCumpSell2 = 387m / 4m - averageBuyPriceThreeShares;
        Assert.AreEqual(plusValueCumpSell1 + plusValueCumpSell2, tickerStateAfterSell2.PlusValueCumpBase, Instance.Basics.Precision);
        Assert.AreEqual(0, tickerStateAfterSell2.MinusValueCumpBase);
        
        // The plus value PEPS for the event is the difference between the sell price and the buy price of the oldest 3 shares bought,
        // among the 6 shares left after the third buy event:
        // - oldest share bought at 303.20 USD / 3 shares (local currency)
        // - next 3 shares bought at 334 USD / 3 shares (local currency)
        // - last 2 shares bought at 243 USD / 2 shares (local currency)
        // This value is added to the plus value PEPS accumulated so far (first sell event)
        // The minus value PEPS is 0, since no minus value has been realized
        var buyPriceFirstThreeShares = 1 * 303.20m / 3m / 4m + 2 * 334m / 3m / 4m;
        var plusValuePepsSell2 = 387m / 4m - buyPriceFirstThreeShares;
        Assert.AreEqual(plusValuePepsSell1 + plusValuePepsSell2, tickerStateAfterSell2.PlusValuePepsBase, Instance.Basics.Precision);
        Assert.AreEqual(0, tickerStateAfterSell2.MinusValuePepsBase);

        // TODO: calculate the plus value and minus value crypto

        // The PEPS current index is 2, since the only remaining share of the first buy has been sold, together with
        // the first two shares of the second buy, where the last share is left not sold for now
        Assert.AreEqual(2, tickerStateAfterSell2.PepsCurrentIndex);
        // The PEPS current index sold quantity is 2, since 2 shares out of 3 have been sold
        Assert.AreEqual(2, tickerStateAfterSell2.PepsCurrentIndexSoldQuantity);

        // -------
        // Sell 1 shares at 10 USD, with fees of 1 USD => Total Amount Local of 10 USD - 1 USD = 9 USD
        var sellEvent3 = new Event(T0 + 5 * D, SellLimit, Ticker, 1, 10m, 9m, 1m, localCurrency, 4m, -1);
        var tickerStateAfterSell3 = tickerProcessing.ProcessSell(
            sellEvent3, [buyEvent1, sellEvent1, buyEvent2, buyEvent3, sellEvent2, sellEvent3], 5, tickerStateAfterSell2, new StringWriter());

        // Only 2 shares left
        Assert.AreEqual(2, tickerStateAfterSell3.TotalQuantity);
        // The total amount is decreased to 2/3 (1 share out of 3 sold), and not by the total amount of the sell event 3
        var totalAmountBaseAfterSell3 = totalAmountBaseAfterSell2 * (2m / 3m);
        Assert.AreEqual(totalAmountBaseAfterSell3, tickerStateAfterSell3.TotalAmountBase, Instance.Basics.Precision);
        
        // The plus value CUMP for the event is the difference between the sell price and the average buy price for the one share sold
        // This value is negative, so it is actually a minus value, taken with reversed sign
        // This value is the first CUMP minus value realized. The plus value CUMP doesn't change.
        var averageBuyPriceOneShare = totalAmountBaseAfterSell2 - totalAmountBaseAfterSell3;
        var minusValueCumpSell3 = averageBuyPriceOneShare - 9m / 4m;
        Assert.AreEqual(tickerStateAfterSell3.PlusValueCumpBase, tickerStateAfterSell2.PlusValueCumpBase);
        Assert.AreEqual(minusValueCumpSell3, tickerStateAfterSell3.MinusValueCumpBase, Instance.Basics.Precision);
        
        // The plus value PEPS for the event is the difference between the sell price and the buy price of the oldest share bought,
        // among the 3 shares left after the third sell event:
        // - oldest share bought at 334 USD / 3 shares (local currency)
        // - last 2 shares bought at 243 USD / 2 shares (local currency)
        // This value is negative, so it is actually a minus value, taken with reversed sign.
        // This value is the first PEPS minus value realized. The plus value PEPS doesn't change.
        var buyPriceOldestShare = 334m / 3m / 4m;
        var minusValuePepsSell3 = buyPriceOldestShare - 9m / 4m;
        Assert.AreEqual(tickerStateAfterSell3.PlusValuePepsBase, tickerStateAfterSell2.PlusValuePepsBase);
        Assert.AreEqual(minusValuePepsSell3, tickerStateAfterSell3.MinusValuePepsBase, Instance.Basics.Precision);

        // TODO: calculate the plus value and minus value crypto

        // The PEPS current index is 3, since the last share of the second buy has been sold, and the pointer moves
        // one position forward
        Assert.AreEqual(3, tickerStateAfterSell3.PepsCurrentIndex);
        // The PEPS current index sold quantity is 3, since the current index has been moved forward
        Assert.AreEqual(0, tickerStateAfterSell3.PepsCurrentIndexSoldQuantity);

        // -------
        // Sell 2 shares at 10 USD, with fees of 1 USD => Total Amount Local of 20 USD - 1 USD = 19 USD
        var sellEvent4 = new Event(T0 + 6 * D, SellLimit, Ticker, 2, 10m, 19m, 1m, localCurrency, 4m, -1);
        var tickerStateAfterSell4 = tickerProcessing.ProcessSell(
            sellEvent4, [buyEvent1, sellEvent1, buyEvent2, buyEvent3, sellEvent2, sellEvent3, sellEvent4], 6, 
            tickerStateAfterSell3, new StringWriter());

        // No shares left
        Assert.AreEqual(0, tickerStateAfterSell4.TotalQuantity);
        // The total amount is decreased to 0 (all share sold), and not by the total amount of the sell event 4
        var totalAmountBaseAfterSell4 = 0;
        Assert.AreEqual(totalAmountBaseAfterSell4, tickerStateAfterSell4.TotalAmountBase, Instance.Basics.Precision);

        // The plus value CUMP for the event is the difference between the sell price and the average buy price for the
        // two shares sold
        // This value is negative, so it is actually a minus value, taken with reversed sign
        // This value is the second CUMP minus value realized. The plus value CUMP doesn't change.
        var averageBuyPriceTwoSharesSell4 = totalAmountBaseAfterSell3 - totalAmountBaseAfterSell4;
        var minusValueCumpSell4 = averageBuyPriceTwoSharesSell4 - 19m / 4m;
        Assert.AreEqual(tickerStateAfterSell4.PlusValueCumpBase, tickerStateAfterSell3.PlusValueCumpBase);
        Assert.AreEqual(minusValueCumpSell3 + minusValueCumpSell4, tickerStateAfterSell4.MinusValueCumpBase, Instance.Basics.Precision);

        // The plus value PEPS for the event is the difference between the sell price and the buy price of the two
        // oldest share bought, that are the last two shares remaining
        // This value is negative, so it is actually a minus value, taken with reversed sign
        // This value is the second PEPS minus value realized. The plus value PEPS doesn't change.
        var buyPriceTwoOldestShares = 243m / 4m;
        var minusValuePepsSell4 = buyPriceTwoOldestShares - 19m / 4m;
        Assert.AreEqual(tickerStateAfterSell4.PlusValuePepsBase, tickerStateAfterSell3.PlusValuePepsBase);
        Assert.AreEqual(minusValuePepsSell3 + minusValuePepsSell4, tickerStateAfterSell4.MinusValuePepsBase, Instance.Basics.Precision);
    }

}
