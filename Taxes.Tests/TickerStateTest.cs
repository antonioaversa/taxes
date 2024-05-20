namespace Taxes.Test;

[TestClass]
public class TickerStateTest
{
    private const string Ticker = "AAPL";
    private const string Isin = "US0378331005";

    [TestMethod]
    public void DefaultCtor_InitializesPropertiesToDefault()
    {
        var tickerState = new TickerState(Ticker, Isin);

        Assert.AreEqual("AAPL", tickerState.Ticker);
        Assert.AreEqual("US0378331005", tickerState.Isin);

        Assert.AreEqual(0m, tickerState.PlusValueCumpBase);
        Assert.AreEqual(0m, tickerState.PlusValuePepsBase);
        Assert.AreEqual(0m, tickerState.PlusValueCryptoBase);
        Assert.AreEqual(0m, tickerState.MinusValueCumpBase);
        Assert.AreEqual(0m, tickerState.MinusValuePepsBase);
        Assert.AreEqual(0m, tickerState.MinusValueCryptoBase);

        Assert.AreEqual(0m, tickerState.TotalQuantity);
        Assert.AreEqual(0m, tickerState.TotalAmountBase);

        Assert.AreEqual(0m, tickerState.NetDividendsBase);
        Assert.AreEqual(0m, tickerState.WhtDividendsBase);
        Assert.AreEqual(0m, tickerState.GrossDividendsBase);

        Assert.AreEqual(-1, tickerState.PepsCurrentIndex);
        Assert.AreEqual(0m, tickerState.PepsCurrentIndexSoldQuantity);
        Assert.AreEqual(0m, tickerState.CryptoPortfolioAcquisitionValueBase);
        Assert.AreEqual(0m, tickerState.CryptoFractionOfInitialCapitalBase);
    }

    [TestMethod]
    public void ToString_IncludesDividends()
    {
        var tickerState = new TickerState(Ticker, Isin)
        {
            NetDividendsBase = 1.23m,
            WhtDividendsBase = 0.12m,
            GrossDividendsBase = 1.35m,
        };
        var tickerStateString = tickerState.ToString();
        Assert.IsTrue(tickerStateString.Contains("Dividends ="));
    }

    [TestMethod]
    public void ToString_IncludesInterests()
    {
        var tickerState = new TickerState(Ticker, Isin)
        {
            NetInterestsBase = 1.23m,
            WhtInterestsBase = 0.12m,
            GrossInterestsBase = 1.35m,
        };
        var tickerStateString = tickerState.ToString();
        Assert.IsTrue(tickerStateString.Contains("Interests ="));
    }
}