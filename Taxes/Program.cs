using Taxes;

var basics = new Basics();
var fxRatesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.FXRatesFilePath);
var fxRatesReader = new FxRatesReader(basics);
var fxRates = basics.FXRatesInputType == FXRatesInputType.SingleCurrency
    ? fxRatesReader.ParseSingleCurrencyFromFile(basics.FXRatesSingleCurrency, fxRatesFilePath)
    : fxRatesReader.ParseMultiCurrenciesFromFile(fxRatesFilePath);

var stockEventsReader = new StockEventsReader(basics);
var stockEvents = basics.StockEventsFilePaths
    .SelectMany(pattern => Directory.GetFiles(basics.ReportsDirectoryPath, pattern))
    .Order()
    .SelectMany(filePath => stockEventsReader.Parse(filePath, fxRates))
    .ToList();
ProcessEvents(stockEvents, basics);

var cryptoEventsReader = new CryptoEventsReader(basics);
var cryptoEvents = basics.CryptoEventsFilePaths
    .SelectMany(pattern => Directory.GetFiles(basics.ReportsDirectoryPath, pattern))
    .Order()
    .SelectMany(filePath => cryptoEventsReader.Parse(
        filePath, basics.CryptoPortfolioValuesCurrency, basics.CryptoPortfolioValuesFilePath, fxRates))
    .ToList();
ProcessEvents(cryptoEvents, basics);

static void ProcessEvents(IList<Event> events, Basics basics)
{
    var tickerProcessing = new TickerProcessing(basics);

    var eventsByTicker = (
        from e in events
        group e by e.Ticker into g
        orderby g.Key
        select (ticker: g.Key, tickerEvents: g.OrderBy(e1 => e1.Date).ToArray()))
        .ToList();

    var tickerStates = (
        from e in eventsByTicker
        select tickerProcessing.ProcessTicker(e.ticker, e.tickerEvents))
        .ToList();

    tickerStates.PrintAggregatedMetrics(Console.Out, basics);
}
