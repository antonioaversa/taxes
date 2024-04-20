using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Taxes;

public enum FXRatesInputType { SingleCurrency, MultiCurrency }

public partial class Basics
{
    // Hardcoded
    public string ReportsDirectoryPath { get; } = "Reports";

    public string BasicsFileName { get; } = "Basics.json";

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
    };

    public string[] FxRatesHeaderLinesFirstWord { get; } =
        ["Titre", "Code série", "Unité", "Magnitude", "Méthode", "Source"];

    public CultureInfo DefaultCulture { get; } = CultureInfo.InvariantCulture;

    public CultureInfo FxRatesMultiCurrenciesCulture { get; } = CultureInfo.GetCultureInfo("fr-FR");

    // Read from Basics.json
    public Func<decimal, decimal> Rounding { get; }
    public decimal Precision { get; }
    public string BaseCurrency { get; }
    public ReadOnlyDictionary<string, string> ISINs { get; }
    public ReadOnlyCollection<string> StockEventsFilePaths { get; }
    public ReadOnlyCollection<string> CryptoEventsFilePaths { get; }
    public string CryptoPortfolioValuesCurrency { get; }
    public string CryptoPortfolioValuesFilePath { get; }
    public FXRatesInputType FXRatesInputType { get; }
    public string FXRatesSingleCurrency { get; }
    public string FXRatesFilePath { get; }

    public Basics()
    {
        var basicsFileContentStr = File.ReadAllText(Path.Combine(ReportsDirectoryPath, BasicsFileName));
        var basicsFile = JsonConvert.DeserializeObject<BasicsFile>(basicsFileContentStr) 
            ?? throw new Exception($"Invalid {BasicsFileName}");

        Rounding = (basicsFile.Rounding
            ?? throw new Exception($"Invalid {nameof(Rounding)} in {BasicsFileName}")) switch
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
                var r => throw new Exception($"Invalid {nameof(Rounding)} value in {BasicsFileName}: {r}")
            };
        Precision = basicsFile.Precision 
            ?? throw new Exception($"Invalid {nameof(Precision)} in {BasicsFileName}");
        BaseCurrency = basicsFile.BaseCurrency
            ?? throw new Exception($"Invalid {nameof(BaseCurrency)} in {BasicsFileName}");
        ISINs = new ReadOnlyDictionary<string, string>(basicsFile.ISINs
            ?? throw new Exception($"Invalid {nameof(ISINs)} in {BasicsFileName}"));
        StockEventsFilePaths = (basicsFile.StockEventsFilePaths
            ?? throw new Exception($"Invalid {nameof(StockEventsFilePaths)} in {BasicsFileName}")).AsReadOnly();
        
        CryptoEventsFilePaths = (basicsFile.CryptoEventsFilePaths
            ?? throw new Exception($"Invalid {nameof(CryptoEventsFilePaths)} in {BasicsFileName}")).AsReadOnly();
        CryptoPortfolioValuesCurrency = basicsFile.CryptoPortfolioValuesCurrency
            ?? throw new Exception($"Invalid {nameof(CryptoPortfolioValuesCurrency)} in {BasicsFileName}");
        CryptoPortfolioValuesFilePath = basicsFile.CryptoPortfolioValuesFilePath
            ?? throw new Exception($"Invalid {nameof(CryptoPortfolioValuesFilePath)} in {BasicsFileName}");

        FXRatesInputType = Enum.Parse<FXRatesInputType>(basicsFile.FXRatesInputType
            ?? throw new Exception($"Invalid {nameof(FXRatesInputType)} in {BasicsFileName}"));
        FXRatesSingleCurrency = FXRatesInputType == FXRatesInputType.MultiCurrency == string.IsNullOrEmpty(basicsFile.FXRatesSingleCurrency)
            ? basicsFile.FXRatesSingleCurrency!
            : throw new Exception($"Inconsistent {nameof(FXRatesInputType)} and {nameof(FXRatesSingleCurrency)} in {BasicsFileName}");
        FXRatesFilePath = basicsFile.FXRatesFilePath
            ?? throw new Exception($"Invalid {nameof(FXRatesFilePath)} in {BasicsFileName}");

        static decimal RoundingWithNumberOfDigits(decimal value, int numberOfDigits) =>
            Math.Round(value, numberOfDigits);

        static decimal RoundingWithResolutionAroundZero(decimal value, int numberOfDigits, decimal resolutionAroundZero) =>
            Math.Abs(Math.Round(value, numberOfDigits)) < resolutionAroundZero ? 0m : Math.Round(value, numberOfDigits);
    }

    public decimal WitholdingTaxFor(string isin) => 
        isin switch
        {
            var s when s.StartsWith("US") && s[2] - '0' <= 9 => 0.15m,
            var s when s.StartsWith("BMG") && s[3] - '0' <= 9 => 0.15m,
            var s => throw new NotSupportedException($"Unknown WHT for {s}"),
        };

    [GeneratedRegex(@"^Fixed_(?<numberOfDigits>\d+)$")]
    private static partial Regex Regex_RoundingWithNumberOfDigits();
    
    [GeneratedRegex(@"^Fixed_(?<numberOfDigits>\d+)_(?<resolutionAroundZero>[\d\.]+)$")]
    private static partial Regex Regex_RoundingWithResolutionAroundZero();

    // This class is used to deserialize the Basics.json file
    private sealed class BasicsFile
    {
        public string? Rounding { get; set; }
        public decimal? Precision { get; set; }
        public string? BaseCurrency { get; set; }
        public Dictionary<string, string>? ISINs { get; set; }
        public List<string>? StockEventsFilePaths { get; set; }
        
        public List<string>? CryptoEventsFilePaths { get; set; }
        public string? CryptoPortfolioValuesCurrency { get; set; }
        public string? CryptoPortfolioValuesFilePath { get; set; }
        
        public string? FXRatesInputType { get; set; }
        public string? FXRatesSingleCurrency { get; set; }
        public string? FXRatesFilePath { get; set; }
    }
}

public static class DecimalExtensions
{
    public static decimal R(this decimal value, Basics basics) => basics.Rounding(value);
}