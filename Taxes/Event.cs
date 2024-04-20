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

/// <summary>
/// An event in the portfolio, that may or may not change the state of the portfolio.
/// Examples of events are: buying or selling shares, receiving dividends and paying custody fees.
/// 
/// Nomenclature:
/// - PerShare: single share in this event
/// - Shares: all shares in this event (quantity)
/// - Total: all shares in this event + fees for this event
/// - Fees: fees of all shares in this event
/// - Portfolio: all shares of all events, not just this event
/// </summary>
record Event(
    /// <summary>
    /// The date and time at which the event occurred.
    /// It's mandatory for all types of events, even synthetic ones like Reset.
    /// This is because it's necessary to sort events chronologically, in order to process them accordingly.
    /// </summary>
    DateTime Date,

    /// <summary>
    /// The type of event.
    /// It's mandatory for all events.
    /// Depending on the type, other properties are mandatory or not, and can assume a different semantics.
    /// For example, for Buy* and Sell* events, the Quantity property is mandatory.
    /// For Buy* events, the TotalAmountLocal is bigger than Quantity * PricePerShareLocal, as it includes fees.
    /// For Sell* events, the TotalAmountLocal is instead smaller than Quantity * PricePerShareLocal, as fees are 
    /// deducted from the proceeds.
    /// </summary>
    EventType Type,

    /// <summary>
    /// Mandatory for Buy*, Sell*, StockSplit and Dividend. Null otherwise.
    /// All other events type are not ticker-specific (e.g. custody fee or change) so this property is null.
    /// </summary>
    string? Ticker,

    /// <summary>
    /// Mandatory for Buy*, Sell* and StockSplit. Null otherwise.
    /// - It doesn't make sense for Dividend, since it's a cash event (and it's not the number of shares generating the 
    ///   dividend).
    /// - The same applies for CashTopUp and CashWithdrawal.
    /// - It doesn't make sense for CustodyChange and Reset, as they are not transactions.
    /// When defined, must be strictly positive, even for Sell* events.
    /// </summary>
    decimal? Quantity,

    /// <summary>
    /// The market price of the share at the time of the event.
    /// It's mandatory for Buy* and Sell* events, and null otherwise.
    /// For limit orders, it's the price at which the order was executed, not the limit price nor the market price at
    /// the time the order was placed.
    /// </summary>
    decimal? PricePerShareLocal,

    /// <summary>
    /// In the case of Reset, it's non-relevant, as it's a synthetic event not present in the original format.
    /// In the case of CashTopUp and CashWithdrawal, it's the amount of money added or withdrawn from the investment
    /// account (that is segregated from the normal cash account used for daily expenses).
    /// In the case of CustodyFee, it's the total amount of the fee.
    /// In the case of CustodyChange, it's non-relevant, and set to 0.
    /// In the case of Buy* and Sell*, it's the total amount of the transaction, including fees: 
    /// - for Buy* events, it's bigger than Quantity * PricePerShareLocal, as it includes the fees payed for those shares
    /// - for Sell* events, it's smaller than Quantity * PricePerShareLocal, as it includes the fees deducted from the proceeds
    /// In the case of Dividend, it's the amount of the dividend.
    /// In the case of StockSplit, it's non-relevant, and set to 0 mandatorily.
    /// </summary>
    decimal TotalAmountLocal,

    /// <summary>
    /// Mandatory for Buy* and Sell*. Null otherwise.
    /// - It cannot be calculated for Dividend, as there is not enough information in the input file.
    /// - It doesn't make sense for StockSplit, as it's not a transaction, so fees are not applied.
    /// - It's not defined for CustodyFee: the fee is conventionally defined as a TotalAmountLocal.
    /// It's always a positive number, both for Buy* and Sell* events. It's automatically calculated as follows:
    /// - for Buy events as TotalAmountLocal - Quantity * PricePerShareLocal
    /// - for Sell events as Quantity * PricePerShareLocal - TotalAmountLocal
    /// </summary>
    decimal? FeesLocal,

    /// <summary>
    /// The currency in which the event has happened. 
    /// If the event is about a transaction, this is the currency in which the transaction took place.
    /// The currency is mandatory for all types of events, and relevant for all of them except for Reset.
    /// </summary>
    string Currency,

    /// <summary>
    /// The exchange rate between the currency of the event and the base currency.
    /// This exchange rate is defined by the broker providing the input file with all the events.
    /// It is not the exchange rate as defined by the European Central Bank.
    /// So it's not the use to be used for tax purposes, unless no ECB rate is available for the date of the event.
    /// That can happen, for example, for events happening on weekends or holidays, when the ECB doesn't provide rates.
    /// An example of such scenario is for crypto, that are traded 24/7.
    /// 
    /// The exchange rate is defined as the amount of local currency needed to buy 1 unit of the base currency.
    /// For example, if the event is in USD and the base currency is EUR, the event has happened on the 1st of January
    /// 2023, the FXRate would be 1.06, because on the 1st of January 2023, 1 EUR was worth 1.06 USD.
    /// 
    /// Therefore, to convert to the base currency, one needs to divide the local amount by the FXRate.
    /// </summary>
    decimal FXRate,

    decimal PortfolioCurrentValueBase)
{
    public bool IsBuy => Type is EventType.BuyMarket or EventType.BuyLimit;
    public bool IsSell => Type is EventType.SellMarket or EventType.SellLimit;

    private static readonly Basics basics = new(); // TODO: remove it after checking where ToString is used

    public override string ToString() => 
        $"{Date:yyyy-MM-dd HH:mm:ss} {Type} " +
        (PricePerShareLocal != null && Quantity != null 
            ? $"{Quantity.Value.R(basics)} shares at {PricePerShareLocal.Value.R(basics)} {Currency}/share " 
            : string.Empty) + 
        $"=> {TotalAmountLocal.R(basics)} {Currency} (FXRate = {FXRate})";
}
