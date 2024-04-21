﻿using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Taxes;

public enum FXRatesInputType { SingleCurrency, MultiCurrency }

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
    };

    public string[] FxRatesHeaderLinesFirstWord { get; } =
        ["Titre", "Code série", "Unité", "Magnitude", "Méthode", "Source"];

    public CultureInfo DefaultCulture { get; } = CultureInfo.InvariantCulture;

    public CultureInfo FxRatesMultiCurrenciesCulture { get; } = CultureInfo.GetCultureInfo("fr-FR");

    // Read from Basics.json
    public Func<decimal, decimal> Rounding { get; init; }
    public decimal Precision { get; init; }
    public string BaseCurrency { get; init; }
    public ReadOnlyDictionary<string, string> ISINs { get; init; }
    public ReadOnlyCollection<string> StockEventsFilePaths { get; init; }
    public ReadOnlyCollection<string> CryptoEventsFilePaths { get; init; }
    public string CryptoPortfolioValuesCurrency { get; init; }
    public string CryptoPortfolioValuesFilePath { get; init; }
    public FXRatesInputType FXRatesInputType { get; init; }
    public string FXRatesSingleCurrency { get; init; }
    public string FXRatesFilePath { get; init; }

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
                var r => throw new Exception($"Invalid {nameof(Rounding)} value in {basicsFileName}: {r}")
            };
        Precision = basicsFile.Precision 
            ?? throw new InvalidDataException($"Invalid {nameof(Precision)} in {basicsFileName}");
        BaseCurrency = basicsFile.BaseCurrency
            ?? throw new InvalidDataException($"Invalid {nameof(BaseCurrency)} in {basicsFileName}");
        ISINs = new ReadOnlyDictionary<string, string>(basicsFile.ISINs
            ?? throw new InvalidDataException($"Invalid {nameof(ISINs)} in {basicsFileName}"));
        StockEventsFilePaths = (basicsFile.StockEventsFilePaths
            ?? throw new InvalidDataException($"Invalid {nameof(StockEventsFilePaths)} in {basicsFileName}")).AsReadOnly();
        
        CryptoEventsFilePaths = (basicsFile.CryptoEventsFilePaths
            ?? throw new InvalidDataException($"Invalid {nameof(CryptoEventsFilePaths)} in {basicsFileName}")).AsReadOnly();
        CryptoPortfolioValuesCurrency = basicsFile.CryptoPortfolioValuesCurrency
            ?? throw new InvalidDataException($"Invalid {nameof(CryptoPortfolioValuesCurrency)} in {basicsFileName}");
        CryptoPortfolioValuesFilePath = basicsFile.CryptoPortfolioValuesFilePath
            ?? throw new InvalidDataException($"Invalid {nameof(CryptoPortfolioValuesFilePath)} in {basicsFileName}");

        FXRatesInputType = Enum.Parse<FXRatesInputType>(basicsFile.FXRatesInputType
            ?? throw new InvalidDataException($"Invalid {nameof(FXRatesInputType)} in {basicsFileName}"));
        FXRatesSingleCurrency = FXRatesInputType == FXRatesInputType.MultiCurrency == string.IsNullOrEmpty(basicsFile.FXRatesSingleCurrency)
            ? basicsFile.FXRatesSingleCurrency!
            : throw new InvalidDataException($"Inconsistent {nameof(FXRatesInputType)} and {nameof(FXRatesSingleCurrency)} in {basicsFileName}");
        FXRatesFilePath = basicsFile.FXRatesFilePath
            ?? throw new InvalidDataException($"Invalid {nameof(FXRatesFilePath)} in {basicsFileName}");

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
        public string? Rounding { get; set; } = null;
        public decimal? Precision { get; set; } = null;
        public string? BaseCurrency { get; set; } = null;
        public Dictionary<string, string>? ISINs { get; set; } = null;
        public List<string>? StockEventsFilePaths { get; set; } = null;

        public List<string>? CryptoEventsFilePaths { get; set; } = null;
        public string? CryptoPortfolioValuesCurrency { get; set; } = null;
        public string? CryptoPortfolioValuesFilePath { get; set; } = null;

        public string? FXRatesInputType { get; set; } = null;
        public string? FXRatesSingleCurrency { get; set; } = null;
        public string? FXRatesFilePath { get; set; } = null;
    }
}
