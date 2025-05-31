namespace Taxes;

internal static class ProgramExtensions
{
    public static IEnumerable<EventsFileAndBroker> FindEventsFiles(this IEnumerable<Basics.EventsFiles> files, Basics basics, OutWriters outWriters) =>
        files.SelectMany(eventsFiles => Directory
                .GetFiles(basics.ReportsDirectoryPath, eventsFiles.FilePattern)
                .EnsureNonEmpty()
                .Select(path => new EventsFileAndBroker(path, eventsFiles.Broker)))
            .OrderBy(eventsFileAndBroker => eventsFileAndBroker.FilePath)
            .PrintEachElement(outWriters.Default, efb => $"- Parsing File {System.IO.Path.GetFileName(efb.FilePath)}...");

    public static void ProcessEvents(this IList<Event> events, Basics basics, CryptoPortfolioValues cryptoPortfolioValues, OutWriters outWriters)
    {
        var tickerProcessing = new TickerProcessing(basics, cryptoPortfolioValues);

        outWriters.Default.WriteLine(); // Empty line before H2
        outWriters.Default.WriteLine("## Process events");

        // Taken into account in each ticker
        var nonTickerRelatedEvents = (
                from e in events
                where string.IsNullOrWhiteSpace(e.Ticker)
                select e)
            .ToList();
        var eventsByTicker = (
                from e in events
                group e by e.Ticker
                into g
                orderby g.Key
                select (ticker: g.Key, tickerEvents: g.Concat(nonTickerRelatedEvents).OrderBy(e1 => e1.Date).ToArray()))
            .ToList();

        var tickerStates = (
                from e in eventsByTicker
                select tickerProcessing.ProcessTicker(e.ticker, e.tickerEvents, outWriters))
            .ToList();

        outWriters.Default.WriteLine();
        outWriters.Default.WriteLine("## Aggregated Metrics");
        outWriters.Default.WriteLine();
        tickerStates.PrintAggregatedMetrics(outWriters.Default, basics);

        var anyCryptoEvent = events.Any(e =>
            e.Ticker == CryptoEventsReader.CryptoTicker);
        var anyNonCryptoEvent = events.Any(e =>
            !string.IsNullOrEmpty(e.Ticker) && e.Ticker != CryptoEventsReader.CryptoTicker);

        switch (anyCryptoEvent, anyNonCryptoEvent)
        {
            case (true, true):
                throw new InvalidOperationException(
                    $"Cannot process crypto and non-crypto events at the same time. Please split them into different {nameof(ProcessEvents)} executions.");
            case (true, false): // Crypto case -> 2086 Form
                outWriters.Default.WriteLine(); 
                outWriters.Default.WriteLine("## 2086 Form");
                outWriters.Default.WriteLine(); 
                outWriters.Default.Write(outWriters.Form2086Writer.ToString());
                break;
            case (false, true): // Stocks case -> 2074 Form
                outWriters.Default.WriteLine(); 
                outWriters.Default.WriteLine("## 2074 Form");
                outWriters.Default.WriteLine(); 
                outWriters.Default.Write(outWriters.Form2047Writer.ToString());
                break;
            case (false, false):
                throw new InvalidOperationException(
                    "Neither crypto nor non-crypto events found. Please check your input files.");
        }
    }
}