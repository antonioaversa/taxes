using static Taxes.Basics;

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

    [TestMethod]
    public void PrintAggregatedMetrics_AggregatesDividendsByCountry()
    {
        var basics = new Basics
        {
            Positions = new Dictionary<string, Position>
            {
                ["TICKER1"] = new() { Country = "C1", ISIN = "ISIN1" },
                ["TICKER2"] = new() { Country = "C1", ISIN = "ISIN2" },
                ["TICKER3"] = new() { Country = "C2", ISIN = "ISIN3" },
            }.AsReadOnly()
        };
        List<TickerState> e = [
            new("TICKER1", Isin, NetDividendsBase: 2, WhtDividendsBase: 1, GrossDividendsBase: 3),
            new("TICKER2", Isin, NetDividendsBase: 20, WhtDividendsBase: 10, GrossDividendsBase: 30),
            new("TICKER3", Isin, NetDividendsBase: 200, WhtDividendsBase: 100, GrossDividendsBase: 300),
        ];
        var writer = new StringWriter();
        e.PrintAggregatedMetrics(writer, basics);
        var metrics = writer.ToString().Split(Environment.NewLine);
        Assert.IsTrue(metrics.Contains("Total Net Dividends - Country = C1 (EUR)  = 22"));
        Assert.IsTrue(metrics.Contains("Total Net Dividends - Country = C2 (EUR)  = 200"));
        Assert.IsTrue(metrics.Contains("Total WHT Dividends - Country = C1 (EUR)  = 11"));
        Assert.IsTrue(metrics.Contains("Total WHT Dividends - Country = C2 (EUR)  = 100"));
        Assert.IsTrue(metrics.Contains("Total Gross Dividends - Country = C1 (EUR)  = 33"));
        Assert.IsTrue(metrics.Contains("Total Gross Dividends - Country = C2 (EUR)  = 300"));
    }
}
