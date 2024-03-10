using Taxes;
using static Taxes.Basics;

var fxRates = FXRates.Parse("Reports/BCE-FXRate-EUR-USD.txt");
ProcessEvents(
    StockEventsReader.Parse("Reports/stocks_2022.csv", fxRates)
        .Concat(StockEventsReader.Parse("Reports/stocks_2023.csv", fxRates))
        .ToList());
ProcessEvents(CryptoEventsReader.Parse("Reports/crypto_*.csv", "Reports/cryptoportfoliovalues.csv", fxRates));

static void ProcessEvents(IList<Event> events)
{
    var eventsByTicker = (
        from e in events
        group e by e.Ticker into g
        orderby g.Key
        select (ticker: g.Key, tickerEvents: g.OrderBy(e1 => e1.Date).ToArray()))
        .ToList();

    var tickerStates = new List<TickerState>();
    foreach (var (ticker, tickerEvents) in eventsByTicker)
    {
        tickerStates.Add(TickerProcessing.ProcessTicker(ticker, tickerEvents));
    }

    tickerStates.PrintAggregatedMetrics();
}
