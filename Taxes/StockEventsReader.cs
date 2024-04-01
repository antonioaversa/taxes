using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace Taxes;

using static Basics;

static class StockEventsReader
{
    private readonly static Dictionary<string, EventType> StringToEventType = new()
    {
        ["RESET"] = EventType.Reset,
        ["CASH TOP-UP"] = EventType.CashTopUp,
        ["CASH WITHDRAWAL"] = EventType.CashWithdrawal,
        ["BUY - MARKET"] = EventType.BuyMarket,
        ["BUY - LIMIT"] = EventType.BuyLimit,
        ["SELL - MARKET"] = EventType.SellMarket,
        ["SELL - LIMIT"] = EventType.SellLimit,
        ["CUSTODY FEE"] = EventType.CustodyFee,
        ["TRANSFER FROM REVOLUT BANK UAB TO REVOLUT SECURITIES EUROPE UAB"] = EventType.CustodyChange,
        ["TRANSFER FROM REVOLUT TRADING LTD TO REVOLUT SECURITIES EUROPE UAB"] = EventType.CustodyChange,
        ["STOCK SPLIT"] = EventType.StockSplit,
        ["DIVIDEND"] = EventType.Dividend,
    };

    public static IList<Event> Parse(string path, FxRates fxRates)
    {
        using var reader = new StreamReader(path);
        return Parse(reader, fxRates);
    }

    public static IList<Event> Parse(TextReader textReader, FxRates fxRates)
    {
        var csvConfiguration = new CsvConfiguration(DefaultCulture)
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
            var date = ReadDateTime(record);

            if (!fxRates.Rates.TryGetValue(currency, out var currencyRates)
                || !currencyRates.TryGetValue(date.Date, out var fxRate))
            {
                Console.WriteLine($"WARN: No FX Rate found for currency {record.Currency} and day {date.Date} -> using {record.FXRate}");
                fxRate = decimal.Parse(record.FXRate);
            }

            decimal? quantity = string.IsNullOrWhiteSpace(record.Quantity) ? null : decimal.Parse(record.Quantity);
            decimal? pricePerShareLocal = string.IsNullOrWhiteSpace(record.PricePerShare) ? null : decimal.Parse(Sanitize(record.PricePerShare));
            decimal? sharesPriceLocal = pricePerShareLocal * quantity;
            decimal? totalAmountLocal = string.IsNullOrWhiteSpace(record.TotalAmount) ? null : decimal.Parse(Sanitize(record.TotalAmount));

            // The difference between total amount and shares price is GENERALLY positive for BUY and negative for SELL
            decimal? feesLocal = totalAmountLocal == null || sharesPriceLocal == null 
                ? null 
                : Math.Abs(totalAmountLocal.Value - sharesPriceLocal.Value);

            events.Add(new(
                Date: date,
                Type: StringToEventType[record.Type],
                Ticker: string.IsNullOrWhiteSpace(record.Ticker) ? null : record.Ticker,
                Quantity: quantity,
                PricePerShareLocal: pricePerShareLocal,
                TotalAmountLocal: totalAmountLocal,
                FeesLocal: feesLocal,
                Currency: currency,
                FXRate: fxRate,
                PortfolioCurrentValueBase: -1m // Info not available, only available in crypto sell events
            ));
        }

        return events;
    }

    private static DateTime ReadDateTime(EventStr record)
    {
        if (DateTime.TryParseExact(record.Date, "yyyy-MM-ddTHH:mm:ss.ffffffK", DefaultCulture, 
            DateTimeStyles.RoundtripKind, out var sixDecimalsDate))
            return sixDecimalsDate;
        if (DateTime.TryParseExact(record.Date, "yyyy-MM-ddTHH:mm:ss.fffK", DefaultCulture,
            DateTimeStyles.RoundtripKind, out var threeDecimalsDate))
            return threeDecimalsDate;
        throw new FormatException($"Unable to parse date: '{record.Date}'");
    }

    private static string Sanitize(string value) => value.Trim('(').Trim(')').Trim('$');

    [Delimiter(",")]
    class EventStr
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
