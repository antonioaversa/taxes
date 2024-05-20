namespace Taxes.Test;

static class TickerStateExtensions
{
    public static void AssertState(
        this TickerState tickerState,
        decimal? plusValueCumpBase = null,
        decimal? plusValuePepsBase = null,
        decimal? plusValueCryptoBase = null,
        decimal? minusValueCumpBase = null,
        decimal? minusValuePepsBase = null,
        decimal? minusValueCryptoBase = null,
        decimal? totalQuantity = null,
        decimal? totalAmountBase = null,
        decimal? netDividendsBase = null,
        decimal? whtDividendsBase = null,
        decimal? grossDividendsBase = null,
        int? pepsCurrentIndex = null,
        decimal? pepsCurrentIndexSoldQuantity = null,
        decimal? cryptoPortfolioAcquisitionValueBase = null,
        decimal? cryptoFractionOfInitialCapitalBase = null)
    {
        if (plusValueCumpBase is not null) 
            Assert.AreEqual(plusValueCumpBase, tickerState.PlusValueCumpBase);
        if (plusValuePepsBase is not null) 
            Assert.AreEqual(plusValuePepsBase, tickerState.PlusValuePepsBase);
        if (plusValueCryptoBase is not null) 
            Assert.AreEqual(plusValueCryptoBase, tickerState.PlusValueCryptoBase);
        if (minusValueCumpBase is not null) 
            Assert.AreEqual(minusValueCumpBase, tickerState.MinusValueCumpBase);
        if (minusValuePepsBase is not null) 
            Assert.AreEqual(minusValuePepsBase, tickerState.MinusValuePepsBase);
        if (minusValueCryptoBase is not null) 
            Assert.AreEqual(minusValueCryptoBase, tickerState.MinusValueCryptoBase);
        
        if (totalQuantity is not null) 
            Assert.AreEqual(totalQuantity, tickerState.TotalQuantity);
        if (totalAmountBase is not null) 
            Assert.AreEqual(totalAmountBase, tickerState.TotalAmountBase);
        
        if (netDividendsBase is not null) 
            Assert.AreEqual(netDividendsBase, tickerState.NetDividendsBase);
        if (whtDividendsBase is not null) 
            Assert.AreEqual(whtDividendsBase, tickerState.WhtDividendsBase);
        if (grossDividendsBase is not null) 
            Assert.AreEqual(grossDividendsBase, tickerState.GrossDividendsBase);

        if (pepsCurrentIndex is not null) 
            Assert.AreEqual(pepsCurrentIndex, tickerState.PepsCurrentIndex);
        if (pepsCurrentIndexSoldQuantity is not null) 
            Assert.AreEqual(pepsCurrentIndexSoldQuantity, tickerState.PepsCurrentIndexSoldQuantity);
        if (cryptoPortfolioAcquisitionValueBase is not null) 
            Assert.AreEqual(cryptoPortfolioAcquisitionValueBase, tickerState.CryptoPortfolioAcquisitionValueBase);
        if (cryptoFractionOfInitialCapitalBase is not null) 
            Assert.AreEqual(cryptoFractionOfInitialCapitalBase, tickerState.CryptoFractionOfInitialCapitalBase);
    }

    // This is like the previous method, but asserts the default value (0 or -1) when the value is null
    public static void AssertDefaultExceptFor(
        this TickerState tickerState,
        decimal? plusValueCumpBase = null,
        decimal? plusValuePepsBase = null,
        decimal? plusValueCryptoBase = null,
        decimal? minusValueCumpBase = null,
        decimal? minusValuePepsBase = null,
        decimal? minusValueCryptoBase = null,
        decimal? totalQuantity = null,
        decimal? totalAmountBase = null,
        decimal? netDividendsBase = null,
        decimal? whtDividendsBase = null,
        decimal? grossDividendsBase = null,
        int? pepsCurrentIndex = null,
        decimal? pepsCurrentIndexSoldQuantity = null,
        decimal? portfolioAcquisitionValueBase = null,
        decimal? cryptoFractionOfInitialCapital = null) => 
        tickerState.AssertState(
            plusValueCumpBase: plusValueCumpBase ?? 0,
            plusValuePepsBase: plusValuePepsBase ?? 0,
            plusValueCryptoBase: plusValueCryptoBase ?? 0,
            minusValueCumpBase: minusValueCumpBase ?? 0,
            minusValuePepsBase: minusValuePepsBase ?? 0,
            minusValueCryptoBase: minusValueCryptoBase ?? 0,

            totalQuantity: totalQuantity ?? 0,
            totalAmountBase: totalAmountBase ?? 0,
                
            netDividendsBase: netDividendsBase ?? 0,
            whtDividendsBase: whtDividendsBase ?? 0,
            grossDividendsBase: grossDividendsBase ?? 0,
                
            pepsCurrentIndex: pepsCurrentIndex ?? -1,
            pepsCurrentIndexSoldQuantity: pepsCurrentIndexSoldQuantity ?? 0,
            cryptoPortfolioAcquisitionValueBase: portfolioAcquisitionValueBase ?? 0,
            cryptoFractionOfInitialCapitalBase: cryptoFractionOfInitialCapital ?? 0);
}

