using System.Globalization;

namespace Taxes;

internal static class CryptoQuotesReader
{
    public static IEnumerable<CryptoQuote> Read(string filePath)
    {
        var lines = File.ReadLines(filePath).Skip(1); // Skip header

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(',');
            if (columns.Length < 10)
            {
                throw new FormatException($"Line has {columns.Length} columns, expected at least 10. File: '{filePath}', Line: '{line}'");
            }

            if (!long.TryParse(columns[0], out var unix) ||
                !DateTime.TryParse(columns[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
                !decimal.TryParse(columns[3], NumberStyles.Any, CultureInfo.InvariantCulture, out var open) ||
                !decimal.TryParse(columns[4], NumberStyles.Any, CultureInfo.InvariantCulture, out var high) ||
                !decimal.TryParse(columns[5], NumberStyles.Any, CultureInfo.InvariantCulture, out var low) ||
                !decimal.TryParse(columns[6], NumberStyles.Any, CultureInfo.InvariantCulture, out var close) ||
                !decimal.TryParse(columns[7], NumberStyles.Any, CultureInfo.InvariantCulture, out var volumeCrypto) ||
                !decimal.TryParse(columns[8], NumberStyles.Any, CultureInfo.InvariantCulture, out var volumeBase) ||
                !long.TryParse(columns[9], out var tradeCount))
            {
                throw new FormatException($"Failed to parse one or more values. File: '{filePath}', Line: '{line}'");
            }

            yield return new CryptoQuote
            {
                Unix = unix,
                Date = date,
                Symbol = columns[2],
                Open = open,
                High = high,
                Low = low,
                Close = close,
                VolumeCrypto = volumeCrypto,
                VolumeBase = volumeBase,
                TradeCount = tradeCount
            };
        }
    }
} 