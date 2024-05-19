using Taxes.Test;

namespace Taxes.Tests;

using static EventType;

[TestClass]
public class FR2074Section5Test
{
    private const string Ticker = "AAPL";
    private const string Isin = "US0378331005";
    private const string USD = "USD";
    private static readonly DateTime T0 = (2022, 1, 1).ToUtc(); // Date of the first event
    private static readonly Basics Basics = new();

    [TestMethod]
    public void Print_IncludesAllRelevantInformation()
    {
        var initialState = new TickerState(Ticker, Isin, 
            PlusValueCumpBase: 101m,
            PlusValuePepsBase: 102m,
            PlusValueCryptoBase: 103m,
            MinusValueCumpBase: 201m,
            MinusValuePepsBase: 202m,
            MinusValueCryptoBase: 203m,
            TotalQuantity: 10m,
            TotalAmountBase: 300000m,
            NetDividendsBase: 401,
            WhtDividendsBase: 40,
            GrossDividendsBase: 441,
            NetInterestsBase: 501,
            WhtInterestsBase: 50,
            GrossInterestsBase: 551);
        var sell = new Event(T0, SellLimit, Ticker, 2, 150m, 297.50m, 2.50m, USD, 4m, "THE BROKER");
        var writer = new StringWriter();
        var data = new FR2074Section5.Data
        (
            Basics: Basics,
            TickerState: initialState,
            TickerEvent: sell,
            PerShareSellPriceBase: 1.99m,
            TotalSellFeesBase: 1.49m,
            PerShareAvgBuyPriceBase: 0.99m,
            TotalAvgBuyPriceBase: 10.99m,
            PlusValueCumpBase: 1.01m

        );
        FR2074Section5.Print(data, writer);

        var text = writer.ToString();
        Assert.IsTrue(text.Contains(Ticker));
        Assert.IsTrue(text.Contains(Isin));
        Assert.IsTrue(text.Contains(sell.Broker));
        Assert.IsTrue(text.Contains(sell.Date.ToString("dd'/'MM'/'yyyy")));
        Assert.IsTrue(text.Contains("10"));
        var lines = text.Split(Environment.NewLine);
        Assert.IsTrue(Array.Exists(lines,
            l => decimal.TryParse(l, out var n) && n - data.PerShareSellPriceBase <= 0.005m));
        Assert.IsTrue(Array.Exists(lines,
            l => decimal.TryParse(l, out var n) && n - data.TotalSellFeesBase <= 0.5m));
        Assert.IsTrue(Array.Exists(lines,
            l => decimal.TryParse(l, out var n) && n - data.PerShareAvgBuyPriceBase <= 0.005m));
        Assert.IsTrue(Array.Exists(lines,
            l => decimal.TryParse(l, out var n) && n - data.TotalAvgBuyPriceBase <= data.TickerEvent.Quantity!.Value * 0.5m));
    }
}
