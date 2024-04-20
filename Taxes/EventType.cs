namespace Taxes;

/// <summary>
/// The type of an event.
/// </summary>
public enum EventType
{
    /// <summary>
    /// Synthetic event added to reset aggregates calculation at the beginning of a year.
    /// It's not present in the original format, and has been added to be able to process events for multiple years
    /// in sequence, in a single run of the program. This is important for crypto, as the value of the portfolio is
    /// required at every taxable event.
    /// It's a tax-relevant event, because it resets the state used for calculation of capital gain and dividends,
    /// both for normal stocks and for crypto.
    /// </summary>
    Reset,
    /// <summary>
    /// Internal transfer from expenditure account to investing account (which are segregated).
    /// Increases the cash in the investing account, that is currently not tracked in the portfolio.
    /// It's not a tax-relevant event, since, unlike dividends, it's just a transfer between personal accounts.
    /// </summary>
    CashTopUp,
    /// <summary>
    /// Internal transfer from investing account to expenditure account.
    /// Reduces the cash in the investing account, that is currently not tracked in the portfolio.
    /// It's not a tax-relevant event, since, unlike dividends, it's just a transfer between personal accounts.
    /// </summary>
    CashWithdrawal,
    /// <summary>
    /// Monthly fees for custody of cash and positions.
    /// Reduces the cash in the investing account, that is currently not tracked in the portfolio.
    /// It may be a tax-relevant event, as these fees may be deductable from taxes.
    /// However, that hasn't been investigated yet, and currently such fees, unlike transaction-specific fees, are not
    /// taken into account for the calculation of capital gain and dividends.
    /// </summary>
    CustodyFee,
    /// <summary>
    /// Change of custody entity (una-tantum events).
    /// It may be a tax-relevant event, if the custody changes country, since different taxation rules for foreign
    /// entities may apply. So, it's important to keep an eye on such events.
    /// </summary>
    CustodyChange,
    /// <summary>
    /// Stock exchange standard events.
    /// These events change the state of the portfolio.
    /// Selling events are also tax-relevant, since proceeds are subject to capital gain.
    /// Whether the event is market or limit, it's not relevant for the tax calculation, as the price in both cases
    /// is the market price at the moment the event happened, and not at the moment the order was placed.
    /// </summary>
    BuyMarket, BuyLimit, SellMarket, SellLimit,
    /// <summary>
    /// A split of the shared of stocks into different sizes.
    /// It changes the state of the portfolio, in terms of quantity for the ticker that is subject to the split.
    /// It is not a tax-relevant event, since it doesn't change the total value of the portfolio.
    /// </summary>
    StockSplit, 
    /// <summary>
    /// A cash payment to the investor, as a share of the profits of the company.
    /// It changes the state of the portfolio, as the amount received by dividends is tracked by the ticker states.
    /// It is also a tax-relevant event, since dividends are subject to both withholding and normal taxes.
    /// </summary>
    Dividend,
}
