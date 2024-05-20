using Taxes;

ProcessUtils.PrintEnvironmentAndSettings(Console.Out);

var basics = new Basics();
var fxRatesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.FXRatesFilePath);
var fxRatesReader = new FxRatesReader(basics);
var fxRates = fxRatesReader.ParseMultiCurrenciesFromFile(fxRatesFilePath);
var cryptoPortfolioValuesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.CryptoPortfolioValuesFilePath);
var cryptoPortfolioValues = new CryptoPortfolioValues(basics, fxRates, basics.CryptoPortfolioValuesCurrency, cryptoPortfolioValuesFilePath);

var stockEventsReader = new StockEventsReader(basics);
var stockEvents = basics.StockEventsFiles
    .SelectMany(eventsFiles => Directory
        .GetFiles(basics.ReportsDirectoryPath, eventsFiles.FilePattern)
        .EnsureNonEmpty()
        .Select(path => new EventsFileAndBroker(path, eventsFiles.Broker)))
    .OrderBy(eventsFileAndBroker => eventsFileAndBroker.FilePath)
    .SelectMany(eventsFileAndBroker => stockEventsReader.Parse(eventsFileAndBroker.FilePath, fxRates, eventsFileAndBroker.Broker))
    .ToList();
ProcessEvents(stockEvents, basics, cryptoPortfolioValues);

var cryptoEventsReader = new CryptoEventsReader(basics);
var cryptoEvents = basics.CryptoEventsFiles
    .SelectMany(eventsFiles => Directory
        .GetFiles(basics.ReportsDirectoryPath, eventsFiles.FilePattern)
        .EnsureNonEmpty()
        .Select(path => new EventsFileAndBroker(path, eventsFiles.Broker)))
    .OrderBy(eventsFileAndBroker => eventsFileAndBroker.FilePath)
    .SelectMany(eventsFileAndBroker => cryptoEventsReader.Parse(eventsFileAndBroker.FilePath, eventsFileAndBroker.Broker))
    .ToList();
ProcessEvents(cryptoEvents, basics, cryptoPortfolioValues);

static void ProcessEvents(IList<Event> events, Basics basics, CryptoPortfolioValues cryptoPortfolioValues)
{
    var tickerProcessing = new TickerProcessing(basics, cryptoPortfolioValues);
    var form2047Writer = new StringWriter();

    // Taken into account in each ticker
    var nonTickerRelatedEvents = (
        from e in events
        where string.IsNullOrWhiteSpace(e.Ticker)
        select e)
        .ToList();
    var eventsByTicker = (
        from e in events
        group e by e.Ticker into g
        orderby g.Key
        select (ticker: g.Key, tickerEvents: g.Concat(nonTickerRelatedEvents).OrderBy(e1 => e1.Date).ToArray()))
        .ToList();

    var tickerStates = (
        from e in eventsByTicker
        select tickerProcessing.ProcessTicker(e.ticker, e.tickerEvents, Console.Out, form2047Writer))
        .ToList();

    tickerStates.PrintAggregatedMetrics(Console.Out, basics);
    Console.Out.Write(form2047Writer.ToString());
}

