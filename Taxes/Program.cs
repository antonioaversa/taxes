using Taxes;
using static Taxes.Basics;

var fxRates = FXRates.Parse("Reports/BCE-FXRate-EUR-USD.txt");
ProcessEvents(StockEventsReader.Parse("Reports/stocks.csv", fxRates));
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

    var totalPlusValueCumpBase = tickerStates.Sum(ts => ts.PlusValueCumpBase);
    var totalPlusValuePepsBase = tickerStates.Sum(ts => ts.PlusValuePepsBase);
    var totalPlusValueCryptoBase = tickerStates.Sum(ts => ts.PlusValueCryptoBase);
    var totalMinusValueCumpBase = tickerStates.Sum(ts => ts.MinusValueCumpBase);
    var totalMinusValuePepsBase = tickerStates.Sum(ts => ts.MinusValuePepsBase);
    var totalMinusValueCryptoBase = tickerStates.Sum(ts => ts.MinusValueCryptoBase);
    var totalNetDividendsBase = tickerStates.Sum(ts => ts.NetDividendsBase);
    var totalWhtDividendsBase = tickerStates.Sum(ts => ts.WhtDividendsBase);
    var totalGrossDividendsBase = tickerStates.Sum(ts => ts.GrossDividendsBase);
    Console.WriteLine($"Total Plus Value CUMP ({BaseCurrency}) = {totalPlusValueCumpBase.R()}");
    Console.WriteLine($"Total Plus Value PEPS ({BaseCurrency}) = {totalPlusValuePepsBase.R()}");
    Console.WriteLine($"Total Plus Value CRYPTO ({BaseCurrency}) = {totalPlusValueCryptoBase.R()}");
    Console.WriteLine($"Total Minus Value CUMP ({BaseCurrency}) = {totalMinusValueCumpBase.R()}");
    Console.WriteLine($"Total Minus Value PEPS ({BaseCurrency}) = {totalMinusValuePepsBase.R()}");
    Console.WriteLine($"Total Minus Value CRYPTO ({BaseCurrency}) = {totalMinusValueCryptoBase.R()}");
    Console.WriteLine($"Total Net Dividends ({BaseCurrency}) = {totalNetDividendsBase.R()}");
    Console.WriteLine($"Total WHT Dividends ({BaseCurrency}) = {totalWhtDividendsBase.R()}");
    Console.WriteLine($"Total Gross Dividends ({BaseCurrency}) = {totalGrossDividendsBase.R()}");
}
