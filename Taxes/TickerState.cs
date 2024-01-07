namespace Taxes;

using static Basics;

record TickerState(
    string Ticker, string Isin,
    decimal PlusValueCumpBase = 0m, decimal PlusValuePepsBase = 0m, decimal PlusValueCryptoBase = 0m,
    decimal MinusValueCumpBase = 0m, decimal MinusValuePepsBase = 0m, decimal MinusValueCryptoBase = 0m,
    decimal TotalQuantity = 0m, decimal TotalAmountBase = 0m,
    decimal NetDividendsBase = 0m, decimal WhtDividendsBase = 0m, decimal GrossDividendsBase = 0m,
    int PepsCurrentIndex = 0, decimal PepsCurrentIndexBoughtQuantity = 0m,
    decimal PortfolioAcquisitionValueBase = 0m, decimal CryptoFractionOfInitialCapital = 0m)
{
    public override string ToString() =>
        $"{TotalQuantity.R()} shares => {TotalAmountBase.R()} {BaseCurrency}, " +
        $"+V = CUMP {PlusValueCumpBase.R()} {BaseCurrency}, PEPS {PlusValuePepsBase.R()} {BaseCurrency}, CRYP {PlusValueCryptoBase.R()} {BaseCurrency}, " +
        $"-V = CUMP {MinusValueCumpBase.R()} {BaseCurrency}, PEPS {MinusValuePepsBase.R()} {BaseCurrency}, CRYP {MinusValueCryptoBase.R()} {BaseCurrency}, " +
        $"Dividends = {NetDividendsBase.R()} {BaseCurrency} + WHT {WhtDividendsBase.R()} {BaseCurrency} = {GrossDividendsBase.R()} {BaseCurrency}";  
}

delegate TickerState TickerAction(Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState);
