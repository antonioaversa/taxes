namespace Taxes;

static class Form2074
{
    private static readonly string Separator = new('*', 100);

    public static void PrintDataForSection5(Data data, TextWriter outWriter)
    {
        outWriter.WriteLine(Separator.Insert(5, " 2074 Section 5 "));
        outWriter.WriteLine($"{data.TickerEvent.Ticker} [{data.TickerState.Isin}]");
        outWriter.WriteLine(data.TickerEvent.Broker);
        outWriter.WriteLine(data.TickerEvent.Date.ToString("dd'/'MM'/'yyyy"));
        outWriter.WriteLine(Math.Round(data.PerShareSellPriceBase, 2));
        outWriter.WriteLine(Math.Round(data.TickerEvent.Quantity!.Value, 0));
        outWriter.WriteLine(Math.Round(data.TickerEvent.FeesLocal!.Value));
        outWriter.WriteLine(Math.Round(data.PerShareAvgBuyPriceBase, 2).ToString(data.Basics.Form2074Culture));
        outWriter.WriteLine(Math.Round(data.TotalAvgBuyPriceBase, 0));
        outWriter.WriteLine(0);
        outWriter.WriteLine(Math.Round(data.PlusValueCumpBase, 0));
    }

    public record Data(
        Basics Basics,
        TickerState TickerState,
        Event TickerEvent,
        decimal PerShareSellPriceBase,
        decimal TotalSellFeesBase,
        decimal PerShareAvgBuyPriceBase,
        decimal TotalAvgBuyPriceBase,
        decimal PlusValueCumpBase);
}
