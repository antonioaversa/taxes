namespace Taxes.Test;

[TestClass]
public class EventExtensionsTest
{
    private const string Ticker = "AAPL";
    private const string Currency = "USD";
    private static readonly DateTime Date = (2021, 1, 1).ToUtc();

    [TestMethod]
    public void AssertEvent_ThrowsWhenExpectedAndActualAreDifferent()
    {
        var tickerEvent = new Event(Date, EventType.BuyMarket, Ticker, 2, 20m, 22m, 2m, Currency, 1.2m);
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(date: DateTime.Now.AddDays(1)));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(type: EventType.BuyLimit));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(ticker: "GOOGL"));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(quantity: 3));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(pricePerShareLocal: 30m));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(totalAmountLocal: 33m));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(feesLocal: 3m));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(currency: "EUR"));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(fxRate: 1.3m));
    }

    [TestMethod]
    public void AssertEvent_DoesNotThrowWhenExpectedAndActualAreEqual()
    {
        var tickerEvent = new Event(Date, EventType.BuyMarket, Ticker, 2, 20m, 22m, 2m, Currency, 1.2m);
        tickerEvent.AssertEvent(date: Date);
        tickerEvent.AssertEvent(type: EventType.BuyMarket);
        tickerEvent.AssertEvent(ticker: Ticker);
        tickerEvent.AssertEvent(quantity: 2);
        tickerEvent.AssertEvent(pricePerShareLocal: 20m);
        tickerEvent.AssertEvent(totalAmountLocal: 22m);
        tickerEvent.AssertEvent(feesLocal: 2m);
        tickerEvent.AssertEvent(currency: Currency);
        tickerEvent.AssertEvent(fxRate: 1.2m);
    }

    [TestMethod]
    public void AssertEvent_DoesNotThrowWhenNoPropertyIsAsserted()
    {
        var tickerEvent = new Event(Date, EventType.BuyMarket, Ticker, 2, 20m, 22m, 2m, Currency, 1.2m);
        tickerEvent.AssertEvent();
    }
}
