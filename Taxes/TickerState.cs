namespace Taxes;

using System.Reflection;
using static Basics;

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = true)]
class MetricAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}

record TickerState(
    string Ticker, 
    string Isin,
    [Metric("Total Plus Value CUMP")] decimal PlusValueCumpBase = 0m,
    [Metric("Total Plus Value PEPS")] decimal PlusValuePepsBase = 0m,
    [Metric("Total Plus Value CRYPTO")] decimal PlusValueCryptoBase = 0m,
    [Metric("Total Minus Value CUMP")] decimal MinusValueCumpBase = 0m,
    [Metric("Total Minus Value PEPS")] decimal MinusValuePepsBase = 0m,
    [Metric("Total Minus Value CRYPTO")] decimal MinusValueCryptoBase = 0m,
    decimal TotalQuantity = 0m,
    decimal TotalAmountBase = 0m,
    [Metric("Total Net Dividends")] decimal NetDividendsBase = 0m,
    [Metric("Total WHT Dividends")] decimal WhtDividendsBase = 0m,
    [Metric("Total Gross Dividends")] decimal GrossDividendsBase = 0m,
    int PepsCurrentIndex = 0, 
    decimal PepsCurrentIndexBoughtQuantity = 0m,
    decimal PortfolioAcquisitionValueBase = 0m, 
    decimal CryptoFractionOfInitialCapital = 0m)
{
    public override string ToString() =>
        $"{TotalQuantity.R()} shares => {TotalAmountBase.R()} {BaseCurrency}, " +
        $"+V = CUMP {PlusValueCumpBase.R()} {BaseCurrency}, PEPS {PlusValuePepsBase.R()} {BaseCurrency}, CRYP {PlusValueCryptoBase.R()} {BaseCurrency}, " +
        $"-V = CUMP {MinusValueCumpBase.R()} {BaseCurrency}, PEPS {MinusValuePepsBase.R()} {BaseCurrency}, CRYP {MinusValueCryptoBase.R()} {BaseCurrency}, " +
        $"Dividends = {NetDividendsBase.R()} {BaseCurrency} + WHT {WhtDividendsBase.R()} {BaseCurrency} = {GrossDividendsBase.R()} {BaseCurrency}";  
}

delegate TickerState TickerAction(Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState);

internal static class TickerStateListExtensions
{
    internal static void PrintAggregatedMetrics(this IEnumerable<TickerState> tickerStates)
    {
         var metricStrings = 
             from property in typeof(TickerState).GetProperties()
             let metricAttribute = property.GetCustomAttribute<MetricAttribute>()
             where metricAttribute is not null
             let metricSum = tickerStates.Sum(ts => (decimal)property.GetValue(ts))
             select $"{metricAttribute.Description} ({BaseCurrency}) = {metricSum.R()}";

        metricStrings.ToList().ForEach(Console.WriteLine);
    }
}