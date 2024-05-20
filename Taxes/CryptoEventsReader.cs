using CsvHelper;
using CsvHelper.Configuration.Attributes;

namespace Taxes;

class CryptoEventsReader(Basics basics)
{
    const string Type_Reset = "RESET";
    const string Type_Transfer = "TRANSFER";
    const string Type_Exchange = "EXCHANGE";
    const string Type_Reward = "REWARD";
    const string Product_Current = "Current";
    const string State_Completed = "COMPLETED";

    public Basics Basics => basics;

    public IList<Event> Parse(string path, string broker)
    {
        using var reader = new StreamReader(path);
        return Parse(reader, broker);
    }

    public IList<Event> Parse(TextReader eventsReader, string broker)
    {
        // TODO: fix culture in decimal.Parse
        using var eventsCsv = new CsvReader(eventsReader, basics.DefaultCulture);

        var events = new List<Event>();
        foreach (var record in eventsCsv.GetRecords<EventStr>())
        {
            var date = DateTime.ParseExact(record.StartedDate, "yyyy-MM-dd HH:mm:ss", basics.DefaultCulture);
            if (record.StartedDate != record.CompletedDate)
                throw new NotSupportedException($"Started date != completed date: {record}");
            if (record.BaseCurrency != Basics.BaseCurrency)
                throw new NotSupportedException($"Record base currency {record.BaseCurrency}: {record}");

            if (record.Type == Type_Reset)
            {
                events.Add(new(date, EventType.Reset, null, null, null, 0, null, record.BaseCurrency, 1.0m, broker));
                continue;
            }

            if (record.Type == Type_Transfer)
            {
                Console.WriteLine($"Ignore record type {Type_Transfer}: {record}");
                continue;
            }

            // TODO: fix
            if (record.Type == Type_Reward)
            {
                Console.WriteLine($"Ignore record type {Type_Reward}: {record}");
                continue;
            }

            if (record.Product != Product_Current)
                throw new NotSupportedException($"Record product {record.Product}: {record}");

            if (record.Type != Type_Exchange)
                throw new NotSupportedException($"Record type {record.Type}: {record}");

            if (record.State != State_Completed)
                throw new NotSupportedException($"Record state {record.State}: {record}");

            var amount = decimal.Parse(record.Amount);
            if (amount == 0)
                throw new InvalidOperationException("Amount is 0");

            var quantity = Math.Abs(amount);
            var type = amount >= 0 ? EventType.BuyMarket : EventType.SellMarket;

            var fiatAmount = decimal.Parse(record.FiatAmount);
            if (Math.Sign(amount) != Math.Sign(fiatAmount))
                throw new InvalidOperationException("Amount and fiat amount have different signs");
            var pricePerShareLocal = Math.Abs(fiatAmount) / quantity;

            var fiatAmountIncFees = decimal.Parse(record.FiatAmountIncFees);
            if (Math.Sign(fiatAmount) != Math.Sign(fiatAmountIncFees))
                throw new InvalidOperationException("Fiat amount and fiat amount inc. fees have different signs");
            var totalAmountLocal = Math.Abs(fiatAmountIncFees);

            // Unlike stocks, the record contains a dedicated field for fees
            var feesLocal = decimal.Parse(record.Fee);
            if (feesLocal < 0)
                throw new InvalidOperationException("Fees are negative");

            // Unlike stocks, which are exchanged against Local FIAT, crypto are exchanged against Base FIAT
            var fxRate = 1m;

            events.Add(new(
                Date: date,
                Type: type,
                Ticker: "CRYPTO",
                Quantity: quantity,
                PricePerShareLocal: pricePerShareLocal,
                TotalAmountLocal: totalAmountLocal,
                FeesLocal: feesLocal,
                Currency: record.BaseCurrency,
                FXRate: fxRate,
                Broker: broker));
        }

        return events;
    }

    [Delimiter(",")]
    private sealed class EventStr
    {
        [Name("Type")] public string Type { get; set; } = string.Empty;
        [Name("Product")] public string Product { get; set; } = string.Empty;
        [Name("Started Date")] public string StartedDate { get; set; } = string.Empty;
        [Name("Completed Date")] public string CompletedDate { get; set; } = string.Empty;
        [Name("Description")] public string Description { get; set; } = string.Empty;
        [Name("Amount")] public string Amount { get; set; } = string.Empty;
        [Name("Currency")] public string Currency { get; set; } = string.Empty;
        [Name("Fiat amount")] public string FiatAmount { get; set; } = string.Empty;
        [Name("Fiat amount (inc. fees)")] public string FiatAmountIncFees { get; set; } = string.Empty;
        [Name("Fee")] public string Fee { get; set; } = string.Empty;
        [Name("Base currency")] public string BaseCurrency { get; set; } = string.Empty;
        [Name("State")] public string State { get; set; } = string.Empty;
        [Name("Balance")] public string Balance { get; set; } = string.Empty;
    }
}
