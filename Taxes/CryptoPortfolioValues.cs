using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;

namespace Taxes;

class CryptoPortfolioValues
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
    private string PortfolioValuesCurrency { get; }
    private Dictionary<DateTime, decimal> PortfolioValuesLocal { get; }

    public CryptoPortfolioValues(
        Basics basics, FxRates fxRates, string portfolioValuesCurrency, string portfolioValuesPath)
    {
        FxRates = fxRates;
        PortfolioValuesCurrency = portfolioValuesCurrency;
        using var portfolioValuesReader = new StreamReader(portfolioValuesPath);
        using var portfolioValuesCsvReader = new CsvReader(portfolioValuesReader, CsvConfiguration(basics));
        PortfolioValuesLocal = LoadPortfolioValues(basics, portfolioValuesCsvReader);
    }

    internal /* for testing */ CryptoPortfolioValues(
        Basics basics, FxRates fxRates, string portfolioValuesCurrency, TextReader portfolioValuesReader)
    {
        FxRates = fxRates;
        PortfolioValuesCurrency = portfolioValuesCurrency;
        using var portfolioValuesCsvReader = new CsvReader(portfolioValuesReader, CsvConfiguration(basics));
        PortfolioValuesLocal = LoadPortfolioValues(basics, portfolioValuesCsvReader);
    }

    private static Dictionary<DateTime, decimal> LoadPortfolioValues(
        Basics basics, CsvReader portfolioValuesCsvReader) => 
        portfolioValuesCsvReader.GetRecords<PortfolioValueStr>().ToDictionary(
            record => DateTime.ParseExact(record.Date, "yyyy-MM-dd", basics.DefaultCulture),
            record => decimal.Parse(record.PortfolioValue, basics.DefaultCulture));

    public decimal this[DateTime date]
    {
        get
        {
            // We don't have portfolio current value for all events, we barely have it for sell events
            if (!PortfolioValuesLocal.TryGetValue(date.Date, out var portfolioValueLocal))
                return -1m;

            // An FX Rate is considered valid if it's found for the local currency of the portfolio and for the day or,
            // in case of weekend days, one of the next two days following the date
            decimal fxRate = 0m;
            var validFxRateFound =
                FxRates.Rates.TryGetValue(PortfolioValuesCurrency, out var currencyFxRates) &&
                (
                    currencyFxRates.TryGetValue(date.Date, out fxRate) ||
                    (IsWeekend(date) && TryGetForTheFollowingTwoDays(date, currencyFxRates, out fxRate))
                );

            if (!validFxRateFound)
                throw new InvalidDataException($"Missing FX Rate for currency {PortfolioValuesCurrency} and day {date.Date}");

            // FX Rates are Base/Local and portfolio value is in Local, so we need to divide to get the value in Bases
            return portfolioValueLocal / fxRate;

            static bool IsWeekend(DateTime date) =>
                date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

            static bool TryGetForTheFollowingTwoDays(DateTime date, Dictionary<DateTime, decimal> currencyFxRates, out decimal fxRate) =>
                currencyFxRates.TryGetValue(date.Date.AddDays(1), out fxRate) ||
                currencyFxRates.TryGetValue(date.Date.AddDays(2), out fxRate);
        }
    }

    [Delimiter(",")]
    private sealed class PortfolioValueStr
    {
        [Name("Date")] public string Date { get; set; } = string.Empty;
        [Name("PortfolioValue")] public string PortfolioValue { get; set; } = string.Empty;
    }
}
