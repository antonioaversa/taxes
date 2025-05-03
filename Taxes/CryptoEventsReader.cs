using CsvHelper;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace Taxes;

class CryptoEventsReader(Basics basics)
{
    // Example of pre-2025 CSV format:
    // Type,Product,Started Date,Completed Date,Description,Amount,Currency,Fiat amount,Fiat amount (inc. fees),Fee,Base currency,State,Balance
    // RESET,,2023-01-01 00:00:00,2023-01-01 00:00:00,,,,,,,EUR,,
    // EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
    // TRANSFER,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
    // REWARD,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000

    const string Type_Reset = "RESET";
    const string Type_Transfer = "TRANSFER";
    const string Type_Exchange = "EXCHANGE";
    const string Type_Reward = "REWARD";
    const string Product_Current = "Current";
    const string State_Completed = "COMPLETED";

    // Example of 2025 CSV format:
    // Symbol,Type,Quantity,Price,Value,Fees,Date
    // ,Reset,,,,,"Jan 1, 2024, 12:00:00 AM"
    // BTC,Sell,0.02,"EUR 62,671.63","EUR 1,253.43",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
    // BTC,Buy,0.02,"EUR 62,654.96","EUR 1,253.10",EUR 12.41,"Mar 15, 2024, 12:05:32 PM"
    // BTC,Sell,0.03053533,"EUR 63,551.31","EUR 1,940.56",EUR 19.21,"May 22, 2024, 3:30:49 PM"
    // ETH,Sell,0.1,"3,235.24 CHF",323.52 CHF,2.55 CHF,"Dec 1, 2024, 7:58:16 PM"

    const string Type2025_Buy = "Buy";
    const string Type2025_Sell = "Sell";
    const string Type2025_Reset = "Reset";

    public Basics Basics => basics;

    public IList<Event> Parse(string path, string broker, TextWriter outWriter)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentNullException(nameof(path));
        if (string.IsNullOrWhiteSpace(broker))
            throw new ArgumentNullException(nameof(broker));
        if (outWriter == null)
            throw new ArgumentNullException(nameof(outWriter));

        if (!File.Exists(path))
            throw new FileNotFoundException(path);

