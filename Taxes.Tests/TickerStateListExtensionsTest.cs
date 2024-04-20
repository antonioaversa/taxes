namespace Taxes.Test;

[TestClass]
public class TickerStateListExtensionsTest
{
    private const string Ticker = "AAPL";
    private const string Isin = "US0378331005";

    private readonly Basics DefaultBasics = new();

    [TestMethod]
    public void GetAggregatedMetrics_NoEvents_ReturnsZeroForAllMetrics()
    {
        List<TickerState> e = [];
        Assert.IsTrue(e.GetAggregatedMetrics(DefaultBasics).All(s => s.EndsWith(" = 0")));
    }

    [TestMethod]
    public void GetAggregatedMetrics_DoesntIncludePropertiesWithoutMetricAttribute()
    {
        List<TickerState> e = [new(Ticker, Isin)];
        var metrics = e.GetAggregatedMetrics(DefaultBasics).ToArray();
        Assert.IsFalse(metrics.Any(s => s.StartsWith("Ticker (")));
        Assert.IsFalse(metrics.Any(s => s.StartsWith("Isin (")));
    }

    [TestMethod]
    public void GetAggregatedMetrics_OneEvent_ReturnsMetricsForThatEvent()
    {
        List<TickerState> e = [new(Ticker, Isin, PlusValueCumpBase: 34, MinusValuePepsBase: 35)];
        var metrics = e.GetAggregatedMetrics(DefaultBasics).ToArray();
        Assert.IsTrue(metrics.Contains($"Total Plus Value CUMP ({DefaultBasics.BaseCurrency}) = 34"));
        Assert.IsTrue(metrics.Contains($"Total Minus Value PEPS ({DefaultBasics.BaseCurrency}) = 35"));
    }

    [TestMethod]
    public void GetAggregatedMetrics_MultipleEvents_ReturnsAggregatedMetrics()
    {
        List<TickerState> e = [
            new(Ticker, Isin, PlusValueCumpBase: 34, MinusValuePepsBase: 35),
            new(Ticker, Isin, PlusValueCumpBase: 36, MinusValuePepsBase: 37)
        ];
        var metrics = e.GetAggregatedMetrics(DefaultBasics).ToArray();
        Assert.IsTrue(metrics.Contains($"Total Plus Value CUMP ({DefaultBasics.BaseCurrency}) = 70"));
        Assert.IsTrue(metrics.Contains($"Total Minus Value PEPS ({DefaultBasics.BaseCurrency}) = 72"));
    }

    [TestMethod]
    public void PrintAggregatedMetrics_WritesOnTheProvidedTextWriter()
    {
        List<TickerState> e = [new(Ticker, Isin, PlusValueCumpBase: 34, MinusValuePepsBase: 35)];
        var writer = new StringWriter();
        e.PrintAggregatedMetrics(writer, DefaultBasics);
        var metrics = writer.ToString().Split(Environment.NewLine);
        Assert.IsTrue(metrics.Contains($"Total Plus Value CUMP ({DefaultBasics.BaseCurrency}) = 34"));
        Assert.IsTrue(metrics.Contains($"Total Minus Value PEPS ({DefaultBasics.BaseCurrency}) = 35"));
    }
}
