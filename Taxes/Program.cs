using Taxes;

PrintEnvironmentAndSettings(Console.Out);

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
        .Select(path => new EventsFileAndBroker(path, eventsFiles.Broker)))
    .OrderBy(eventsFileAndBroker => eventsFileAndBroker.FilePath)
    .SelectMany(eventsFileAndBroker => stockEventsReader.Parse(eventsFileAndBroker.FilePath, fxRates, eventsFileAndBroker.Broker))
    .ToList();
ProcessEvents(stockEvents, basics, cryptoPortfolioValues);

var cryptoEventsReader = new CryptoEventsReader(basics);
var cryptoEvents = basics.CryptoEventsFiles
    .SelectMany(eventsFiles => Directory
        .GetFiles(basics.ReportsDirectoryPath, eventsFiles.FilePattern)
        .Select(path => new EventsFileAndBroker(path, eventsFiles.Broker)))
    .OrderBy(eventsFileAndBroker => eventsFileAndBroker.FilePath)
    .SelectMany(eventsFileAndBroker => cryptoEventsReader.Parse(eventsFileAndBroker.FilePath, eventsFileAndBroker.Broker))
    .ToList();
ProcessEvents(cryptoEvents, basics, cryptoPortfolioValues);

static void ProcessEvents(IList<Event> events, Basics basics, CryptoPortfolioValues cryptoPortfolioValues)
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
        select tickerProcessing.ProcessTicker(e.ticker, e.tickerEvents, Console.Out))
        .ToList();

    tickerStates.PrintAggregatedMetrics(Console.Out, basics);
}

static void PrintEnvironmentAndSettings(TextWriter outWriter) 
{
    outWriter.WriteLine("ENVIRONMENT AND SETTINGS");
    outWriter.WriteLine();
    outWriter.WriteLine($"Date and time: {DateTime.Now}");
    outWriter.WriteLine($"Machine name: {Environment.MachineName}");
    outWriter.WriteLine($"User name: {Environment.UserName}");
    outWriter.WriteLine($"Current working directory: {Environment.CurrentDirectory}");
    outWriter.WriteLine($"Command line parameters: {string.Join(' ', Environment.GetCommandLineArgs())}");
    outWriter.WriteLine($"Commit hash: {ProcessUtils.CommandOutput("git rev-parse HEAD").Trim()}");
    outWriter.WriteLine($"Modified files: {ProcessUtils.CommandOutput("git diff")}");
    outWriter.WriteLine("MD5 digest of files in Reports folder:");
    foreach (var filePath in Directory.GetFiles("Reports"))
    {
        Console.WriteLine($"- {filePath}: {FileUtils.CalculateMD5Digest(filePath)}");
    }

    outWriter.WriteLine(new string('=', 100));
}

record EventsFileAndBroker(string FilePath, string Broker);