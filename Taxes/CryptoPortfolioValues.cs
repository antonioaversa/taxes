using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace Taxes;

public class CryptoPortfolioValues
{
    private static CsvConfiguration CsvConfiguration(Basics basics) => 
        new(basics.DefaultCulture)
        {
            AllowComments = true,
            Comment = '#',
            Delimiter = ",",
            DetectColumnCountChanges = true,
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
        };

    private FxRates FxRates { get; }
    private Dictionary<DateTime, PortfolioValueEntry> PortfolioValuesLocal { get; }

    public CryptoPortfolioValues(
        Basics basics, FxRates fxRates, string portfolioValuesPath)
    {
        FxRates = fxRates;
        using var portfolioValuesReader = new StreamReader(portfolioValuesPath);
        using var portfolioValuesCsvReader = new CsvReader(portfolioValuesReader, CsvConfiguration(basics));
        PortfolioValuesLocal = LoadPortfolioValues(basics, portfolioValuesCsvReader);
    }

    internal /* for testing */ CryptoPortfolioValues(
        Basics basics, FxRates fxRates, TextReader portfolioValuesReader)
    {
        FxRates = fxRates;
        using var portfolioValuesCsvReader = new CsvReader(portfolioValuesReader, CsvConfiguration(basics));
        PortfolioValuesLocal = LoadPortfolioValues(basics, portfolioValuesCsvReader);
    }

    private static Dictionary<DateTime, PortfolioValueEntry> LoadPortfolioValues(
        Basics basics, CsvReader portfolioValuesCsvReader) => 
        portfolioValuesCsvReader.GetRecords<PortfolioValueStr>().ToDictionary(
            record => DateTime.ParseExact(record.Date, "yyyy-MM-dd", basics.DefaultCulture),
            record => new PortfolioValueEntry(
                decimal.Parse(record.PortfolioValue, basics.DefaultCulture),
                record.Currency
            )
        );

    public decimal this[DateTime date]
    {
        get
        {
            // We don't have portfolio current value for all events, we barely have it for sell events
            if (!PortfolioValuesLocal.TryGetValue(date.Date, out var entry))
                return -1m;

            // An FX Rate is considered valid if it's found for the local currency of the portfolio and for the day or,
            // in case of weekend days, one of the next two days following the date
            var fxRate = FxRates[entry.Currency, date.Date];

            // FX Rates are Base/Local and portfolio value is in Local, so we need to divide to get the value in Bases
            return entry.Amount / fxRate;
        }
    }

    [Delimiter(",")]
    private sealed class PortfolioValueStr
    {
        [Name("Date")] public string Date { get; set; } = string.Empty;
        [Name("PortfolioValue")] public string PortfolioValue { get; set; } = string.Empty;
        [Name("Currency")] public string Currency { get; set; } = string.Empty;
    }

    private sealed record PortfolioValueEntry(decimal Amount, string Currency);
}
