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
    private static readonly DateTime T0 = (2022, 1, 1).ToUtc();
    private static readonly TimeSpan D = TimeSpan.FromDays(1);
    private static readonly TextWriter NoOut = TextWriter.Null;

    private readonly TickerProcessing Instance = new(new());

    [TestMethod]
    public void ProcessTicker_NoEvents()
    {
        Instance.ProcessTicker(Ticker, []).AssertZeroExceptFor();
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit_IncreasesTotalQuantityAndAmountAccordingly()
    {
        // Quantity, PricePerShareLocal, TotalAmountLocal, FeesLocal
        List<Event> e = [new(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1)];
        Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 3, totalAmountBase: 303, portfolioAcquisitionValueBase: 303);
        e.Add(new(T0 + 1 * D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1));
        Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 5, totalAmountBase: 525, portfolioAcquisitionValueBase: 525);
        e.Add(new(T0 + 2 * D, BuyLimit, Ticker, 1, 90, 91, 1, EUR, 1));
        Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 6, totalAmountBase: 616, portfolioAcquisitionValueBase: 616);
        e.Add(new(T0 + 2 * D, BuyLimit, Ticker, 1, 90, 92, 2, EUR, 1));
        Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 7, totalAmountBase: 708, portfolioAcquisitionValueBase: 708);
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit_DifferentCurrency_ConvertsToBase()
    {
        List<Event> e = [new(T0, BuyLimit, Ticker, 3, 100, 303, 3, USD, 1.2m)];
        Instance.ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 3, totalAmountBase: 303 / 1.2m, portfolioAcquisitionValueBase: 303 / 1.2m);
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit_NonPositiveQuantity_RaisesException()
    {
        ThrowsAny<Exception>(() => Instance.ProcessTicker(Ticker, [new(T0, BuyLimit, Ticker, -3, 100, 303, 3, EUR, 1)]));
        ThrowsAny<Exception>(() => Instance.ProcessTicker(Ticker, [new(T0, BuyLimit, Ticker, 0, 100, 303, 3, EUR, 1)]));
    }

    [TestMethod]
    public void ProcessTicker_BuyMarket_Fees()
    {
        // Assert fees after 2022-12-28T20:12:29.182442Z,AAPL,BUY - MARKET,5,$126.21,$632.62,EUR,1
        List<Event> e = [new(T0, BuyMarket, Ticker, 5, 126.21m, 632.62m, 1.58m, EUR, 1)];
        // TODO: continue
    }

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellWithoutBuying_RaisesException() => 
        Instance.ProcessTicker(Ticker, [new(T0, SellLimit, Ticker, 3, 100, 303, 3, EUR, 1)]);

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellMoreThanOwned_RaisesException() =>
         Instance.ProcessTicker(Ticker, [
             new(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1),
             new(T0, SellLimit, Ticker, 4, 100, 404, 4, EUR, 1)]);

    // TODO: fix
    // [TestMethod]
    // public void ProcessTicker_SellExactQuantityBought_GeneratingPlusValue()
    // {
    //     List<Event> e = [
    //         new(T0, BuyLimit, Ticker, 
    //             Quantity: 3, PricePerShareLocal: 100, TotalAmountLocal: 303, FeesLocal: 3, 
    //             EUR, 1, -1),
    //         new(T0 + 1 * D, SellLimit, Ticker, 
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
        var state = new TickerState(Ticker, Isin);
        var tickerEvent = new Event(T0 + 0 * D, eventType, Ticker, null, null, 100, null, EUR, 1);
        var stateAfterTopUp = Instance.ProcessTicker(Ticker, [tickerEvent], NoOut);
        Assert.AreEqual(state, stateAfterTopUp);
    }

    [TestMethod]
    public void ProcessReset_KeepsTickerAndIsin()
    {
        var reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, null, 0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var stateAfterReset = Instance.ProcessReset(reset1, [reset1], 0, state, NoOut);
        Assert.AreEqual(Ticker, stateAfterReset.Ticker);
        Assert.AreEqual(Isin, stateAfterReset.Isin);
    }

    [TestMethod]
    public void ProcessReset_WhenTickerEventAndIndexAreInconsistent_RaisesException()
    {
        var cashWithdrawal1 = new Event(T0 + 0 * D, CashWithdrawal, Ticker, null, null, 100, null, EUR, 1);
        var reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, null, 0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessReset(reset1, [cashWithdrawal1, reset1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessReset_WhenPassingNotSupportedType_RaisesException()
    {
        var custodyFee1 = new Event(T0 + 0 * D, CustodyFee, Ticker, 0, 0, 0, 12.0m, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessReset(custodyFee1, [custodyFee1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessReset_WhenPassingANonZeroTotalAmount_RaisesException()
    {
        var state = new TickerState(Ticker, Isin);
        var reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, null, TotalAmountLocal: 303, FeesLocal: null, Currency: EUR, FXRate: 1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(reset1, [reset1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessReset_WhenPassingTransactionRelatedInfo_RaisesException()
    {
        var state = new TickerState(Ticker, Isin);
        var reset1 = new Event(T0 + 0 * D, Reset, Ticker, Quantity: 4, PricePerShareLocal: null, TotalAmountLocal: 0m, FeesLocal: null, Currency: EUR, FXRate: 1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(reset1, [reset1], 0, state, NoOut));
        reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, PricePerShareLocal: 100, TotalAmountLocal: 0m, FeesLocal: null, Currency: EUR, FXRate: 1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(reset1, [reset1], 0, state, NoOut));
        reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, null, 0m, FeesLocal: 3, Currency: EUR, FXRate: 1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(reset1, [reset1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessReset_PreservesTotalQuantityAndAmountBase()
    {
        var reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, null, 0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var stateAfterReset = Instance.ProcessReset(reset1, [reset1], 0, state, NoOut);
        AssertEq(3, stateAfterReset.TotalQuantity);
        AssertEq(5.5m, stateAfterReset.TotalAmountBase);
    }

    [TestMethod]
    public void ProcessReset_PreservesPepsIndexes()
    {
        var reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, null, 0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin, PepsCurrentIndex: 3, PepsCurrentIndexSoldQuantity: 5.5m);
        var stateAfterReset = Instance.ProcessReset(reset1, [reset1], 0, state, NoOut);
        AssertEq(3, stateAfterReset.PepsCurrentIndex);
        AssertEq(5.5m, stateAfterReset.PepsCurrentIndexSoldQuantity);
    }

    [TestMethod]
    public void ProcessReset_PreservesPortfolioAcquisitionValueBaseAndCryptoFractionOfInitialCapital()
    {
        var reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, null, 0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin, 
            PortfolioAcquisitionValueBase: 5.5m, CryptoFractionOfInitialCapital: 0.75m);
        var stateAfterReset = Instance.ProcessReset(reset1, [reset1], 0, state, NoOut);
        AssertEq(5.5m, stateAfterReset.PortfolioAcquisitionValueBase);
        AssertEq(0.75m, stateAfterReset.CryptoFractionOfInitialCapital);
    }

    [TestMethod]
    public void ProcessReset_ResetsPlusValues()
    {
        var reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, null, 0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin, 
            PlusValueCumpBase: 5.5m, PlusValuePepsBase: 5.5m, PlusValueCryptoBase: 5.5m);
        var stateAfterReset = Instance.ProcessReset(reset1, [reset1], 0, state, NoOut);
        AssertEq(0, stateAfterReset.PlusValueCumpBase);
        AssertEq(0, stateAfterReset.PlusValuePepsBase);
        AssertEq(0, stateAfterReset.PlusValueCryptoBase);
    }

    [TestMethod]
    public void ProcessReset_ResetsMinusValues()
    {
        var reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, null, 0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin, 
                       MinusValueCumpBase: 5.5m, MinusValuePepsBase: 5.5m, MinusValueCryptoBase: 5.5m);
        var stateAfterReset = Instance.ProcessReset(reset1, [reset1], 0, state, NoOut);
        AssertEq(0, stateAfterReset.MinusValueCumpBase);
        AssertEq(0, stateAfterReset.MinusValuePepsBase);
        AssertEq(0, stateAfterReset.MinusValueCryptoBase);
    }

    [TestMethod]
    public void ProcessReset_ResetsDividends()
    {
        var reset1 = new Event(T0 + 0 * D, Reset, Ticker, null, null, 0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin, 
                       NetDividendsBase: 5.5m, WhtDividendsBase: 5.5m, GrossDividendsBase: 5.5m);
        var stateAfterReset = Instance.ProcessReset(reset1, [reset1], 0, state, NoOut);
        AssertEq(0, stateAfterReset.NetDividendsBase);
        AssertEq(0, stateAfterReset.WhtDividendsBase);
        AssertEq(0, stateAfterReset.GrossDividendsBase);
    }

    [TestMethod]
    public void ProcessNoop_LeavesTickerStateUnchanged()
    {
        var cashTopUp1 = new Event(T0 + 0 * D, CashTopUp, Ticker, null, null, 0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin, 
            PlusValueCumpBase: 5.5m, PlusValuePepsBase: 5.6m, PlusValueCryptoBase: 13.22m,
            MinusValueCumpBase: 3.2m, MinusValuePepsBase: 0m, MinusValueCryptoBase: 3.9m,
            TotalQuantity: 3, TotalAmountBase: 25.46m,
            NetDividendsBase: 4.5m, WhtDividendsBase: 0.2m, GrossDividendsBase: 4.7m,
            PepsCurrentIndex: 3, PepsCurrentIndexSoldQuantity: 2.1m,
            PortfolioAcquisitionValueBase: 23.3m, CryptoFractionOfInitialCapital: 0.75m);
        var stateAfterNoop = Instance.ProcessNoop(cashTopUp1, [cashTopUp1], 0, state, NoOut);
        Assert.AreEqual(state, stateAfterNoop);
    }

    [TestMethod]
    public void ProcessNoop_WhenTickerEventAndIndexAreInconsistent_RaisesException()
    {
        var cashWithdrawal1 = new Event(T0 + 0 * D, CashWithdrawal, Ticker, null, null, 100, null, EUR, 1);
        var cashTopUp1 = new Event(T0 + 0 * D, CashTopUp, Ticker, null, null, 0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessNoop(cashTopUp1, [cashWithdrawal1, cashTopUp1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_WhenTickerEventAndIndexAreInconsistent_RaisesException()
    {
        var cashWithdrawal1 = new Event(T0 + 0 * D, CashWithdrawal, Ticker, null, null, 100, null, EUR, 1);
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buy1, [cashWithdrawal1, buy1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_WhenPassingNotSupportedType_RaisesException()
    {
        var custodyFee1 = new Event(T0 + 0 * D, CustodyFee, Ticker, null, null, 12.0m, null, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(custodyFee1, [custodyFee1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_WhenTickerNameIsNull_RaisesException()
    {
        var buy1 = new Event(T0 + 0 * D, BuyLimit, null, 3, 100, 303, 3, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buy1, [buy1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_WhenPricePerShareLocalIsNull_RaisesException()
    {
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, null, 303, 3, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buy1, [buy1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_WhenQuantityIsNull_RaisesException()
    {
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, null, 100, 303, 3, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buy1, [buy1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_WhenTotalAmountLocalIsNonPositive_RaisesException()
    {
        // Buying 3 shares at 100 EUR, with fees of 3 EUR => Total Amount Local of 0 EUR
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100, 0m, 3, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buy1, [buy1], 0, state, NoOut));

        // Buying 3 shares at 100 EUR, with fees of 3 EUR => Total Amount Local of -1 EUR
        buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100, -1m, 3, EUR, 1);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buy1, [buy1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_WhenFeesLocalIsNull_RaisesException()
    {
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100, 303, null, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buy1, [buy1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_WhenCurrenciesDontMatch_RaisesException()
    {
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100, 303, 3, USD, 1);
        var state = new TickerState(Ticker, Isin);
        var events = new[] { new Event(T0 + 0 * D, BuyLimit, Ticker, 0, 0, 0, 0, EUR, 1), buy1 };
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buy1, events, 1, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_WhenTickersDontMatch_RaisesException()
    {
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1);
        var state = new TickerState(AnotherTicker, AnotherIsin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buy1, [buy1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_WhenFeesDontMatch_RaisesException()
    {
        var state = new TickerState(Ticker, Isin);
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100, 303, 2, EUR, 1); // Fees should be 3
        ThrowsAny<Exception>(() => Instance.ProcessBuy(buy1, [buy1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessBuy_IncreasesTotalQuantityByTheQuantityInTheEvent()
    {
        var state = new TickerState(Ticker, Isin);
        // First buy of 3 shares
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1);
        var stateAfterBuy = Instance.ProcessBuy(buy1, [buy1], 0, state, NoOut);
        AssertEq(3, stateAfterBuy.TotalQuantity);
        // Second buy of 2 shares
        var buy2 = new Event(T0 + 1 * D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1);
        stateAfterBuy = Instance.ProcessBuy(buy2, [buy1, buy2], 1, stateAfterBuy, NoOut);
        AssertEq(5, stateAfterBuy.TotalQuantity);
        // Third buy of 2.5 shares
        var buy3 = new Event(T0 + 2 * D, BuyLimit, Ticker, 2.5m, 90, 227.5m, 2.5m, EUR, 1);
        stateAfterBuy = Instance.ProcessBuy(buy3, [buy1, buy2, buy3], 2, stateAfterBuy, NoOut);
        AssertEq(7.5m, stateAfterBuy.TotalQuantity);
    }

    [TestMethod]
    public void ProcessBuy_IncreasesTotalAmountBase_BySharesAmountPlusFees()
    {
        var state = new TickerState(Ticker, Isin);
        // First buy of 3 shares at 100 EUR, with fees of 3 EUR => Total Amount Local of 303 EUR
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1);
        var stateAfterBuy = Instance.ProcessBuy(buy1, [buy1], 0, state, NoOut);
        AssertEq(303, stateAfterBuy.TotalAmountBase);
        // Second buy of 2 shares at 110 EUR, with fees of 2 EUR => Total Amount Local of 222 EUR
        var buy2 = new Event(T0 + 1 * D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1);
        stateAfterBuy = Instance.ProcessBuy(buy2, [buy1, buy2], 1, stateAfterBuy, NoOut);
        AssertEq(525, stateAfterBuy.TotalAmountBase);
        // Third buy of 2.5 shares at 90 EUR, with fees of 2.5 EUR => Total Amount Local of 227.5 EUR
        var buy3 = new Event(T0 + 2 * D, BuyLimit, Ticker, 2.5m, 90, 227.5m, 2.5m, EUR, 1);
        stateAfterBuy = Instance.ProcessBuy(buy3, [buy1, buy2, buy3], 2, stateAfterBuy, NoOut);
        AssertEq(752.5m, stateAfterBuy.TotalAmountBase);
    }

    [TestMethod]
    public void ProcessBuy_CalculatesAndPrintsSteps()
    {
        var tickerProcessing = new TickerProcessing(new Basics() { Rounding = x => decimal.Round(x, 2) });
        var writer = new StringWriter();
        var localCurrency = USD; // FX Rate is 2 USD for 1 EUR
        var initialState = new TickerState(Ticker, Isin);

        // First buy 3 shares at 100.10002 USD, with fees of 3.20003 USD => Total Amount Local of 300.30006 USD + 3.20003 USD = 303.50009 USD
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100.10002m, 303.50009m, 3.20003m, localCurrency, 2m);
        tickerProcessing.ProcessBuy(buy1, [buy1], 0, initialState, writer);
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
        var cashWithdrawal1 = new Event(T0 + 0 * D, CashWithdrawal, Ticker, null, null, 100, null, EUR, 1);
        var sellLimit1 = new Event(T0 + 0 * D, SellLimit, Ticker, 3, 100, 303, 3, EUR, 1);
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sellLimit1, [cashWithdrawal1, sellLimit1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_WhenPassingNotSupportedType_RaisesException()
    {
        // Custody fees for 12 EUR
        var custodyFee1 = new Event(T0 + 0 * D, CustodyFee, Ticker, 0, 0, 0, 12.0m, EUR, 1);
        // While owning no shares
        var state = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessSell(custodyFee1, [custodyFee1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_WhenTickerNameIsNull_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var sell1 = new Event(T0 + 0 * D, SellLimit, null, 1, 2.0m, 1.8m, 0.2m, EUR, 1);
        // While owning 3 shares for a total of 5.5 EUR
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [sell1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_WhenTotalAmountLocalIsNonPositive_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 0 EUR
        var sell1 = new Event(T0 + 0 * D, SellLimit, Ticker, 1, 2.0m, 0m, 0.2m, EUR, 1);
        // While owning 3 shares for a total of 5.5 EUR
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [sell1], 0, state, NoOut));
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of -1 EUR
        sell1 = new Event(T0 + 0 * D, SellLimit, Ticker, 1, 2.0m, -1m, 0.2m, EUR, 1);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [sell1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_WhenFeesLocalIsNull_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of NULL EUR => Total Amount Local of 1.8 EUR
        var sell1 = new Event(T0 + 0 * D, SellLimit, Ticker, 1, 2.0m, 1.8m, null, EUR, 1);
        // While owning 3 shares for a total of 5.5 EUR
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [sell1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_WhenTickersMismatch_RaisesException()
    {
        // Selling 1 share of A at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var sell1 = new Event(T0 + 0 * D, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, EUR, 1);
        // While owning 3 shares of B for a total of 5.5 EUR
        var state = new TickerState(AnotherTicker, AnotherIsin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [sell1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_WhenPricePerShareLocalIsNull_RaisesException()
    {
        // Selling 1 share at NULL, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var sell1 = new Event(T0 + 0 * D, SellLimit, Ticker, 1, null, 1.8m, 0.2m, EUR, 1);
        // While owning 3 shares for a total of 5.5 EUR
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [sell1], 0, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_WhenQuantityIsNull_RaisesException()
    {
        // Selling NULL shares at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var sell1 = new Event(T0 + 0 * D, SellLimit, Ticker, null, 2.0m, 1.8m, 0.2m, EUR, 1);
        // While owning 3 shares for a total of 5.5 EUR
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [sell1], 0, state, NoOut));

    }

    [TestMethod]
    public void ProcessSell_WhenCurrenciesAreEtherogenous_RaisesException()
    {
        // While owning 3 shares for a total of 5.5 EUR
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 0, 0, 0, 0, EUR, 1);
        // Selling 1 share at 2.0 USD, with fees of 0.2 USD => Total Amount Local of 1.8 USD
        var sell1 = new Event(T0 + 0 * D, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, USD, 1);
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [buy1, sell1], 1, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_WhenSellingMoreThanOwned_RaisesException()
    {
        // While owning 3 shares for a total of 5.5 EUR
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 0, 0, 0, EUR, 1);
        // Selling 4 shares at 2.0 EUR, with fees of 0.8 EUR => Total Amount Local of 7.2 EUR
        var sell1 = new Event(T0 + 0 * D, SellLimit, Ticker, 4, 8.0m, 7.2m, 0.8m, EUR, 1);
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [buy1, sell1], 1, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_WhenFeesAreInconsistent_RaisesException()
    {
        // While owning 3 shares for a total of 5.5 EUR
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 2, 5.5m, 0.5m, EUR, 1);
        // Selling 1 share at 2.0 EUR, with fees of 0.4 EUR => Total Amount Local of 1.7 EUR (fees should be 0.3 EUR)
        var sell1 = new Event(T0 + 0 * D, SellLimit, Ticker, 1, 2.0m, 1.7m, 0.4m, EUR, 1);
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [buy1, sell1], 1, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_WhenPastBuyEventDoesntHaveQuantity_RaisesException()
    {
        // While owning 3 shares for a total of 5.5 EUR
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, null, 2, 5.5m, 0.5m, EUR, 1);
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var sell1 = new Event(T0 + 0 * D, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, EUR, 1);
        var state = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(sell1, [buy1, sell1], 1, state, NoOut));
    }

    [TestMethod]
    public void ProcessSell_CalculatesAndPrintsSteps()
    {
        var tickerProcessing = new TickerProcessing(new Basics() { Rounding = x => decimal.Round(x, 2) });
        var localCurrency = USD; // FX rate between USD and EUR stays stable at 4 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin);
        
        // First buy 3 shares at 100.10002 USD, with fees of 3.20003 USD => Total Amount Local of 303.50009 USD
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100.10002m, 303.50009m, 3.20003m, localCurrency, 4m);
        var stateAfterBuy = tickerProcessing.ProcessBuy(buy1, [buy1], 0, initialState, NoOut);
        // Then sell 2 shares at 150.15003 USD, with fees of 2.50005 USD => Total Amount Local of 300.30006 USD - 2.50005 USD = 297.80001 USD
        var sell1 = new Event(T0 + 1 * D, SellLimit, Ticker, 2, 150.15003m, 297.80001m, 2.50005m, localCurrency, 4m);
        var writer = new StringWriter();
        tickerProcessing.ProcessSell(sell1, [buy1, sell1], 1, stateAfterBuy, writer);
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
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 3, 100m, 303.20m, 3.20m, localCurrency, 4m);
        var stateAfterBuy1 = tickerProcessing.ProcessBuy(
            buy1, [buy1], 0, initialState, NoOut);
        
        AssertStateAfterBuy1();

        void AssertStateAfterBuy1()
        {
            // 3 shares available
            AssertEq(3, stateAfterBuy1.TotalQuantity);
            // The total amount corresponds to the total amount of the buy event 1
            AssertEq(303.20m / 4m, stateAfterBuy1.TotalAmountBase);
        }

        // -------
        // Then sell 2 shares at 150 USD, with fees of 2.50 USD => Total Amount Local of 300 USD - 2.50 USD = 297.50 USD
        var sell1 = new Event(T0 + 1 * D, SellLimit, Ticker, 2, 150m, 297.50m, 2.50m, localCurrency, 4m);
        var stateAfterSell1 = tickerProcessing.ProcessSell(
            sell1, [buy1, sell1], 1, stateAfterBuy1, NoOut);

        AssertStateAfterSell1();

        void AssertStateAfterSell1()
        {
            // Only 1 share left
            AssertEq(1, stateAfterSell1.TotalQuantity);
            // The share left (1 remaining out of 3 shares) has the same average buy price as before
            var totalAmountBaseAfterSell1 = stateAfterBuy1.TotalAmountBase * (1m / 3m);
            AssertEq(totalAmountBaseAfterSell1, stateAfterSell1.TotalAmountBase);

            // The plus value CUMP is the difference between the sell price and the average buy price for the 2 shares sold
            // The minus value CUMP is 0, since no minus value has been realized
            var averageBuyPriceTwoShares = totalAmountBaseAfterSell1 * 2;
            var plusValueCumpSell1 = 297.50m / 4m - averageBuyPriceTwoShares;
            AssertEq(plusValueCumpSell1, stateAfterSell1.PlusValueCumpBase);
            AssertEq(0, stateAfterSell1.MinusValueCumpBase);

            // The plus value PEPS is the difference between the sell price and the buy price of the first two shares bought
            // The minus value PEPS is 0, since no minus value has been realized
            var buyPriceFirstTwoShares = (2 * 303.20m / 3) / 4m;
            var plusValuePepsSell1 = 297.50m / 4m - buyPriceFirstTwoShares;
            AssertEq(plusValuePepsSell1, stateAfterSell1.PlusValuePepsBase);
            AssertEq(0, stateAfterSell1.MinusValuePepsBase);

            // The PEPS current index is 0, since only 2 shares out of 3 have been sold
            AssertEq(0, stateAfterSell1.PepsCurrentIndex);
            // The PEPS current index sold quantity is 2, since 2 shares out of 3 have been sold
            AssertEq(2, stateAfterSell1.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Buy 3 shares at 110 USD, with fees of 4 USD => Total Amount Local of 330 USD + 4 USD = 334 USD
        var buy2 = new Event(T0 + 2 * D, BuyLimit, Ticker, 3, 110m, 334m, 4m, localCurrency, 4m);
        var stateAfterBuy2 = tickerProcessing.ProcessBuy(
            buy2, [buy1, sell1, buy2], 2, stateAfterSell1, NoOut);

        AssertStateAfterBuy2();

        void AssertStateAfterBuy2()
        {
            // The total quantity is increased by 3
            AssertEq(4, stateAfterBuy2.TotalQuantity);
            // The total amount is increased by the total amount of the buy event 2
            var totalAmountBaseAfterBuy2 = stateAfterSell1.TotalAmountBase + 334m / 4m;
            AssertEq(totalAmountBaseAfterBuy2, stateAfterBuy2.TotalAmountBase);
        }

        // -------
        // Buy 2 shares at 120 USD, with fees of 3 USD => Total Amount Local of 240 USD + 3 USD = 243 USD
        var buy3 = new Event(T0 + 3 * D, BuyLimit, Ticker, 2, 120m, 243m, 3m, localCurrency, 4m);
        var stateAfterBuy3 = tickerProcessing.ProcessBuy(
            buy3, [buy1, sell1, buy2, buy3], 3, stateAfterBuy2, NoOut);

        AssertStateAfterBuy3();

        void AssertStateAfterBuy3()
        {
            // The total quantity is increased by 2
            AssertEq(6, stateAfterBuy3.TotalQuantity);
            // The total amount is increased by the total amount of the buy event 3
            var totalAmountBaseAfterBuy3 = stateAfterBuy2.TotalAmountBase + 243m / 4m;
            AssertEq(totalAmountBaseAfterBuy3, stateAfterBuy3.TotalAmountBase);
        }

        // -------
        // Sell 3 shares at 130 USD, with fees of 3 USD => Total Amount Local of 390 USD - 3 USD = 387 USD
        var sell2 = new Event(T0 + 4 * D, SellLimit, Ticker, 3, 130m, 387m, 3m, localCurrency, 4m);
        var stateAfterSell2 = tickerProcessing.ProcessSell(
            sell2, [buy1, sell1, buy2, buy3, sell2], 4, stateAfterBuy3, NoOut);

        AssertStateAfterSell2();

        void AssertStateAfterSell2()
        {
            // Only 3 shares left
            AssertEq(3, stateAfterSell2.TotalQuantity);
            // The total amount is decreased by half (3 remaining out of 6 shares), and not by the total amount of the sell event 2
            var totalAmountBaseAfterSell2 = stateAfterBuy3.TotalAmountBase * (3m / 6m);
            AssertEq(totalAmountBaseAfterSell2, stateAfterSell2.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 3 shares sold
            // This value is added to the plus value CUMP accumulated so far (first sell event)
            // The minus value CUMP is 0, since no minus value has been realized
            var averageBuyPriceThreeShares = stateAfterBuy3.TotalAmountBase - totalAmountBaseAfterSell2;
            var plusValueCumpSell2 = 387m / 4m - averageBuyPriceThreeShares;
            AssertEq(stateAfterBuy3.PlusValueCumpBase + plusValueCumpSell2, stateAfterSell2.PlusValueCumpBase);
            AssertEq(0, stateAfterSell2.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the oldest 3 shares bought,
            // among the 6 shares left after the third buy event:
            // - oldest share bought at 303.20 USD / 3 shares (local currency)
            // - next 3 shares bought at 334 USD / 3 shares (local currency)
            // - last 2 shares bought at 243 USD / 2 shares (local currency)
            // This value is added to the plus value PEPS accumulated so far (first sell event)
            // The minus value PEPS is 0, since no minus value has been realized
            var buyPriceFirstThreeShares = 1 * 303.20m / 3m / 4m + 2 * 334m / 3m / 4m;
            var plusValuePepsSell2 = 387m / 4m - buyPriceFirstThreeShares;
            AssertEq(stateAfterBuy3.PlusValuePepsBase + plusValuePepsSell2, stateAfterSell2.PlusValuePepsBase);
            AssertEq(0, stateAfterSell2.MinusValuePepsBase);

            // The PEPS current index is 2, since the only remaining share of the first buy has been sold, together with
            // the first two shares of the second buy, where the last share is left not sold for now
            AssertEq(2, stateAfterSell2.PepsCurrentIndex);
            // The PEPS current index sold quantity is 2, since 2 shares out of 3 have been sold
            AssertEq(2, stateAfterSell2.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Sell 1 shares at 10 USD, with fees of 1 USD => Total Amount Local of 10 USD - 1 USD = 9 USD
        var sell3 = new Event(T0 + 5 * D, SellLimit, Ticker, 1, 10m, 9m, 1m, localCurrency, 4m);
        var stateAfterSell3 = tickerProcessing.ProcessSell(
            sell3, [buy1, sell1, buy2, buy3, sell2, sell3], 5, stateAfterSell2, NoOut);

        AssertStateAfterSell3();

        void AssertStateAfterSell3()
        {
            // Only 2 shares left
            AssertEq(2, stateAfterSell3.TotalQuantity);
            // The total amount is decreased to 2/3 (2 remaining out of 3 shares), and not by the total amount of the sell event 3
            var totalAmountBaseAfterSell3 = stateAfterSell2.TotalAmountBase * (2m / 3m);
            AssertEq(totalAmountBaseAfterSell3, stateAfterSell3.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 1 share sold
            // This value is negative, so it is actually a minus value, taken with reversed sign
            // This value is the first CUMP minus value realized. The plus value CUMP doesn't change.
            var averageBuyPriceOneShare = stateAfterSell2.TotalAmountBase - totalAmountBaseAfterSell3;
            var minusValueCumpSell3 = averageBuyPriceOneShare - 9m / 4m;
            AssertEq(stateAfterSell3.PlusValueCumpBase, stateAfterSell2.PlusValueCumpBase);
            AssertEq(minusValueCumpSell3, stateAfterSell3.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the oldest share bought,
            // among the 3 shares left after the third sell event:
            // - oldest share bought at 334 USD / 3 shares (local currency)
            // - last 2 shares bought at 243 USD / 2 shares (local currency)
            // This value is negative, so it is actually a minus value, taken with reversed sign.
            // This value is the first PEPS minus value realized. The plus value PEPS doesn't change.
            var buyPriceOldestShare = 334m / 3m / 4m;
            var minusValuePepsSell3 = buyPriceOldestShare - 9m / 4m;
            AssertEq(stateAfterSell3.PlusValuePepsBase, stateAfterSell2.PlusValuePepsBase);
            AssertEq(minusValuePepsSell3, stateAfterSell3.MinusValuePepsBase);

            // The PEPS current index is 3, since the last share of the second buy has been sold, and the pointer moves
            // forward to the next buy event, that is in position 3 in the list of events
            AssertEq(3, stateAfterSell3.PepsCurrentIndex);
            // The PEPS current index sold quantity is 0, since the current index has been moved forward
            AssertEq(0, stateAfterSell3.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Sell 2 shares at 10 USD, with fees of 1 USD => Total Amount Local of 20 USD - 1 USD = 19 USD
        var sell4 = new Event(T0 + 6 * D, SellLimit, Ticker, 2, 10m, 19m, 1m, localCurrency, 4m);
        var stateAfterSell4 = tickerProcessing.ProcessSell(
            sell4, [buy1, sell1, buy2, buy3, sell2, sell3, sell4], 6, stateAfterSell3, NoOut);

        AssertStateAfterSell4();

        void AssertStateAfterSell4()
        {
            // No shares left
            AssertEq(0, stateAfterSell4.TotalQuantity);
            // The total amount is decreased to 0 (0 remaining out of 2 shares), and not by the total amount of the sell event 4
            var totalAmountBaseAfterSell4 = stateAfterSell3.TotalAmountBase * (0m / 2m);
            AssertEq(totalAmountBaseAfterSell4, stateAfterSell4.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 2 shares sold
            // This value is negative, so it is actually a minus value, taken with reversed sign
            // This value is the second CUMP minus value realized. The plus value CUMP doesn't change.
            var averageBuyPriceTwoSharesSell4 = stateAfterSell3.TotalAmountBase - totalAmountBaseAfterSell4;
            var minusValueCumpSell4 = averageBuyPriceTwoSharesSell4 - 19m / 4m;
            AssertEq(stateAfterSell3.PlusValueCumpBase, stateAfterSell4.PlusValueCumpBase);
            AssertEq(stateAfterSell3.MinusValueCumpBase + minusValueCumpSell4, stateAfterSell4.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the two
            // oldest share bought, that are the last two shares remaining
            // This value is negative, so it is actually a minus value, taken with reversed sign
            // This value is the second PEPS minus value realized. The plus value PEPS doesn't change.
            var buyPriceTwoOldestShares = 243m / 4m;
            var minusValuePepsSell4 = buyPriceTwoOldestShares - 19m / 4m;
            AssertEq(stateAfterSell3.PlusValuePepsBase, stateAfterSell4.PlusValuePepsBase);
            AssertEq(stateAfterSell3.MinusValuePepsBase + minusValuePepsSell4, stateAfterSell4.MinusValuePepsBase);

            // The PEPS current index is 4, since the last two shares of the third buy have been sold, and the pointer moves
            // to the next buy event, that is not present -> moved after the end of the list of events
            AssertEq(7, stateAfterSell4.PepsCurrentIndex);
            // The PEPS current index sold quantity is 0, since the current index has been moved forward
            AssertEq(0, stateAfterSell4.PepsCurrentIndexSoldQuantity);

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
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 10, 100m, 1020m, 20m, localCurrency, 2m);
        var stateAfterBuy1 = tickerProcessing.ProcessBuy(
            buy1, [buy1], 0, initialState, NoOut);

        // --------
        // Sell 5 shares at 150 USD, with fees of 10 USD => Total Amount Local of 750 USD - 10 USD = 740 USD
        var sell1 = new Event(T0 + 1 * D, SellLimit, Ticker, 5, 150m, 740m, 10m, localCurrency, 2m);
        var stateAfterSell1 = tickerProcessing.ProcessSell(
            sell1, [buy1, sell1], 1, stateAfterBuy1, NoOut);

        AssertStateAfterSell1();

        void AssertStateAfterSell1() {
            // 5 shares left
            AssertEq(5, stateAfterSell1.TotalQuantity);
            // The total amount is decreased by half (5 remaining out of 10 shares), and not by the total amount of the sell event 1
            var totalAmountBaseAfterSell1 = stateAfterBuy1.TotalAmountBase * (5m / 10m);
            AssertEq(totalAmountBaseAfterSell1, stateAfterSell1.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 5 shares sold
            // The minus value CUMP is 0, since no minus value has been realized
            var averageBuyPriceFiveShares = totalAmountBaseAfterSell1;
            var plusValueCumpSell1 = 740m / 2m - averageBuyPriceFiveShares;
            AssertEq(plusValueCumpSell1, stateAfterSell1.PlusValueCumpBase);
            AssertEq(0, stateAfterSell1.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the first 5 shares bought
            // The minus value PEPS is 0, since no minus value has been realized
            var buyPriceFirstFiveShares = 1020m / 2m / 2m;
            var plusValuePepsSell1 = 740m / 2m - buyPriceFirstFiveShares;
            AssertEq(plusValuePepsSell1, stateAfterSell1.PlusValuePepsBase);
            AssertEq(0, stateAfterSell1.MinusValuePepsBase);

            // The PEPS current index is 0, since only 5 shares out of 10 have been sold
            AssertEq(0, stateAfterSell1.PepsCurrentIndex);
            // The PEPS current index sold quantity is 5, since 5 shares out of 10 have been sold
            AssertEq(5, stateAfterSell1.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Reset the ticker state
        var reset1 = new Event(T0 + 2 * D, Reset, Ticker, null, null, 0, null, localCurrency, 2m);
        var stateAfterReset1 = tickerProcessing.ProcessReset(
            reset1, [buy1, sell1, reset1], 2, stateAfterSell1, NoOut);

        AssertStateAfterReset1();

        void AssertStateAfterReset1() {
            // The total quantity and amount are the same as before the reset
            AssertEq(stateAfterSell1.TotalQuantity, stateAfterReset1.TotalQuantity);
            AssertEq(stateAfterSell1.TotalAmountBase, stateAfterReset1.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto are reset to 0
            AssertEq(0, stateAfterReset1.PlusValueCumpBase);
            AssertEq(0, stateAfterReset1.MinusValueCumpBase);
            AssertEq(0, stateAfterReset1.PlusValuePepsBase);
            AssertEq(0, stateAfterReset1.MinusValuePepsBase);
            AssertEq(0, stateAfterReset1.PlusValueCryptoBase);
            AssertEq(0, stateAfterReset1.MinusValueCryptoBase);

            // The PEPS current index and sold quantity are the same as before the reset
            AssertEq(stateAfterSell1.PepsCurrentIndex, stateAfterReset1.PepsCurrentIndex);
            AssertEq(stateAfterSell1.PepsCurrentIndexSoldQuantity, stateAfterReset1.PepsCurrentIndexSoldQuantity);
        }

        // -------
        // Sell 2 shares at 80 USD, with fees of 6 USD => Total Amount Local of 160 USD - 6 USD = 154 USD
        var sell2 = new Event(T0 + 3 * D, SellLimit, Ticker, 2, 80m, 154m, 6m, localCurrency, 2m);
        var stateAfterSell2 = tickerProcessing.ProcessSell(
            sell2, [buy1, sell1, reset1, sell2], 3, stateAfterReset1, NoOut);

        AssertStateAfterSell2();

        void AssertStateAfterSell2() {
            // 3 shares left
            AssertEq(3, stateAfterSell2.TotalQuantity);
            // The total amount is decreased to 3/5 (2 shares out of 5 sold), and not by the total amount of the sell event 2
            var totalAmountBaseAfterSell2 = stateAfterSell1.TotalAmountBase * (3m / 5m);
            AssertEq(totalAmountBaseAfterSell2, stateAfterSell2.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 2 shares sold
            // This value is negative, so it is actually a minus value, taken with reversed sign
            // This value is the first CUMP minus value realized. The plus value CUMP doesn't change.
            var averageBuyPriceTwoSharesSell2 = stateAfterReset1.TotalAmountBase - totalAmountBaseAfterSell2;
            var minusValueCumpSell2 = averageBuyPriceTwoSharesSell2 - 154m / 2m;
            AssertEq(0, stateAfterSell2.PlusValueCumpBase);
            AssertEq(minusValueCumpSell2, stateAfterSell2.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the oldest
            // 2 shares bought:
            // - oldest 2 shares bought at 1020 USD / 10 shares (local currency)
            // This value is negative, so it is actually a minus value, taken with reversed sign
            // This value is the first PEPS minus value realized. The plus value PEPS doesn't change.
            var buyPriceOldestTwoShares = 2 * 1020m / 10m / 2m;
            var minusValuePepsSell2 = buyPriceOldestTwoShares - 154m / 2m;
            AssertEq(0, stateAfterSell2.PlusValuePepsBase);
            AssertEq(minusValuePepsSell2, stateAfterSell2.MinusValuePepsBase);

            // The PEPS current index is 0, since there are still 3 shares left of the first buy that have not been sold
            AssertEq(0, stateAfterSell2.PepsCurrentIndex);
            // The PEPS current index sold quantity is 7, since 7 shares out of 10 have been sold
            AssertEq(7, stateAfterSell2.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Buy 1 share at 100 USD, with fees of 2 USD => Total Amount Local of 100 USD + 2 USD = 102 USD
        var buy2 = new Event(T0 + 4 * D, BuyLimit, Ticker, 1, 100m, 102m, 2m, localCurrency, 2m);
        var stateAfterBuy2 = tickerProcessing.ProcessBuy(
            buy2, [buy1, sell1, reset1, sell2, buy2], 4, stateAfterSell2, NoOut);

        // Buy 2 shares at 110 USD, with fees of 4 USD => Total Amount Local of 220 USD + 4 USD = 224 USD
        var buy3 = new Event(T0 + 5 * D, BuyLimit, Ticker, 2, 110m, 224m, 4m, localCurrency, 2m);
        var stateAfterBuy3 = tickerProcessing.ProcessBuy(
            buy3, [buy1, sell1, reset1, sell2, buy2, buy3], 5, 
            stateAfterBuy2, NoOut);

        // Cash withdrawal of 50 USD, with fees of 1 USD => Total Amount Local of 49 USD
        var cashWithdrawal1 = new Event(T0 + 6 * D, CashWithdrawal, Ticker, null, null, 49m, 1m, localCurrency, 2m);
        var stateAfterCashWithdrawal1 = tickerProcessing.ProcessNoop(
            cashWithdrawal1, [buy1, sell1, reset1, sell2, buy2, buy3, cashWithdrawal1], 6, stateAfterBuy3, NoOut);

        // Sell 4 shares at 120 USD, with fees of 8 USD => Total Amount Local of 480 USD - 8 USD = 472 USD
        var sell3 = new Event(T0 + 7 * D, SellLimit, Ticker, 4, 120m, 472m, 8m, localCurrency, 2m);
        var stateAfterSell3 = tickerProcessing.ProcessSell(
            sell3, [buy1, sell1, reset1, sell2, buy2, buy3, cashWithdrawal1, sell3], 7, stateAfterCashWithdrawal1, NoOut);

        AssertStateAfterSell3();

        void AssertStateAfterSell3() 
        {
            // 2 shares left
            AssertEq(2, stateAfterSell3.TotalQuantity);
            // The total amount is decreased to 2/6 (2 remaining out of 6 shares), and not by the total amount of the sell event 3
            var totalAmountBaseAfterSell3 = stateAfterCashWithdrawal1.TotalAmountBase * (2m / 6m);
            AssertEq(totalAmountBaseAfterSell3, stateAfterSell3.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 4 shares sold
            // The minus value CUMP remains the same as before, since no new minus value has been realized
            var averageBuyPriceFourSharesSell3 = stateAfterCashWithdrawal1.TotalAmountBase - totalAmountBaseAfterSell3;
            var plusValueCumpSell3 = 472m / 2m - averageBuyPriceFourSharesSell3;
            AssertEq(plusValueCumpSell3, stateAfterSell3.PlusValueCumpBase);
            AssertEq(stateAfterCashWithdrawal1.MinusValueCumpBase, stateAfterSell3.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the oldest 4 shares bought:
            // - oldest 3 shares bought at 1020 USD / 10 shares (local currency)
            // - next 1 share bought at 102 USD / 2 shares (local currency)
            // - last 2 shares bought at 224 USD / 2 shares (local currency)
            // The minus value PEPS remains the same as before, since no new minus value has been realized
            var buyPriceOldestFourShares = 3 * 1020m / 10m / 2m + 1 * 102m / 1m / 2m;
            var plusValuePepsSell3 = 472m / 2m - buyPriceOldestFourShares;
            AssertEq(plusValuePepsSell3, stateAfterSell3.PlusValuePepsBase);
            AssertEq(stateAfterCashWithdrawal1.MinusValuePepsBase, stateAfterSell3.MinusValuePepsBase);

            // The PEPS current index is 5, since all the 3 oldest shares, together with the next 1 share, have been sold,
            // and the pointer moves to the next buy event (last 2 shares bought), that is in position 5 in the list of events
            AssertEq(5, stateAfterSell3.PepsCurrentIndex);
            // The PEPS current index sold quantity is 0, since 0 shares out of 2 have been sold
            AssertEq(0, stateAfterSell3.PepsCurrentIndexSoldQuantity);

            // TODO: calculate the plus value and minus value crypto
        }

        // -------
        // Reset the ticker state
        var resetEvent2 = new Event(T0 + 8 * D, Reset, Ticker, null, null, 0, null, localCurrency, 2m);
        var stateAfterReset2 = tickerProcessing.ProcessReset(
            resetEvent2, [buy1, sell1, reset1, sell2, buy2, buy3, cashWithdrawal1, sell3, resetEvent2], 8, stateAfterSell3, NoOut);

        AssertStateAfterReset2();

        void AssertStateAfterReset2()
        {
            // The total quantity and amount are the same as before the reset
            AssertEq(stateAfterSell3.TotalQuantity, stateAfterReset2.TotalQuantity);
            AssertEq(stateAfterSell3.TotalAmountBase, stateAfterReset2.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto are reset to 0
            AssertEq(0, stateAfterReset2.PlusValueCumpBase);
            AssertEq(0, stateAfterReset2.MinusValueCumpBase);
            AssertEq(0, stateAfterReset2.PlusValuePepsBase);
            AssertEq(0, stateAfterReset2.MinusValuePepsBase);
            AssertEq(0, stateAfterReset2.PlusValueCryptoBase);
            AssertEq(0, stateAfterReset2.MinusValueCryptoBase);

            // The PEPS current index and sold quantity are the same as before the reset
            AssertEq(stateAfterSell3.PepsCurrentIndex, stateAfterReset2.PepsCurrentIndex);
            AssertEq(stateAfterSell3.PepsCurrentIndexSoldQuantity, stateAfterReset2.PepsCurrentIndexSoldQuantity);
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
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 10, 100m, 1020m, 20m, localCurrency, 2m);
        events.Add(buy1);
        var stateAfterBuy1 = tickerProcessing.ProcessBuy(
            buy1, events, 0, initialState, NoOut);

        // --------
        // Sell 5 shares at 150 USD, with fees of 10 USD => Total Amount Local of 750 USD - 10 USD = 740 USD
        var sell1 = new Event(T0 + 1 * D, SellLimit, Ticker, 5, 150m, 740m, 10m, localCurrency, 2m);
        events.Add(sell1);
        var stateAfterSell1 = tickerProcessing.ProcessSell(
            sell1, events, 1, stateAfterBuy1, NoOut);

        AssertStateAfterSell1();

        void AssertStateAfterSell1()
        {
            // 5 shares left
            AssertEq(5, stateAfterSell1.TotalQuantity);
            // The total amount is decreased by half (5 remaining out of 10 shares), and not by the total amount of the sell event 1
            var totalAmountBaseAfterSell1 = stateAfterBuy1.TotalAmountBase * (5m / 10m);
            AssertEq(totalAmountBaseAfterSell1, stateAfterSell1.TotalAmountBase);

            // The plus value CUMP for the event is the difference between the sell price and the average buy price for the 5 shares sold
            // The minus value CUMP is 0, since no minus value has been realized
            var averageBuyPriceFiveShares = totalAmountBaseAfterSell1;
            var plusValueCumpSell1 = 740m / 2m - averageBuyPriceFiveShares;
            AssertEq(plusValueCumpSell1, stateAfterSell1.PlusValueCumpBase);
            AssertEq(0, stateAfterSell1.MinusValueCumpBase);

            // The plus value PEPS for the event is the difference between the sell price and the buy price of the first 5 shares bought
            // The minus value PEPS is 0, since no minus value has been realized
            var buyPriceFirstFiveShares = 1020m / 2m / 2m;
            var plusValuePepsSell1 = 740m / 2m - buyPriceFirstFiveShares;
            AssertEq(plusValuePepsSell1, stateAfterSell1.PlusValuePepsBase);
            AssertEq(0, stateAfterSell1.MinusValuePepsBase);

            // The PEPS current index is 0, since only 5 shares out of 10 have been sold
            AssertEq(0, stateAfterSell1.PepsCurrentIndex);
            // The PEPS current index sold quantity is 5, since 5 shares out of 10 have been sold
            AssertEq(5, stateAfterSell1.PepsCurrentIndexSoldQuantity);
        }

        // -------
        // Stock split of 1:3, starting from 5 shares => 10 additional shares
        var stockSplit1 = new Event(T0 + 2 * D, StockSplit, Ticker, 10m, null, 0, null, localCurrency, 2m);
        events.Add(stockSplit1);
        var stateAfterStockSplit1 = tickerProcessing.ProcessStockSplit(
            stockSplit1, events, 2, stateAfterSell1, NoOut);

        AssertStateAfterStockSplit1();

        void AssertStateAfterStockSplit1()
        {
            // The total quantity is increased by 10
            AssertEq(15, stateAfterStockSplit1.TotalQuantity);
            // The total amount remains the same
            AssertEq(stateAfterSell1.TotalAmountBase, stateAfterStockSplit1.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto remain the same
            AssertEq(stateAfterSell1.PlusValueCumpBase, stateAfterStockSplit1.PlusValueCumpBase);
            AssertEq(stateAfterSell1.MinusValueCumpBase, stateAfterStockSplit1.MinusValueCumpBase);
            AssertEq(stateAfterSell1.PlusValuePepsBase, stateAfterStockSplit1.PlusValuePepsBase);
            AssertEq(stateAfterSell1.MinusValuePepsBase, stateAfterStockSplit1.MinusValuePepsBase);
            AssertEq(stateAfterSell1.PlusValueCryptoBase, stateAfterStockSplit1.PlusValueCryptoBase);
            AssertEq(stateAfterSell1.MinusValueCryptoBase, stateAfterStockSplit1.MinusValueCryptoBase);

            // The PEPS current index remains unchanged, but the PEPS current index sold quantity is multiplied by 3
            AssertEq(0, stateAfterStockSplit1.PepsCurrentIndex);
            AssertEq(stateAfterSell1.PepsCurrentIndexSoldQuantity * 3m, stateAfterStockSplit1.PepsCurrentIndexSoldQuantity);

            // Previous Buy and Sell stock events are retroactively updated in the list of events, to take into account the 1:3 stock split
            var modifiedBuy1 = events[0];
            AssertEq(10 * 3m, modifiedBuy1.Quantity); // 10 shares bought, now 30 shares
            AssertEq(100m / 3m, modifiedBuy1.PricePerShareLocal); // Buy price per share is divided by 3
            AssertEq(1020m, modifiedBuy1.TotalAmountLocal); // Remains the same
            AssertEq(20m, modifiedBuy1.FeesLocal); // Remains the same
            AssertEq(2m, modifiedBuy1.FXRate); // Remains the same

            var modifiedSell1 = events[1];
            AssertEq(5 * 3m, modifiedSell1.Quantity); // 5 shares sold, now 15 shares
            AssertEq(150m / 3m, modifiedSell1.PricePerShareLocal); // Sell price per share is divided by 3
            AssertEq(740m, modifiedSell1.TotalAmountLocal); // Remains the same
            AssertEq(10m, modifiedSell1.FeesLocal); // Remains the same
            AssertEq(2m, modifiedSell1.FXRate); // Remains the same

            // The original Buy and Sell stock events are not modified
            AssertEq(10, buy1.Quantity);
            AssertEq(100m, buy1.PricePerShareLocal);
            AssertEq(5, sell1.Quantity);
            AssertEq(150m, sell1.PricePerShareLocal);
        }

        // -------
        // Stock split of 1:4, starting from 15 shares => 45 additional shares
        var stockSplit2 = new Event(T0 + 3 * D, StockSplit, Ticker, 45m, null, 0, null, localCurrency, 2m);
        events.Add(stockSplit2);
        var stateAfterStockSplit2 = tickerProcessing.ProcessStockSplit(
            stockSplit2, events, 3, stateAfterStockSplit1, NoOut);

        AssertStateAfterStockSplit2();

        void AssertStateAfterStockSplit2()
        {
            // The total quantity is increased by 45
            AssertEq(60, stateAfterStockSplit2.TotalQuantity);
            // The total amount remains the same
            AssertEq(stateAfterStockSplit1.TotalAmountBase, stateAfterStockSplit2.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto remain the same
            AssertEq(stateAfterStockSplit1.PlusValueCumpBase, stateAfterStockSplit2.PlusValueCumpBase);
            AssertEq(stateAfterStockSplit1.MinusValueCumpBase, stateAfterStockSplit2.MinusValueCumpBase);
            AssertEq(stateAfterStockSplit1.PlusValuePepsBase, stateAfterStockSplit2.PlusValuePepsBase);
            AssertEq(stateAfterStockSplit1.MinusValuePepsBase, stateAfterStockSplit2.MinusValuePepsBase);
            AssertEq(stateAfterStockSplit1.PlusValueCryptoBase, stateAfterStockSplit2.PlusValueCryptoBase);
            AssertEq(stateAfterStockSplit1.MinusValueCryptoBase, stateAfterStockSplit2.MinusValueCryptoBase);

            // The PEPS current index remains unchanged, but the PEPS current index sold quantity is multiplied by 4
            AssertEq(0, stateAfterStockSplit2.PepsCurrentIndex);
            AssertEq(stateAfterStockSplit1.PepsCurrentIndexSoldQuantity * 4m, stateAfterStockSplit2.PepsCurrentIndexSoldQuantity);

            // Previous Buy and Sell stock events are retroactively updated in the list of events, to take into account the 1:4 stock split
            var modifiedBuy1 = events[0];
            AssertEq(10 * 3m * 4m, modifiedBuy1.Quantity); // 30 shares bought, now 120 shares
            AssertEq(100m / 3m / 4m, modifiedBuy1.PricePerShareLocal); // Buy price per share is divided by 4
            AssertEq(1020m, modifiedBuy1.TotalAmountLocal); // Remains the same
            AssertEq(20m, modifiedBuy1.FeesLocal); // Remains the same
            AssertEq(2m, modifiedBuy1.FXRate); // Remains the same

            var modifiedSell1 = events[1];
            AssertEq(5 * 3m * 4m, modifiedSell1.Quantity); // 15 shares sold, now 60 shares
            AssertEq(150m / 3m / 4m, modifiedSell1.PricePerShareLocal); // Sell price per share is divided by 4
            AssertEq(740m, modifiedSell1.TotalAmountLocal); // Remains the same
            AssertEq(10m, modifiedSell1.FeesLocal); // Remains the same
            AssertEq(2m, modifiedSell1.FXRate); // Remains the same

            // Previous Stock Split events are not modified
            var modifiedStockSplit1 = events[2];
            AssertEq(10, modifiedStockSplit1.Quantity);
            AssertEq(0, modifiedStockSplit1.TotalAmountLocal);

            // The original Buy and Sell stock events are not modified
            AssertEq(10, buy1.Quantity);
            AssertEq(100m, buy1.PricePerShareLocal);
            AssertEq(5, sell1.Quantity);
            AssertEq(150m, sell1.PricePerShareLocal);

            // The original Stock Split event is not modified
            AssertEq(10, stockSplit1.Quantity);
            AssertEq(0, stockSplit1.TotalAmountLocal);
        }

        // -------
        // Custody fee of 10 USD
        var custodyFee1 = new Event(T0 + 4 * D, CustodyFee, Ticker, null, null, 10m, 0, localCurrency, 2m);
        events.Add(custodyFee1);
        var stateAfterCustodyFees1 = tickerProcessing.ProcessNoop(
            custodyFee1, events, 4, stateAfterStockSplit2, NoOut);

        AssertStateAfterCustodyFee1();

        void AssertStateAfterCustodyFee1()
        {
            // The total quantity and amount are the same as before the custody fees
            AssertEq(stateAfterStockSplit2.TotalQuantity, stateAfterCustodyFees1.TotalQuantity);
            AssertEq(stateAfterStockSplit2.TotalAmountBase, stateAfterCustodyFees1.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto remain the same
            AssertEq(stateAfterStockSplit2.PlusValueCumpBase, stateAfterCustodyFees1.PlusValueCumpBase);
            AssertEq(stateAfterStockSplit2.MinusValueCumpBase, stateAfterCustodyFees1.MinusValueCumpBase);
            AssertEq(stateAfterStockSplit2.PlusValuePepsBase, stateAfterCustodyFees1.PlusValuePepsBase);
            AssertEq(stateAfterStockSplit2.MinusValuePepsBase, stateAfterCustodyFees1.MinusValuePepsBase);
            AssertEq(stateAfterStockSplit2.PlusValueCryptoBase, stateAfterCustodyFees1.PlusValueCryptoBase);
            AssertEq(stateAfterStockSplit2.MinusValueCryptoBase, stateAfterCustodyFees1.MinusValueCryptoBase);

            // The PEPS current index and sold quantity are the same as before the custody fees
            AssertEq(stateAfterStockSplit2.PepsCurrentIndex, stateAfterCustodyFees1.PepsCurrentIndex);
            AssertEq(stateAfterStockSplit2.PepsCurrentIndexSoldQuantity, stateAfterCustodyFees1.PepsCurrentIndexSoldQuantity);
        }

        // -------
        // Stock split of 2:1, starting from 60 shares => -30 additional shares
        var stockSplit3 = new Event(T0 + 5 * D, StockSplit, Ticker, -30m, null, 0, null, localCurrency, 2m);
        events.Add(stockSplit3);
        var stateAfterStockSplit3 = tickerProcessing.ProcessStockSplit(
            stockSplit3, events, 5, stateAfterCustodyFees1, NoOut);

        AssertStateAfterStockSplit3();

        void AssertStateAfterStockSplit3()
        {
            // The total quantity is decreased by 30
            AssertEq(30, stateAfterStockSplit3.TotalQuantity);
            // The total amount remains the same
            AssertEq(stateAfterCustodyFees1.TotalAmountBase, stateAfterStockSplit3.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto remain the same
            AssertEq(stateAfterCustodyFees1.PlusValueCumpBase, stateAfterStockSplit3.PlusValueCumpBase);
            AssertEq(stateAfterCustodyFees1.MinusValueCumpBase, stateAfterStockSplit3.MinusValueCumpBase);
            AssertEq(stateAfterCustodyFees1.PlusValuePepsBase, stateAfterStockSplit3.PlusValuePepsBase);
            AssertEq(stateAfterCustodyFees1.MinusValuePepsBase, stateAfterStockSplit3.MinusValuePepsBase);
            AssertEq(stateAfterCustodyFees1.PlusValueCryptoBase, stateAfterStockSplit3.PlusValueCryptoBase);
            AssertEq(stateAfterCustodyFees1.MinusValueCryptoBase, stateAfterStockSplit3.MinusValueCryptoBase);

            // The PEPS current index remains unchanged, but the PEPS current index sold quantity is divided by 2
            AssertEq(0, stateAfterStockSplit3.PepsCurrentIndex);
            AssertEq(stateAfterCustodyFees1.PepsCurrentIndexSoldQuantity / 2m, stateAfterStockSplit3.PepsCurrentIndexSoldQuantity);

            // Previous Buy and Sell stock events are retroactively updated in the list of events, to take into account the 1:4 stock split
            var modifiedBuy1 = events[0];
            AssertEq(10 * 3m * 4m / 2m, modifiedBuy1.Quantity); // 120 shares bought, now 60 shares
            AssertEq(100m / 3m / 4m * 2m, modifiedBuy1.PricePerShareLocal); // Buy price per share is multiplied by 2
            AssertEq(1020m, modifiedBuy1.TotalAmountLocal); // Remains the same
            AssertEq(20m, modifiedBuy1.FeesLocal); // Remains the same
            AssertEq(2m, modifiedBuy1.FXRate); // Remains the same

            var modifiedSell1 = events[1];
            AssertEq(5 * 3m * 4m / 2m, modifiedSell1.Quantity); // 60 shares sold, now 30 shares
            AssertEq(150m / 3m / 4m * 2m, modifiedSell1.PricePerShareLocal); // Sell price per share is multiplied by 2
            AssertEq(740m, modifiedSell1.TotalAmountLocal); // Remains the same
            AssertEq(10m, modifiedSell1.FeesLocal); // Remains the same
            AssertEq(2m, modifiedSell1.FXRate); // Remains the same

            // First Stock Split event is not modified
            var modifiedStockSplit1 = events[2];
            AssertEq(10, modifiedStockSplit1.Quantity);
            AssertEq(0, modifiedStockSplit1.TotalAmountLocal);

            // Second Stock Split event is not modified
            var modifiedStockSplit2 = events[3];
            AssertEq(45, modifiedStockSplit2.Quantity);
            AssertEq(0, modifiedStockSplit2.TotalAmountLocal);

            // First Custody Fee event is not modified
            var modifiedCustodyFee1 = events[4];
            AssertEq(10m, modifiedCustodyFee1.TotalAmountLocal);

            // The original Buy and Sell stock events are not modified
            AssertEq(10, buy1.Quantity);
            AssertEq(100m, buy1.PricePerShareLocal);
            AssertEq(5, sell1.Quantity);
            AssertEq(150m, sell1.PricePerShareLocal);

            // The original first Stock Split event is not modified
            AssertEq(10, stockSplit1.Quantity);
            AssertEq(0, stockSplit1.TotalAmountLocal);

            // The original second Stock Split event is not modified
            AssertEq(45, stockSplit2.Quantity);
            AssertEq(0, stockSplit2.TotalAmountLocal);

            // The original Custody Fee event is not modified
            AssertEq(10m, custodyFee1.TotalAmountLocal);
        }
    }

    [TestMethod]
    public void ProcessStockSplit_WhenTickerEventAndIndexAreInconsistent_RaisesException()
    {
        var tickerProcessing = Instance;
        var localCurrency = USD;
        var fxRate = 2m; // FX rate between USD and EUR stays stable at 2 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin, TotalQuantity: 15, TotalAmountBase: 225m);
        var events = new List<Event>
        {
            new(T0, BuyLimit, Ticker, 10, 100m, 1020m, 20m, localCurrency, fxRate),
            new(T0 + 1 * D, StockSplit, Ticker, 45m, null, 0, null, localCurrency, fxRate),
        };

        ThrowsAny<Exception>(() => tickerProcessing.ProcessStockSplit(events[1], events, 0, initialState, NoOut));
    }

    [TestMethod]
    public void ProcessStockSplit_WhenTypeIsInvalid_RaisesException()
    {
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 10, 100m, 1020m, 20m, USD, 2m);
        ThrowsAny<Exception>(() => Instance.ProcessStockSplit(
            buy1, [buy1], 0, new TickerState(Ticker, Isin), NoOut));
    }

    [TestMethod]
    public void ProcessStockSplit_WhenTickerIsNull_RaisesException()
    {
        var stockSplit1 = new Event(T0 + 0 * D, StockSplit, Ticker: null, Quantity: 10, PricePerShareLocal: null, TotalAmountLocal: 0, FeesLocal: null, Currency: USD, FXRate: 2m);
        ThrowsAny<Exception>(() => Instance.ProcessStockSplit(
            stockSplit1, [stockSplit1], 0, new TickerState(Ticker, Isin), NoOut));
    }

    [TestMethod]
    public void ProcessStockSplit_WhenQuantityIsNull_RaisesException()
    {
        var stockSplit1 = new Event(T0 + 0 * D, StockSplit, Ticker, Quantity: null, PricePerShareLocal: null, TotalAmountLocal: 0, FeesLocal: null, Currency: USD, FXRate: 2m);
        ThrowsAny<Exception>(() => Instance.ProcessStockSplit(
            stockSplit1, [stockSplit1], 0, new TickerState(Ticker, Isin), NoOut));
    }

    [TestMethod]
    public void ProcessStockSplit_WhenPricePerShareIsNotNull_RaisesException()
    {
        var stockSplit1 = new Event(T0 + 0 * D, StockSplit, Ticker, 10, PricePerShareLocal: 100m, TotalAmountLocal: 0, FeesLocal: null, Currency: USD, FXRate: 2m);
        ThrowsAny<Exception>(() => Instance.ProcessStockSplit(
            stockSplit1, [stockSplit1], 0, new TickerState(Ticker, Isin), NoOut));
    }

    [TestMethod]
    public void ProcessStockSplit_WhenTotalAmountIsNotZero_RaisesException()
    {
        var stockSplit1 = new Event(T0 + 0 * D, StockSplit, Ticker, 10, null, TotalAmountLocal: 100m, FeesLocal: 0, Currency: USD, FXRate: 2m);
        ThrowsAny<Exception>(() => Instance.ProcessStockSplit(
            stockSplit1, [stockSplit1], 0, new TickerState(Ticker, Isin), NoOut));
    }

    [TestMethod]
    public void ProcessStockSplit_WhenFeesAreNotNull_RaisesException()
    {
        var stockSplit1 = new Event(T0 + 0 * D, StockSplit, Ticker, 10, null, 0, FeesLocal: 20m, Currency: USD, FXRate: 2m);
        ThrowsAny<Exception>(() => Instance.ProcessStockSplit(
            stockSplit1, [stockSplit1], 0, new TickerState(Ticker, Isin), NoOut));
    }

    [TestMethod]
    public void ProcessStockSplit_WhenTickersAreInconsistent_RaisesException()
    {
        var stockSplit1 = new Event(T0 + 0 * D, StockSplit, Ticker, 10, null, 0, null, USD, 2m);
        ThrowsAny<Exception>(() => Instance.ProcessStockSplit(
            stockSplit1, [stockSplit1], 0, new TickerState(AnotherTicker, AnotherIsin), NoOut));
    }

    [TestMethod]
    public void ProcessStockSplit_CalculatesAndPrintsSteps()
    {
        var tickerProcessing = new TickerProcessing(new Basics() { Rounding = x => decimal.Round(x, 2) });
        var localCurrency = USD;
        var fxRate = 2m; // FX rate between USD and EUR stays stable at 2 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin);

        // First buy 10 shares at 100 USD, with fees of 20 USD => Total Amount Local of 1000 USD + 20 USD = 1020 USD
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 10, 100m, 1020m, 20m, localCurrency, fxRate);
        var stateAfterBuy1 = tickerProcessing.ProcessBuy(buy1, [buy1], 0, initialState, NoOut);

        // Then stock split of 1:3, starting from 10 shares => 20 additional shares
        var stockSplit1 = new Event(T0 + 1 * D, StockSplit, Ticker, 20m, null, 0, null, localCurrency, fxRate);
        var writer = new StringWriter();
        tickerProcessing.ProcessStockSplit(stockSplit1, [buy1, stockSplit1], 1, stateAfterBuy1, writer);

        var output = writer.ToString();

        Assert.IsTrue(output.Contains("Split Delta = 20"));
        Assert.IsTrue(output.Contains("Split Ratio = 3"));
    }

    [TestMethod]
    public void ProcessDividend_WhenTickerEventAndIndexAreInconsistent_RaisesException()
    {
        var tickerProcessing = Instance;
        var localCurrency = USD;
        var fxRate = 2m; // FX rate between USD and EUR stays stable at 2 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin, TotalQuantity: 15, TotalAmountBase: 225m);
        var events = new List<Event>
        {
            new(T0, BuyLimit, Ticker, 10, 100m, 1020m, 20m, localCurrency, fxRate),
            new(T0 + 1 * D, Dividend, Ticker, null, null, 10m, null, localCurrency, fxRate),
        };

        ThrowsAny<Exception>(() => tickerProcessing.ProcessDividend(events[1], events, 0, initialState, NoOut));
    }

    [TestMethod]
    public void ProcessDividend_WhenTypeIsInvalid_RaisesException()
    {
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 10, 100m, 1020m, 20m, USD, 2m);
        ThrowsAny<Exception>(() => Instance.ProcessDividend(
            buy1, [buy1], 0, new TickerState(Ticker, Isin), NoOut));
    }

    [TestMethod]
    public void ProcessDividend_WhenTickerIsNull_RaisesException()
    {
        var dividend1 = new Event(T0 + 0 * D, Dividend, Ticker: null, Quantity: null, PricePerShareLocal: null, TotalAmountLocal: 10m, FeesLocal: null, Currency: USD, FXRate: 2m);
        ThrowsAny<Exception>(() => Instance.ProcessDividend(
            dividend1, [dividend1], 0, new TickerState(Ticker, Isin), NoOut));
    }

    [TestMethod]
    public void ProcessDividend_WhenTotalAmountIsNotPositive_RaisesException()
    {
        var dividend1 = new Event(T0 + 0 * D, Dividend, Ticker, null, null, TotalAmountLocal: 0m, FeesLocal: 0, Currency: USD, FXRate: 2m);
        ThrowsAny<Exception>(() => Instance.ProcessDividend(
            dividend1, [dividend1], 0, new TickerState(Ticker, Isin), NoOut));

        var dividend2 = new Event(T0 + 0 * D, Dividend, Ticker, null, null, TotalAmountLocal: -10m, FeesLocal: 0, Currency: USD, FXRate: 2m);
        ThrowsAny<Exception>(() => Instance.ProcessDividend(
            dividend2, [dividend1, dividend2], 1, new TickerState(Ticker, Isin), NoOut));
    }

    [TestMethod]
    public void ProcessDividend_CalculatesAndPrintsSteps()
    {
        var tickerProcessing = new TickerProcessing(new Basics() { Rounding = x => decimal.Round(x, 2) });
        var localCurrency = USD;
        var fxRate = 2m; // FX rate between USD and EUR stays stable at 2 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin);

        // First buy 10 shares at 100 USD, with fees of 20 USD => Total Amount Local of 1000 USD + 20 USD = 1020 USD
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 10, 100m, 1020m, 20m, localCurrency, fxRate);
        var stateAfterBuy1 = tickerProcessing.ProcessBuy(buy1, [buy1], 0, initialState, NoOut);

        // Then dividend of 8.5 USD: 1 USD per share for 10 shares, with withholding tax of 15% from US =>
        // Total Amount Local of 10 USD - (15% * 10 USD) = 8.5 USD
        var dividend1 = new Event(T0 + 1 * D, Dividend, Ticker, null, null, 8.5m, null, localCurrency, fxRate);
        var writer = new StringWriter();
        tickerProcessing.ProcessDividend(dividend1, [buy1, dividend1], 1, stateAfterBuy1, writer);

        var output = writer.ToString();

        Assert.IsTrue(output.Contains($"Net Dividend ({localCurrency}) = 8.5"));
        Assert.IsTrue(output.Contains($"Net Dividend ({Instance.Basics.BaseCurrency}) = 4.25")); // 8.5 USD / (2 USD/EUR) 
        Assert.IsTrue(output.Contains($"WHT Dividend ({Instance.Basics.BaseCurrency}) = 0.75")); // 15% of 10 USD / (2 USD/EUR)
        Assert.IsTrue(output.Contains($"Gross Dividend ({Instance.Basics.BaseCurrency}) = 5")); // 10 USD / (2 USD/EUR)
    }

    [TestMethod]
    public void ProcessDividend_UpdatesTickerStateCorrectly()
    {
        var tickerProcessing = Instance;
        var localCurrency = USD;
        var fxRate = 2m; // FX rate between USD and EUR stays stable at 2 USD for 1 EUR across events
        var initialState = new TickerState(Ticker, Isin);

        // First buy 10 shares at 100 USD, with fees of 20 USD => Total Amount Local of 1000 USD + 20 USD = 1020 USD
        var buy1 = new Event(T0 + 0 * D, BuyLimit, Ticker, 10, 100m, 1020m, 20m, localCurrency, fxRate);
        var stateAfterBuy1 = tickerProcessing.ProcessBuy(
            buy1, [buy1], 0, initialState, NoOut);

        // Then dividend of 25.5 USD: 3 USD per share for 10 shares, with withholding tax of 15% from US =>
        // Total Amount Local of 30 USD - (15% * 30 USD) = 25.5 USD
        var dividend1 = new Event(T0 + 1 * D, Dividend, Ticker, null, null, 25.5m, null, localCurrency, fxRate);
        var stateAfterDividend1 = tickerProcessing.ProcessDividend(
            dividend1, [buy1, dividend1], 1, stateAfterBuy1, NoOut);

        AssertStateAfterDividend1();

        void AssertStateAfterDividend1()
        {
            // The total quantity and amount are the same as before the dividend
            AssertEq(stateAfterBuy1.TotalQuantity, stateAfterDividend1.TotalQuantity);
            AssertEq(stateAfterBuy1.TotalAmountBase, stateAfterDividend1.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto remain the same
            AssertEq(stateAfterBuy1.PlusValueCumpBase, stateAfterDividend1.PlusValueCumpBase);
            AssertEq(stateAfterBuy1.MinusValueCumpBase, stateAfterDividend1.MinusValueCumpBase);
            AssertEq(stateAfterBuy1.PlusValuePepsBase, stateAfterDividend1.PlusValuePepsBase);
            AssertEq(stateAfterBuy1.MinusValuePepsBase, stateAfterDividend1.MinusValuePepsBase);
            AssertEq(stateAfterBuy1.PlusValueCryptoBase, stateAfterDividend1.PlusValueCryptoBase);
            AssertEq(stateAfterBuy1.MinusValueCryptoBase, stateAfterDividend1.MinusValueCryptoBase);

            // The PEPS current index and sold quantity are the same as before the dividend
            AssertEq(stateAfterBuy1.PepsCurrentIndex, stateAfterDividend1.PepsCurrentIndex);
            AssertEq(stateAfterBuy1.PepsCurrentIndexSoldQuantity, stateAfterDividend1.PepsCurrentIndexSoldQuantity);

            // Total dividends are increased accordingly
            AssertEq(25.5m / 2m, stateAfterDividend1.NetDividendsBase); // 25.5 USD / (2 USD/EUR)
            AssertEq(4.5m / 2m, stateAfterDividend1.WhtDividendsBase); // 15% of 30 USD / (2 USD/EUR)
            AssertEq(30m / 2m, stateAfterDividend1.GrossDividendsBase); // 30 USD / (2 USD/EUR)
        }

        // Then dividend of 34 USD: 4 USD per share for 10 shares, with withholding tax of 15% from US =>
        // Total Amount Local of 40 USD - (15% * 40 USD) = 34 USD
        var dividend2 = new Event(T0 + 2 * D, Dividend, Ticker, null, null, 34m, null, localCurrency, fxRate);
        var stateAfterDividend2 = tickerProcessing.ProcessDividend(
            dividend2, [buy1, dividend1, dividend2], 2, stateAfterDividend1, NoOut);

        AssertStateAfterDividend2();

        void AssertStateAfterDividend2()
        {
            // The total quantity and amount are the same as before the dividend
            AssertEq(stateAfterDividend1.TotalQuantity, stateAfterDividend2.TotalQuantity);
            AssertEq(stateAfterDividend1.TotalAmountBase, stateAfterDividend2.TotalAmountBase);

            // The plus and minus values CUMP, PEPS and crypto remain the same
            AssertEq(stateAfterDividend1.PlusValueCumpBase, stateAfterDividend2.PlusValueCumpBase);
            AssertEq(stateAfterDividend1.MinusValueCumpBase, stateAfterDividend2.MinusValueCumpBase);
            AssertEq(stateAfterDividend1.PlusValuePepsBase, stateAfterDividend2.PlusValuePepsBase);
            AssertEq(stateAfterDividend1.MinusValuePepsBase, stateAfterDividend2.MinusValuePepsBase);
            AssertEq(stateAfterDividend1.PlusValueCryptoBase, stateAfterDividend2.PlusValueCryptoBase);
            AssertEq(stateAfterDividend1.MinusValueCryptoBase, stateAfterDividend2.MinusValueCryptoBase);

            // The PEPS current index and sold quantity are the same as before the dividend
            AssertEq(stateAfterDividend1.PepsCurrentIndex, stateAfterDividend2.PepsCurrentIndex);
            AssertEq(stateAfterDividend1.PepsCurrentIndexSoldQuantity, stateAfterDividend2.PepsCurrentIndexSoldQuantity);

            // Total dividends are increased accordingly
            AssertEq(59.5m / 2m, stateAfterDividend2.NetDividendsBase); // (25.5 USD + 34 USD) / (2 USD/EUR)
            AssertEq(10.5m / 2m, stateAfterDividend2.WhtDividendsBase); // (4.5 USD + 6 USD) / (2 USD/EUR)
            AssertEq(70m / 2m, stateAfterDividend2.GrossDividendsBase); // (30 USD + 40 USD) / (2 USD/EUR)
        }
    }

    [AssertionMethod]
    private void AssertEq(decimal expected, decimal actual, string message = "") => 
        Assert.AreEqual(expected, actual, Instance.Basics.Precision, message);

    [AssertionMethod]
    private static void AssertEq(int expected, int actual, string message = "") => 
        Assert.AreEqual(expected, actual, message);

    [AssertionMethod]
    private void AssertEq(decimal? expected, decimal? actual, string message = "") => 
        AssertEq(expected!.Value, actual!.Value, message);
}
