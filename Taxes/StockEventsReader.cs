using CsvHelper;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace Taxes;

static class StockEventsReader
{
    private readonly static Dictionary<string, EventType> StringToEventType = new()
    {
        ["CASH TOP-UP"] = EventType.CashTopUp,
        ["CASH WITHDRAWAL"] = EventType.CashWithdrawal,
        ["BUY - MARKET"] = EventType.BuyMarket,
        ["BUY - LIMIT"] = EventType.BuyLimit,
        ["SELL - MARKET"] = EventType.SellMarket,
        ["SELL - LIMIT"] = EventType.SellLimit,
        ["CUSTODY FEE"] = EventType.CustodyFee,
        ["STOCK SPLIT"] = EventType.StockSplit,
        ["DIVIDEND"] = EventType.Dividend,
    };

    public static IList<Event> Parse(string path, IDictionary<DateTime, decimal> fxRates)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        var events = new List<Event>();
        foreach (var record in csv.GetRecords<EventStr>())
        {
            var date = ReadDateTime(record);

            if (!fxRates.TryGetValue(date.Date, out var fxRate))
            {
                Console.WriteLine($"WARN: No FX Rate found for day {date.Date} -> using {record.FXRate}");
                fxRate = decimal.Parse(record.FXRate);
            }

            decimal? quantity = string.IsNullOrWhiteSpace(record.Quantity) ? null : decimal.Parse(record.Quantity);
            decimal? pricePerShareLocal = string.IsNullOrWhiteSpace(record.PricePerShare) ? null : decimal.Parse(Sanitize(record.PricePerShare));
            decimal? sharesPriceLocal = pricePerShareLocal == null ? null : pricePerShareLocal * quantity;
            decimal totalAmountLocal = decimal.Parse(Sanitize(record.TotalAmount));

            // The difference between total amount and shares price is GENERALLY positive for BUY and negative for SELL
            decimal? feesLocal = sharesPriceLocal == null ? null : Math.Abs(totalAmountLocal - sharesPriceLocal.Value);

            events.Add(new(
                Date: date,
                Ticker: string.IsNullOrWhiteSpace(record.Ticker) ? null : record.Ticker,
                Type: StringToEventType[record.Type],
                Quantity: quantity,
                PricePerShareLocal: pricePerShareLocal,
                TotalAmountLocal: totalAmountLocal,
                FeesLocal: feesLocal,
                Currency: record.Currency,
                FXRate: fxRate,
                PortfolioCurrentValueBase: -1m // Info not available, only available in crypto sell events
            ));
        }

        return events;
    }

    private static DateTime ReadDateTime(EventStr record)
    {
        if (DateTime.TryParseExact(record.Date, "yyyy-MM-ddTHH:mm:ss.ffffffK", CultureInfo.InvariantCulture, 
            DateTimeStyles.RoundtripKind, out var sixDecimalsDate))
            return sixDecimalsDate;
        if (DateTime.TryParseExact(record.Date, "yyyy-MM-ddTHH:mm:ss.fffK", CultureInfo.InvariantCulture,
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
