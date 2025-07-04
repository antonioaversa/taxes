﻿using CsvHelper;
using CsvHelper.Configuration.Attributes;
using System.Globalization;

namespace Taxes;

public class CryptoEventsReader(Basics basics)
{
    // Unlike stocks, which are considered distinct assets, all cryptos, from a tax perspective, are considered a single asset.
    // When a stock is bought or sold, the tax office looks into what specific stock has been affected by the financial event, and tax
    // calculation is done stock by stock.
    // In the case of crypto, however, all cryptocurrencies are consider a single financial asset, and what is taken into account for
    // the calculation of capital gains and losses is the total value of the portfolio at the time the taxable event (e.g. sell of a
    // crypto) is made.
    public const string CryptoTicker = "CRYPTO";
    
    // Example of pre-2025 CSV format:
    // Type,Product,Started Date,Completed Date,Description,Amount,Currency,Fiat amount,Fiat amount (inc. fees),Fee,Base currency,State,Balance
    // RESET,,2023-01-01 00:00:00,2023-01-01 00:00:00,,,,,,,EUR,,
    // EXCHANGE,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
    // TRANSFER,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000
    // REWARD,Current,2022-06-25 13:29:03,2022-06-25 13:29:03,Exchanged to ZRX,1000.0000000000,ZRX,293.9067439000,298.3167439000,4.4100000000,EUR,COMPLETED,1000.0000000000

    private const string Type_Reset = "RESET";
    private const string Type_Transfer = "TRANSFER";
    private const string Type_Exchange = "EXCHANGE";
    private const string Type_Reward = "REWARD";
    private const string Product_Current = "Current";
    private const string Product_CryptoStaking = "Crypto Staking";
    private const string State_Completed = "COMPLETED";

    // Example of 2025 CSV format:
    // Symbol,Type,Quantity,Price,Value,Fees,Date
    // ,Reset,,,,,"Jan 1, 2024, 12:00:00 AM"
    // BTC,Sell,0.02,"EUR 62,671.63","EUR 1,253.43",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
    // BTC,Buy,0.02,"EUR 62,654.96","EUR 1,253.10",EUR 12.41,"Mar 15, 2024, 12:05:32 PM"
    // BTC,Sell,0.03053533,"EUR 63,551.31","EUR 1,940.56",EUR 19.21,"May 22, 2024, 3:30:49 PM"
    // ETH,Sell,0.1,"3,235.24 CHF",323.52 CHF,2.55 CHF,"Dec 1, 2024, 7:58:16 PM"

    private const string Type2025_Buy = "Buy";
    private const string Type2025_Sell = "Sell";
    private const string Type2025_Reset = "Reset";

    public Basics Basics => basics;

