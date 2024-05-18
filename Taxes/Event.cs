using System.Text;

namespace Taxes;

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
    /// 
    /// IBKR
    /// This corresponds in IBKR Trades reporting to Date/Time, and in IBKR Dividends reporting to Date, 
    /// although the formats of the CSV report from IBKR are different from the ones of Revolut Stocks:
    /// - Revolut Stock transactions and Dividends, old format: "2022-06-10T18:58:44.270334Z"
    /// - Revolut Stock transactions and Dividends, new format: "2022-06-10T18:58:44.270Z" (old also present)
    /// - IBKR Stock transactions: "2023-01-01, 00:00:00"
    /// - IBKR Dividends and Interest transactions: "2023-01-01" (no time part)
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
    /// 
    /// IBKR
    /// IBKR has different sections of the Activity Statement (both in CSV and PDF), for the different types of events:
    /// - Trades: Buy* and Sell* events
    /// - Dividends and Withholding Tax: Dividend events
    /// - Interest: Interest events
    /// </summary>
    EventType Type,

    /// <summary>
    /// Mandatory for Buy*, Sell*, StockSplit, Dividend and Interest. Null otherwise.
    /// All other events type are not ticker-specific (e.g. custody fee or change) so this property is null.
    /// This Ticker must be defined in the Basics, where the Country of residence for the financial asset identified by
    /// the Ticker needs to be specified.
    /// The Country of residence is important since different countries have different rules in relation to the country
    /// of fiscal residence: for example the percentage of WHT for a dividend depends on what country the security
    /// generating dividend is from.
    /// 
    /// The Ticker of an Interest is a fake Ticker that identify the source: for example, all Interest
    /// events coming from IBKR can have a Ticker "INTEREST_IBKR". This is important to link the interest to
    /// a country (via the Basics), so that the Ticker Processing is able to calculate the WHT for the event.
    /// 
    /// IBKR
    /// This corresponds in the IBKR Trades reporting to Symbol, for both Buy* and Sell* events.
    /// For Dividend events, there is no dedicated column for the Ticker, which is however included in the Description.
    /// </summary>
    string? Ticker,

    /// <summary>
    /// Mandatory for Buy*, Sell* and StockSplit. Null otherwise.
    /// - It doesn't make sense for Dividend or Interest, since they are cash events (and it's not the number 
    ///   of shares generating the dividend).
    /// - The same applies for CashTopUp and CashWithdrawal.
    /// - It doesn't make sense for CustodyChange and Reset, as they are not transactions.
    /// When defined, must be strictly positive, even for Sell* events.
    /// It's always strictly positive for Buy* and Sell* events.
    /// It can be negative for StockSplit events, when the split is actually a merge.
    /// 
    /// IBKR
    /// This corresponds in the IBKR Trades reporting to Quantity for Buy* events, and to -Quantity for Sell* events.
    /// This is because Quantity is negative in IBKR for Sell* events.
    /// </summary>
    decimal? Quantity,

    /// <summary>
    /// The market price of the share at the time of the event.
    /// It's mandatory for Buy* and Sell* events, and null otherwise.
    /// For limit orders, it's the price at which the order was executed, not the limit price nor the market price at
    /// the time the order was placed.
    /// 
    /// IBKR
    /// This corresponds in the IBKR Trades reporting to T.Price, for both Buy* and Sell* events.
    /// This is because T.Price is positive in IBKR for both Buy* and Sell* events.
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
    /// - for Sell* events, it's smaller than Quantity * PricePerShareLocal, as it includes the fees deducted from the 
    ///   proceeds
    /// In the case of Dividend, it's the Net amount of the dividend received (Gross - WHT).
    /// In the case of Interest, it's the Net amount of the interest received (Gross - WHT).
    /// In the case of StockSplit, it's non-relevant, and set to 0 mandatorily.
    /// 
    /// IBKR
    /// - For Buy* events, this corresponds in IBKR Trades reporting to the Basis, calculated as 
    ///   Basis = -(Proceeds + Comm/Fee), where Proceeds = -(Quantity * T.Price) so 
    ///   Basis = Quantity * T.Price - Comm/Fee, knowing that:
    ///   - Quantity is always a positive value for Buy* events
    ///   - T.Price is always a positive value for both Buy* and Sell* events
    ///   - So Proceeds is always a negative value for Buy* events
    ///   - Comm/Fee is always a negative value for both Buy* and Sell* events
    ///   - So Basis is bigger than Proceeds in absolute value, and it's a positive value (as in Sell* events)
    /// - For Sell* events, this corresponds in IBKR Trades reporting to 
    ///   X = Proceeds + Comm/Tax, where Proceeds = -(Quantity * T.Price), so 
    ///   X = Quantity * T.Price - Comm/Tax, knowing that:
    ///   - Quantity is always a negative value for Sell* events
    ///   - T.Price is always a positive value for both Buy* and Sell* events
    ///   - So Proceeds is always a positive value for Sell* events
    ///   - Comm/Tax is always a negative value for both Buy* and Sell* events
    ///   - So X is smaller than Proceeds in absolute value, and it's a positive value (as in Buy* events)
    ///   - Notice that, unlike in Buy* events, it's not Basis, as the Basis for a Sell* event is the one for the 
    ///     corresponding Buy* event (it is defined like so because it allows easy calculation of the capital 
    ///     gain/loss)
    ///   - X is not calculated and reported into a dedicated column in IBKR
    /// - For Dividend events, this corresponds in IBKR Dividends and Withholding Tax reporting to
    ///   - X = Dividends.Amount + WHT.Amount, knowing that:
    ///   - Dividends.Amount is always a positive value for Dividend events, and corresponds to Gross dividend
    ///   - WHT.Amount is always a negative value for Dividend events
    ///   - So X is smaller than Dividends.Amount, and corresponds to the Net dividend
    /// - For Interest events, this corresponds in IBKR Interest reporting to
    ///   - X = Interest.Amount + WHT.Amount, knowing that:
    ///   - Interest.Amount is always a positive value for Interest events, and corresponds to Gross interest
    ///   - WHT.Amount is always a negative value for Interest events
    ///   - So X is smaller than Interest.Amount, and corresponds to the Net interest
    /// Check the "Trades", "Dividends", "Fees" and "Interests" sections in Chapter 3 of the Reporting Guide
    /// Reference: https://www.interactivebrokers.com/download/reportingguide.pdf
    /// </summary>
    decimal TotalAmountLocal,

    /// <summary>
    /// Mandatory for Buy* and Sell*. Null otherwise.
    /// - It does not make sense for Dividend, since there are no fees associated with it, only taxes.
    ///   - Regarding taxes on dividends: there is not enough information in the input file to calculate them.
    ///   - The Dividend event only contains the Net amount, but not the Gross amount, nor the WHT amount.
    ///   - So, Gross amount and WHT are calculated based on the country of the ticker, and the country of the owner.
    ///   - For example for US stocks, the WHT is 15% for France residents, so the Net amount is divided by 85%.
    /// - It does not make sense for Interest, same as for Dividend.
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
    
    string Broker = "DEFAULT")
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
