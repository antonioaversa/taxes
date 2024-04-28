namespace Taxes.Test;

static class EventExtensions
{
    private const decimal DefaultDelta = 0.01m;

    [AssertionMethod]
    public static void AssertEvent(
        this Event @event, 
        DateTime? date = null, 
        EventType? type = null, 
        string? ticker = null, 
        decimal? quantity = null, 
        decimal? pricePerShareLocal = null, 
        decimal? totalAmountLocal = null, 
        decimal? feesLocal = null, 
        string? currency = null, 
        decimal? fxRate = null, 
        decimal? portfolioCurrentValueBase = null)
    {
        if (date is not null)
            Assert.AreEqual(date.Value, @event.Date);
        if (type is not null)
            Assert.AreEqual(type.Value, @event.Type);
        if (ticker is not null)
            Assert.AreEqual(ticker, @event.Ticker);
        if (quantity is not null)
            Assert.AreEqual(quantity.Value, @event.Quantity!.Value, DefaultDelta);
        if (pricePerShareLocal is not null)
            Assert.AreEqual(pricePerShareLocal.Value, @event.PricePerShareLocal!.Value, DefaultDelta);
        if (totalAmountLocal is not null)
            Assert.AreEqual(totalAmountLocal.Value, @event.TotalAmountLocal, DefaultDelta);
        if (feesLocal is not null)
            Assert.AreEqual(feesLocal.Value, @event.FeesLocal!.Value, DefaultDelta);
        if (currency is not null)
            Assert.AreEqual(currency, @event.Currency);
        if (fxRate is not null)
            Assert.AreEqual(fxRate.Value, @event.FXRate, DefaultDelta);
        if (portfolioCurrentValueBase is not null)
            Assert.AreEqual(portfolioCurrentValueBase.Value, @event.PortfolioCurrentValueBase, DefaultDelta);
    }
}