    public IList<Event> ParseFile(string path, FxRates fxRates, string broker, TextWriter outWriter)
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
        return ParseContent(content, fxRates, broker, outWriter);
    }

    internal IList<Event> ParseContent(string content, FxRates fxRates, string broker, TextWriter outWriter)
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
            return Parse2025(reader, fxRates, broker, outWriter);
        }
        throw new NotSupportedException($"Unknown file format - headerLine: {headerLine}");
    }

    private IList<Event> ParsePre2025(TextReader eventsReader, string broker, TextWriter outWriter)
    {
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
            
            if (record.State != State_Completed)
                throw new NotSupportedException($"Record state {record.State}: {record}");

            var amount = decimal.Parse(record.Amount, basics.DefaultCulture);
            if (amount == 0)
                throw new InvalidOperationException("Amount is 0");

            var quantity = Math.Abs(amount);
            var type = (amount, record.Type, record.Product) switch
            {
                (<= 0, Type_Reward, Product_CryptoStaking) => throw new InvalidOperationException("Reward Amount is not positive"),
                (> 0, Type_Reward, Product_CryptoStaking) => EventType.Reward,
                (>= 0, Type_Exchange, Product_Current) => EventType.BuyMarket,
                (< 0, Type_Exchange, Product_Current) => EventType.SellMarket,
                _ => throw new InvalidOperationException($"Amount {amount}, type {record.Type}, and product {record.Product} are inconsistent"),
            };
            
            // Unlike stocks, which are considered distinct assets, all cryptos, from a tax perspective, are considered a single asset.
            // This default behavior can be changed by setting mergeAllCryptos to false.
            var ticker = basics.MergeAllCryptos ? CryptoTicker : record.Currency;

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
            // That has changed in 2025, where you can exchange against any FIAT of choice, whether Base FIAT
            // (e.g. EUR) or not (e.g. CHF).
            var currency = record.BaseCurrency;
            var fxRate = 1m;

            events.Add(new(
                Date: date,
                Type: type,
                Ticker: ticker,
                Quantity: quantity,
                PricePerShareLocal: pricePerShareLocal,
                TotalAmountLocal: totalAmountLocal,
                FeesLocal: feesLocal,
                Currency: currency,
                FXRate: fxRate,
                Broker: broker,
                OriginalTicker: record.Currency));
        }

        return events;
    }

    private IList<Event> Parse2025(TextReader eventsReader, FxRates fxRates, string broker, TextWriter outWriter)
    {
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
            
            // Unlike stocks, which are considered distinct assets, all cryptos, from a tax perspective, are considered a single asset.
            // This default behavior can be changed by setting mergeAllCryptos to false.
            var ticker = basics.MergeAllCryptos ? CryptoTicker : record.Symbol;

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
            
            if (Math.Abs(pricePerShareLocal * quantity - priceAllSharesLocal) > basics.Precision)
                throw new InvalidOperationException($"Quantity * Price != Value: {quantity} * {pricePerShareLocal} != {priceAllSharesLocal}");

            var (feesLocal, feesCurrency) = ParseAmountWithCurrency(record.Fees);
            if (feesLocal < 0) // Fees can be zero
                throw new InvalidOperationException("Fees negative");
            
            if (feesCurrency != pricePerShareCurrency)
                throw new InvalidOperationException($"Currencies are inconsistent: Price = {pricePerShareCurrency}, Fees = {feesCurrency}");
            
            var (type, totalAmountLocal) = record.Type switch
            {
                Type2025_Buy => (EventType.BuyMarket, priceAllSharesLocal + feesLocal),
                Type2025_Sell => (EventType.SellMarket, priceAllSharesLocal - feesLocal),
                _ => throw new NotSupportedException($"Record type {record.Type}: {record}"),
            };
            
            // The currency is set always to the base currency (e.g. EUR), and the FX rate is determined from the
            // cryptocurrency (identified from the pricePerShare, and from the priceAllShares) to the base currency.
            var currency = basics.BaseCurrency;
            var fxRate = fxRates[pricePerShareCurrency, date.Date];
            outWriter.WriteLine($"FX rate used for conversion of {pricePerShareCurrency} to base currency ({basics.BaseCurrency}): {fxRate}");
            
            events.Add(new(
                Date: date,
                Type: type,
                Ticker: ticker,
                Quantity: quantity,
                PricePerShareLocal: pricePerShareLocal,
                TotalAmountLocal: totalAmountLocal,
                FeesLocal: feesLocal,
                Currency: currency,
                FXRate: fxRate,
                Broker: broker,
                OriginalTicker: record.Symbol));
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

        if (decimal.TryParse(parts[1], NumberStyles.Any, basics.DefaultCulture, out amount))
        {
            // Second part is a number
            var currency = parts[0];
            return (amount, currency);
        }

        throw new InvalidOperationException($"Invalid amount with currency: {amountWithCurrency}");
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

        public override string ToString()
        {
            return $"{Type},{Product},{StartedDate},{CompletedDate},{Description},{Amount},{Currency},{FiatAmount},{FiatAmountIncFees},{Fee},{BaseCurrency},{State},{Balance}";
        }
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
