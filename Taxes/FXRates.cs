using System.Globalization;

namespace Taxes;

using static Basics;

/// <summary>
/// Defines the FX Rates by currency and day.
/// FX Rates are stored in a dictionary where:
/// - the key is the symbol of the currency (e.g. USD)
/// - and the value is another dictionary where:
///   - the key is the day
///   - and the value is the FX Rate, for that currency, that day.
/// </summary>
public record FxRates(Dictionary<string, Dictionary<DateTime, decimal>> Rates)
{
    /// <summary>
    /// Parses the FX Rates for a single currency from a file with the provided path.
    /// The file needs to have the following format: 
    /// - each line represents the FX Rate for a given day
    /// - the line format is: "M/d/yyyy\tFXRate"
    /// - the file can have comment lines starting with "//"
    /// - the FX Rate must be a positive number in the default culture
    /// </summary>
    public static FxRates ParseSingleCurrency(string currency, string path)
    {
        var currencyFxRates = new Dictionary<DateTime, decimal>();

        foreach (var line in File.ReadAllLines(path))
        {
            if (line.TrimStart().StartsWith("//"))
            {
                Console.WriteLine($"Skipping comment line '{line}'...");
                continue;
            }
            var fields = line.Split('\t');
            if (fields.Length != 2)
                throw new InvalidDataException($"Invalid line: '{line}'");
            if (!DateTime.TryParseExact(fields[0], "M/d/yyyy", DefaultCulture, DateTimeStyles.AssumeLocal, out var date))
                throw new InvalidDataException($"Invalid line: '{line}'");
            if (currencyFxRates.ContainsKey(date))
                throw new InvalidDataException($"Multiple FX Rates for day {date}");

            if (fields[1] == "-")
            {
                Console.WriteLine($"Skipping no-data line: '{line}'...");
                continue;
            }
            if (!decimal.TryParse(fields[1], NumberStyles.Currency, DefaultCulture, out var fxRate))
                throw new InvalidDataException($"Invalid line: '{line}'");
            if (fxRate <= 0)
                throw new InvalidDataException($"Invalid line: '{line}'");
            currencyFxRates[date] = fxRate;
        }

        return new(new Dictionary<string, Dictionary<DateTime, decimal>>() { [currency] = currencyFxRates });
    }
}
