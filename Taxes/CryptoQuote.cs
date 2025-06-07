namespace Taxes;

internal record CryptoQuote
{
    public long Unix { get; init; }
    public DateTime Date { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public decimal Open { get; init; }
    public decimal High { get; init; }
    public decimal Low { get; init; }
    public decimal Close { get; init; }
    public decimal VolumeCrypto { get; init; }
    public decimal VolumeBase { get; init; }
    public long TradeCount { get; init; }
} 