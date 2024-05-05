namespace Taxes.Test;

static class EventExtensions
{
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
        decimal defaultDelta = 0.01m)
    {
        if (date is not null)
            Assert.AreEqual(date.Value, @event.Date);
        if (type is not null)
            Assert.AreEqual(type.Value, @event.Type);
        if (ticker is not null)
            Assert.AreEqual(ticker, @event.Ticker);
        if (quantity is not null)
            Assert.AreEqual(quantity.Value, @event.Quantity!.Value, defaultDelta);
        if (pricePerShareLocal is not null)
            Assert.AreEqual(pricePerShareLocal.Value, @event.PricePerShareLocal!.Value, defaultDelta);
        if (totalAmountLocal is not null)
            Assert.AreEqual(totalAmountLocal.Value, @event.TotalAmountLocal, defaultDelta);
        if (feesLocal is not null)
            Assert.AreEqual(feesLocal.Value, @event.FeesLocal!.Value, defaultDelta);
        if (currency is not null)
            Assert.AreEqual(currency, @event.Currency);
        if (fxRate is not null)
            Assert.AreEqual(fxRate.Value, @event.FXRate, defaultDelta);
    }
}
