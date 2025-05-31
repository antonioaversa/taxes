using Taxes;

var appBaseDirectory = AppContext.BaseDirectory;

var teeTextWriter = new TeeTextWriter(
    outputDirectoryPath: Path.Combine(appBaseDirectory, "logs"),
    fileNameFormat: "Taxes-{datetime}.md",
    primaryWriter: Console.Out);
var outWriters = new OutWriters(teeTextWriter, new StringWriter(), new StringWriter());

ProcessUtils.PrintEnvironmentAndSettings(outWriters.Default, teeTextWriter.FilePath);

var basics = new Basics(Path.Combine(appBaseDirectory, "Reports"));
var fxRatesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.FXRatesFilePath);
var fxRatesReader = new FxRatesReader(basics);
var fxRates = fxRatesReader.ParseMultiCurrenciesFromFile(fxRatesFilePath);
var cryptoPortfolioValuesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.CryptoPortfolioValuesFilePath);
var cryptoPortfolioValues = new CryptoPortfolioValues(basics, fxRates, cryptoPortfolioValuesFilePath);

await outWriters.Default.WriteLineAsync("");
await outWriters.Default.WriteLineAsync("# STOCKS");
await outWriters.Default.WriteLineAsync("");
await outWriters.Default.WriteLineAsync("## Parsing files");
await outWriters.Default.WriteLineAsync("");
var stockEventsReader = new StockEventsReader(basics);
basics.StockEventsFiles
    .FindEventsFiles(basics, outWriters)
    .SelectMany(eventsFileAndBroker => stockEventsReader
        .Parse(eventsFileAndBroker.FilePath, fxRates, eventsFileAndBroker.Broker, outWriters.Default))
    .PrintEachElement(outWriters.Default, @event => $"  - Parsing Event {@event.ToString(basics)}...")
    .ToList()
    .ProcessEvents(basics, cryptoPortfolioValues, outWriters);

await outWriters.Default.WriteLineAsync("");
await outWriters.Default.WriteLineAsync("# CRYPTO");
await outWriters.Default.WriteLineAsync("");
await outWriters.Default.WriteLineAsync("## Parsing files");
await outWriters.Default.WriteLineAsync("");
var cryptoEventsReader = new CryptoEventsReader(basics);
basics.CryptoEventsFiles
    .FindEventsFiles(basics, outWriters)
    .SelectMany(eventsFileAndBroker => cryptoEventsReader
        .ParseFile(eventsFileAndBroker.FilePath, fxRates, eventsFileAndBroker.Broker, outWriters.Default))
    .PrintEachElement(outWriters.Default, @event => $"  - Parsing Event {@event.ToString(basics)}...")
    .ToList()
    .ProcessEvents(basics, cryptoPortfolioValues, outWriters);
