namespace Taxes;

using System.Reflection;
using static Basics;

internal static class TickerStateListExtensions
{
    internal static void PrintAggregatedMetrics(this IEnumerable<TickerState> tickerStates, TextWriter writer, Basics basics) =>
        tickerStates.GetAggregatedMetrics(basics).ToList().ForEach(writer.WriteLine);

    internal static IEnumerable<string> GetAggregatedMetrics(this IEnumerable<TickerState> tickerStates, Basics basics) => 
        from property in typeof(TickerState).GetProperties()
        let metricAttribute = property.GetCustomAttribute<MetricAttribute>()
        where metricAttribute is not null
        let metricSum = tickerStates.Sum(ts => (decimal)property.GetValue(ts)!)
        select $"{metricAttribute.Description} ({basics.BaseCurrency}) = {metricSum.R(basics)}";
}