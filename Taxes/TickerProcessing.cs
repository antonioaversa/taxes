namespace Taxes;

using static Basics;

static class TickerProcessing
{
    public static TickerState ProcessTicker(string ticker, IList<Event> tickerEvents, TextWriter? textWriter = null)
    {
        textWriter ??= TextWriter.Null;

        var isin = (string.IsNullOrWhiteSpace(ticker) ? "" : ISINs[ticker]);
        if (string.IsNullOrWhiteSpace(ticker))
            Console.WriteLine($"PROCESS NON-TICKER-RELATED EVENTS");
        else 
            Console.WriteLine($"PROCESS {ticker} [{isin}]");

        var eventIndex = 0;
        var tickerState = new TickerState(ticker, isin);
        foreach (var tickerEvent in tickerEvents)
        {
            Console.WriteLine($"{eventIndex}: {tickerEvent}");
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

            tickerState = tickerAction(tickerEvent, tickerEvents, eventIndex++, tickerState, textWriter);

            Console.WriteLine($"\tTicker State: {tickerState}");
            Console.WriteLine();
        }

        Console.WriteLine(new string('=', 100));

        return tickerState;
    }

    internal /* for testing */ static TickerState ProcessReset(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        if (tickerEvent.Type is not EventType.Reset)
            throw new NotSupportedException($"Unsupported type: {tickerEvent.Type}");
        if (tickerEvent.Quantity != null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Quantity)} not null");
        if (tickerEvent.PricePerShareLocal != null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.PricePerShareLocal)} not null");
        if (tickerEvent.TotalAmountLocal != null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} not null");
        if (tickerEvent.FeesLocal != null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.FeesLocal)} not null");

        return new TickerState(tickerState.Ticker, tickerState.Isin) with
        {
            TotalQuantity = tickerState.TotalQuantity,
            TotalAmountBase = tickerState.TotalAmountBase,
            PepsCurrentIndex = tickerState.PepsCurrentIndex,
            PepsCurrentIndexBoughtQuantity = tickerState.PepsCurrentIndexBoughtQuantity,
            PortfolioAcquisitionValueBase = tickerState.PortfolioAcquisitionValueBase,
            CryptoFractionOfInitialCapital = tickerState.CryptoFractionOfInitialCapital,
        };
    }

    internal /* for testing */ static TickerState ProcessNoop(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        return tickerState;
    }

    internal /* for testing */ static TickerState ProcessBuy(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        if (tickerEvent.Type is not (EventType.BuyMarket or EventType.BuyLimit))
            throw new NotSupportedException($"Unsupported type: {tickerEvent.Type}");
        if (tickerEvent.Ticker == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Ticker)} null");
        if (tickerEvent.PricePerShareLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.PricePerShareLocal)} null");
        if (tickerEvent.Quantity == null || tickerEvent.Quantity.Value <= 0)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Quantity)} null or non-positive");
        if (tickerEvent.TotalAmountLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} null");
        if (tickerEvents.FirstOrDefault(e => e.Currency != tickerEvent.Currency) is { } previousEvent)
            throw new NotSupportedException($"Etherogenous currencies: {previousEvent} vs {tickerEvent}");
        if (tickerEvent.Ticker != tickerState.Ticker)
            throw new InvalidDataException($"Event and state tickers don't match");
        if (tickerEvent.FeesLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.FeesLocal)} null");

        var tickerCurrency = tickerEvent.Currency;

        var totalBuyPriceLocal = tickerEvent.TotalAmountLocal.Value;
        Console.WriteLine($"\tTotal Buy Price ({tickerCurrency}) = {totalBuyPriceLocal.R()}");

        var totalBuyPriceBase = totalBuyPriceLocal / tickerEvent.FXRate;
        Console.WriteLine($"\tTotal Buy Price ({BaseCurrency}) = {totalBuyPriceBase.R()}");

        var sharesBuyPriceLocal = tickerEvent.PricePerShareLocal.Value * tickerEvent.Quantity.Value;
        Console.WriteLine($"\tShares Buy Price ({tickerCurrency}) = {sharesBuyPriceLocal.R()}");

        var sharesBuyPriceBase = sharesBuyPriceLocal / tickerEvent.FXRate;
        Console.WriteLine($"\tShares Buy Price ({BaseCurrency}) = {sharesBuyPriceBase.R()}");

        var perShareBuyPriceLocal = tickerEvent.PricePerShareLocal.Value;
        Console.WriteLine($"\tPerShare Buy Price ({tickerCurrency}) = {perShareBuyPriceLocal.R()}");

        var perShareBuyPriceBase = perShareBuyPriceLocal / tickerEvent.FXRate;
        Console.WriteLine($"\tPerShare Buy Price ({BaseCurrency}) = {perShareBuyPriceBase.R()}");

        var buyFees1Base = Math.Abs(sharesBuyPriceBase - totalBuyPriceBase);
        Console.WriteLine($"\tBuy Fees 1 ({BaseCurrency}) = {buyFees1Base.R()}");

        var buyFees2Base = tickerEvent.FeesLocal.Value / tickerEvent.FXRate;
        Console.WriteLine($"\tBuy Fees 2 ({BaseCurrency}) = {buyFees2Base.R()}");

        // TODO: ensure that buyFees1Base and buyFees2Base are consistent with each other, which is currently not the case

        return tickerState with
        {
            TotalQuantity = tickerState.TotalQuantity + tickerEvent.Quantity.Value,
            TotalAmountBase = tickerState.TotalAmountBase + totalBuyPriceBase,
            PortfolioAcquisitionValueBase = tickerState.PortfolioAcquisitionValueBase + totalBuyPriceBase,
        };
    }

    internal /* for testing */ static TickerState ProcessStockSplit(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        if (tickerEvent.Type is not EventType.StockSplit)
            throw new NotSupportedException($"Unsupported type: {tickerEvent.Type}");
        if (tickerEvent.Ticker == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Ticker)} null");
        if (tickerEvent.Quantity == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Quantity)} null");
        if (tickerEvent.PricePerShareLocal != null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.PricePerShareLocal)} not null");
        if (tickerEvent.TotalAmountLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} null");
        if (tickerEvent.TotalAmountLocal != 0m)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} not zero");
        if (tickerEvent.Ticker != tickerState.Ticker)
            throw new InvalidDataException($"Event and state tickers don't match");

        var splitDelta = tickerEvent.Quantity.Value;
        Console.WriteLine($"\tSplit Delta = {splitDelta}");

        var splitRatio = (tickerState.TotalQuantity + splitDelta) / tickerState.TotalQuantity;
        Console.WriteLine($"\tSplit Ratio = {splitRatio}");

        Console.WriteLine($"\tRetroactively update previous buy and sell events:");
        for (var i = 0; i < eventIndex; i++)
        {
            if (tickerEvents[i].IsBuy || tickerEvents[i].IsSell)
            {
                var normalizedEvent = tickerEvents[i] with
                {
                    Quantity = tickerEvents[i].Quantity * splitRatio,
                    PricePerShareLocal = tickerEvents[i].PricePerShareLocal / splitRatio,
                };
                Console.WriteLine($"\t\t{tickerEvents[i]}");
                Console.WriteLine($"\t\t\tbecomes {normalizedEvent}");
                tickerEvents[i] = normalizedEvent;
            }
        }

        return tickerState with
        {
            TotalQuantity = tickerState.TotalQuantity + splitDelta,
        };
    }

    internal /* for testing */ static TickerState ProcessSell(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
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
        if (tickerState.TotalQuantity - tickerEvent.Quantity.Value < -Precision)
            throw new InvalidDataException($"Invalid event - Cannot sell more than owned");
        if (tickerEvent.TotalAmountLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} null");
        if (tickerEvent.Ticker != tickerState.Ticker)
            throw new InvalidDataException($"Event and state tickers don't match");
        if (tickerEvent.FeesLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.FeesLocal)} null");
        
        var tickerCurrency = tickerEvent.Currency;

        var totalSellPriceLocal = tickerEvent.TotalAmountLocal.Value;
        outWriter.WriteLine($"\tTotal Sell Price ({tickerCurrency}) = {totalSellPriceLocal.R()}");

        var sharesSellPriceLocal = tickerEvent.PricePerShareLocal.Value * tickerEvent.Quantity.Value;
        outWriter.WriteLine($"\tShares Sell Price ({tickerCurrency}) = {sharesSellPriceLocal.R()}");

        var perShareAvgBuyPriceBase = tickerState.TotalAmountBase / tickerState.TotalQuantity;
        outWriter.WriteLine($"\tPerShare Average Buy Price ({BaseCurrency}) = {perShareAvgBuyPriceBase.R()}");

        var totalAvgBuyPriceBase = perShareAvgBuyPriceBase * tickerEvent.Quantity.Value;
        outWriter.WriteLine($"\tTotal Average Buy Price ({BaseCurrency}) = {totalAvgBuyPriceBase.R()}");

        var perShareSellPriceBase = tickerEvent.PricePerShareLocal.Value / tickerEvent.FXRate;
        outWriter.WriteLine($"\tPerShare Sell Price ({BaseCurrency}) = {perShareSellPriceBase.R()}");

        var sharesSellPriceBase = perShareSellPriceBase * tickerEvent.Quantity.Value;
        outWriter.WriteLine($"\tShares Sell Price ({BaseCurrency}) = {sharesSellPriceBase.R()}");

        var totalSellPriceBase = totalSellPriceLocal / tickerEvent.FXRate;
        outWriter.WriteLine($"\tTotal Sell Price ({BaseCurrency}) = {totalSellPriceBase.R()}");

        var sellFees1Base = Math.Abs(sharesSellPriceBase - totalSellPriceBase);
        outWriter.WriteLine($"\tSell Fees 1 ({BaseCurrency}) = {sellFees1Base.R()}");

        var sellFees2Base = tickerEvent.FeesLocal.Value / tickerEvent.FXRate;
        outWriter.WriteLine($"\tSell Fees 2 ({BaseCurrency}) = {sellFees2Base.R()}");

        // TODO: ensure that buyFees1Base and buyFees2Base are consistent with each other, which is currently not the case

        var plusValueCumpBase = 
            CalculatePlusValueCumpBase(totalAvgBuyPriceBase, totalSellPriceBase);
        var (plusValuePepsBase, pepsCurrentIndex, pepsCurrentIndexBoughtQuantity) = 
            CalculatePlusValuePepsBase(tickerEvent, tickerEvents, tickerState, totalSellPriceBase);
        var (plusValueCryptoBase, cryptoFractionInitialCapital) =
            CalculatePlusValueCryptoBase(tickerEvent, tickerState, totalSellPriceBase, Math.Max(sellFees1Base, sellFees2Base));

        return tickerState with
        {
            TotalQuantity = tickerState.TotalQuantity - tickerEvent.Quantity.Value,
            TotalAmountBase = tickerState.TotalAmountBase - totalSellPriceBase,
            PlusValueCumpBase = tickerState.PlusValueCumpBase + (plusValueCumpBase >= 0 ? plusValueCumpBase : 0),
            PlusValuePepsBase = tickerState.PlusValuePepsBase + (plusValuePepsBase >= 0 ? plusValuePepsBase : 0),
            PlusValueCryptoBase = tickerState.PlusValueCryptoBase + (plusValueCryptoBase >= 0 ? plusValueCryptoBase : 0),
            MinusValueCumpBase = tickerState.MinusValueCumpBase + (plusValueCumpBase < 0 ? -plusValueCumpBase : 0),
            MinusValuePepsBase = tickerState.MinusValuePepsBase + (plusValuePepsBase < 0 ? -plusValuePepsBase : 0),
            MinusValueCryptoBase = tickerState.MinusValueCryptoBase + (plusValueCryptoBase < 0 ? -plusValueCryptoBase : 0),
            PepsCurrentIndex = pepsCurrentIndex,
            PepsCurrentIndexBoughtQuantity = pepsCurrentIndexBoughtQuantity,
            CryptoFractionOfInitialCapital = cryptoFractionInitialCapital,
        };

        static decimal CalculatePlusValueCumpBase(
            decimal totalAvgBuyPriceBase, decimal totalSellPriceBase)
        {
            var plusValueCumpBase = totalSellPriceBase - totalAvgBuyPriceBase;

            if (plusValueCumpBase >= 0)
                Console.WriteLine($"\tPlus Value CUMP ({BaseCurrency}) = {plusValueCumpBase.R()}");
            else
                Console.WriteLine($"\tMinus Value CUMP ({BaseCurrency}) = {-plusValueCumpBase.R()}");

            return plusValueCumpBase;
        }

        static (decimal, int, decimal) CalculatePlusValuePepsBase(
            Event tickerEvent, IList<Event> tickerEvents, TickerState tickerState, decimal totalSellPriceBase)
        {
            if (tickerEvent.Quantity == null)
                throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Quantity)} null");

            var remainingQuantity = tickerEvent.Quantity.Value;
            var pepsCurrentIndex = tickerState.PepsCurrentIndex;
            var pepsCurrentIndexBoughtQuantity = tickerState.PepsCurrentIndexBoughtQuantity;
            var totalPepsBuyPriceBase = 0m;
            while (remainingQuantity >= 0m)
            {
                if (remainingQuantity > 0m)
                {
                    Console.WriteLine($"\tPEPS Remaining Quantity to match: {remainingQuantity.R()} => FIND Buy Event");
                }
                else
                {
                    Console.WriteLine($"\tPEPS Remaining Quantity to match: {remainingQuantity.R()} => DONE");
                    break;
                }

                if (pepsCurrentIndex >= tickerEvents.Count)
                    throw new InvalidDataException($"PEPS Invalid Current Index");

                var pepsBuyEvent = tickerEvents[pepsCurrentIndex];
                if (!pepsBuyEvent.IsBuy || pepsBuyEvent.Quantity == null || pepsBuyEvent.PricePerShareLocal == null)
                    throw new InvalidDataException($"PEPS Current Index not pointing to a Buy event");
                var pepsBuyEventQuantity = pepsBuyEvent.Quantity.Value;

                if (pepsCurrentIndexBoughtQuantity > pepsBuyEventQuantity)
                    throw new InvalidDataException($"PEPS Current Index Bought Quantity > Total Event Quantity");
                
                var boughtQuantity = Math.Min(remainingQuantity, pepsBuyEventQuantity - pepsCurrentIndexBoughtQuantity);
                pepsCurrentIndexBoughtQuantity += boughtQuantity;
                remainingQuantity -= boughtQuantity;

                totalPepsBuyPriceBase += boughtQuantity * pepsBuyEvent.PricePerShareLocal.Value / pepsBuyEvent.FXRate;

                if (pepsCurrentIndexBoughtQuantity < pepsBuyEventQuantity)
                {
                    Console.WriteLine(
                        $"\tPEPS Buy Event {pepsBuyEvent} at index {pepsCurrentIndex} bought partially");

                }
                else if (pepsCurrentIndexBoughtQuantity == pepsBuyEventQuantity)
                {
                    Console.WriteLine(
                        $"\tPEPS Buy Event {pepsBuyEvent} at index {pepsCurrentIndex} bought entirely => move to next");

                    do { pepsCurrentIndex++; } 
                    while (pepsCurrentIndex < tickerEvents.Count && !tickerEvents[pepsCurrentIndex].IsBuy);
                    pepsCurrentIndexBoughtQuantity = 0m;
                }
            }

            var plusValuePepsBase = totalSellPriceBase - totalPepsBuyPriceBase;

            if (plusValuePepsBase >= 0)
                Console.WriteLine($"\tPlus Value PEPS ({BaseCurrency}) = {plusValuePepsBase.R()}");
            else
                Console.WriteLine($"\tMinus Value PEPS ({BaseCurrency}) = {-plusValuePepsBase.R()}");

            return (plusValuePepsBase, pepsCurrentIndex, pepsCurrentIndexBoughtQuantity);
        }
    
        static (decimal, decimal) CalculatePlusValueCryptoBase(
            Event tickerEvent, TickerState tickerState, decimal totalSellPriceBase, decimal sellFeesBase)
        {
            var portfolioCurrentValueBase = tickerEvent.PortfolioCurrentValueBase;

            if (portfolioCurrentValueBase < 0)
            {
                Console.WriteLine($"\tPortfolio Current Value not known => Skipping Crypto +/- value calculation...");
                return (0m, 0m);
            }

            Console.WriteLine($"\tPortfolio Current Value ({BaseCurrency}) = {portfolioCurrentValueBase.R()}");

            var portfolioAcquisitionValueBase = tickerState.PortfolioAcquisitionValueBase;
            Console.WriteLine($"\tPortfolio Acquisition Value ({BaseCurrency}) = {portfolioAcquisitionValueBase.R()}");

            var currentCryptoFractionInitialCapital = tickerState.CryptoFractionOfInitialCapital;
            Console.WriteLine($"\tCurrent Fraction of Initial Capital CRYPTO ({BaseCurrency}) = {currentCryptoFractionInitialCapital}");

            var portfolioNetAcquisitionValueBase = portfolioAcquisitionValueBase - tickerState.CryptoFractionOfInitialCapital;
            Console.WriteLine($"\tPortfolio Net Acquisition Value ({BaseCurrency}) = {portfolioNetAcquisitionValueBase.R()}");

            var totalNetSellPriceBase = totalSellPriceBase - sellFeesBase; // TODO: Check it should be equal to sharesSellPriceBase

            var deltaCryptoFractionInitialCapital = portfolioNetAcquisitionValueBase * totalSellPriceBase / portfolioCurrentValueBase;
            Console.WriteLine($"\tDelta Fraction of Initial Capital CRYPTO ({BaseCurrency}) = {deltaCryptoFractionInitialCapital}");

            var nextCryptoFractionInitialCapital = currentCryptoFractionInitialCapital + deltaCryptoFractionInitialCapital;
            Console.WriteLine($"\tNext Fraction of Initial Capital CRYPTO ({BaseCurrency}) = {nextCryptoFractionInitialCapital}");

            var plusValueCryptoBase = totalNetSellPriceBase - deltaCryptoFractionInitialCapital;

            if (plusValueCryptoBase >= 0)
                Console.WriteLine($"\tPlus Value CRYPTO ({BaseCurrency}) = {plusValueCryptoBase.R()}");
            else
                Console.WriteLine($"\tMinus Value CRYPTO ({BaseCurrency}) = {-plusValueCryptoBase.R()}");
            
            return (plusValueCryptoBase, nextCryptoFractionInitialCapital);
        }
    }

    internal static /* for testing */ TickerState ProcessDividend(
        Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, TextWriter outWriter)
    {
        if (tickerEvent.Type is not EventType.Dividend)
            throw new NotSupportedException($"Unsupported type: {tickerEvent.Type}");
        if (tickerEvent.Ticker == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.Ticker)} null");
        if (tickerEvent.TotalAmountLocal == null)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} null");
        if (tickerEvent.TotalAmountLocal == 0m)
            throw new InvalidDataException($"Invalid event - {nameof(tickerEvent.TotalAmountLocal)} zero");

        var netDividendLocal = tickerEvent.TotalAmountLocal.Value;
        Console.WriteLine($"\tNet Dividend ({tickerEvent.Currency}) = {netDividendLocal.R()}");

        var netDividendBase = netDividendLocal / tickerEvent.FXRate;
        Console.WriteLine($"\tNet Dividend ({BaseCurrency}) = {netDividendBase.R()}");

        var whtDividendBase = netDividendBase * WitholdingTaxFor(tickerState.Isin) / (1m - WitholdingTaxFor(tickerState.Isin));
        Console.WriteLine($"\tWHT Dividend ({BaseCurrency}) = {whtDividendBase.R()}");

        var grossDividendBase = netDividendBase + whtDividendBase;
        Console.WriteLine($"\tGross Dividend ({BaseCurrency}) = {grossDividendBase.R()}");

        return tickerState with
        {
            NetDividendsBase = tickerState.NetDividendsBase + netDividendBase,
            WhtDividendsBase = tickerState.WhtDividendsBase + whtDividendBase,
            GrossDividendsBase = tickerState.GrossDividendsBase + grossDividendBase,
        };
    }
}
