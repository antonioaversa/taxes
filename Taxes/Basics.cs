using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Taxes;

public static partial class Basics
{
    private const string BasicsFileName = "Basics.json";

    public static string PathOf(this string fileName) => Path.Combine("Reports", fileName);

    public static readonly Func<decimal, decimal> Rounding;
    public static readonly decimal Precision;
    public static readonly string BaseCurrency;
    public static readonly ReadOnlyDictionary<string, string> ISINs;

    static Basics()
    {
        var basicsFile = JsonConvert.DeserializeObject<BasicsFile>(File.ReadAllText(PathOf(BasicsFileName))) 
            ?? throw new Exception($"Invalid {BasicsFileName}");

        Rounding = (basicsFile.Rounding
            ?? throw new Exception($"Invalid {nameof(R)} in {BasicsFileName}")) switch
        {
            var r when Regex_RoundingWithNumberOfDigits().Match(r) is { Success: true, Groups: var groups } =>
                value => RoundingWithNumberOfDigits(
                    value, 
                    int.Parse(groups["numberOfDigits"].Value, CultureInfo.InvariantCulture)),
            var r when Regex_RoundingWithResolutionAroundZero().Match(r) is { Success: true, Groups: var groups } =>
                value => RoundingWithResolutionAroundZero(
                    value, 
                    int.Parse(groups["numberOfDigits"].Value, CultureInfo.InvariantCulture),
                    int.Parse(groups["resolutionAroundZero"].Value, CultureInfo.InvariantCulture)),
            var r => throw new Exception($"Invalid {nameof(R)} value in {BasicsFileName}: {r}")
        };
        Precision = basicsFile.Precision 
            ?? throw new Exception($"Invalid {nameof(Precision)} in {BasicsFileName}");
        BaseCurrency = basicsFile.BaseCurrency
            ?? throw new Exception($"Invalid {nameof(BaseCurrency)} in {BasicsFileName}");
        ISINs = new ReadOnlyDictionary<string, string>(basicsFile.ISINs
            ?? throw new Exception($"Invalid {nameof(ISINs)} in {BasicsFileName}"));

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

    private sealed class BasicsFile
    {
        public string? Rounding { get; set; }
        public decimal? Precision { get; set; }
        public string? BaseCurrency { get; set; }
        public Dictionary<string, string>? ISINs { get; set; }
    }

    [GeneratedRegex(@"Fixed_(?<numberOfDigits>\d+)")]
    private static partial Regex Regex_RoundingWithNumberOfDigits();
    
    [GeneratedRegex(@"Fixed_(?<numberOfDigits>\d+)_(?<resolutionAroundZero>\d+)")]
    private static partial Regex Regex_RoundingWithResolutionAroundZero();
}
