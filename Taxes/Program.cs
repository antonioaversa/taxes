using Taxes;

var basics = new Basics();
var fxRatesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.FXRatesFilePath);
var fxRatesReader = new FxRatesReader(basics);
var fxRates = fxRatesReader.ParseMultiCurrenciesFromFile(fxRatesFilePath);
var cryptoPortfolioValuesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.CryptoPortfolioValuesFilePath);
var cryptoPortfolioValues = new CryptoPortfolioValues(basics, fxRates, basics.CryptoPortfolioValuesCurrency, cryptoPortfolioValuesFilePath);

var stockEventsReader = new StockEventsReader(basics);
var stockEvents = basics.StockEventsFilePaths
    .SelectMany(pattern => Directory.GetFiles(basics.ReportsDirectoryPath, pattern))
    .Order()
    .SelectMany(filePath => stockEventsReader.Parse(filePath, fxRates))
    .ToList();
ProcessEvents(stockEvents, basics, cryptoPortfolioValues);

var cryptoEventsReader = new CryptoEventsReader(basics);
var cryptoEvents = basics.CryptoEventsFilePaths
    .SelectMany(pattern => Directory.GetFiles(basics.ReportsDirectoryPath, pattern))
    .Order()
    .SelectMany(cryptoEventsReader.Parse)
    .ToList();
ProcessEvents(cryptoEvents, basics, cryptoPortfolioValues);

static void ProcessEvents(IList<Event> events, Basics basics, CryptoPortfolioValues cryptoPortfolioValues)
{
    var tickerProcessing = new TickerProcessing(basics, cryptoPortfolioValues);

    var eventsByTicker = (
        from e in events
        group e by e.Ticker into g
        orderby g.Key
        select (ticker: g.Key, tickerEvents: g.OrderBy(e1 => e1.Date).ToArray()))
        .ToList();

    var tickerStates = (
        from e in eventsByTicker
        select tickerProcessing.ProcessTicker(e.ticker, e.tickerEvents, Console.Out))
        .ToList();

    tickerStates.PrintAggregatedMetrics(Console.Out, basics);
}
