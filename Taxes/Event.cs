namespace Taxes;

enum EventType
{
    Reset,          // Synthetic event added to reset aggregates calculation at the beginning of a year
    CashTopUp,      // Internal transfer from expenditure account to investing account (which are segregated)
    CashWithdrawal, // Internal transfer from investing account to expenditure account
    CustodyFee,     // Monthly fees for custody of cash and positions
    CustodyChange,  // Change of custody entity (una-tantum events)
    BuyMarket, BuyLimit, SellMarket, SellLimit, // Stock exchange events
    StockSplit, Dividend,                       // Stock general events
}

// Nomenclature:
// - PerShare: single share in this event
// - Shares: all shares in this event (quantity)
// - Total: all shares in this event + fees for this event
// - Fees: fees of all shares in this event
// - Portfolio: all shares of all events, not just this event

record Event(
    DateTime Date,
    EventType Type,

    /// <summary>
    /// Mandatory for Buy*, Sell*, StockSplit and Dividend. Null otherwise.
    /// All other events type are not ticker-specific (e.g. custody fee or change).
    /// </summary>
    string? Ticker,

    /// <summary>
    /// Mandatory for Buy*, Sell* and StockSplit. Null otherwise.
    /// - It doesn't make sense for Dividend, since it's a cash event.
    /// - The same applies for CashTopUp and CashWithdrawal.
    /// - It doesn't make sense for CustodyChange and Reset, as they are not transactions.
    /// When defined, must be strictly positive, even for Sell* events.
    /// </summary>
    decimal? Quantity,

    decimal? PricePerShareLocal,
    decimal? TotalAmountLocal,

    /// <summary>
    /// Mandatory for Buy* and Sell*. Null otherwise.
    /// - It cannot be calculated for Dividend, as there is not enough information in the input file.
    /// - It doesn't make sense for StockSplit, as it's not a transaction, so fees are not applied.
    /// Automatically calculated:
    /// - for Buy events as TotalAmountLocal - Quantity * PricePerShareLocal
    /// - for Sell events as Quantity * PricePerShareLocal - TotalAmountLocal
    /// </summary>
    decimal? FeesLocal,
    string Currency,
    decimal FXRate,
    decimal PortfolioCurrentValueBase)
{
    public bool IsBuy => Type is EventType.BuyMarket or EventType.BuyLimit;
    public bool IsSell => Type is EventType.SellMarket or EventType.SellLimit;

    public override string ToString() => 
        $"{Date:yyyy-MM-dd HH:mm:ss} {Type} " +
        (PricePerShareLocal != null && Quantity != null 
            ? $"{Quantity.Value.R()} shares at {PricePerShareLocal.Value.R()} {Currency}/share " 
            : string.Empty) +
        (TotalAmountLocal != null 
            ? $"=> {TotalAmountLocal.Value.R()} {Currency} (FXRate = {FXRate})"
            : string.Empty);
}
