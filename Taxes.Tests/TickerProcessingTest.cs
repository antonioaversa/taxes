namespace Taxes.Test;

using static EventType;
using static AssertExtensions;

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
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(Ticker, tickerStateAfterReset.Ticker);
        Assert.AreEqual(Isin, tickerStateAfterReset.Isin);
    }

    [TestMethod]
    public void ProcessReset_WhenPassingNotSupportedType_RaisesException()
    {
        var tickerEvent = new Event(T0, CustodyFee, Ticker, 0, 0, 0, 12.0m, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessReset_WhenPassingANonZeroTotalAmount_RaisesException()
    {
        var tickerState = new TickerState(Ticker, Isin);
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, TotalAmountLocal: 303, null, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessReset_WhenPassingTransactionRelatedInfo_RaisesException()
    {
        var tickerState = new TickerState(Ticker, Isin);
        var tickerEvent = new Event(T0, Reset, Ticker, Quantity: 4, null, 0m, null, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null));
        tickerEvent = new Event(T0, Reset, Ticker, null, PricePerShareLocal: 100, 0m, null, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null));
        tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, FeesLocal: 3, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessReset_PreservesTotalQuantityAndAmountBase()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(3, tickerStateAfterReset.TotalQuantity);
        Assert.AreEqual(5.5m, tickerStateAfterReset.TotalAmountBase);
    }

    [TestMethod]
    public void ProcessReset_PreservesPepsIndexes()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, PepsCurrentIndex: 3, PepsCurrentIndexBoughtQuantity: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(3, tickerStateAfterReset.PepsCurrentIndex);
        Assert.AreEqual(5.5m, tickerStateAfterReset.PepsCurrentIndexBoughtQuantity);
    }

    [TestMethod]
    public void ProcessReset_PreservesPortfolioAcquisitionValueBaseAndCryptoFractionOfInitialCapital()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
            PortfolioAcquisitionValueBase: 5.5m, CryptoFractionOfInitialCapital: 0.75m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(5.5m, tickerStateAfterReset.PortfolioAcquisitionValueBase);
        Assert.AreEqual(0.75m, tickerStateAfterReset.CryptoFractionOfInitialCapital);
    }

    [TestMethod]
    public void ProcessReset_ResetsPlusValues()
    {
        var tickerEvent = new Event(T0, Reset, Ticker, null, null, 0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin, 
            PlusValueCumpBase: 5.5m, PlusValuePepsBase: 5.5m, PlusValueCryptoBase: 5.5m);
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
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
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
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
        var tickerStateAfterReset = Instance.ProcessReset(tickerEvent, [], 0, tickerState, TextWriter.Null);
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
            PepsCurrentIndex: 3, PepsCurrentIndexBoughtQuantity: 2.1m,
            PortfolioAcquisitionValueBase: 23.3m, CryptoFractionOfInitialCapital: 0.75m);
        var tickerStateAfterNoop = Instance.ProcessNoop(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(tickerState, tickerStateAfterNoop);
    }

    [TestMethod]
    public void ProcessBuy_WhenPassingNotSupportedType_RaisesException()
    {
        var tickerEvent = new Event(T0, CustodyFee, Ticker, null, null, 12.0m, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenTickerNameIsNull_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, null, 3, 100, 303, 3, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenPricePerShareLocalIsNull_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, null, 303, 3, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenQuantityIsNull_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, Ticker, null, 100, 303, 3, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenTotalAmountLocalIsNonPositive_RaisesException()
    {
        // Buying 3 shares at 100 EUR, with fees of 3 EUR => Total Amount Local of 0 EUR
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 0m, 3, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null));

        // Buying 3 shares at 100 EUR, with fees of 3 EUR => Total Amount Local of -1 EUR
        tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, -1m, 3, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenFeesLocalIsNull_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, null, EUR, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenCurrenciesDontMatch_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, USD, 1, -1);
        var tickerState = new TickerState(Ticker, Isin);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 0, 0, 0, 0, EUR, 1, -1) };
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, tickerEvents, 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenTickersDontMatch_RaisesException()
    {
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1);
        var tickerState = new TickerState(AnotherTicker, AnotherIsin);
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_WhenFeesDontMatch_RaisesException()
    {
        var tickerState = new TickerState(Ticker, Isin);
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 2, EUR, 1, -1); // Fees should be 3
        ThrowsAny<Exception>(() => Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessBuy_IncreasesTotalQuantityByTheQuantityInTheEvent()
    {
        var tickerState = new TickerState(Ticker, Isin);
        // First buy of 3 shares
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1);
        var tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(3, tickerStateAfterBuy.TotalQuantity);
        // Second buy of 2 shares
        tickerEvent = new Event(T0 + D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [], 0, tickerStateAfterBuy, TextWriter.Null);
        Assert.AreEqual(5, tickerStateAfterBuy.TotalQuantity);
        // Third buy of 2.5 shares
        tickerEvent = new Event(T0 + 2 * D, BuyLimit, Ticker, 2.5m, 90, 227.5m, 2.5m, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [], 0, tickerStateAfterBuy, TextWriter.Null);
        Assert.AreEqual(7.5m, tickerStateAfterBuy.TotalQuantity);
    }

    [TestMethod]
    public void ProcessBuy_IncreasesTotalAmountBase_BySharesAmountPlusFees()
    {
        var tickerState = new TickerState(Ticker, Isin);
        // First buy of 3 shares at 100 EUR, with fees of 3 EUR => Total Amount Local of 303 EUR
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, EUR, 1, -1);
        var tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [], 0, tickerState, TextWriter.Null);
        Assert.AreEqual(303, tickerStateAfterBuy.TotalAmountBase);
        // Second buy of 2 shares at 110 EUR, with fees of 2 EUR => Total Amount Local of 222 EUR
        tickerEvent = new Event(T0 + D, BuyLimit, Ticker, 2, 110, 222, 2, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [], 0, tickerStateAfterBuy, TextWriter.Null);
        Assert.AreEqual(525, tickerStateAfterBuy.TotalAmountBase);
        // Third buy of 2.5 shares at 90 EUR, with fees of 2.5 EUR => Total Amount Local of 227.5 EUR
        tickerEvent = new Event(T0 + 2 * D, BuyLimit, Ticker, 2.5m, 90, 227.5m, 2.5m, EUR, 1, -1);
        tickerStateAfterBuy = Instance.ProcessBuy(tickerEvent, [], 0, tickerStateAfterBuy, TextWriter.Null);
        Assert.AreEqual(752.5m, tickerStateAfterBuy.TotalAmountBase);
    }

    [TestMethod]
    public void ProcessBuy_PrintsSteps()
    {
        var writer = new StringWriter();
        var tickerState = new TickerState(Ticker, Isin);
        var localCurrency = USD;
        // First buy of 3 shares at 100.10002 USD, with fees of 3.20003 USD => Total Amount Local of 303.50009 USD
        var tickerEvent = new Event(T0, BuyLimit, Ticker, 3, 100.10002m, 303.50009m, 3.20003m, localCurrency, 2m, -1);
        var tickerProcessing = new TickerProcessing(new Basics() { Rounding = x => decimal.Round(x, 2) });
        tickerProcessing.ProcessBuy(tickerEvent, [], 0, tickerState, writer);
        var output = writer.ToString();

        // Prints Total Buy Price in local currency as rounded value
        Assert.IsTrue(output.Contains($"Total Buy Price ({localCurrency}) = 303.50"));
        // Prints Total Buy Price in base currency as rounded value
        Assert.IsTrue(output.Contains($"Total Buy Price ({Instance.Basics.BaseCurrency}) = 151.75"));  
        // Prints Shares Buy Price in local currency as rounded value
        Assert.IsTrue(output.Contains($"Shares Buy Price ({localCurrency}) = 300.30"));
        // Prints Shares Buy Price in base currency as rounded value
        Assert.IsTrue(output.Contains($"Shares Buy Price ({Instance.Basics.BaseCurrency}) = 150.15"));
        // Prints PerShare Buy Price in local currency as rounded value
        Assert.IsTrue(output.Contains($"PerShare Buy Price ({localCurrency}) = 100.10"));
        // Prints PerShare Buy Price in base currency as rounded value
        Assert.IsTrue(output.Contains($"PerShare Buy Price ({Instance.Basics.BaseCurrency}) = 50.05"));
        // Prints Buy Fees in local currency as rounded value
        Assert.IsTrue(output.Contains($"Buy Fees ({localCurrency}) = 3.20"));
        // Prints Buy Fees in base currency as rounded value
        Assert.IsTrue(output.Contains($"Buy Fees ({Instance.Basics.BaseCurrency}) = 1.60"));
    }

    [TestMethod]
    public void ProcessSell_WhenPassingNotSupportedType_RaisesException()
    {
        // Custody fees for 12 EUR
        var tickerEvent = new Event(T0, CustodyFee, Ticker, 0, 0, 0, 12.0m, EUR, 1, -1);
        // While owning no shares
        var tickerState = new TickerState(Ticker, Isin);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenTickerNameIsNull_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, null, 1, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenTotalAmountLocalIsNonPositive_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 0 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 0m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));

        // Selling 1 share at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of -1 EUR
        tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, -1m, 0.2m, EUR, 1, -1);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenFeesLocalIsNull_RaisesException()
    {
        // Selling 1 share at 2.0 EUR, with fees of NULL EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, null, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenTickersMismatch_RaisesException()
    {
        // Selling 1 share of A at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares of B for a total of 5.5 EUR
        var tickerState = new TickerState(AnotherTicker, AnotherIsin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenPricePerShareLocalIsNull_RaisesException()
    {
        // Selling 1 share at NULL, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, null, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenQuantityIsNull_RaisesException()
    {
        // Selling NULL shares at 2.0 EUR, with fees of 0.2 EUR => Total Amount Local of 1.8 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, null, 2.0m, 1.8m, 0.2m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, [], 0, tickerState, TextWriter.Null));

    }

    [TestMethod]
    public void ProcessSell_WhenCurrenciesAreEtherogenous_RaisesException()
    {
        // Selling 1 share at 2.0 USD, with fees of 0.2 USD => Total Amount Local of 1.8 USD
        var tickerEvent = new Event(T0, SellLimit, Ticker, 1, 2.0m, 1.8m, 0.2m, USD, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 0, 0, 0, 0, EUR, 1, -1) };
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, tickerEvents, 0, tickerState, TextWriter.Null));
    }

    [TestMethod]
    public void ProcessSell_WhenSellingMoreThanOwned_RaisesException()
    {
        // Selling 4 shares at 2.0 EUR, with fees of 0.8 EUR => Total Amount Local of 7.2 EUR
        var tickerEvent = new Event(T0, SellLimit, Ticker, 4, 8.0m, 7.2m, 0.8m, EUR, 1, -1);
        // While owning 3 shares for a total of 5.5 EUR
        var tickerState = new TickerState(Ticker, Isin, TotalQuantity: 3, TotalAmountBase: 5.5m);
        var tickerEvents = new[] { new Event(T0, BuyLimit, Ticker, 3, 0, 0, 0, EUR, 1, -1) };
        ThrowsAny<Exception>(() => Instance.ProcessSell(tickerEvent, tickerEvents, 0, tickerState, TextWriter.Null));
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
        Instance.ProcessSell(tickerEvent, tickerEvents, 0, tickerState, outWriter);
        var output = outWriter.ToString();
        Assert.IsTrue(output.Contains($"Total Sell Price ({Instance.Basics.BaseCurrency}) = 6.0"));
    }

}
