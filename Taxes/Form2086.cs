namespace Taxes;

static class Form2086
{
    private static readonly string Separator = new('*', 100);

    public static void PrintDataForSection3(Data data, TextWriter outWriter)
    {
        outWriter.WriteLine(Separator.Insert(5, $" 2086 Section 3 for {data.TickerEvent.Ticker}({data.TickerEvent.OriginalTicker}) "));
        outWriter.WriteLine(data.TickerEvent.Date.ToString("dd'/'MM'/'yyyy"));
        outWriter.WriteLine(Math.Round(data.PortfolioCurrentValueBase, 0));
        outWriter.WriteLine(Math.Round(data.TickerEvent.PricePerShareLocal!.Value * data.TickerEvent.Quantity!.Value, 0));
        outWriter.WriteLine(Math.Round(data.SellFeesBase, 0));
        outWriter.WriteLine(Math.Round(data.TickerState.CryptoPortfolioAcquisitionValueBase, 0));
        outWriter.WriteLine(Math.Round(data.TickerState.CryptoFractionOfInitialCapitalBase, 0));
        outWriter.WriteLine(Math.Round(data.PlusValueCryptoBase, 0));
    }

    public record Data(
        TickerState TickerState,
        Event TickerEvent,
        decimal PortfolioCurrentValueBase,
        decimal SellFeesBase,
        decimal PlusValueCryptoBase);
}
