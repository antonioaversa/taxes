namespace Taxes.Test;

[TestClass]
public class EventTest
{
    private static readonly DateTime Date = (2021, 1, 1).ToUtc();
    private const string Ticker = "AAPL";
    private const string Currency = "USD";
    private const string Broker = "REVOLUT RSEUAB";

    [DataTestMethod]
    [DataRow(EventType.Reset, false)]
    [DataRow(EventType.CashTopUp, false)]
    [DataRow(EventType.BuyMarket, true)]
    [DataRow(EventType.BuyLimit, true)]
    [DataRow(EventType.SellMarket, false)]
    [DataRow(EventType.SellLimit, false)]
    public void IsBuy(EventType eventType, bool expected)
    {
        var tickerEvent = new Event(Date, eventType, Ticker, 0, 0, 0, 0, Currency, 0, Broker);
        Assert.AreEqual(expected, tickerEvent.IsBuy);
    }

    [DataTestMethod]
    [DataRow(EventType.Reset, false)]
    [DataRow(EventType.CashTopUp, false)]
    [DataRow(EventType.BuyMarket, false)]
    [DataRow(EventType.BuyLimit, false)]
    [DataRow(EventType.SellMarket, true)]
    [DataRow(EventType.SellLimit, true)]
    public void IsSell(EventType eventType, bool expected)
    {
        var tickerEvent = new Event(Date, eventType, Ticker, 0, 0, 0, 0, Currency, 0, Broker);
        Assert.AreEqual(expected, tickerEvent.IsSell);
    }

    [TestMethod]
    public void ToString_Parameterless_ThrowsNotSupportedException()
    {
        var anEvent = new Event(Date, EventType.BuyMarket, Ticker, 10, 100, 1000, 1, Currency, 1.2m, Broker);
        Assert.ThrowsException<NotSupportedException>(() => anEvent.ToString());
    }
}