        var content = File.ReadAllText(path);
        return ParseContent(content, broker, outWriter);
    }

    internal IList<Event> ParseContent(string content, string broker, TextWriter outWriter)
    {
        // Check first line to determine the format: pre-2025 or 2025
        var headerLine = content.Split(Environment.NewLine, 2).First();
        if (headerLine.StartsWith("Type"))
        {
            using var reader = new StringReader(content);
            return ParsePre2025(reader, broker, outWriter);
        }
        if (headerLine.StartsWith("Symbol"))
        {
            using var reader = new StringReader(content);
            return Parse2025(reader, broker, outWriter);
        }
        throw new NotSupportedException($"Unknown file format - headerLine: {headerLine}");
    }

    private IList<Event> ParsePre2025(TextReader eventsReader, string broker, TextWriter outWriter)
    {
        // TODO: fix culture in decimal.Parse
        using var eventsCsv = new CsvReader(eventsReader, basics.DefaultCulture);

        var events = new List<Event>();
        foreach (var record in eventsCsv.GetRecords<EventStr>())
        {
            if (string.IsNullOrWhiteSpace(record.StartedDate))
                throw new InvalidOperationException("Started date is empty");

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
                outWriter.WriteLine($"Ignore record type {Type_Transfer}: {record}");
                continue;
            }

            // TODO: implement
            if (record.Type == Type_Reward)
            {
                outWriter.WriteLine($"Ignore record type {Type_Reward}: {record}");
                continue;
            }

            if (record.Product != Product_Current)
                throw new NotSupportedException($"Record product {record.Product}: {record}");

            if (record.Type != Type_Exchange)
                throw new NotSupportedException($"Record type {record.Type}: {record}");

            if (record.State != State_Completed)
                throw new NotSupportedException($"Record state {record.State}: {record}");

            var amount = decimal.Parse(record.Amount, basics.DefaultCulture);
            if (amount == 0)
                throw new InvalidOperationException("Amount is 0");

            var quantity = Math.Abs(amount);
            var type = amount >= 0 ? EventType.BuyMarket : EventType.SellMarket;

            var fiatAmount = decimal.Parse(record.FiatAmount, basics.DefaultCulture);
            if (Math.Sign(amount) != Math.Sign(fiatAmount))
                throw new InvalidOperationException("Amount and fiat amount have different signs");
            var pricePerShareLocal = Math.Abs(fiatAmount) / quantity;

            var fiatAmountIncFees = decimal.Parse(record.FiatAmountIncFees, basics.DefaultCulture);
            if (Math.Sign(fiatAmount) != Math.Sign(fiatAmountIncFees))
                throw new InvalidOperationException("Fiat amount and fiat amount inc. fees have different signs");
            var totalAmountLocal = Math.Abs(fiatAmountIncFees);

            // Unlike stocks, the record contains a dedicated field for fees
            var feesLocal = decimal.Parse(record.Fee, basics.DefaultCulture);
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

    private IList<Event> Parse2025(TextReader eventsReader, string broker, TextWriter outWriter)
    {
        // TODO: fix culture in decimal.Parse
        using var eventsCsv = new CsvReader(eventsReader, basics.DefaultCulture);

        var events = new List<Event>();
        foreach (var record in eventsCsv.GetRecords<Event2025Str>())
        {
            // Date is mandatory for all records types, since it is used to sort the records chronologically
            if (string.IsNullOrWhiteSpace(record.Date))
                throw new InvalidOperationException("Date is empty");
            
            var date = DateTime.ParseExact(record.Date, "MMM d, yyyy, h:mm:ss tt", basics.DefaultCulture);

            if (record.Type == Type2025_Reset)
            {
                if (!string.IsNullOrWhiteSpace(record.Symbol))
                    throw new InvalidOperationException("Symbol is not empty");
                if (!string.IsNullOrWhiteSpace(record.Quantity))
                    throw new InvalidOperationException("Quantity is not empty");
                if (!string.IsNullOrWhiteSpace(record.Price))
                    throw new InvalidOperationException("Price is not empty");
                if (!string.IsNullOrWhiteSpace(record.Value))
                    throw new InvalidOperationException("Value is not empty");
                if (!string.IsNullOrWhiteSpace(record.Fees))
                    throw new InvalidOperationException("Fees is not empty");

                events.Add(new(date, EventType.Reset, null, null, null, 0, null, basics.BaseCurrency, 1.0m, broker));
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.Symbol))
                throw new InvalidOperationException("Symbol is empty");

            if (string.IsNullOrWhiteSpace(record.Quantity))
                throw new InvalidOperationException("Quantity is empty");

            var quantity = decimal.Parse(record.Quantity, basics.DefaultCulture);
            if (quantity <= 0)
                throw new InvalidOperationException("Quantity not positive");

            var (pricePerShareLocal, pricePerShareCurrency) = ParseAmountWithCurrency(record.Price);
            if (pricePerShareLocal <= 0)
                throw new InvalidOperationException("Price not positive");
            
            var (priceAllSharesLocal, priceAllSharesCurrency) = ParseAmountWithCurrency(record.Value);
            if (priceAllSharesLocal <= 0)
                throw new InvalidOperationException("Price all shares not positive");
            
            if (pricePerShareCurrency != priceAllSharesCurrency)
                throw new InvalidOperationException($"Currencies are inconsistent: Price = {pricePerShareCurrency}, Value = {priceAllSharesCurrency}");
            
            if (Math.Abs(pricePerShareLocal * quantity - priceAllSharesLocal) >= basics.Precision)
                throw new InvalidOperationException($"Quantity * Price != Value: {quantity} * {pricePerShareLocal} != {priceAllSharesLocal}");

            var (feesLocal, feesCurrency) = ParseAmountWithCurrency(record.Fees);
            if (feesLocal < 0) // Fees can be zero
                throw new InvalidOperationException("Fees negative");
            
            if (feesCurrency != pricePerShareCurrency)
                throw new InvalidOperationException($"Currencies are inconsistent: Price = {pricePerShareCurrency}, Fees = {feesCurrency}");
            
            if (record.Type == Type2025_Buy)
            {
                var totalAmountLocal = priceAllSharesLocal + feesLocal;
                events.Add(new(
                    Date: date,
                    Type: EventType.BuyMarket,
                    Ticker: record.Symbol,
                    Quantity: quantity,
                    PricePerShareLocal: pricePerShareLocal,
                    TotalAmountLocal: totalAmountLocal,
                    FeesLocal: feesLocal,
                    Currency: pricePerShareCurrency,
                    FXRate: 1m, // TODO: fix this
                    Broker: broker));
                continue;
            }

            if (record.Type == Type2025_Sell)
            {
                var totalAmountLocal = priceAllSharesLocal - feesLocal;
                events.Add(new(
                    Date: date,
                    Type: EventType.SellMarket,
                    Ticker: record.Symbol,
                    Quantity: quantity,
                    PricePerShareLocal: pricePerShareLocal,
                    TotalAmountLocal: totalAmountLocal,
                    FeesLocal: feesLocal,
                    Currency: pricePerShareCurrency,
                    FXRate: 1m,
                    Broker: broker));
                continue;
            }

            throw new NotSupportedException($"Record type {record.Type}: {record}");
        }

        return events;
    }

    // Parses an amount with currency, e.g. "EUR 1,000.00" or "1,000.00 EUR"
    // Returns the amount as a decimal, e.g. 1000.00, converted to the base currency, 
    // defined by basics.BaseCurrency
    private (decimal amount, string currency) ParseAmountWithCurrency(string amountWithCurrency)
    {
        var parts = amountWithCurrency.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            throw new InvalidOperationException($"Invalid amount with currency: {amountWithCurrency}");

        if (decimal.TryParse(parts[0], NumberStyles.Any, basics.DefaultCulture, out var amount))
        {
            // First part is a number
            var currency = parts[1];
            return (amount, currency);
        }
        else if (decimal.TryParse(parts[1], NumberStyles.Any, basics.DefaultCulture, out amount))
        {
            // Second part is a number
            var currency = parts[0];
            return (amount, currency);
        }
        else
        {
            throw new InvalidOperationException($"Invalid amount with currency: {amountWithCurrency}");
        }
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

    [Delimiter(",")]
    private sealed class Event2025Str
    {
        [Name("Symbol")] public string Symbol { get; set; } = string.Empty;
        [Name("Type")] public string Type { get; set; } = string.Empty;
        [Name("Quantity")] public string Quantity { get; set; } = string.Empty;
        [Name("Price")] public string Price { get; set; } = string.Empty;
        [Name("Value")] public string Value { get; set; } = string.Empty;
        [Name("Fees")] public string Fees { get; set; } = string.Empty;
        [Name("Date")] public string Date { get; set; } = string.Empty;
    }
}
