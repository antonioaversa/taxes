namespace Taxes.Test;

static class EventExtensions
{
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
            Assert.AreEqual(date, @event.Date);
        if (type is not null)
            Assert.AreEqual(type, @event.Type);
        if (ticker is not null)
            Assert.AreEqual(ticker, @event.Ticker);
        if (quantity is not null)
            Assert.AreEqual(quantity, @event.Quantity);
        if (pricePerShareLocal is not null)
            Assert.AreEqual(pricePerShareLocal, @event.PricePerShareLocal);
        if (totalAmountLocal is not null)
            Assert.AreEqual(totalAmountLocal, @event.TotalAmountLocal);
        if (feesLocal is not null)
            Assert.AreEqual(feesLocal, @event.FeesLocal);
        if (currency is not null)
            Assert.AreEqual(currency, @event.Currency);
        if (fxRate is not null)
            Assert.AreEqual(fxRate, @event.FXRate);
        if (portfolioCurrentValueBase is not null)
            Assert.AreEqual(portfolioCurrentValueBase, @event.PortfolioCurrentValueBase);
    }
}
