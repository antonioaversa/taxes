namespace Taxes;

public static class FR2074Section5
{
    internal static void Print(
        Event tickerEvent, TickerState tickerState, Basics basics, TextWriter outWriter, Data data)
    {
        outWriter.WriteLine(new string('*', 100));
        outWriter.WriteLine($"{tickerEvent.Ticker} [{tickerState.Isin}]");
        outWriter.WriteLine(tickerEvent.Broker);
        outWriter.WriteLine(tickerEvent.Date.ToString("dd'/'MM'/'yyyy"));
        outWriter.WriteLine(Math.Round(data.PerShareSellPriceBase, 2));
        outWriter.WriteLine(Math.Round(tickerEvent.Quantity!.Value, 0));
        outWriter.WriteLine(Math.Round(tickerEvent.FeesLocal!.Value));
        outWriter.WriteLine(Math.Round(data.PerShareAvgBuyPriceBase, 2).ToString(basics.FR2074Section5Culture));
        outWriter.WriteLine(Math.Round(data.TotalAvgBuyPriceBase, 0));
        outWriter.WriteLine(0);
        outWriter.WriteLine(Math.Round(data.PlusValueCumpBase, 0));
        outWriter.WriteLine(new string('*', 100));
    }

    public record Data(
        decimal PerShareSellPriceBase, 
        decimal PerShareAvgBuyPriceBase, 
        decimal TotalAvgBuyPriceBase,
        decimal PlusValueCumpBase);
}
