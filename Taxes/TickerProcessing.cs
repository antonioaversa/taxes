namespace Taxes;

class TickerProcessing(Basics basics, CryptoPortfolioValues? cryptoPortfolioValues = null)
{
    public Basics Basics => basics;

    public TickerState ProcessTicker(string ticker, IList<Event> tickerEvents, TextWriter? outWriter = null)
    {
        outWriter ??= TextWriter.Null;

        var isin = (string.IsNullOrWhiteSpace(ticker) ? "" : basics.ISINs[ticker]);
        if (string.IsNullOrWhiteSpace(ticker))
            outWriter.WriteLine($"PROCESS NON-TICKER-RELATED EVENTS");
        else 
            outWriter.WriteLine($"PROCESS {ticker} [{isin}]");

        var eventIndex = 0;
        var tickerState = new TickerState(ticker, isin);
        foreach (var tickerEvent in tickerEvents)
        {
            outWriter.WriteLine($"{eventIndex}: {tickerEvent}");
            TickerAction tickerAction = tickerEvent.Type switch
            {
                EventType.Reset => ProcessReset,
                EventType.CashTopUp => ProcessNoop,
                EventType.CashWithdrawal => ProcessNoop,
                EventType.CustodyFee => ProcessNoop,
                EventType.CustodyChange => ProcessNoop,
                EventType.BuyMarket or EventType.BuyLimit => ProcessBuy,
                EventType.SellMarket or EventType.SellLimit => ProcessSell,
                EventType.StockSplit => ProcessStockSplit,
                EventType.Dividend => ProcessDividend,
                _ => throw new NotSupportedException($"Event type not supported: {tickerEvent}"),
            };

            tickerState = tickerAction(tickerEvent, tickerEvents, eventIndex++, tickerState, outWriter);

            outWriter.WriteLine($"\tTicker State: {tickerState}");
            outWriter.WriteLine();
        }

        outWriter.WriteLine(new string('=', 100));

        return tickerState;
    }

