namespace Taxes.Test;

using static EventType;

[TestClass]
public class TickerStateListExtensionsTest
{
    private const string Ticker = "AAPL";
    private const string Isin = "US0378331005";

    [TestMethod]
    public void GetAggregatedMetrics_NoEvents_ReturnsZeroForAllMetrics()
    {
        List<TickerState> e = [];
        Assert.IsTrue(e.GetAggregatedMetrics().All(s => s.EndsWith(" = 0")));
    }

    [TestMethod]
    public void GetAggregatedMetrics_DoesntIncludePropertiesWithoutMetricAttribute()
    {
        List<TickerState> e = [new(Ticker, Isin)];
        var metrics = e.GetAggregatedMetrics().ToArray();
        Assert.IsFalse(metrics.Any(s => s.StartsWith("Ticker (")));
        Assert.IsFalse(metrics.Any(s => s.StartsWith("Isin (")));
    }

    [TestMethod]
    public void GetAggregatedMetrics_OneEvent_ReturnsMetricsForThatEvent()
    {
        List<TickerState> e = [new(Ticker, Isin, PlusValueCumpBase: 34, MinusValuePepsBase: 35)];
        var metrics = e.GetAggregatedMetrics().ToArray();
        Assert.IsTrue(metrics.Contains($"Total Plus Value CUMP ({Basics.BaseCurrency}) = 34"));
        Assert.IsTrue(metrics.Contains($"Total Minus Value PEPS ({Basics.BaseCurrency}) = 35"));
    }

    [TestMethod]
    public void GetAggregatedMetrics_MultipleEvents_ReturnsAggregatedMetrics()
    {
        List<TickerState> e = [
            new(Ticker, Isin, PlusValueCumpBase: 34, MinusValuePepsBase: 35),
            new(Ticker, Isin, PlusValueCumpBase: 36, MinusValuePepsBase: 37)
        ];
        var metrics = e.GetAggregatedMetrics().ToArray();
        Assert.IsTrue(metrics.Contains($"Total Plus Value CUMP ({Basics.BaseCurrency}) = 70"));
        Assert.IsTrue(metrics.Contains($"Total Minus Value PEPS ({Basics.BaseCurrency}) = 72"));
    }
}