using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Taxes;

public static partial class Basics
{
    public const string ReportsDirectoryPath = "Reports";
    public const string BasicsFileName = "Basics.json";

    public static CultureInfo DefaultCulture => CultureInfo.InvariantCulture;

    // Read from Basics.json
    public static readonly Func<decimal, decimal> Rounding;
    public static readonly decimal Precision;
    public static readonly string BaseCurrency;
    public static readonly ReadOnlyDictionary<string, string> ISINs;
    public static readonly ReadOnlyCollection<string> StockEventsFilePaths;
    public static readonly ReadOnlyCollection<string> CryptoEventsFilePaths;
    public static readonly string CryptoPortfolioValuesCurrency;
    public static readonly string CryptoPortfolioValuesFilePath;

    // Derived
    public static readonly string FXRatesFilePath;

    static Basics()
    {
        var basicsFileContentStr = File.ReadAllText(Path.Combine(ReportsDirectoryPath, BasicsFileName));
        var basicsFile = JsonConvert.DeserializeObject<BasicsFile>(basicsFileContentStr) 
            ?? throw new Exception($"Invalid {BasicsFileName}");

        Rounding = (basicsFile.Rounding
            ?? throw new Exception($"Invalid {nameof(R)} in {BasicsFileName}")) switch
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
                var r => throw new Exception($"Invalid {nameof(R)} value in {BasicsFileName}: {r}")
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

        FXRatesFilePath = $"BCE-FXRate-{BaseCurrency}-USD.txt";

        static decimal RoundingWithNumberOfDigits(decimal value, int numberOfDigits) =>
            Math.Round(value, numberOfDigits);

        static decimal RoundingWithResolutionAroundZero(decimal value, int numberOfDigits, decimal resolutionAroundZero) =>
            Math.Abs(Math.Round(value, numberOfDigits)) < resolutionAroundZero ? 0m : Math.Round(value, numberOfDigits);
    }

    public static decimal R(this decimal value) => Rounding(value);

    public static decimal WitholdingTaxFor(string isin) => 
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
    }
}
