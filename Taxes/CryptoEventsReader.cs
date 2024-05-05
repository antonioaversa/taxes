using CsvHelper;
using CsvHelper.Configuration.Attributes;

namespace Taxes;

class CryptoEventsReader(Basics basics)
{
    const string Type_Transfer = "TRANSFER";
    const string Type_Exchange = "EXCHANGE";
    const string Type_Reward = "REWARD";
    const string Product_Current = "Current";
    const string State_Completed = "COMPLETED";

    public Basics Basics => basics;

    public IList<Event> Parse(string pattern)
    {
        var events = new List<Event>();

        foreach (var path in Directory.GetFiles(".", pattern))
        {
            using var eventsReader = new StreamReader(path);
            using var eventsCsv = new CsvReader(eventsReader, basics.DefaultCulture);

            foreach (var record in eventsCsv.GetRecords<EventStr>())
            {
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

                if (record.Type != Type_Exchange)
                    throw new NotSupportedException($"Record type {record.Type}: {record}");

                if (record.Product != Product_Current)
                    throw new NotSupportedException($"Record product {record.Product}: {record}");

                if (record.StartedDate != record.CompletedDate)
                    throw new NotSupportedException($"Started date != completed date: {record}");

                if (record.State != State_Completed)
                    throw new NotSupportedException($"Record state {record.State}: {record}");

                if (record.BaseCurrency != Basics.BaseCurrency)
                    throw new NotSupportedException($"Record base currency {record.BaseCurrency}: {record}");

                var date = DateTime.ParseExact(record.StartedDate, "yyyy-MM-dd HH:mm:ss", basics.DefaultCulture);
                var amount = decimal.Parse(record.Amount);
                var quantity = Math.Abs(amount);
                var type = amount >= 0 ? EventType.BuyMarket : EventType.SellMarket;
                var totalAmountLocal = Math.Abs(decimal.Parse(record.FiatAmountIncFees));

                // Unlike stocks, the record contains a dedicated field for fees
                var feesLocal = decimal.Parse(record.Fee);

                // Unlike stocks, which are exchanged against Local FIAT, crypto are exchanged against Base FIAT
                var fxRate = 1m;

                events.Add(new(
                    Date: date,
                    Type: type,
                    Ticker: "CRYPTO",
                    Quantity: quantity,
                    PricePerShareLocal: totalAmountLocal / quantity,
                    TotalAmountLocal: totalAmountLocal,
                    FeesLocal: feesLocal,
                    Currency: record.BaseCurrency,
                    FXRate: fxRate));
            }
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
