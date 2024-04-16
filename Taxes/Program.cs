using Taxes;

var fxRatesFilePath = Path.Combine(Basics.ReportsDirectoryPath, Basics.FXRatesFilePath);
var fxRates = Basics.FXRatesInputType == FXRatesInputType.SingleCurrency
    ? FxRates.ParseSingleCurrencyFromFile(Basics.FXRatesSingleCurrency, fxRatesFilePath)
    : FxRates.ParseMultiCurrenciesFromFile(fxRatesFilePath);

var stockEvents = Basics.StockEventsFilePaths
    .SelectMany(pattern => Directory.GetFiles(Basics.ReportsDirectoryPath, pattern))
    .Order()
    .SelectMany(filePath => StockEventsReader.Parse(filePath, fxRates))
    .ToList();
ProcessEvents(stockEvents);

var cryptoEvents = Basics.CryptoEventsFilePaths
    .SelectMany(pattern => Directory.GetFiles(Basics.ReportsDirectoryPath, pattern))
    .Order()
    .SelectMany(filePath => CryptoEventsReader.Parse(
        filePath, Basics.CryptoPortfolioValuesCurrency, Basics.CryptoPortfolioValuesFilePath, fxRates))
    .ToList();
ProcessEvents(cryptoEvents);

static void ProcessEvents(IList<Event> events)
{
    var eventsByTicker = (
        from e in events
        group e by e.Ticker into g
        orderby g.Key
        select (ticker: g.Key, tickerEvents: g.OrderBy(e1 => e1.Date).ToArray()))
        .ToList();

    var tickerStates = (
        from e in eventsByTicker
        select TickerProcessing.ProcessTicker(e.ticker, e.tickerEvents))
        .ToList();

    tickerStates.PrintAggregatedMetrics(Console.Out);
}
