using System.Globalization;

namespace Taxes;

using static Basics;

static class FXRates
{
    public static IDictionary<DateTime, decimal> Parse(string path)
    {
        var result = new Dictionary<DateTime, decimal>();

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
            if (result.ContainsKey(date))
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
            result[date] = fxRate;
        }

        return result;
    }
}