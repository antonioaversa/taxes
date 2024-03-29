namespace Taxes.Test;

using static TickerProcessing;
using static EventType;

[TestClass]
public class TickerProcessingTest
{
    private const string Ticker = "AAPL";
    private static readonly DateTime T0 = new DateTime(2022, 01, 01, 00, 00, 00);
    private static readonly TimeSpan D = TimeSpan.FromDays(1);

    [TestMethod]
    public void ProcessTicker_BuyLimit()
    {
        TickerState s;
        s = ProcessTicker(Ticker, [new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, "EUR", 1, 0)]);
        s.AssertState(
            plusValueCumpBase: 0, plusValuePepsBase: 0, plusValueCryptoBase: 0,
            minusValueCumpBase: 0, minusValuePepsBase: 0, minusValueCryptoBase: 0,
            totalQuantity: 3, totalAmountBase: 300, 
            netDividendsBase: 0, whtDividendsBase: 0, grossDividendsBase: 0,
            0, 0, 0, 0);
    }

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellWithoutBuying() => 
        ProcessTicker(Ticker, [new Event(T0, SellLimit, Ticker, 3, 100, 303, 3, "EUR", 1, 0)]);

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellMoreThanBuying() =>
         ProcessTicker(Ticker, [
             new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, "EUR", 1, 0),
             new Event(T0, SellLimit, Ticker, 4, 100, 404, 4, "EUR", 1, 0)]);

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
            Assert.AreEqual(plusValuePepsBase, tickerState.PlusValuePepsBase);
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
}