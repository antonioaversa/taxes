using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Taxes;

public partial class Basics
{
    public string ReportsDirectoryPath { get; }

    public string BasicsFileName { get; }

    // Hardcoded
    public Dictionary<string, EventType> StringToEventType { get; } = new()
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
        ["INTEREST"]  = EventType.Interest,
    };

    public string[] FxRatesHeaderLinesFirstWord { get; } =
        ["Titre", "Code série", "Unité", "Magnitude", "Méthode", "Source"];

    public CultureInfo DefaultCulture { get; } = CultureInfo.InvariantCulture;

    public CultureInfo FxRatesMultiCurrenciesCulture { get; } = CultureInfo.GetCultureInfo("fr-FR");

    public CultureInfo Form2074Culture { get; } = CultureInfo.GetCultureInfo("fr-FR");

    // Read from Basics.json
    public Func<decimal, decimal> Rounding { get; init; }
    public decimal Precision { get; init; }
    public string BaseCurrency { get; init; }
    public ReadOnlyDictionary<string, Position> Positions { get; init; }
    public ReadOnlyCollection<EventsFiles> StockEventsFiles { get; init; }
    public ReadOnlyCollection<EventsFiles> CryptoEventsFiles { get; init; }
    public string CryptoPortfolioValuesFilePath { get; init; }
    public bool MergeAllCryptos { get; init; }
    public string FXRatesFilePath { get; init; }
    public ReadOnlyDictionary<string, CountryWithholdingTaxes> WithholdingTaxes { get; init; }

    public Basics(string reportsDirectoryPath = "Reports", string basicsFileName = "Basics.json")
    {
        ReportsDirectoryPath = Directory.Exists(reportsDirectoryPath)
            ? reportsDirectoryPath
            : throw new DirectoryNotFoundException(reportsDirectoryPath);
        BasicsFileName = basicsFileName;

        var basicsFilePath = Path.Combine(reportsDirectoryPath, basicsFileName);
        if (!File.Exists(basicsFilePath))
            throw new FileNotFoundException(basicsFilePath);

        var basicsFileContentStr = File.ReadAllText(Path.Combine(reportsDirectoryPath, basicsFileName));
        var basicsFile = JsonConvert.DeserializeObject<BasicsFile>(basicsFileContentStr) 
            ?? throw new InvalidDataException($"Invalid {basicsFileName}");

        Rounding = (basicsFile.Rounding
            ?? throw new InvalidDataException($"Invalid {nameof(Rounding)} in {basicsFileName}")) switch
            {
                var r when Regex_RoundingWithNumberOfDigits().Match(r) is { Success: true, Groups: var groups } =>
                    value => RoundingWithNumberOfDigits(
                        value, 
                        int.Parse(groups["numberOfDigits"].Value, DefaultCulture)),
                var r when Regex_RoundingWithResolutionAroundZero().Match(r) is { Success: true, Groups: var groups } =>
                    value => RoundingWithResolutionAroundZero(
                        value, 
                        int.Parse(groups["numberOfDigits"].Value, DefaultCulture),
                        decimal.Parse(groups["resolutionAroundZero"].Value, DefaultCulture)),
                var r => throw new InvalidDataException($"Invalid {nameof(Rounding)} value in {basicsFileName}: {r}")
            };
        Precision = basicsFile.Precision 
            ?? throw new InvalidDataException($"Invalid {nameof(Precision)} in {basicsFileName}");
        BaseCurrency = basicsFile.BaseCurrency
            ?? throw new InvalidDataException($"Invalid {nameof(BaseCurrency)} in {basicsFileName}");
        Positions = new ReadOnlyDictionary<string, Position>(basicsFile.Positions
            ?? throw new InvalidDataException($"Invalid {nameof(Positions)} in {basicsFileName}"));
        StockEventsFiles = (basicsFile.StockEventsFiles
            ?? throw new InvalidDataException($"Invalid {nameof(StockEventsFiles)} in {basicsFileName}")).AsReadOnly();
        
        CryptoEventsFiles = (basicsFile.CryptoEventsFiles
            ?? throw new InvalidDataException($"Invalid {nameof(CryptoEventsFiles)} in {basicsFileName}")).AsReadOnly();
        CryptoPortfolioValuesFilePath = basicsFile.CryptoPortfolioValuesFilePath
            ?? throw new InvalidDataException($"Invalid {nameof(CryptoPortfolioValuesFilePath)} in {basicsFileName}");
        MergeAllCryptos = basicsFile.MergeAllCryptos
            ?? throw new InvalidDataException($"Invalid {nameof(MergeAllCryptos)} in {basicsFileName}");
        
        FXRatesFilePath = basicsFile.FXRatesFilePath
            ?? throw new InvalidDataException($"Invalid {nameof(FXRatesFilePath)} in {basicsFileName}");

        WithholdingTaxes = new ReadOnlyDictionary<string, CountryWithholdingTaxes>(basicsFile.WithholdingTaxes
            ?? throw new InvalidDataException($"Invalid {nameof(WithholdingTaxes)} in {basicsFileName}"));

        static decimal RoundingWithNumberOfDigits(decimal value, int numberOfDigits) =>
            Math.Round(value, numberOfDigits);

        static decimal RoundingWithResolutionAroundZero(decimal value, int numberOfDigits, decimal resolutionAroundZero) =>
            Math.Abs(Math.Round(value, numberOfDigits)) < resolutionAroundZero ? 0m : Math.Round(value, numberOfDigits);
    }

    [GeneratedRegex(@"^Fixed_(?<numberOfDigits>\d+)$")]
    private static partial Regex Regex_RoundingWithNumberOfDigits();
    
    [GeneratedRegex(@"^Fixed_(?<numberOfDigits>\d+)_(?<resolutionAroundZero>[\d\.]+)$")]
    private static partial Regex Regex_RoundingWithResolutionAroundZero();

    // This class is used to deserialize the Basics.json file
    private sealed class BasicsFile
    {
        [JsonRequired]
        public string? Rounding { get; set; } = null;
        [JsonRequired]
        public decimal? Precision { get; set; } = null;
        [JsonRequired]
        public string? BaseCurrency { get; set; } = null;
        [JsonRequired]
        public Dictionary<string, Position>? Positions { get; set; } = null;
        [JsonRequired]
        public List<EventsFiles>? StockEventsFiles { get; set; } = null;

        [JsonRequired]
        public List<EventsFiles>? CryptoEventsFiles { get; set; } = null;
        [JsonRequired]
        public string? CryptoPortfolioValuesFilePath { get; set; } = null;
        [JsonRequired]
        public bool? MergeAllCryptos { get; set; } = null;

        [JsonRequired]
        public string? FXRatesFilePath { get; set; } = null;

        [JsonRequired]
        public Dictionary<string, CountryWithholdingTaxes>? WithholdingTaxes { get; set; } = null;
    }

    public sealed class Position
    {
        [JsonRequired]
        public string Country { get; set; } = string.Empty;
        [JsonRequired]
        public string ISIN { get; set; } = string.Empty;
    }

    public sealed class CountryWithholdingTaxes
    {
        [JsonRequired]
        public decimal Dividends { get; set; } = 0m;
        [JsonRequired]
        public decimal Interests { get; set; } = 0m;
    }

    public sealed record EventsFiles(string FilePattern, string Broker);
}
