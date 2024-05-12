namespace Taxes;

using System.Reflection;

internal static class TickerStateListExtensions
{
    internal static void PrintAggregatedMetrics(this IEnumerable<TickerState> tickerStates, TextWriter writer, Basics basics) =>
        tickerStates.GetAggregatedMetrics(basics).ToList().ForEach(writer.WriteLine);

    internal static IEnumerable<string> GetAggregatedMetrics(this IEnumerable<TickerState> tickerStates, Basics basics)
    {
        var propertiesWithMetric =
            from property in typeof(TickerState).GetProperties()
            let metric = property.GetCustomAttribute<MetricAttribute>()
            where metric is not null
            select (property, metric);

        var propertiesWithMetricNotByCountry =
            from propertyWithMetric in propertiesWithMetric
            where !propertyWithMetric.metric.AggregateByCountry
            select propertyWithMetric;
        foreach (var (property, metric) in propertiesWithMetricNotByCountry)
        {
            var metricSum = tickerStates.Sum(ts => (decimal)property.GetValue(ts)!);
            yield return $"{metric.Description} ({basics.BaseCurrency}) = {metricSum.R(basics)}";
        }

        var propertiesWithMetricsByCountry =
            from propertyWithMetric in propertiesWithMetric
            where propertyWithMetric.metric.AggregateByCountry
            select propertyWithMetric;
        foreach (var (property, metric) in propertiesWithMetricsByCountry)
        {
            var metricSumByCountry =
                from tickerState in tickerStates
                where tickerState.Ticker is not null // Required to identify the country
                let metricSum = (decimal)property.GetValue(tickerState)!
                let country = basics.Positions[tickerState.Ticker].Country
                group metricSum by country into g
                select (country: g.Key, metricSum: g.Sum());
            foreach (var (country, metricSum) in metricSumByCountry)
                yield return $"{metric.Description} - Country = {country} ({basics.BaseCurrency})  = {metricSum.R(basics)}";
        }
    }
}