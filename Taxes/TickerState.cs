namespace Taxes;

using static Basics;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
class MetricAttribute(string description) : Attribute
{
    public string Description { get; } = description;
}

/// <summary>
/// The state of a ticker in the portfolio, during the processing of events for that portfolio.
/// </summary>
record TickerState(
    /// <summary>
    /// The mandatory alphanumeric code that identifies the ticker.
    /// While tickers are exchange-specific, and only unique on a given exchange, this symbol is supposed to be unique
    /// within the portfolio.
    /// It is only used for grouping events on the same financial instrument, and for display purposes.
    /// So it's not necessary to be the real ticker symbol on the exchange. 
    /// However, all events that refer to the same financial instrument must have the same ticker.
    /// </summary>
    string Ticker,
    
    /// <summary>
    /// The ISIN code of the financial instrument.
    /// In general, it is used to identify the financial instrument in a unique way, independently from the exchange.
    /// In this context, it is only used for display purposes, to make it easier to compile the tax declaration.
    /// Mapping from Ticker to ISIN has to be one-to-one, and defined in the Basics configuration.
    /// Every time a new Ticker is added to the portfolio, it must be added to the Basics configuration.
    /// </summary>
    string Isin,

    [property: Metric("Total Plus Value CUMP")] decimal PlusValueCumpBase = 0m,
    [property: Metric("Total Plus Value PEPS")] decimal PlusValuePepsBase = 0m,
    [property: Metric("Total Plus Value CRYPTO")] decimal PlusValueCryptoBase = 0m,
    [property: Metric("Total Minus Value CUMP")] decimal MinusValueCumpBase = 0m,
    [property: Metric("Total Minus Value PEPS")] decimal MinusValuePepsBase = 0m,
    [property: Metric("Total Minus Value CRYPTO")] decimal MinusValueCryptoBase = 0m,

    /// <summary>
    /// The number of shares for this ticker in the portfolio.
    /// It is the sum of all quantities of all events of this ticker.
    /// It can go to zero if all shares are sold, but it can't never go negative.
    /// It can be fractional, as it can happen with stock splits.
    /// </summary>
    decimal TotalQuantity = 0m,

    /// <summary>
    /// The algebric sum of total amounts for all events of this ticker, in the base currency.
    /// When buying, the total amount for the buying event is added to the current value.
    /// When selling, the total amount for the selling event is subtracted from the current value.
    /// Because of fees, the total amount of an event is not the product of quantity for the event and market price per
    /// share at the time of the event.
    /// So the total amount is not the sum of the products of quantity in the portfolio and average price per share in
    /// the portfolio. Instead, it is that amount, adjusted for all the fees of all the events for that ticker.
    /// </summary>
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
