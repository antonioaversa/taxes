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
        AssertEq(3, tickerStateAfterReset.TotalQuantity);
        AssertEq(5.5m, tickerStateAfterReset.TotalAmountBase);
    }

    [TestMethod]
    public void ProcessReset_PreservesPepsIndexes()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, PepsCurrentIndex: 3, PepsCurrentIndexSoldQuantity: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        AssertEq(3, tickerStateAfterReset.PepsCurrentIndex);
        AssertEq(5.5m, tickerStateAfterReset.PepsCurrentIndexSoldQuantity);
    }

    [TestMethod]
    public void ProcessReset_PreservesPortfolioAcquisitionValueBaseAndCryptoFractionOfInitialCapital()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
            PortfolioAcquisitionValueBase: 5.5m, CryptoFractionOfInitialCapital: 0.75m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        AssertEq(5.5m, tickerStateAfterReset.PortfolioAcquisitionValueBase);
        AssertEq(0.75m, tickerStateAfterReset.CryptoFractionOfInitialCapital);
    }

    [TestMethod]
    public void ProcessReset_ResetsPlusValues()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
            PlusValueCumpBase: 5.5m, PlusValuePepsBase: 5.5m, PlusValueCryptoBase: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        AssertEq(0, tickerStateAfterReset.PlusValueCumpBase);
        AssertEq(0, tickerStateAfterReset.PlusValuePepsBase);
        AssertEq(0, tickerStateAfterReset.PlusValueCryptoBase);
    }

    [TestMethod]
    public void ProcessReset_ResetsMinusValues()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
                       MinusValueCumpBase: 5.5m, MinusValuePepsBase: 5.5m, MinusValueCryptoBase: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        AssertEq(0, tickerStateAfterReset.MinusValueCumpBase);
        AssertEq(0, tickerStateAfterReset.MinusValuePepsBase);
        AssertEq(0, tickerStateAfterReset.MinusValueCryptoBase);
    }

    [TestMethod]
    public void ProcessReset_ResetsDividends()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
                       NetDividendsBase: 5.5m, WhtDividendsBase: 5.5m, GrossDividendsBase: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        AssertEq(0, tickerStateAfterReset.NetDividendsBase);
        AssertEq(0, tickerStateAfterReset.WhtDividendsBase);
        AssertEq(0, tickerStateAfterReset.GrossDividendsBase);
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
        AssertEq(3, tickerStateAfterBuy.TotalQuantity);
        // Second buy of 2 shares
        tickerEvent = new Event(T0 + D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerStateAfterBuy, TextWriter.Null);
        AssertEq(5, tickerStateAfterBuy.TotalQuantity);
        // Third buy of 2.5 shares
        tickerEvent = new Event(T0 + 2 * D, BuyLimit, Ticker, 2.5m, 90, 227.5m, 2.5m, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerStateAfterBuy, TextWriter.Null);
        AssertEq(7.5m, tickerStateAfterBuy.TotalQuantity);
    }

    [TestMethod]
    public void ProcessBuy_IncreasesTotalAmountBase_BySharesAmountPlusFees()
    {
        var tickerState = new TickerState(Ticker, Isin);
        // First buy of 3 shares at 100 EUR, with fees of 3 EUR => Total Amount Local of 303 EUR
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1);
        var tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerState, TextWriter.Null);
        AssertEq(303, tickerStateAfterBuy.TotalAmountBase);
        // Second buy of 2 shares at 110 EUR, with fees of 2 EUR => Total Amount Local of 222 EUR
        tickerEvent = new Event(T0 + D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerStateAfterBuy, TextWriter.Null);
        AssertEq(525, tickerStateAfterBuy.TotalAmountBase);
        // Third buy of 2.5 shares at 90 EUR, with fees of 2.5 EUR => Total Amount Local of 227.5 EUR
        tickerEvent = new Event(T0 + 2 * D, BuyLimit, Ticker, 2.5m, 90, 227.5m, 2.5m, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [tickerEvent], 0, tickerStateAfterBuy, TextWriter.Null);
        AssertEq(752.5m, tickerStateAfterBuy.TotalAmountBase);
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
        var tickerStateAfterBuy = tickerProcessing.ProcessBuy(buyEvent, [buyEvent], 0, initialState, TextWriter.Null);
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
        var tickerProcessing = Instance;
        var localCurrency = USD; // FX rate between USD and EUR stays stable at 4 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin);

        // -------
        // First buy 3 shares at 100 USD, with fees of 3.20 USD => Total Amount Local of 300 USD + 3.20 USD = 303.20 USD
        var buyEvent1 = new Event(T0, BuyLimit, Ticker, 3, 100m, 303.20m, 3.20m, localCurrency, 4m, -1);
        var tickerStateAfterBuy1 = tickerProcessing.ProcessBuy(
            buyEvent1, [buyEvent1], 0, initialState, TextWriter.Null);
        
        AssertStateAfterBuy1();

        void AssertStateAfterBuy1()
        {
            // 3 shares available
            AssertEq(3, tickerStateAfterBuy1.TotalQuantity);
            // The total amount corresponds to the total amount of the buy event 1
            AssertEq(303.20m / 4m, tickerStateAfterBuy1.TotalAmountBase);
        }

        // -------
        // Then sell 2 shares at 150 USD, with fees of 2.50 USD => Total Amount Local of 300 USD - 2.50 USD = 297.50 USD
        var sellEvent1 = new Event(T0 + D, SellLimit, Ticker, 2, 150m, 297.50m, 2.50m, localCurrency, 4m, -1);
        var tickerStateAfterSell1 = tickerProcessing.ProcessSell(
            sellEvent1, [buyEvent1, sellEvent1], 1, tickerStateAfterBuy1, TextWriter.Null);

        AssertStateAfterSell1();

        void AssertStateAfterSell1()
        {
            // Only 1 share left
            AssertEq(1, tickerStateAfterSell1.TotalQuantity);
            // The share left (1 remaining out of 3 shares) has the same average buy price as before
            var totalAmountBaseAfterSell1 = tickerStateAfterBuy1.TotalAmountBase * (1m / 3m);
            AssertEq(totalAmountBaseAfterSell1, tickerStateAfterSell1.TotalAmountBase);

            // The plus value CUMP is the difference between the sell price and the average buy price for the 2 shares sold
            // The minus value CUMP is 0, since no minus value has been realized
            var averageBuyPriceTwoShares = totalAmountBaseAfterSell1 * 2;
            var plusValueCumpSell1 = 297.50m / 4m - averageBuyPriceTwoShares;
            AssertEq(plusValueCumpSell1, tickerStateAfterSell1.PlusValueCumpBase);
            AssertEq(0, tickerStateAfterSell1.MinusValueCumpBase);

            // The plus value PEPS is the difference between the sell price and the buy price of the first two shares bought
            // The minus value PEPS is 0, since no minus value has been realized
            var buyPriceFirstTwoShares = (2 * 303.20m / 3) / 4m;
            var plusValuePepsSell1 = 297.50m / 4m - buyPriceFirstTwoShares;
            AssertEq(plusValuePepsSell1, tickerStateAfterSell1.PlusValuePepsBase);
            AssertEq(0, tickerStateAfterSell1.MinusValuePepsBase);

            // The PEPS current index is 0, since only 2 shares out of 3 have been sold
            AssertEq(0, tickerStateAfterSell1.PepsCurrentIndex);
            // The PEPS current index sold quantity is 2, since 2 shares out of 3 have been sold
            AssertEq(2, tickerStateAfterSell1.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Buy 3 shares at 110 USD, with fees of 4 USD => Total Amount Local of 330 USD + 4 USD = 334 USD
        var buyEvent2 = new Event(T0 + 2 * D, BuyLimit, Ticker, 3, 110m, 334m, 4m, localCurrency, 4m, -1);
        var tickerStateAfterBuy2 = tickerProcessing.ProcessBuy(
            buyEvent2, [buyEvent1, sellEvent1, buyEvent2], 2, tickerStateAfterSell1, TextWriter.Null);

        AssertStateAfterBuy2();

        void AssertStateAfterBuy2()
        {
            // The total quantity is increased by 3
            AssertEq(4, tickerStateAfterBuy2.TotalQuantity);
            // The total amount is increased by the total amount of the buy event 2
            var totalAmountBaseAfterBuy2 = tickerStateAfterSell1.TotalAmountBase + 334m / 4m;
            AssertEq(totalAmountBaseAfterBuy2, tickerStateAfterBuy2.TotalAmountBase);
        }

        // -------
        // Buy 2 shares at 120 USD, with fees of 3 USD => Total Amount Local of 240 USD + 3 USD = 243 USD
        var buyEvent3 = new Event(T0 + 3 * D, BuyLimit, Ticker, 2, 120m, 243m, 3m, localCurrency, 4m, -1);
        var tickerStateAfterBuy3 = tickerProcessing.ProcessBuy(
            buyEvent3, [buyEvent1, sellEvent1, buyEvent2, buyEvent3], 3, tickerStateAfterBuy2, TextWriter.Null);

        AssertStateAfterBuy3();

        void AssertStateAfterBuy3()
        {
            // The total quantity is increased by 2
            AssertEq(6, tickerStateAfterBuy3.TotalQuantity);
            // The total amount is increased by the total amount of the buy event 3
            var totalAmountBaseAfterBuy3 = tickerStateAfterBuy2.TotalAmountBase + 243m / 4m;
            AssertEq(totalAmountBaseAfterBuy3, tickerStateAfterBuy3.TotalAmountBase);
        }

        // -------
        // Sell 3 shares at 130 USD, with fees of 3 USD => Total Amount Local of 390 USD - 3 USD = 387 USD
        var sellEvent2 = new Event(T0 + 4 * D, SellLimit, Ticker, 3, 130m, 387m, 3m, localCurrency, 4m, -1);
        var tickerStateAfterSell2 = tickerProcessing.ProcessSell(
            sellEvent2, [buyEvent1, sellEvent1, buyEvent2, buyEvent3, sellEvent2], 4, 
            tickerStateAfterBuy3, TextWriter.Null);

        AssertStateAfterSell2();

        void AssertStateAfterSell2()
        {
            // Only 3 shares left
            AssertEq(3, tickerStateAfterSell2.TotalQuantity);
            // The total amount is decreased by half (3 remaining out of 6 shares), and not by the total amount of the sell event 2
            var totalAmountBaseAfterSell2 = tickerStateAfterBuy3.TotalAmountBase * (3m / 6m);
            AssertEq(totalAmountBaseAfterSell2, tickerStateAfterSell2.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 3 shares sold
            // This value is added to the plus value CUMP accumulated so far (first sell event)
            // The minus value CUMP is 0, since no minus value has been realized
            var averageBuyPriceThreeShares = tickerStateAfterBuy3.TotalAmountBase - totalAmountBaseAfterSell2;
            var plusValueCumpSell2 = 387m / 4m - averageBuyPriceThreeShares;
            AssertEq(tickerStateAfterBuy3.PlusValueCumpBase + plusValueCumpSell2, tickerStateAfterSell2.PlusValueCumpBase);
            AssertEq(0, tickerStateAfterSell2.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the oldest 3 shares bought,
            // among the 6 shares left after the third buy event:
            // - oldest share bought at 303.20 USD / 3 shares (local currency)
            // - next 3 shares bought at 334 USD / 3 shares (local currency)
            // - last 2 shares bought at 243 USD / 2 shares (local currency)
            // This value is added to the plus value PEPS accumulated so far (first sell event)
            // The minus value PEPS is 0, since no minus value has been realized
            var buyPriceFirstThreeShares = 1 * 303.20m / 3m / 4m + 2 * 334m / 3m / 4m;
            var plusValuePepsSell2 = 387m / 4m - buyPriceFirstThreeShares;
            AssertEq(tickerStateAfterBuy3.PlusValuePepsBase + plusValuePepsSell2, tickerStateAfterSell2.PlusValuePepsBase);
            AssertEq(0, tickerStateAfterSell2.MinusValuePepsBase);

            // The PEPS current index is 2, since the only remaining share of the first buy has been sold, together with
            // the first two shares of the second buy, where the last share is left not sold for now
            AssertEq(2, tickerStateAfterSell2.PepsCurrentIndex);
            // The PEPS current index sold quantity is 2, since 2 shares out of 3 have been sold
            AssertEq(2, tickerStateAfterSell2.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Sell 1 shares at 10 USD, with fees of 1 USD => Total Amount Local of 10 USD - 1 USD = 9 USD
        var sellEvent3 = new Event(T0 + 5 * D, SellLimit, Ticker, 1, 10m, 9m, 1m, localCurrency, 4m, -1);
        var tickerStateAfterSell3 = tickerProcessing.ProcessSell(
            sellEvent3, [buyEvent1, sellEvent1, buyEvent2, buyEvent3, sellEvent2, sellEvent3], 5, 
            tickerStateAfterSell2, TextWriter.Null);

        AssertStateAfterSell3();

        void AssertStateAfterSell3()
        {
            // Only 2 shares left
            AssertEq(2, tickerStateAfterSell3.TotalQuantity);
            // The total amount is decreased to 2/3 (2 remaining out of 3 shares), and not by the total amount of the sell event 3
            var totalAmountBaseAfterSell3 = tickerStateAfterSell2.TotalAmountBase * (2m / 3m);
            AssertEq(totalAmountBaseAfterSell3, tickerStateAfterSell3.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 1 share sold
            // This value is negative, so it is actually a minus value, taken with reversed sign
            // This value is the first CUMP minus value realized. The plus value CUMP doesn't change.
            var averageBuyPriceOneShare = tickerStateAfterSell2.TotalAmountBase - totalAmountBaseAfterSell3;
            var minusValueCumpSell3 = averageBuyPriceOneShare - 9m / 4m;
            AssertEq(tickerStateAfterSell3.PlusValueCumpBase, tickerStateAfterSell2.PlusValueCumpBase);
            AssertEq(minusValueCumpSell3, tickerStateAfterSell3.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the oldest share bought,
            // among the 3 shares left after the third sell event:
            // - oldest share bought at 334 USD / 3 shares (local currency)
            // - last 2 shares bought at 243 USD / 2 shares (local currency)
            // This value is negative, so it is actually a minus value, taken with reversed sign.
            // This value is the first PEPS minus value realized. The plus value PEPS doesn't change.
            var buyPriceOldestShare = 334m / 3m / 4m;
            var minusValuePepsSell3 = buyPriceOldestShare - 9m / 4m;
            AssertEq(tickerStateAfterSell3.PlusValuePepsBase, tickerStateAfterSell2.PlusValuePepsBase);
            AssertEq(minusValuePepsSell3, tickerStateAfterSell3.MinusValuePepsBase);

            // The PEPS current index is 3, since the last share of the second buy has been sold, and the pointer moves
            // forward to the next buy event, that is in position 3 in the list of events
            AssertEq(3, tickerStateAfterSell3.PepsCurrentIndex);
            // The PEPS current index sold quantity is 0, since the current index has been moved forward
            AssertEq(0, tickerStateAfterSell3.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Sell 2 shares at 10 USD, with fees of 1 USD => Total Amount Local of 20 USD - 1 USD = 19 USD
        var sellEvent4 = new Event(T0 + 6 * D, SellLimit, Ticker, 2, 10m, 19m, 1m, localCurrency, 4m, -1);
        var tickerStateAfterSell4 = tickerProcessing.ProcessSell(
            sellEvent4, [buyEvent1, sellEvent1, buyEvent2, buyEvent3, sellEvent2, sellEvent3, sellEvent4], 6, 
            tickerStateAfterSell3, TextWriter.Null);

        AssertStateAfterSell4();

        void AssertStateAfterSell4()
        {
            // No shares left
            AssertEq(0, tickerStateAfterSell4.TotalQuantity);
            // The total amount is decreased to 0 (0 remaining out of 2 shares), and not by the total amount of the sell event 4
            var totalAmountBaseAfterSell4 = tickerStateAfterSell3.TotalAmountBase * (0m / 2m);
            AssertEq(totalAmountBaseAfterSell4, tickerStateAfterSell4.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 2 shares sold
            // This value is negative, so it is actually a minus value, taken with reversed sign
            // This value is the second CUMP minus value realized. The plus value CUMP doesn't change.
            var averageBuyPriceTwoSharesSell4 = tickerStateAfterSell3.TotalAmountBase - totalAmountBaseAfterSell4;
            var minusValueCumpSell4 = averageBuyPriceTwoSharesSell4 - 19m / 4m;
            AssertEq(tickerStateAfterSell3.PlusValueCumpBase, tickerStateAfterSell4.PlusValueCumpBase);
            AssertEq(tickerStateAfterSell3.MinusValueCumpBase + minusValueCumpSell4, tickerStateAfterSell4.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the two
            // oldest share bought, that are the last two shares remaining
            // This value is negative, so it is actually a minus value, taken with reversed sign
            // This value is the second PEPS minus value realized. The plus value PEPS doesn't change.
            var buyPriceTwoOldestShares = 243m / 4m;
            var minusValuePepsSell4 = buyPriceTwoOldestShares - 19m / 4m;
            AssertEq(tickerStateAfterSell3.PlusValuePepsBase, tickerStateAfterSell4.PlusValuePepsBase);
            AssertEq(tickerStateAfterSell3.MinusValuePepsBase + minusValuePepsSell4, tickerStateAfterSell4.MinusValuePepsBase);

            // The PEPS current index is 4, since the last two shares of the third buy have been sold, and the pointer moves
            // to the next buy event, that is not present -> moved after the end of the list of events
            AssertEq(7, tickerStateAfterSell4.PepsCurrentIndex);
            // The PEPS current index sold quantity is 0, since the current index has been moved forward
            AssertEq(0, tickerStateAfterSell4.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }
    }

    [TestMethod]
    public void ProcessBuyAndSell_AfterReset_UpdateTickerStateCorrectly()
    {
        var tickerProcessing = Instance;
        var localCurrency = USD; // FX rate between USD and EUR stays stable at 2 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin);

        // -------
        // First buy 10 shares at 100 USD, with fees of 20 USD => Total Amount Local of 1000 USD + 20 USD = 1020 USD
        var buyEvent1 = new Event(T0, BuyLimit, Ticker, 10, 100m, 1020m, 20m, localCurrency, 2m, -1);
        var tickerStateAfterBuy1 = tickerProcessing.ProcessBuy(
            buyEvent1, [buyEvent1], 0, initialState, TextWriter.Null);

        // --------
        // Sell 5 shares at 150 USD, with fees of 10 USD => Total Amount Local of 750 USD - 10 USD = 740 USD
        var sellEvent1 = new Event(T0 + D, SellLimit, Ticker, 5, 150m, 740m, 10m, localCurrency, 2m, -1);
        var tickerStateAfterSell1 = tickerProcessing.ProcessSell(
            sellEvent1, [buyEvent1, sellEvent1], 1, tickerStateAfterBuy1, TextWriter.Null);

        AssertStateAfterSell1();

        void AssertStateAfterSell1() {
            // 5 shares left
            AssertEq(5, tickerStateAfterSell1.TotalQuantity);
            // The total amount is decreased by half (5 remaining out of 10 shares), and not by the total amount of the sell event 1
            var totalAmountBaseAfterSell1 = tickerStateAfterBuy1.TotalAmountBase * (5m / 10m);
            AssertEq(totalAmountBaseAfterSell1, tickerStateAfterSell1.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 5 shares sold
            // The minus value CUMP is 0, since no minus value has been realized
            var averageBuyPriceFiveShares = totalAmountBaseAfterSell1;
            var plusValueCumpSell1 = 740m / 2m - averageBuyPriceFiveShares;
            AssertEq(plusValueCumpSell1, tickerStateAfterSell1.PlusValueCumpBase);
            AssertEq(0, tickerStateAfterSell1.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the first 5 shares bought
            // The minus value PEPS is 0, since no minus value has been realized
            var buyPriceFirstFiveShares = 1020m / 2m / 2m;
            var plusValuePepsSell1 = 740m / 2m - buyPriceFirstFiveShares;
            AssertEq(plusValuePepsSell1, tickerStateAfterSell1.PlusValuePepsBase);
            AssertEq(0, tickerStateAfterSell1.MinusValuePepsBase);

            // The PEPS current index is 0, since only 5 shares out of 10 have been sold
            AssertEq(0, tickerStateAfterSell1.PepsCurrentIndex);
            // The PEPS current index sold quantity is 5, since 5 shares out of 10 have been sold
            AssertEq(5, tickerStateAfterSell1.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Reset the ticker state
        var resetEvent1 = new Event(T0 + 2 * D, Reset, Ticker, null, null, 0, null, localCurrency, 2m, -1);
        var tickerStateAfterReset1 = tickerProcessing.ProcessReset(
            resetEvent1, [buyEvent1, sellEvent1, resetEvent1], 2, tickerStateAfterSell1, TextWriter.Null);

        AssertStateAfterReset1();

        void AssertStateAfterReset1() {
            // The total quantity and amount are the same as before the reset
            AssertEq(tickerStateAfterSell1.TotalQuantity, tickerStateAfterReset1.TotalQuantity);
            AssertEq(tickerStateAfterSell1.TotalAmountBase, tickerStateAfterReset1.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto are reset to 0
            AssertEq(0, tickerStateAfterReset1.PlusValueCumpBase);
            AssertEq(0, tickerStateAfterReset1.MinusValueCumpBase);
            AssertEq(0, tickerStateAfterReset1.PlusValuePepsBase);
            AssertEq(0, tickerStateAfterReset1.MinusValuePepsBase);
            AssertEq(0, tickerStateAfterReset1.PlusValueCryptoBase);
            AssertEq(0, tickerStateAfterReset1.MinusValueCryptoBase);

            // The PEPS current index and sold quantity are the same as before the reset
            AssertEq(tickerStateAfterSell1.PepsCurrentIndex, tickerStateAfterReset1.PepsCurrentIndex);
            AssertEq(tickerStateAfterSell1.PepsCurrentIndexSoldQuantity, tickerStateAfterReset1.PepsCurrentIndexSoldQuantity);
        }

        // -------
        // Sell 2 shares at 80 USD, with fees of 6 USD => Total Amount Local of 160 USD - 6 USD = 154 USD
        var sellEvent2 = new Event(T0 + 3 * D, SellLimit, Ticker, 2, 80m, 154m, 6m, localCurrency, 2m, -1);
        var tickerStateAfterSell2 = tickerProcessing.ProcessSell(
            sellEvent2, [buyEvent1, sellEvent1, resetEvent1, sellEvent2], 3, 
            tickerStateAfterReset1, TextWriter.Null);

        AssertStateAfterSell2();

        void AssertStateAfterSell2() {
            // 3 shares left
            AssertEq(3, tickerStateAfterSell2.TotalQuantity);
            // The total amount is decreased to 3/5 (2 shares out of 5 sold), and not by the total amount of the sell event 2
            var totalAmountBaseAfterSell2 = tickerStateAfterSell1.TotalAmountBase * (3m / 5m);
            AssertEq(totalAmountBaseAfterSell2, tickerStateAfterSell2.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 2 shares sold
            // This value is negative, so it is actually a minus value, taken with reversed sign
            // This value is the first CUMP minus value realized. The plus value CUMP doesn't change.
            var averageBuyPriceTwoSharesSell2 = tickerStateAfterReset1.TotalAmountBase - totalAmountBaseAfterSell2;
            var minusValueCumpSell2 = averageBuyPriceTwoSharesSell2 - 154m / 2m;
            AssertEq(0, tickerStateAfterSell2.PlusValueCumpBase);
            AssertEq(minusValueCumpSell2, tickerStateAfterSell2.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the oldest
            // 2 shares bought:
            // - oldest 2 shares bought at 1020 USD / 10 shares (local currency)
            // This value is negative, so it is actually a minus value, taken with reversed sign
            // This value is the first PEPS minus value realized. The plus value PEPS doesn't change.
            var buyPriceOldestTwoShares = 2 * 1020m / 10m / 2m;
            var minusValuePepsSell2 = buyPriceOldestTwoShares - 154m / 2m;
            AssertEq(0, tickerStateAfterSell2.PlusValuePepsBase);
            AssertEq(minusValuePepsSell2, tickerStateAfterSell2.MinusValuePepsBase);

            // The PEPS current index is 0, since there are still 3 shares left of the first buy that have not been sold
            AssertEq(0, tickerStateAfterSell2.PepsCurrentIndex);
            // The PEPS current index sold quantity is 7, since 7 shares out of 10 have been sold
            AssertEq(7, tickerStateAfterSell2.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Buy 1 share at 100 USD, with fees of 2 USD => Total Amount Local of 100 USD + 2 USD = 102 USD
        var buyEvent2 = new Event(T0 + 4 * D, BuyLimit, Ticker, 1, 100m, 102m, 2m, localCurrency, 2m, -1);
        var tickerStateAfterBuy2 = tickerProcessing.ProcessBuy(
            buyEvent2, [buyEvent1, sellEvent1, resetEvent1, sellEvent2, buyEvent2], 4, 
            tickerStateAfterSell2, TextWriter.Null);

        // Buy 2 shares at 110 USD, with fees of 4 USD => Total Amount Local of 220 USD + 4 USD = 224 USD
        var buyEvent3 = new Event(T0 + 5 * D, BuyLimit, Ticker, 2, 110m, 224m, 4m, localCurrency, 2m, -1);
        var tickerStateAfterBuy3 = tickerProcessing.ProcessBuy(
            buyEvent3, [buyEvent1, sellEvent1, resetEvent1, sellEvent2, buyEvent2, buyEvent3], 5, 
            tickerStateAfterBuy2, TextWriter.Null);

        // Cash withdrawal of 50 USD, with fees of 1 USD => Total Amount Local of 49 USD
        var cashWithdrawalEvent1 = new Event(T0 + 6 * D, CashWithdrawal, Ticker, null, null, 49m, 1m, localCurrency, 2m, -1);
        var tickerStateAfterCashWithdrawal1 = tickerProcessing.ProcessNoop(
            cashWithdrawalEvent1, [buyEvent1, sellEvent1, resetEvent1, sellEvent2, buyEvent2, buyEvent3, cashWithdrawalEvent1], 6, 
            tickerStateAfterBuy3, TextWriter.Null);

        // Sell 4 shares at 120 USD, with fees of 8 USD => Total Amount Local of 480 USD - 8 USD = 472 USD
        var sellEvent3 = new Event(T0 + 7 * D, SellLimit, Ticker, 4, 120m, 472m, 8m, localCurrency, 2m, -1);
        var tickerStateAfterSell3 = tickerProcessing.ProcessSell(
            sellEvent3, [buyEvent1, sellEvent1, resetEvent1, sellEvent2, buyEvent2, buyEvent3, cashWithdrawalEvent1, sellEvent3], 7, 
            tickerStateAfterCashWithdrawal1, TextWriter.Null);

        AssertStateAfterSell3();

        void AssertStateAfterSell3() 
        {
            // 2 shares left
            AssertEq(2, tickerStateAfterSell3.TotalQuantity);
            // The total amount is decreased to 2/6 (2 remaining out of 6 shares), and not by the total amount of the sell event 3
            var totalAmountBaseAfterSell3 = tickerStateAfterCashWithdrawal1.TotalAmountBase * (2m / 6m);
            AssertEq(totalAmountBaseAfterSell3, tickerStateAfterSell3.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 4 shares sold
            // The minus value CUMP remains the same as before, since no new minus value has been realized
            var averageBuyPriceFourSharesSell3 = tickerStateAfterCashWithdrawal1.TotalAmountBase - totalAmountBaseAfterSell3;
            var plusValueCumpSell3 = 472m / 2m - averageBuyPriceFourSharesSell3;
            AssertEq(plusValueCumpSell3, tickerStateAfterSell3.PlusValueCumpBase);
            AssertEq(tickerStateAfterCashWithdrawal1.MinusValueCumpBase, tickerStateAfterSell3.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the oldest 4 shares bought:
            // - oldest 3 shares bought at 1020 USD / 10 shares (local currency)
            // - next 1 share bought at 102 USD / 2 shares (local currency)
            // - last 2 shares bought at 224 USD / 2 shares (local currency)
            // The minus value PEPS remains the same as before, since no new minus value has been realized
            var buyPriceOldestFourShares = 3 * 1020m / 10m / 2m + 1 * 102m / 1m / 2m;
            var plusValuePepsSell3 = 472m / 2m - buyPriceOldestFourShares;
            AssertEq(plusValuePepsSell3, tickerStateAfterSell3.PlusValuePepsBase);
            AssertEq(tickerStateAfterCashWithdrawal1.MinusValuePepsBase, tickerStateAfterSell3.MinusValuePepsBase);

            // The PEPS current index is 5, since all the 3 oldest shares, together with the next 1 share, have been sold,
            // and the pointer moves to the next buy event (last 2 shares bought), that is in position 5 in the list of events
            AssertEq(5, tickerStateAfterSell3.PepsCurrentIndex);
            // The PEPS current index sold quantity is 0, since 0 shares out of 2 have been sold
            AssertEq(0, tickerStateAfterSell3.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Reset the ticker state
        var resetEvent2 = new Event(T0 + 8 * D, Reset, Ticker, null, null, 0, null, localCurrency, 2m, -1);
        var tickerStateAfterReset2 = tickerProcessing.ProcessReset(
            resetEvent2, [buyEvent1, sellEvent1, resetEvent1, sellEvent2, buyEvent2, buyEvent3, cashWithdrawalEvent1, sellEvent3, resetEvent2], 8, 
            tickerStateAfterSell3, TextWriter.Null);

        AssertStateAfterReset2();

        void AssertStateAfterReset2()
        {
            // The total quantity and amount are the same as before the reset
            AssertEq(tickerStateAfterSell3.TotalQuantity, tickerStateAfterReset2.TotalQuantity);
            AssertEq(tickerStateAfterSell3.TotalAmountBase, tickerStateAfterReset2.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto are reset to 0
            AssertEq(0, tickerStateAfterReset2.PlusValueCumpBase);
            AssertEq(0, tickerStateAfterReset2.MinusValueCumpBase);
            AssertEq(0, tickerStateAfterReset2.PlusValuePepsBase);
            AssertEq(0, tickerStateAfterReset2.MinusValuePepsBase);
            AssertEq(0, tickerStateAfterReset2.PlusValueCryptoBase);
            AssertEq(0, tickerStateAfterReset2.MinusValueCryptoBase);

            // The PEPS current index and sold quantity are the same as before the reset
            AssertEq(tickerStateAfterSell3.PepsCurrentIndex, tickerStateAfterReset2.PepsCurrentIndex);
            AssertEq(tickerStateAfterSell3.PepsCurrentIndexSoldQuantity, tickerStateAfterReset2.PepsCurrentIndexSoldQuantity);
        }           
    }

    [TestMethod]
    public void ProcessStockSplit_UpdatesTickerStateCorrecly()
    {
        var tickerProcessing = Instance;
        var localCurrency = USD; // FX rate between USD and EUR stays stable at 2 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin);
        var events = new List<Event>();

        // -------
        // First buy 10 shares at 100 USD, with fees of 20 USD => Total Amount Local of 1000 USD + 20 USD = 1020 USD
        var buyEvent1 = new Event(T0, BuyLimit, Ticker, 10, 100m, 1020m, 20m, localCurrency, 2m, -1);
        events.Add(buyEvent1);
        var tickerStateAfterBuy1 = tickerProcessing.ProcessBuy(
            buyEvent1, events, 0, initialState, TextWriter.Null);

        // --------
        // Sell 5 shares at 150 USD, with fees of 10 USD => Total Amount Local of 750 USD - 10 USD = 740 USD
        var sellEvent1 = new Event(T0 + D, SellLimit, Ticker, 5, 150m, 740m, 10m, localCurrency, 2m, -1);
        events.Add(sellEvent1);
        var tickerStateAfterSell1 = tickerProcessing.ProcessSell(
            sellEvent1, events, 1, tickerStateAfterBuy1, TextWriter.Null);

        AssertStateAfterSell1();

        void AssertStateAfterSell1()
        {
            // 5 shares left
            AssertEq(5, tickerStateAfterSell1.TotalQuantity);
            // The total amount is decreased by half (5 remaining out of 10 shares), and not by the total amount of the sell event 1
            var totalAmountBaseAfterSell1 = tickerStateAfterBuy1.TotalAmountBase * (5m / 10m);
            AssertEq(totalAmountBaseAfterSell1, tickerStateAfterSell1.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 5 shares sold
            // The minus value CUMP is 0, since no minus value has been realized
            var averageBuyPriceFiveShares = totalAmountBaseAfterSell1;
            var plusValueCumpSell1 = 740m / 2m - averageBuyPriceFiveShares;
            AssertEq(plusValueCumpSell1, tickerStateAfterSell1.PlusValueCumpBase);
            AssertEq(0, tickerStateAfterSell1.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the first 5 shares bought
            // The minus value PEPS is 0, since no minus value has been realized
            var buyPriceFirstFiveShares = 1020m / 2m / 2m;
            var plusValuePepsSell1 = 740m / 2m - buyPriceFirstFiveShares;
            AssertEq(plusValuePepsSell1, tickerStateAfterSell1.PlusValuePepsBase);
            AssertEq(0, tickerStateAfterSell1.MinusValuePepsBase);

            // The PEPS current index is 0, since only 5 shares out of 10 have been sold
            AssertEq(0, tickerStateAfterSell1.PepsCurrentIndex);
            // The PEPS current index sold quantity is 5, since 5 shares out of 10 have been sold
            AssertEq(5, tickerStateAfterSell1.PepsCurrentIndexSoldQuantity);
        }

        // -------
        // Stock split of 1:3, starting from 5 shares => 10 additional shares
        var stockSplitEvent1 = new Event(T0 + 2 * D, StockSplit, Ticker, 10m, null, 0, null, localCurrency, 2m, -1);
        events.Add(stockSplitEvent1);
        var tickerStateAfterStockSplit1 = tickerProcessing.ProcessStockSplit(
            stockSplitEvent1, events, 2, tickerStateAfterSell1, TextWriter.Null);

        AssertStateAfterStockSplit1();

        void AssertStateAfterStockSplit1()
        {
            // The total quantity is increased by 10
            AssertEq(15, tickerStateAfterStockSplit1.TotalQuantity);
            // The total amount remains the same
            AssertEq(tickerStateAfterSell1.TotalAmountBase, tickerStateAfterStockSplit1.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto remain the same
            AssertEq(tickerStateAfterSell1.PlusValueCumpBase, tickerStateAfterStockSplit1.PlusValueCumpBase);
            AssertEq(tickerStateAfterSell1.MinusValueCumpBase, tickerStateAfterStockSplit1.MinusValueCumpBase);
            AssertEq(tickerStateAfterSell1.PlusValuePepsBase, tickerStateAfterStockSplit1.PlusValuePepsBase);
            AssertEq(tickerStateAfterSell1.MinusValuePepsBase, tickerStateAfterStockSplit1.MinusValuePepsBase);
            AssertEq(tickerStateAfterSell1.PlusValueCryptoBase, tickerStateAfterStockSplit1.PlusValueCryptoBase);
            AssertEq(tickerStateAfterSell1.MinusValueCryptoBase, tickerStateAfterStockSplit1.MinusValueCryptoBase);

            // The PEPS current index remains unchanged, but the PEPS current index sold quantity is multiplied by 3
            AssertEq(0, tickerStateAfterStockSplit1.PepsCurrentIndex);
            AssertEq(tickerStateAfterSell1.PepsCurrentIndexSoldQuantity * 3m, tickerStateAfterStockSplit1.PepsCurrentIndexSoldQuantity);

            // Previous Buy and Sell stock events are retroactively updated in the list of events, to take into account the 1:3 stock split
            var modifiedBuyEvent1 = events[0];
            AssertEq(10 * 3m, modifiedBuyEvent1.Quantity); // 10 shares bought, now 30 shares
            AssertEq(100m / 3m, modifiedBuyEvent1.PricePerShareLocal); // Buy price per share is divided by 3
            AssertEq(1020m, modifiedBuyEvent1.TotalAmountLocal); // Remains the same
            AssertEq(20m, modifiedBuyEvent1.FeesLocal); // Remains the same
            AssertEq(2m, modifiedBuyEvent1.FXRate); // Remains the same

            var modifiedSellEvent1 = events[1];
            AssertEq(5 * 3m, modifiedSellEvent1.Quantity); // 5 shares sold, now 15 shares
            AssertEq(150m / 3m, modifiedSellEvent1.PricePerShareLocal); // Sell price per share is divided by 3
            AssertEq(740m, modifiedSellEvent1.TotalAmountLocal); // Remains the same
            AssertEq(10m, modifiedSellEvent1.FeesLocal); // Remains the same
            AssertEq(2m, modifiedSellEvent1.FXRate); // Remains the same

            // The original Buy and Sell stock events are not modified
            AssertEq(10, buyEvent1.Quantity);
            AssertEq(100m, buyEvent1.PricePerShareLocal);
            AssertEq(5, sellEvent1.Quantity);
            AssertEq(150m, sellEvent1.PricePerShareLocal);
        }

        // -------
        // Stock split of 1:4, starting from 15 shares => 45 additional shares
        var stockSplitEvent2 = new Event(T0 + 3 * D, StockSplit, Ticker, 45m, null, 0, null, localCurrency, 2m, -1);
        events.Add(stockSplitEvent2);
        var tickerStateAfterStockSplit2 = tickerProcessing.ProcessStockSplit(
            stockSplitEvent2, events, 3, tickerStateAfterStockSplit1, TextWriter.Null);

        AssertStateAfterStockSplit2();

        void AssertStateAfterStockSplit2()
        {
            // The total quantity is increased by 45
            AssertEq(60, tickerStateAfterStockSplit2.TotalQuantity);
            // The total amount remains the same
            AssertEq(tickerStateAfterStockSplit1.TotalAmountBase, tickerStateAfterStockSplit2.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto remain the same
            AssertEq(tickerStateAfterStockSplit1.PlusValueCumpBase, tickerStateAfterStockSplit2.PlusValueCumpBase);
            AssertEq(tickerStateAfterStockSplit1.MinusValueCumpBase, tickerStateAfterStockSplit2.MinusValueCumpBase);
            AssertEq(tickerStateAfterStockSplit1.PlusValuePepsBase, tickerStateAfterStockSplit2.PlusValuePepsBase);
            AssertEq(tickerStateAfterStockSplit1.MinusValuePepsBase, tickerStateAfterStockSplit2.MinusValuePepsBase);
            AssertEq(tickerStateAfterStockSplit1.PlusValueCryptoBase, tickerStateAfterStockSplit2.PlusValueCryptoBase);
            AssertEq(tickerStateAfterStockSplit1.MinusValueCryptoBase, tickerStateAfterStockSplit2.MinusValueCryptoBase);

            // The PEPS current index remains unchanged, but the PEPS current index sold quantity is multiplied by 4
            AssertEq(0, tickerStateAfterStockSplit2.PepsCurrentIndex);
            AssertEq(tickerStateAfterStockSplit1.PepsCurrentIndexSoldQuantity * 4m, tickerStateAfterStockSplit2.PepsCurrentIndexSoldQuantity);

            // Previous Buy and Sell stock events are retroactively updated in the list of events, to take into account the 1:4 stock split
            var modifiedBuyEvent1 = events[0];
            AssertEq(10 * 3m * 4m, modifiedBuyEvent1.Quantity); // 30 shares bought, now 120 shares
            AssertEq(100m / 3m / 4m, modifiedBuyEvent1.PricePerShareLocal); // Buy price per share is divided by 4
            AssertEq(1020m, modifiedBuyEvent1.TotalAmountLocal); // Remains the same
            AssertEq(20m, modifiedBuyEvent1.FeesLocal); // Remains the same
            AssertEq(2m, modifiedBuyEvent1.FXRate); // Remains the same

            var modifiedSellEvent1 = events[1];
            AssertEq(5 * 3m * 4m, modifiedSellEvent1.Quantity); // 15 shares sold, now 60 shares
            AssertEq(150m / 3m / 4m, modifiedSellEvent1.PricePerShareLocal); // Sell price per share is divided by 4
            AssertEq(740m, modifiedSellEvent1.TotalAmountLocal); // Remains the same
            AssertEq(10m, modifiedSellEvent1.FeesLocal); // Remains the same
            AssertEq(2m, modifiedSellEvent1.FXRate); // Remains the same

            // Previous Stock Split events are not modified
            var modifiedStockSplit1 = events[2];
            AssertEq(10, modifiedStockSplit1.Quantity);
            AssertEq(0, modifiedStockSplit1.TotalAmountLocal);

            // The original Buy and Sell stock events are not modified
            AssertEq(10, buyEvent1.Quantity);
            AssertEq(100m, buyEvent1.PricePerShareLocal);
            AssertEq(5, sellEvent1.Quantity);
            AssertEq(150m, sellEvent1.PricePerShareLocal);

            // The original Stock Split events is not modified
            AssertEq(10, stockSplitEvent1.Quantity);
            AssertEq(0, stockSplitEvent1.TotalAmountLocal);
        }
    }

    [AssertionMethod]
    private void AssertEq(decimal expected, decimal actual, string message = "") => 
        Assert.AreEqual(expected, actual, Instance.Basics.Precision, message);

    [AssertionMethod]
    private void AssertEq(int expected, int actual, string message = "") => 
        Assert.AreEqual(expected, actual, message);

    [AssertionMethod]
    private void AssertEq(decimal? expected, decimal? actual, string message = "") => 
        AssertEq(expected!.Value, actual!.Value, message);
}
