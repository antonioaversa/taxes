namespace Taxes;

using static Basics;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
class MetricAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}

record TickerState(
    string Ticker, 
    string Isin,
    [property: Metric("Total Plus Value CUMP")] decimal PlusValueCumpBase = 0m,
    [property: Metric("Total Plus Value PEPS")] decimal PlusValuePepsBase = 0m,
    [property: Metric("Total Plus Value CRYPTO")] decimal PlusValueCryptoBase = 0m,
    [property: Metric("Total Minus Value CUMP")] decimal MinusValueCumpBase = 0m,
    [property: Metric("Total Minus Value PEPS")] decimal MinusValuePepsBase = 0m,
    [property: Metric("Total Minus Value CRYPTO")] decimal MinusValueCryptoBase = 0m,
    decimal TotalQuantity = 0m,
    decimal TotalAmountBase = 0m,
    [property: Metric("Total Net Dividends")] decimal NetDividendsBase = 0m,
    [property: Metric("Total WHT Dividends")] decimal WhtDividendsBase = 0m,
    [property: Metric("Total Gross Dividends")] decimal GrossDividendsBase = 0m,
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

delegate TickerState TickerAction(
    Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter);