    internal /* for testing */ TickerState ProcessReset(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        if (tickerEvent != tickerEvents[eventIndex])
            throw new InvalidDataException($"Event and event index inconsistent");
        if (tickerEvent.Type is not EventType.Reset)
            throw new NotSupportedException($"Unsupported type: {tickerEvent.Type}");
        if (tickerEvent.Quantity != null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Quantity)} not null");
        if (tickerEvent.PricePerShareLocal != null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.PricePerShareLocal)} not null");
        if (tickerEvent.TotalAmountLocal != 0m)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} not zero");
        if (tickerEvent.FeesLocal != null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.FeesLocal)} not null");

        return new TickerState(tickerState.Ticker, tickerState.Isin) with
        {
            TotalQuantity = tickerState.TotalQuantity,
            TotalAmountBase = tickerState.TotalAmountBase,
            PepsCurrentIndex = tickerState.PepsCurrentIndex,
            PepsCurrentIndexSoldQuantity = tickerState.PepsCurrentIndexSoldQuantity,
            PortfolioAcquisitionValueBase = tickerState.PortfolioAcquisitionValueBase,
            CryptoFractionOfInitialCapital = tickerState.CryptoFractionOfInitialCapital,
        };
    }

    internal /* for testing */ TickerState ProcessNoop(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        if (tickerEvent != tickerEvents[eventIndex])
            throw new InvalidDataException($"Event and event index inconsistent");

        return tickerState;
    }

    internal /* for testing */ TickerState ProcessBuy(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        if (tickerEvent != tickerEvents[eventIndex])
            throw new InvalidDataException($"Event and event index inconsistent");
        if (tickerEvent.Type is not (EventType.BuyMarket or EventType.BuyLimit))
            throw new NotSupportedException($"Unsupported type: {tickerEvent.Type}");
        if (tickerEvent.Ticker == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Ticker)} null");
        if (tickerEvent.PricePerShareLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.PricePerShareLocal)} null");
        if (tickerEvent.Quantity == null || tickerEvent.Quantity.Value <= 0)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Quantity)} null or non-positive");
        if (tickerEvent.TotalAmountLocal <= 0)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} non-positive");
        if (tickerEvents.FirstOrDefault(e => e.Currency != tickerEvent.Currency) is { } previousEvent)
            throw new NotSupportedException($"Etherogenous currencies: {previousEvent} vs {tickerEvent}");
        if (tickerEvent.Ticker != tickerState.Ticker)
            throw new InvalidDataException($"Event and state tickers don't match");
        if (tickerEvent.FeesLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.FeesLocal)} null");

        var tickerCurrency = tickerEvent.Currency;

        var totalBuyPriceLocal = tickerEvent.TotalAmountLocal;
        outWriter.WriteLine($"\tTotal Buy Price ({tickerCurrency}) = {totalBuyPriceLocal.R(basics)}");

        // Reminder: FXRate is the amount of LocalCurrency per BaseCurrency:
        // e.g. FXRate = 1.2 means 1.2 USD = 1 EUR, where USD is the LocalCurrency and EUR is the BaseCurrency 

        var totalBuyPriceBase = totalBuyPriceLocal / tickerEvent.FXRate;
        outWriter.WriteLine($"\tTotal Buy Price ({basics.BaseCurrency}) = {totalBuyPriceBase.R(basics)}");

        var sharesBuyPriceLocal = tickerEvent.PricePerShareLocal.Value * tickerEvent.Quantity.Value;
        outWriter.WriteLine($"\tShares Buy Price ({tickerCurrency}) = {sharesBuyPriceLocal.R(basics)}");

        var sharesBuyPriceBase = sharesBuyPriceLocal / tickerEvent.FXRate;
        outWriter.WriteLine($"\tShares Buy Price ({basics.BaseCurrency}) = {sharesBuyPriceBase.R(basics)}");

        var perShareBuyPriceLocal = tickerEvent.PricePerShareLocal.Value;
        outWriter.WriteLine($"\tPerShare Buy Price ({tickerCurrency}) = {perShareBuyPriceLocal.R(basics)}");

        var perShareBuyPriceBase = perShareBuyPriceLocal / tickerEvent.FXRate;
        outWriter.WriteLine($"\tPerShare Buy Price ({basics.BaseCurrency}) = {perShareBuyPriceBase.R(basics)}");

        var buyFeesLocal = tickerEvent.FeesLocal.Value;
        outWriter.WriteLine($"\tBuy Fees ({tickerCurrency}) = {buyFeesLocal.R(basics)}");

        // In buy events, fees are added to the total amount, so total buy price is higher than shares buy price
        var buyFees1Base = Math.Max(0, totalBuyPriceBase - sharesBuyPriceBase);
        var buyFees2Base = tickerEvent.FeesLocal.Value / tickerEvent.FXRate;
        if (Math.Abs(buyFees1Base - buyFees2Base) >= basics.Precision)
            throw new InvalidDataException($"Invalid event - Fees are inconsistent");
        outWriter.WriteLine($"\tBuy Fees ({basics.BaseCurrency}) = {buyFees2Base.R(basics)}");

        return tickerState with
        {
            TotalQuantity = tickerState.TotalQuantity + tickerEvent.Quantity.Value,
            TotalAmountBase = tickerState.TotalAmountBase + totalBuyPriceBase,
            PortfolioAcquisitionValueBase = tickerState.PortfolioAcquisitionValueBase + totalBuyPriceBase,
        };
    }

    internal /* for testing */ TickerState ProcessSell(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        if (tickerEvent != tickerEvents[eventIndex])
            throw new InvalidDataException($"Event and event index inconsistent");
        if (tickerEvent.Type is not (EventType.SellMarket or EventType.SellLimit))
            throw new NotSupportedException($"Unsupported type: {tickerEvent.Type}");
        if (tickerEvent.Ticker == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Ticker)} null");
        if (tickerEvent.PricePerShareLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.PricePerShareLocal)} null");
        if (tickerEvent.Quantity == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Quantity)} null");
        if (tickerEvents.FirstOrDefault(e => e.Currency != tickerEvent.Currency) is { } previousEvent)
            throw new NotSupportedException($"Etherogenous currencies: {previousEvent} vs {tickerEvent}");
        if (tickerState.TotalQuantity - tickerEvent.Quantity.Value < -basics.Precision)
            throw new InvalidDataException($"Invalid event - Cannot sell more than owned");
        if (tickerEvent.TotalAmountLocal <= 0)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} non-positive");
        if (tickerEvent.Ticker != tickerState.Ticker)
            throw new InvalidDataException($"Event and state tickers don't match");
        if (tickerEvent.FeesLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.FeesLocal)} null");
        
        var tickerCurrency = tickerEvent.Currency;

        var totalSellPriceLocal = tickerEvent.TotalAmountLocal;
        outWriter.WriteLine($"\tTotal Sell Price ({tickerCurrency}) = {totalSellPriceLocal.R(basics)}");

        var sharesSellPriceLocal = tickerEvent.PricePerShareLocal.Value * tickerEvent.Quantity.Value;
        outWriter.WriteLine($"\tShares Sell Price ({tickerCurrency}) = {sharesSellPriceLocal.R(basics)}");

        var perShareAvgBuyPriceBase = tickerState.TotalAmountBase / tickerState.TotalQuantity;
        outWriter.WriteLine($"\tPerShare Average Buy Price ({basics.BaseCurrency}) = {perShareAvgBuyPriceBase.R(basics)}");

        var totalAvgBuyPriceBase = perShareAvgBuyPriceBase * tickerEvent.Quantity.Value;
        outWriter.WriteLine($"\tTotal Average Buy Price ({basics.BaseCurrency}) = {totalAvgBuyPriceBase.R(basics)}");

        // Reminder: FXRate is the amount of LocalCurrency per BaseCurrency:
        // e.g. FXRate = 1.2 means 1.2 USD = 1 EUR, where USD is the LocalCurrency and EUR is the BaseCurrency

        var perShareSellPriceBase = tickerEvent.PricePerShareLocal.Value / tickerEvent.FXRate;
        outWriter.WriteLine($"\tPerShare Sell Price ({basics.BaseCurrency}) = {perShareSellPriceBase.R(basics)}");

        var sharesSellPriceBase = perShareSellPriceBase * tickerEvent.Quantity.Value;
        outWriter.WriteLine($"\tShares Sell Price ({basics.BaseCurrency}) = {sharesSellPriceBase.R(basics)}");

        var totalSellPriceBase = totalSellPriceLocal / tickerEvent.FXRate;
        outWriter.WriteLine($"\tTotal Sell Price ({basics.BaseCurrency}) = {totalSellPriceBase.R(basics)}");

        // In sell events, fees are deducted from the proceeding, so shares sell price is higher than total amount
        var sellFees1Base = Math.Max(0, sharesSellPriceBase - totalSellPriceBase);
        var sellFees2Base = tickerEvent.FeesLocal.Value / tickerEvent.FXRate;
        if (Math.Abs(sellFees1Base - sellFees2Base) >= basics.Precision)
            throw new InvalidDataException($"Invalid event - Fees are inconsistent");
        outWriter.WriteLine($"\tSell Fees ({basics.BaseCurrency}) = {sellFees2Base.R(basics)}");

        var plusValueCumpBase = 
            CalculatePlusValueCumpBase(totalAvgBuyPriceBase, totalSellPriceBase);
        var (plusValuePepsBase, pepsCurrentIndex, pepsCurrentIndexSoldQuantity) = 
            CalculatePlusValuePepsBase(tickerEvent, tickerEvents, tickerState, totalSellPriceBase);
        var (plusValueCryptoBase, cryptoFractionInitialCapital) =
            CalculatePlusValueCryptoBase(tickerEvent, tickerState, totalSellPriceBase, Math.Max(sellFees1Base, sellFees2Base));

        return tickerState with
        {
            TotalQuantity = tickerState.TotalQuantity - tickerEvent.Quantity.Value,
            TotalAmountBase = tickerState.TotalAmountBase - totalAvgBuyPriceBase, // And not - totalSellPriceBase
            PlusValueCumpBase = tickerState.PlusValueCumpBase + (plusValueCumpBase >= 0 ? plusValueCumpBase : 0),
            PlusValuePepsBase = tickerState.PlusValuePepsBase + (plusValuePepsBase >= 0 ? plusValuePepsBase : 0),
            PlusValueCryptoBase = tickerState.PlusValueCryptoBase + (plusValueCryptoBase >= 0 ? plusValueCryptoBase : 0),
            MinusValueCumpBase = tickerState.MinusValueCumpBase + (plusValueCumpBase < 0 ? -plusValueCumpBase : 0),
            MinusValuePepsBase = tickerState.MinusValuePepsBase + (plusValuePepsBase < 0 ? -plusValuePepsBase : 0),
            MinusValueCryptoBase = tickerState.MinusValueCryptoBase + (plusValueCryptoBase < 0 ? -plusValueCryptoBase : 0),
            PepsCurrentIndex = pepsCurrentIndex,
            PepsCurrentIndexSoldQuantity = pepsCurrentIndexSoldQuantity,
            CryptoFractionOfInitialCapital = cryptoFractionInitialCapital,
        };

        decimal CalculatePlusValueCumpBase(
            decimal totalAvgBuyPriceBase, decimal totalSellPriceBase)
        {
            var plusValueCumpBase = totalSellPriceBase - totalAvgBuyPriceBase;

            if (plusValueCumpBase >= 0)
                outWriter.WriteLine($"\tPlus Value CUMP ({basics.BaseCurrency}) = {plusValueCumpBase.R(basics)}");
            else
                outWriter.WriteLine($"\tMinus Value CUMP ({basics.BaseCurrency}) = {-plusValueCumpBase.R(basics)}");

            return plusValueCumpBase;
        }

        (decimal, int, decimal) CalculatePlusValuePepsBase(
            Event tickerEvent, IList<Event> tickerEvents, TickerState tickerState, decimal totalSellPriceBase)
        {
            var remainingQuantityToSell = tickerEvent.Quantity!.Value;
            var pepsCurrentIndex = tickerState.PepsCurrentIndex;
            var pepsCurrentIndexSoldQuantity = tickerState.PepsCurrentIndexSoldQuantity;
            var totalPepsBuyPriceBase = 0m;
            while (remainingQuantityToSell >= 0m)
            {
                if (pepsCurrentIndex < 0)
                {
                    outWriter.WriteLine($"\tPEPS Move Current Index to first buy event");
                    do { pepsCurrentIndex++; }
                    while (pepsCurrentIndex < tickerEvents.Count && !tickerEvents[pepsCurrentIndex].IsBuy);
                }

                if (remainingQuantityToSell > 0m)
                {
                    outWriter.WriteLine($"\tPEPS Remaining Quantity to match: {remainingQuantityToSell.R(basics)} => FIND Buy Event");
                }
                else
                {
                    outWriter.WriteLine($"\tPEPS Remaining Quantity to match: {remainingQuantityToSell.R(basics)} => DONE");
                    break;
                }

                if (pepsCurrentIndex >= tickerEvents.Count)
                    throw new InvalidDataException($"PEPS Invalid Current Index");

                var pepsBuyEvent = tickerEvents[pepsCurrentIndex];
                if (!pepsBuyEvent.IsBuy || pepsBuyEvent.Quantity == null || pepsBuyEvent.PricePerShareLocal == null)
                    throw new InvalidDataException($"PEPS Current Index not pointing to a Buy event");
                var pepsBuyEventQuantity = pepsBuyEvent.Quantity.Value;

                if (pepsCurrentIndexSoldQuantity > pepsBuyEventQuantity)
                    throw new InvalidDataException($"PEPS Current Index Sold Quantity > Total Event Quantity");
                
                // Tries to match the remaining quantity to sell against the quantity of current buy event according to
                // PEPS (part of which may have already been sold during a previous iteration of this loop or during
                // the processing of a previous sell transaction).
                var soldQuantity = Math.Min(remainingQuantityToSell, pepsBuyEventQuantity - pepsCurrentIndexSoldQuantity);
                pepsCurrentIndexSoldQuantity += soldQuantity;
                remainingQuantityToSell -= soldQuantity;

                var pepsBuyEventShareOfFeesLocal = 
                    (soldQuantity / pepsBuyEvent.Quantity.Value) * pepsBuyEvent.FeesLocal!.Value;
                var pepsBuyEventBuyPriceBase1 = 
                    (soldQuantity * pepsBuyEvent.PricePerShareLocal.Value + pepsBuyEventShareOfFeesLocal) / pepsBuyEvent.FXRate;
                var pepsBuyEventBuyPriceBase2 =
                    (soldQuantity / pepsBuyEvent.Quantity.Value) * pepsBuyEvent.TotalAmountLocal / pepsBuyEvent.FXRate;
                // The precision of the shares buy price is given by the precision of the price for a single share,
                // multiplied by the number of sold shares: e.g. if the price per share has 2 decimal-digits precision,
                // the max error is 0.01 for 1 share, so 0.01 * n for n shares.
                if (Math.Abs(pepsBuyEventBuyPriceBase1 - pepsBuyEventBuyPriceBase2) >= basics.Precision * soldQuantity)
                    throw new InvalidDataException($"PEPS Buy Price Base is inconsistent");
                totalPepsBuyPriceBase += pepsBuyEventBuyPriceBase1;

                if (pepsCurrentIndexSoldQuantity < pepsBuyEventQuantity)
                {
                    outWriter.WriteLine(
                        $"\tPEPS Buy Event {pepsBuyEvent} at index {pepsCurrentIndex} bought partially");

                }
                else if (pepsCurrentIndexSoldQuantity == pepsBuyEventQuantity)
                {
                    outWriter.WriteLine(
                        $"\tPEPS Buy Event {pepsBuyEvent} at index {pepsCurrentIndex} bought entirely => move to next");

                    do { pepsCurrentIndex++; } 
                    while (pepsCurrentIndex < tickerEvents.Count && !tickerEvents[pepsCurrentIndex].IsBuy);
                    pepsCurrentIndexSoldQuantity = 0m;
                }
            }

            var plusValuePepsBase = totalSellPriceBase - totalPepsBuyPriceBase;

            if (plusValuePepsBase >= 0)
                outWriter.WriteLine($"\tPlus Value PEPS ({basics.BaseCurrency}) = {plusValuePepsBase.R(basics)}");
            else
                outWriter.WriteLine($"\tMinus Value PEPS ({basics.BaseCurrency}) = {-plusValuePepsBase.R(basics)}");

            return (plusValuePepsBase, pepsCurrentIndex, pepsCurrentIndexSoldQuantity);
        }
    
        (decimal, decimal) CalculatePlusValueCryptoBase(
            Event tickerEvent, TickerState tickerState, decimal totalSellPriceBase, decimal sellFeesBase)
        {
            if (cryptoPortfolioValues == null)
            {
                outWriter.WriteLine($"\tCrypto Portfolio Values not known => Skipping Crypto +/- value calculation...");
                return (0m, 0m);
            }

            var portfolioCurrentValueBase = cryptoPortfolioValues[tickerEvent.Date];

            if (portfolioCurrentValueBase < 0)
            {
                outWriter.WriteLine($"\tCurrent Crypto Portfolio Value not known => Skipping Crypto +/- value calculation...");
                return (0m, 0m);
            }

            outWriter.WriteLine($"\tPortfolio Current Value ({basics.BaseCurrency}) = {portfolioCurrentValueBase.R(basics)}");

            var portfolioAcquisitionValueBase = tickerState.PortfolioAcquisitionValueBase;
            outWriter.WriteLine($"\tPortfolio Acquisition Value ({basics.BaseCurrency}) = {portfolioAcquisitionValueBase.R(basics)}");

            var currentCryptoFractionInitialCapital = tickerState.CryptoFractionOfInitialCapital;
            outWriter.WriteLine($"\tCurrent Fraction of Initial Capital CRYPTO ({basics.BaseCurrency}) = {currentCryptoFractionInitialCapital}");

            var portfolioNetAcquisitionValueBase = portfolioAcquisitionValueBase - tickerState.CryptoFractionOfInitialCapital;
            outWriter.WriteLine($"\tPortfolio Net Acquisition Value ({basics.BaseCurrency}) = {portfolioNetAcquisitionValueBase.R(basics)}");

            var totalNetSellPriceBase = totalSellPriceBase - sellFeesBase; // TODO: Check it should be equal to sharesSellPriceBase

            var deltaCryptoFractionInitialCapital = portfolioNetAcquisitionValueBase * totalSellPriceBase / portfolioCurrentValueBase;
            outWriter.WriteLine($"\tDelta Fraction of Initial Capital CRYPTO ({basics.BaseCurrency}) = {deltaCryptoFractionInitialCapital}");

            var nextCryptoFractionInitialCapital = currentCryptoFractionInitialCapital + deltaCryptoFractionInitialCapital;
            outWriter.WriteLine($"\tNext Fraction of Initial Capital CRYPTO ({basics.BaseCurrency}) = {nextCryptoFractionInitialCapital}");

            var plusValueCryptoBase = totalNetSellPriceBase - deltaCryptoFractionInitialCapital;

            if (plusValueCryptoBase >= 0)
                outWriter.WriteLine($"\tPlus Value CRYPTO ({basics.BaseCurrency}) = {plusValueCryptoBase.R(basics)}");
            else
                outWriter.WriteLine($"\tMinus Value CRYPTO ({basics.BaseCurrency}) = {-plusValueCryptoBase.R(basics)}");
            
            return (plusValueCryptoBase, nextCryptoFractionInitialCapital);
        }
    }

    internal /* for testing */ TickerState ProcessStockSplit(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        if (tickerEvent != tickerEvents[eventIndex])
            throw new InvalidDataException($"Event and event index inconsistent");
        if (tickerEvent.Type is not EventType.StockSplit)
            throw new NotSupportedException($"Unsupported type: {tickerEvent.Type}");
        if (tickerEvent.Ticker == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Ticker)} null");
        if (tickerEvent.Quantity == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Quantity)} null");
        if (tickerEvent.PricePerShareLocal != null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.PricePerShareLocal)} not null");
        if (tickerEvent.TotalAmountLocal != 0m)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} not zero");
        if (tickerEvent.FeesLocal != null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.FeesLocal)} not null");
        if (tickerEvent.Ticker != tickerState.Ticker)
            throw new InvalidDataException($"Event and state tickers don't match");

        var splitDelta = tickerEvent.Quantity.Value;
        outWriter.WriteLine($"\tSplit Delta = {splitDelta}");

        var splitRatio = (tickerState.TotalQuantity + splitDelta) / tickerState.TotalQuantity;
        outWriter.WriteLine($"\tSplit Ratio = {splitRatio}");

        outWriter.WriteLine($"\tRetroactively update previous buy and sell events:");
        for (var i = 0; i < eventIndex; i++)
        {
            if (tickerEvents[i].IsBuy || tickerEvents[i].IsSell)
            {
                var normalizedEvent = tickerEvents[i] with
                {
                    Quantity = tickerEvents[i].Quantity!.Value * splitRatio,
                    PricePerShareLocal = tickerEvents[i].PricePerShareLocal!.Value / splitRatio,
                };
                outWriter.WriteLine($"\t\t{tickerEvents[i]}");
                outWriter.WriteLine($"\t\t\tbecomes {normalizedEvent}");
                tickerEvents[i] = normalizedEvent;
            }
        }

        return tickerState with
        {
            TotalQuantity = tickerState.TotalQuantity + splitDelta,
            PepsCurrentIndexSoldQuantity = tickerState.PepsCurrentIndexSoldQuantity * splitRatio,
        };
    }

    internal /* for testing */ TickerState ProcessDividend(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        if (tickerEvent != tickerEvents[eventIndex])
            throw new InvalidDataException($"Event and event index inconsistent");
        if (tickerEvent.Type is not EventType.Dividend)
            throw new NotSupportedException($"Unsupported type: {tickerEvent.Type}");
        if (tickerEvent.Ticker == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Ticker)} null");
        if (tickerEvent.TotalAmountLocal <= 0)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} non-positive");

        var netDividendLocal = tickerEvent.TotalAmountLocal;
        outWriter.WriteLine($"\tNet Dividend ({tickerEvent.Currency}) = {netDividendLocal.R(basics)}");

        var netDividendBase = netDividendLocal / tickerEvent.FXRate;
        outWriter.WriteLine($"\tNet Dividend ({basics.BaseCurrency}) = {netDividendBase.R(basics)}");

        var witholdingTaxRate = Basics.WitholdingTaxFor(tickerState.Isin);
        var whtDividendBase = netDividendBase * witholdingTaxRate / (1m - witholdingTaxRate);
        outWriter.WriteLine($"\tWHT Dividend ({basics.BaseCurrency}) = {whtDividendBase.R(basics)}");

        var grossDividendBase = netDividendBase + whtDividendBase;
        outWriter.WriteLine($"\tGross Dividend ({basics.BaseCurrency}) = {grossDividendBase.R(basics)}");

        return tickerState with
        {
            NetDividendsBase = tickerState.NetDividendsBase + netDividendBase,
            WhtDividendsBase = tickerState.WhtDividendsBase + whtDividendBase,
            GrossDividendsBase = tickerState.GrossDividendsBase + grossDividendBase,
        };
    }
}
