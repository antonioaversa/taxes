using System.Globalization;

namespace Taxes.Tests;

[TestClass]
public class CryptoQuotesReaderTest
{
    private const string ReportsPath = "../../../../Taxes/Reports";

    public static IEnumerable<object[]> AllCryptoQuoteFiles
    {
        get
        {
            var files = Directory.GetFiles(ReportsPath, "MarketQuotes_*.csv");
            foreach (var file in files)
            {
                yield return new object[] { file };
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(AllCryptoQuoteFiles))]
    public void Read_AllFiles_DoesNotThrowAndIsNotEmpty(string filePath)
    {
        // Act
        var quotes = CryptoQuotesReader.Read(filePath).ToList();

        // Assert
        Assert.IsNotNull(quotes);
        Assert.IsTrue(quotes.Count > 0, $"File {filePath} should contain at least one quote.");
        
        // Check that dates are parsed correctly and not default
        Assert.IsTrue(quotes[0].Date > new DateTime(2000, 1, 1), $"First quote in {filePath} has an invalid date.");
    }

    [TestMethod]
    public void Read_SpecificFile_ParsesFirstLineCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(ReportsPath, "MarketQuotes_ADAEUR.csv");
        // From file: 1747699200000,2025-05-20,ADAEUR,0.6615,0.6703,0.6433,0.6607,2884102.0,1885827.80505,10704
        var expectedQuote = new CryptoQuote
        {
            Unix = 1747699200000,
            Date = DateTime.ParseExact("2025-05-20 00:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            Symbol = "ADAEUR",
            Open = 0.6615m,
            High = 0.6703m,
            Low = 0.6433m,
            Close = 0.6607m,
            VolumeCrypto = 2884102.0m,
            VolumeBase = 1885827.80505m,
            TradeCount = 10704
        };

        // Act
        var firstQuote = CryptoQuotesReader.Read(filePath).FirstOrDefault();

        // Assert
        Assert.IsNotNull(firstQuote);
        Assert.AreEqual(expectedQuote.Unix, firstQuote.Unix);
        Assert.AreEqual(expectedQuote.Date, firstQuote.Date);
        Assert.AreEqual(expectedQuote.Symbol, firstQuote.Symbol);
        Assert.AreEqual(expectedQuote.Open, firstQuote.Open);
        Assert.AreEqual(expectedQuote.High, firstQuote.High);
        Assert.AreEqual(expectedQuote.Low, firstQuote.Low);
        Assert.AreEqual(expectedQuote.Close, firstQuote.Close);
        Assert.AreEqual(expectedQuote.VolumeCrypto, firstQuote.VolumeCrypto);
        Assert.AreEqual(expectedQuote.VolumeBase, firstQuote.VolumeBase);
        Assert.AreEqual(expectedQuote.TradeCount, firstQuote.TradeCount);
    }

    [TestMethod]
    public void Read_FileWithBlankLine_SkipsLine()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        var fileContent = string.Join(Environment.NewLine,
            "Unix,Date,Symbol,Open,High,Low,Close,Volume ADA,Volume EUR,tradecount",
            "1747699200000,2025-05-20,ADAEUR,0.6615,0.6703,0.6433,0.6607,2884102.0,1885827.80505,10704",
            "", // Blank line
            "1747612800000,2025-05-19,ADAEUR,0.6794,0.6839,0.6344,0.6611,3067215.9,2002787.87862,12394"
        );
        File.WriteAllText(tempFilePath, fileContent);

        // Act
        var quotes = CryptoQuotesReader.Read(tempFilePath).ToList();

        // Assert
        Assert.AreEqual(2, quotes.Count, "Should skip the blank line and read 2 quotes.");

        // Cleanup
        File.Delete(tempFilePath);
    }

    [TestMethod]
    public void Read_LineWithTooFewColumns_ThrowsFormatException()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        var fileContent = string.Join(Environment.NewLine,
            "Unix,Date,Symbol,Open,High,Low,Close,Volume ADA,Volume EUR,tradecount",
            "1747699200000,2025-05-20,ADAEUR,0.6615,0.6703,0.6433" // Not enough columns
        );
        File.WriteAllText(tempFilePath, fileContent);

        // Act & Assert
        var ex = Assert.ThrowsException<FormatException>(() => CryptoQuotesReader.Read(tempFilePath).ToList());
        Assert.IsTrue(ex.Message.Contains("expected at least 10"));

        // Cleanup
        File.Delete(tempFilePath);
    }

    [TestMethod]
    public void Read_LineWithUnparsableData_ThrowsFormatException()
    {
        // Arrange
        var tempFilePath = Path.GetTempFileName();
        // The 'Open' column has non-numeric data
        var fileContent = string.Join(Environment.NewLine,
            "Unix,Date,Symbol,Open,High,Low,Close,Volume ADA,Volume EUR,tradecount",
            "1747699200000,2025-05-20,ADAEUR,not-a-decimal,0.6703,0.6433,0.6607,2884102.0,1885827.80505,10704"
        );
        File.WriteAllText(tempFilePath, fileContent);

        // Act & Assert
        var ex = Assert.ThrowsException<FormatException>(() => CryptoQuotesReader.Read(tempFilePath).ToList());
        Assert.IsTrue(ex.Message.Contains("Failed to parse one or more values"));

        // Cleanup
        File.Delete(tempFilePath);
    }
} 