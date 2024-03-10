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
    public void ProcessTicker_BuyLimit() =>
        ProcessTicker(Ticker, [new Event(T0, BuyLimit, Ticker, 3, 100, 303, 3, "EUR", 1, 0)]);

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