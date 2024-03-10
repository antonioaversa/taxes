using Taxes;
using static Taxes.Basics;

var fxRates = FXRates.Parse(Basics.PathOf($"BCE-FXRate-{BaseCurrency}-USD.txt"));

var stockEvents = StockEventsReader.Parse(
    Basics.PathOf("stocks_2022.csv"), fxRates)
        .Concat(StockEventsReader.Parse(Basics.PathOf("stocks_2023.csv"), fxRates))
        .ToList();
ProcessEvents(stockEvents);

var cryptoEvents = CryptoEventsReader.Parse(
    Basics.PathOf("crypto_*.csv"), Basics.PathOf("cryptoportfoliovalues.csv"), fxRates);
ProcessEvents(cryptoEvents);

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
