namespace Taxes;

/// <summary>
/// Defines the FX Rates by currency and day.
/// FX Rates are stored in a dictionary where:
/// - the key is the symbol of the currency (e.g. USD)
/// - and the value is another dictionary where:
///   - the key is the day (DateTime where only the date part is relevant)
///   - and the value is the FX Rate, for that currency, that day.
/// </summary>
public partial record FxRates(Dictionary<string, Dictionary<DateTime, decimal>> Rates)
{
    public IDictionary<DateTime, decimal> this[string currency] => Rates[currency];
    public decimal this[string currency, DateTime date] => Rates[currency][date];
}
