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
        // Quantity, PricePerShareLocal, TotalAmountLocal, FeesLocal
        List<Event> e = [new(T0, BuyLimit, Ticker, 3, 100, 303, 3, "EUR", 1, -1)];
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 3, totalAmountBase: 303, portfolioAcquisitionValueBase: 303);
        e.Add(new(T0 + D, BuyLimit, Ticker, 2, 110, 222, 2, "EUR", 1, -1));
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 5, totalAmountBase: 525, portfolioAcquisitionValueBase: 525);
        e.Add(new(T0 + 2 * D, BuyLimit, Ticker, 1, 90, 91, 1, "EUR", 1, -1));
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 6, totalAmountBase: 616, portfolioAcquisitionValueBase: 616);
        e.Add(new(T0 + 2 * D, BuyLimit, Ticker, 1, 90, 92, 1, "EUR", 1, -1));
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 7, totalAmountBase: 708, portfolioAcquisitionValueBase: 708);
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit_DifferentCurrency_ConvertsToBase()
    {
        List<Event> e = [new(T0, BuyLimit, Ticker, 3, 100, 303, 3, "USD", 1.2m, -1)];
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            totalQuantity: 3, totalAmountBase: 303 * 1.2m, portfolioAcquisitionValueBase: 303 * 1.2m);
    }

    [TestMethod]
    public void ProcessTicker_BuyMarket_Fees()
    {
        // Assert fees after 2022-12-28T20:12:29.182442Z,AAPL,BUY - MARKET,5,$126.21,$632.62,EUR,1
        List<Event> e = [new(T0, BuyMarket, Ticker, 5, 126.21m, 632.62m, 1.58m, "EUR", 1, -1)];
    }

    [TestMethod]
    public void ProcessTicker_BuyLimit_NonPositiveQuantity_RaisesException()
    {
        ThrowsAny<Exception>(() => ProcessTicker(Ticker, [new(T0, BuyLimit, Ticker, -3, 100, 303, 3, "EUR", 1, -1)]));
        ThrowsAny<Exception>(() => ProcessTicker(Ticker, [new(T0, BuyLimit, Ticker, 0, 100, 303, 3, "EUR", 1, -1)]));
    }

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellWithoutBuying() => 
        ProcessTicker(Ticker, [new(T0, SellLimit, Ticker, 3, 100, 303, 3, "EUR", 1, -1)]);

    [TestMethod]
    [ExpectedException(typeof(Exception), AllowDerivedTypes = true)]
    public void ProcessTicker_SellMoreThanBuying() =>
         ProcessTicker(Ticker, [
             new(T0, BuyLimit, Ticker, 3, 100, 303, 3, "EUR", 1,  -1),
             new(T0, SellLimit, Ticker, 4, 100, 404, 4, "EUR", 1, -1)]);

    [TestMethod]
    public void ProcessTicker_SellExactQuantityBought_GeneratingPlusValue()
    {
        List<Event> e = [
            new(T0, BuyLimit, Ticker, 
                Quantity: 3, PricePerShareLocal: 100, TotalAmountLocal: 303, FeesLocal: 3, 
                "EUR", 1, -1),
            new(T0 + D, SellLimit, Ticker, 
                Quantity: 3, PricePerShareLocal: 110, TotalAmountLocal: 330, FeesLocal: 3, 
                "EUR", 1, -1)];
        ProcessTicker(Ticker, e).AssertZeroExceptFor(
            plusValueCumpBase: 27, plusValuePepsBase: 27, plusValueCryptoBase: 0);
    }

}
