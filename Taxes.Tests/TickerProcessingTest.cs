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
        e.Add(new(T0 + 2 * D, BuyLimit, Ticker, 1, 90, 92, 1, "EUR", 1, 0));
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 7, totalAmountBase: 708, portfolioAcquisitionValueBase: 708);
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

    [TestMethod]
    public void ProcessTicker_SellExactQuantityBought_GeneratingPlusValue()
    {
        List<Event> e = [
            new(T0, BuyLimit, Ticker, 3, 100, 303, 3, "EUR", 1, 0),
            new(T0 + D, SellLimit, Ticker, 3, 110, 330, 3, "EUR", 1, 0)];
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            plusValueCumpBase: 27, plusValuePepsBase: 27, plusValueCryptoBase: 0);
    }

}
