namespace Taxes.Test;

[TestClass]
public class EventExtensionsTest
{
    private const string Ticker = "AAPL";
    private const string Currency = "USD";

    [TestMethod]
    public void AssertEvent_ThrowsWhenExpectedAndActualAreDifferent()
    {
        var tickerEvent = new Event(new DateTime(2022, 01, 01), EventType.BuyMarket, Ticker, 2, 20m, 22m, 2m, Currency, 1.2m, 1000m);
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(date: DateTime.Now.AddDays(1)));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(type: EventType.BuyLimit));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(ticker: "GOOGL"));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(quantity: 3));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(pricePerShareLocal: 30m));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(totalAmountLocal: 33m));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(feesLocal: 3m));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(currency: "EUR"));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(fxRate: 1.3m));
        Assert.ThrowsException<AssertFailedException>(() => tickerEvent.AssertEvent(portfolioCurrentValueBase: 2000m));
    }

    [TestMethod]
    public void AssertEvent_DoesNotThrowWhenExpectedAndActualAreEqual()
    {
        var tickerEvent = new Event(new DateTime(2022, 01, 01), EventType.BuyMarket, Ticker, 2, 20m, 22m, 2m, Currency, 1.2m, 1000m);
        tickerEvent.AssertEvent(date: new DateTime(2022, 01, 01));
        tickerEvent.AssertEvent(type: EventType.BuyMarket);
        tickerEvent.AssertEvent(ticker: Ticker);
        tickerEvent.AssertEvent(quantity: 2);
        tickerEvent.AssertEvent(pricePerShareLocal: 20m);
        tickerEvent.AssertEvent(totalAmountLocal: 22m);
        tickerEvent.AssertEvent(feesLocal: 2m);
        tickerEvent.AssertEvent(currency: Currency);
        tickerEvent.AssertEvent(fxRate: 1.2m);
        tickerEvent.AssertEvent(portfolioCurrentValueBase: 1000m);
    }

    [TestMethod]
    public void AssertEvent_DoesNotThrowWhenNoPropertyIsAsserted()
    {
        var tickerEvent = new Event(new DateTime(2022, 01, 01), EventType.BuyMarket, Ticker, 2, 20m, 22m, 2m, Currency, 1.2m, 1000m);
        tickerEvent.AssertEvent();
    }
}
