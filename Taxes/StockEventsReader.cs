using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Taxes;

partial class StockEventsReader(Basics basics)
{
    public Basics Basics => basics;

    public IList<Event> Parse(string path, FxRates fxRates)
    {
        using var reader = new StreamReader(path);
        return Parse(reader, fxRates);
    }

    public IList<Event> Parse(TextReader textReader, FxRates fxRates)
    {
        var csvConfiguration = new CsvConfiguration(basics.DefaultCulture)
        {
            Delimiter = ",",
            DetectColumnCountChanges = true,
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
        };
        using var csv = new CsvReader(textReader, csvConfiguration);

        var events = new List<Event>();
        foreach (var record in csv.GetRecords<EventStr>())
        {
            var currency = record.Currency;
            var recordFxRate = decimal.Parse(record.FXRate);

            if (string.IsNullOrWhiteSpace(record.TotalAmount))
                throw new InvalidOperationException("Invalid Total Amount");
            if (currency == basics.BaseCurrency && recordFxRate != 1.0m)
                throw new InvalidOperationException($"Invalid FX Rate {record.FXRate} for base currency {basics.BaseCurrency}");

            var date = ReadDateTime(record);
            var type = basics.StringToEventType[record.Type];
            var ticker = string.IsNullOrWhiteSpace(record.Ticker) ? null : record.Ticker;

            if (!fxRates.Rates.TryGetValue(currency, out var currencyRates)
                || !currencyRates.TryGetValue(date.Date, out var fxRate))
            {
                if (currency != basics.BaseCurrency)
                    Console.WriteLine($"WARN: No FX Rate found for currency {record.Currency} and day {date.Date} -> using {record.FXRate}");

                if (recordFxRate < 0)
                    throw new InvalidOperationException(
                        $"No FX Rate found for currency {record.Currency} and day {date.Date} and invalid event FX Rate");

                fxRate = recordFxRate;
            }

            decimal? quantity = string.IsNullOrWhiteSpace(record.Quantity) ? null : decimal.Parse(record.Quantity);
            decimal? pricePerShareLocal = string.IsNullOrWhiteSpace(record.PricePerShare) ? null : decimal.Parse(Sanitize(record.PricePerShare));
            decimal? sharesPriceLocal = pricePerShareLocal * quantity;
            decimal totalAmountLocal =  decimal.Parse(Sanitize(record.TotalAmount));

            // The difference between total amount and shares price is GENERALLY positive for BUY and negative for SELL.
            // However, due to rounding, it can be negative for BUY and positive for SELL.
            // In those cases, fees are set to zero.
            decimal? feesLocal = (sharesPriceLocal, type) switch
            {
                (null, _) => null,
                (not null, EventType.BuyLimit or EventType.BuyMarket) => 
                    Math.Max(0, totalAmountLocal - sharesPriceLocal.Value),
                (not null, EventType.SellLimit or EventType.SellMarket) =>
                    Math.Max(0, sharesPriceLocal.Value - totalAmountLocal),
                (not null, _) => throw new InvalidOperationException($"Invalid type {type}")
            };

            events.Add(new(
                Date: date,
                Type: type,
                Ticker: ticker,
                Quantity: quantity,
                PricePerShareLocal: pricePerShareLocal,
                TotalAmountLocal: totalAmountLocal,
                FeesLocal: feesLocal,
                Currency: currency,
                FXRate: fxRate
            ));
        }

        return events;
    }

    private DateTime ReadDateTime(EventStr record)
    {
        // Old Revolut Stocks and Dividends format
        if (DateTime.TryParseExact(record.Date, "yyyy-MM-ddTHH:mm:ss.ffffffK", basics.DefaultCulture, 
            DateTimeStyles.RoundtripKind, out var sixDecimalsDate))
            return sixDecimalsDate;
        // New Revolut Stocks and Dividends format (the old one is also present in new reports)
        if (DateTime.TryParseExact(record.Date, "yyyy-MM-ddTHH:mm:ss.fffK", basics.DefaultCulture,
            DateTimeStyles.RoundtripKind, out var threeDecimalsDate))
            return threeDecimalsDate;
        // IBKR Trades format
        if (DateTime.TryParseExact(record.Date, "yyyy-MM-dd, HH:mm:ss", basics.DefaultCulture,
            DateTimeStyles.RoundtripKind, out var commaDate))
            return commaDate;
        // IBKR Dividends and Withholding Tax format
        if (DateTime.TryParseExact(record.Date, "yyyy-MM-dd", basics.DefaultCulture,
            DateTimeStyles.RoundtripKind, out var date))
            return date;
        throw new FormatException($"Unable to parse date: '{record.Date}'");
    }

    private static string Sanitize(string value) => 
        value.TrimStart('(').TrimEnd(')').Trim('$', '€', '£') is var valueWithoutParenthesesAndSymbol
            && TotalQuantityCurrencyPrefix().Match(valueWithoutParenthesesAndSymbol) is { Success: true, Length: var length }
            ? valueWithoutParenthesesAndSymbol[length..]
            : valueWithoutParenthesesAndSymbol;

    [GeneratedRegex("^(?<currecyName>[A-Za-z]+)\\s")]
    private static partial Regex TotalQuantityCurrencyPrefix();

    [Delimiter(",")]
    private sealed class EventStr
    {
        [Name("Date")] public string Date { get; set; } = string.Empty;
        [Name("Ticker")] public string Ticker { get; set; } = string.Empty;
        [Name("Type")] public string Type { get; set; } = string.Empty;
        [Name("Quantity")] public string Quantity { get; set; } = string.Empty;
        [Name("Price per share")] public string PricePerShare { get; set; } = string.Empty;
        [Name("Total Amount")] public string TotalAmount { get; set; } = string.Empty;
        [Name("Currency")] public string Currency { get; set; } = string.Empty;
        [Name("FX Rate")] public string FXRate { get; set; } = string.Empty;
    }
}
