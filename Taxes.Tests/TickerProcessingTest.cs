namespace Taxes.Test;

using static TickerProcessing;
using static EventType;
using static AssertExtensions;

[TestClass]
public class TickerProcessingTest
{
    private const string Ticker = "AAPL";
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
        List<Event> e = [new(T0, BuyLimit, Ticker, 3, 100, 303, 3, "EUR", 1, 0)];
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 3, totalAmountBase: 303, portfolioAcquisitionValueBase: 303);
        e.Add(new(T0 + D, BuyLimit, Ticker, 2, 110, 222, 2, "EUR", 1, 0));
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 5, totalAmountBase: 525, portfolioAcquisitionValueBase: 525);
        e.Add(new(T0 + 2 * D, BuyLimit, Ticker, 1, 90, 91, 1, "EUR", 1, 0));
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 6, totalAmountBase: 616, portfolioAcquisitionValueBase: 616);
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit_NonPositiveQuantity_RaisesException()
    {
        ThrowsAny<Exception>(() => ProcessTicker(Ticker, [new(T0, BuyLimit, Ticker, -3, 100, 303, 3, "EUR", 1, 0)]));
        ThrowsAny<Exception>(() => ProcessTicker(Ticker, [new(T0, BuyLimit, Ticker, 0, 100, 303, 3, "EUR", 1, 0)]));
    }

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellWithoutBuying() => 
        ProcessTicker(Ticker, [new(T0, SellLimit, Ticker, 3, 100, 303, 3, "EUR", 1, 0)]);

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellMoreThanBuying() =>
         ProcessTicker(Ticker, [
             new(T0, BuyLimit, Ticker, 3, 100, 303, 3, "EUR", 1, 0),
             new(T0, SellLimit, Ticker, 4, 100, 404, 4, "EUR", 1, 0)]);

}

static class AssertExtensions
{
    public static void ThrowsAny<T>(Action action) where T : Exception
    {
        bool exceptionThrown;
        try
        {
            action();
            exceptionThrown = false;
        }
        catch (T)
        {
            exceptionThrown = true;
        }

        if (!exceptionThrown)
            Assert.Fail($"Expected exception of type {typeof(T)} or derived, but no exception was thrown");

    }
}

static class TickerStateExtensions
{
    public static void AssertState(
        this TickerState tickerState,
        decimal? plusValueCumpBase = null,
        decimal? plusValuePepsBase = null,
        decimal? plusValueCryptoBase = null,
        decimal? minusValueCumpBase = null,
        decimal? minusValuePepsBase = null,
        decimal? minusValueCryptoBase = null,
        decimal? totalQuantity = null,
        decimal? totalAmountBase = null,
        decimal? netDividendsBase = null,
        decimal? whtDividendsBase = null,
        decimal? grossDividendsBase = null,
        int? pepsCurrentIndex = null,
        decimal? pepsCurrentIndexBoughtQuantity = null,
        decimal? portfolioAcquisitionValueBase = null,
        decimal? cryptoFractionOfInitialCapital = null)
    {
        if (plusValueCumpBase is not null) 
            Assert.AreEqual(plusValueCumpBase, tickerState.PlusValueCumpBase);
        if (plusValuePepsBase is not null) 
            Assert.AreEqual(plusValuePepsBase ?? 0, tickerState.PlusValuePepsBase);
        if (plusValueCryptoBase is not null) 
            Assert.AreEqual(plusValueCryptoBase, tickerState.PlusValueCryptoBase);
        if (minusValueCumpBase is not null) 
            Assert.AreEqual(minusValueCumpBase, tickerState.MinusValueCumpBase);
        if (minusValuePepsBase is not null) 
            Assert.AreEqual(minusValuePepsBase, tickerState.MinusValuePepsBase);
        if (minusValueCryptoBase is not null) 
            Assert.AreEqual(minusValueCryptoBase, tickerState.MinusValueCryptoBase);
        
        if (totalQuantity is not null) 
            Assert.AreEqual(totalQuantity, tickerState.TotalQuantity);
        if (totalAmountBase is not null) 
            Assert.AreEqual(totalAmountBase, tickerState.TotalAmountBase);
        
        if (netDividendsBase is not null) 
            Assert.AreEqual(netDividendsBase, tickerState.NetDividendsBase);
        if (whtDividendsBase is not null) 
            Assert.AreEqual(whtDividendsBase, tickerState.WhtDividendsBase);
        if (grossDividendsBase is not null) 
            Assert.AreEqual(grossDividendsBase, tickerState.GrossDividendsBase);

        if (pepsCurrentIndex is not null) 
            Assert.AreEqual(pepsCurrentIndex, tickerState.PepsCurrentIndex);
        if (pepsCurrentIndexBoughtQuantity is not null) 
            Assert.AreEqual(pepsCurrentIndexBoughtQuantity, tickerState.PepsCurrentIndexBoughtQuantity);
        if (portfolioAcquisitionValueBase is not null) 
            Assert.AreEqual(portfolioAcquisitionValueBase, tickerState.PortfolioAcquisitionValueBase);
        if (cryptoFractionOfInitialCapital is not null) 
            Assert.AreEqual(cryptoFractionOfInitialCapital, tickerState.CryptoFractionOfInitialCapital);
    }

    // This is like the previous method, but asserts 0 when the value is null
    public static void AssertZeroExceptFor(
        this TickerState tickerState,
        decimal? plusValueCumpBase = null,
        decimal? plusValuePepsBase = null,
        decimal? plusValueCryptoBase = null,
        decimal? minusValueCumpBase = null,
        decimal? minusValuePepsBase = null,
        decimal? minusValueCryptoBase = null,
        decimal? totalQuantity = null,
        decimal? totalAmountBase = null,
        decimal? netDividendsBase = null,
        decimal? whtDividendsBase = null,
        decimal? grossDividendsBase = null,
        int? pepsCurrentIndex = null,
        decimal? pepsCurrentIndexBoughtQuantity = null,
        decimal? portfolioAcquisitionValueBase = null,
        decimal? cryptoFractionOfInitialCapital = null) => 
        tickerState.AssertState(
            plusValueCumpBase: plusValueCumpBase ?? 0,
            plusValuePepsBase: plusValuePepsBase ?? 0,
            plusValueCryptoBase: plusValueCryptoBase ?? 0,
            minusValueCumpBase: minusValueCumpBase ?? 0,
            minusValuePepsBase: minusValuePepsBase ?? 0,
            minusValueCryptoBase: minusValueCryptoBase ?? 0,

            totalQuantity: totalQuantity ?? 0,
            totalAmountBase: totalAmountBase ?? 0,
                
            netDividendsBase: netDividendsBase ?? 0,
            whtDividendsBase: whtDividendsBase ?? 0,
            grossDividendsBase: grossDividendsBase ?? 0,
                
            pepsCurrentIndex: pepsCurrentIndex ?? 0,
            pepsCurrentIndexBoughtQuantity: pepsCurrentIndexBoughtQuantity ?? 0,
            portfolioAcquisitionValueBase: portfolioAcquisitionValueBase ?? 0,
            cryptoFractionOfInitialCapital: cryptoFractionOfInitialCapital ?? 0);
}