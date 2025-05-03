using Taxes;

var outWriters = new OutWriters(Console.Out, new StringWriter(), new StringWriter());

ProcessUtils.PrintEnvironmentAndSettings(outWriters.Default);

var basics = new Basics();
var fxRatesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.FXRatesFilePath);
var fxRatesReader = new FxRatesReader(basics);
var fxRates = fxRatesReader.ParseMultiCurrenciesFromFile(fxRatesFilePath);
var cryptoPortfolioValuesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.CryptoPortfolioValuesFilePath);
var cryptoPortfolioValues = new CryptoPortfolioValues(basics, fxRates, cryptoPortfolioValuesFilePath);

var stockEventsReader = new StockEventsReader(basics);
var stockEvents = basics.StockEventsFiles
    .SelectMany(eventsFiles => Directory
        .GetFiles(basics.ReportsDirectoryPath, eventsFiles.FilePattern)
        .EnsureNonEmpty()
        .PrintEachElement(outWriters.Default, filePath => $"Parsing File {filePath}...")
        .Select(path => new EventsFileAndBroker(path, eventsFiles.Broker)))
    .OrderBy(eventsFileAndBroker => eventsFileAndBroker.FilePath)
    .SelectMany(eventsFileAndBroker => stockEventsReader
        .Parse(eventsFileAndBroker.FilePath, fxRates, eventsFileAndBroker.Broker, outWriters.Default)
        .PrintEachElement(outWriters.Default, @event => $"Parsing Event {@event}..."))
    .ToList();
ProcessEvents(stockEvents, basics, cryptoPortfolioValues, outWriters);

var cryptoEventsReader = new CryptoEventsReader(basics);
var cryptoEvents = basics.CryptoEventsFiles
    .SelectMany(eventsFiles => Directory
        .GetFiles(basics.ReportsDirectoryPath, eventsFiles.FilePattern)
        .EnsureNonEmpty()
        .PrintEachElement(outWriters.Default, filePath => $"Parsing File {filePath}...")
        .Select(path => new EventsFileAndBroker(path, eventsFiles.Broker)))
    .OrderBy(eventsFileAndBroker => eventsFileAndBroker.FilePath)
    .SelectMany(eventsFileAndBroker => cryptoEventsReader
        .Parse(eventsFileAndBroker.FilePath, eventsFileAndBroker.Broker, outWriters.Default)
        .PrintEachElement(outWriters.Default, @event => $"Parsing Event {@event}..."))
    .ToList();
ProcessEvents(cryptoEvents, basics, cryptoPortfolioValues, outWriters);

static void ProcessEvents(IList<Event> events, Basics basics, CryptoPortfolioValues cryptoPortfolioValues, OutWriters outWriters)
{
    var tickerProcessing = new TickerProcessing(basics, cryptoPortfolioValues);

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
        select tickerProcessing.ProcessTicker(e.ticker, e.tickerEvents, outWriters))
        .ToList();

    tickerStates.PrintAggregatedMetrics(outWriters.Default, basics);
    outWriters.Default.Write(outWriters.Form2047Writer.ToString());
    outWriters.Default.Write(outWriters.Form2086Writer.ToString());
}

