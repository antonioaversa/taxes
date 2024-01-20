namespace Taxes;

enum EventType
{
    CashTopUp, CashWithdrawal, BuyMarket, BuyLimit, SellMarket, SellLimit, CustodyFee, StockSplit, Dividend
}

// Nomenclature:
// - PerShare: single share in this event
// - Shares: all shares in this event (quantity)
// - Total: all shares in this event + fees for this event
// - Fees: fees of all shares in this event
// - Portfolio: all shares of all events, not just this event

record Event(
    DateTime Date,
    string? Ticker,
    EventType Type,
    decimal? Quantity,
    decimal? PricePerShareLocal,
    decimal TotalAmountLocal,
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
        $"=> {TotalAmountLocal.R()} {Currency} (FXRate = {FXRate})";
}
