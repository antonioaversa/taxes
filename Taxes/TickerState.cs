namespace Taxes;

/// <summary>
/// The state of a ticker in the portfolio, during the processing of events for that portfolio.
/// The state of the entire portfolio is given by the list of states of all tickers in the portfolio.
/// </summary>
record TickerState(
    /// <summary>
    /// The mandatory alphanumeric code that identifies the ticker. Example: AAPL for Apple Inc.
    /// While tickers are exchange-specific, and only unique on a given exchange, this symbol is supposed to be unique
    /// within the portfolio.
    /// It is only used for grouping events on the same financial instrument, and for display purposes.
    /// So it's not necessary to be the real ticker symbol on the exchange. 
    /// However, all events that refer to the same financial instrument must have the same ticker.
    /// </summary>
    string Ticker,
    
    /// <summary>
    /// The ISIN code of the financial instrument. Example: US0378331005 for Apple Inc (Ticker AAPL).
    /// In general, it is used to identify the financial instrument in a unique way, independently from the exchange.
    /// In this context, it is only used for display purposes, to make it easier to compile the tax declaration.
    /// Mapping from Ticker to ISIN has to be one-to-one, and defined in the Basics configuration.
    /// Every time a new Ticker is added to the portfolio, it must be added to the Basics configuration.
    /// </summary>
    string Isin,

    /// <summary>
    /// The sum of capital gains for all events of this ticker, in the base currency.
    /// Capital gains are calculated using the CUMP method - Coût Unitaire Moyen Pondéré.
    /// This method of calculation of capital gains is also known as PMP - Prix Moyen Pondéré.
    /// This method is an alternative to the PEPS method - Premier Entré Premier Sorti.
    /// 
    /// RATIONALE BEHIND
    /// The idea behind this method is to consider all shares of a given ticker as a single pool of shares, that have
    /// a buy price given by the average of buy prices per share, weighted by the quantity of shares bought at every
    /// buy event, up to that point in time.
    /// 
    /// ALGORITHM
    /// This method requires the calculation of the average buy price per share at the time of the sell event.
    /// To calculate the average buy price per share, we need to accumulate the total amount of all buy events for the
    /// ticker, as well as the total quantity of the ticker (number of shares for that ticker in the portfolio).
    /// - In the initial state of the ticker, the total amount and the total quantity for the ticker are both zero.
    /// - After buy event, the total amount of the ticker is incremented by the total amount of the buy event.
    /// - Similarly, the total quantity of the ticker is incremented by the quantity of the buy event.
    /// - After sell event, the total amount of the ticker is decremented by the average buy price for the shares sold
    ///   in the sell event (and not by the total amount of the sell event).
    ///   - Notice how the total amount of the ticker is not decremented by the total amount of the sell event, because
    ///     that could lead to negative total amount for the ticker, which is not coherent with the semantics of
    ///     total amount for the ticker being the average buy price for all the shares of the ticker, currently in the 
    ///     portfolio.
    /// - The total quantity of the ticker is decremented by the quantity of the sell event.
    /// - The average buy price per share is the total amount of the ticker divided by the total quantity of the ticker,
    ///   at any point in time.
    ///   - Notice how this quantity changes at every buy event, when the buy price of the new shares is different from
    ///     the average buy price of the shares already in the portfolio.
    ///   - Notice how this quantity doesn't change at sell events, because no new shares have been bought.
    /// 
    /// Given the average buy price per share, the total average buy price is calculated, by multiplying the average 
    /// buy price per share by the quantity of the sell event. 
    /// 
    /// The CUMP capital gain is then calculated as the total amount of the sell event minus the total average buy price.
    /// If such value is positive, it is considered a capital gain. If it is negative, it is considered a capital loss.
    /// The value is added to PlusValueCumpBase if it is positive, and to MinusValueCumpBase (in absolute value) if it 
    /// is negative.
    /// 
    /// Notice that the transactions fees for the sell event have already been deducted from the total amount of the 
    /// sell event.
    /// 
    /// EXAMPLE for ticker AAPL (assuming no fees)
    /// - Initial state => total amount = 0 USD, total quantity = 0 shares
    /// - Buy event for 10 shares at 100 USD per share => total amount = 1000 USD, total quantity = 10 shares
    /// - New average buy price per share = 1000 / 10 = 100 USD per share
    /// - Buy event for 5 shares at 110 USD per share => total amount = 1550 USD, total quantity = 15 shares
    /// - New average buy price per share = 1550 / 15 = 103.33 USD per share
    /// - Sell event for 1 share at 120 USD per share => total amount = 1550 - 103.33 = 1446.67 USD, total quantity = 
    ///   15 - 1 = 14 shares
    ///   - Total amount for the sell event = 120 USD
    ///   - CUMP capital gain = 120 - 103.33 = 16.67 USD
    /// - New average buy price per share = 1446.67 / 14 = 103.33 USD per share (same as before the sell event, since
    ///   no new buy has happened after the sell event)
    /// </summary>
    [property: Metric("Total Plus Value CUMP")] decimal PlusValueCumpBase = 0m,

    /// <summary>
    /// The sum of capital gains for all events of this ticker, in the base currency.
    /// Capital gains are calculated using the PEPS method - Premier Entré Premier Sorti.
    /// This method of calculation of capital gains is also known as FIFO - First In First Out.
    /// This method is an alternative to the CUMP method - Coût Unitaire Moyen Pondéré.
    /// 
    /// RAIONALE BEHIND
    /// The idea behind this method is to consider first the oldest shares bought, and then the newer shares.
    /// That requires to keep the history of all buy events for the ticker, since all buy price are needed for the
    /// calculation of the capital gain. It is also required to keep track of the quantity of shares sold for each buy 
    /// event, to know when to move to the next buy event during capital gain calculation for a sell event.
    /// Unlike the CUMP method, the PEPS method doesn't require the calculation of the average buy price per share at 
    /// the time of the sell event. Therefore, it doesn't require keeping the total amount of the ticker, nor the
    /// total quantity.
    /// 
    /// ALGORITHM
    /// - This method requires keeping two state variables:
    ///   - the current index in the list of buy events for the ticker (PepsCurrentIndex)
    ///   - the quantity of shares bought at the current index (PepsCurrentIndexSoldQuantity)
    /// - In the initial state of the ticker, the current index is zero and the quantity is zero.
    /// - Buy events don't change the two state variables
    /// - Sell events, on the other hand, update the two state variables as follows:
    ///   - if the current index is not pointing to a buying event, the index is incremented until it does
    ///   - the total buy price is initialized at 0
    ///   - the remaining quantity to sell is initialized at the quantity of the sell event
    ///   - while the remaining quantity to sell is bigger than 0, shares are sold, increasing the quantity of shares
    ///     bought at the current index, decreasing the remaining quantity to sell and increasing the total buy price
    ///     by the product of the quantity of shares sold and the buy price per share at the current index
    ///   - if the quantity of shares bought at the current index reaches the quantity of the buy event at the current
    ///     index, the index is moved to the next buy event
    ///   - if there are no more buy events, there is an error in the input data, since we are selling more shares than
    ///     owned
    ///   - once the remaining quantity to sell is zero, we can calculate the PEPS capital gain as the total amount for
    ///     the sell event minus the total buy price
    /// 
    /// EXAMPLE for ticker AAPL (assuming no fees)
    /// - Initial state => total amount = 0 USD, total quantity = 0 shares
    /// - Buy event for 10 shares at 100 USD per share => total quantity = 10 shares
    /// - Buy event for 5 shares at 110 USD per share => total quantity = 15 shares
    /// - Buy event for 2 shares at 105 USD per share => total quantity = 17 shares
    /// - Sell event for 16 share at 120 USD per share => total quantity = 17 - 16 = 1 share
    ///   - Total amount for the sell event = 120 * 16 = 1920 USD
    ///   - Current PEPS index = 0, Current PEPS index sold quantity = 0
    ///   - Remaining quantity to sell = 16
    ///   - Total buy price = 0
    ///   - The index points at a buy event, so we can start selling shares at this index
    ///   - All the 10 shares at the current index are sold
    ///   - Remaining quantity to sell = 16 - 10 = 6
    ///   - Total buy price = 10 * 100 = 1000 USD
    ///   - Move the index to the next buy event
    ///   - All the 5 shares at the current index are sold
    ///   - Remaining quantity to sell = 6 - 5 = 1
    ///   - Total buy price = 1000 + 5 * 110 = 1550 USD
    ///   - Move the index to the next buy event
    ///   - Only 1 share is sold, out of the two bought at the current index
    ///   - Remaining quantity to sell = 1 - 1 = 0
    ///   - Total buy price = 1550 + 1 * 105 = 1655 USD
    ///   - PEPS capital gain = 1920 - 1655 = 265 USD
    /// </summary>
    [property: Metric("Total Plus Value PEPS")] decimal PlusValuePepsBase = 0m,
    [property: Metric("Total Plus Value CRYPTO")] decimal PlusValueCryptoBase = 0m,
    [property: Metric("Total Minus Value CUMP")] decimal MinusValueCumpBase = 0m,
    [property: Metric("Total Minus Value PEPS")] decimal MinusValuePepsBase = 0m,
    [property: Metric("Total Minus Value CRYPTO")] decimal MinusValueCryptoBase = 0m,

    /// <summary>
    /// The number of shares for this ticker in the portfolio.
    /// It is the sum of all quantities of all events of this ticker.
    /// It can go to zero if all shares are sold, but it can't never go negative.
    /// It can be fractional, as it can happen with stock splits.
    /// </summary>
    decimal TotalQuantity = 0m,

    /// <summary>
    /// The algebric sum of total amounts for all events of this ticker, in the base currency.
    /// When buying, the total amount for the buying event is added to the current value.
    /// When selling, the total amount for the selling event is subtracted from the current value.
    /// Because of fees, the total amount of an event is not the product of quantity for the event and market price per
    /// share at the time of the event.
    /// So the total amount is not the sum of the products of quantity in the portfolio and average price per share in
    /// the portfolio. Instead, it is that amount, adjusted for all the fees of all the events for that ticker.
    /// </summary>
    decimal TotalAmountBase = 0m,

    [property: Metric("Total Net Dividends", true)] decimal NetDividendsBase = 0m,
    [property: Metric("Total WHT Dividends", true)] decimal WhtDividendsBase = 0m,
    [property: Metric("Total Gross Dividends", true)] decimal GrossDividendsBase = 0m,

    [property: Metric("Total Net Interests", true)] decimal NetInterestsBase = 0m,
    [property: Metric("Total WHT Interests", true)] decimal WhtInterestsBase = 0m,
    [property: Metric("Total Gross Interests", true)] decimal GrossInterestsBase = 0m,

    /// <summary>
    /// The index in the list of all events, of all types, of the first buy event that hasn't been fully sold yet,
    /// according to the PEPS algorithm: either it has been partially sold, or it hasn't been sold at all.
    /// All buy events before this index have been fully sold.
    /// All buy events after this index haven't been sold at all.
    /// Notice that the initial value is an invalid index, since 0 may be incorrect: for example, the event at index 0
    /// may be a non-ticker related event such as a cash top-up.
    /// So, at the first sell, the index is moved from -1 to the first buy event, that has to necessarily be at an
    /// index lower than the index of the sell event, since you cannot sell more than owned.
    /// </summary>
    int PepsCurrentIndex = -1, 

    /// <summary>
    /// The quantity already sold for the buy event at the index PepsCurrentIndex.
    /// This quantity is used to know when to move to the next buy event, when selling shares.
    /// It is always less than or equal to the quantity of the buy event at the index PepsCurrentIndex.
    /// When it is equal to the quantity of the buy event at the index PepsCurrentIndex, the index is moved to the 
    /// next buy event for that ticker.
    /// </summary>
    decimal PepsCurrentIndexSoldQuantity = 0m,

    /// <summary>
    /// Relevant for tax calculation of crypto only. It is used in conjunction with the CryptoFractionOfInitialCapital.
    /// Represents the total amount of FIAT currency used to acquire all the crypto currencies in the portfolio.
    /// It's always increasing at every buy event, and never decreased by any event, including reset events.
    /// 
    /// If multiple portfolios are owned, all portfolios in all supports (cold wallet, physical, online) and exchanges
    /// need to be summed up, to calculate the portfolio acquisition value. Moreover, when crypto currencies are 
    /// acquired in exchange for services or goods, the total amount in FIAT currency for the crypto currencies 
    /// received at the moment when the service has been executed, or the good delivered, needs to be included in the
    /// portfolio acquisition value.
    /// 
    /// USE OF PORTFOLIO ACQUISITION VALUE IN CRYPTO TAX CALCULATION
    /// When a crypto currency is sold, the ratio R = PortfolioValue[time of the sell] / CryptoPortfolioAcquisitionValueBase
    /// is calculated.
    /// R will be bigger than 1 if the portfolio increased its value, and smaller than 1 otherwise.
    /// For example:
    /// - R = 1.1 if the total value of the crypto portfolio is 10% bigger than the portfolio acquisition.
    /// - R = 0.9 if the total value of the crypto portfolio is 10% smaller than the portfolio acquisition.
    /// If the total gross amount of the sell event is S and the fees are F, the capital gain G is calculated as 
    /// G = (S - F) - S / R = S * (1 - 1 / R) - F.
    /// 1 - 1 / R represents the fraction of the total amount of the sell event that is considered capital gain.
    /// So:
    /// - if R = 2 (portfolio doubled its value), the capital gain is 50% of the total amount of the sell event
    /// - if R = 3 (portfolio tripled its value), the capital gain is 66.67% of the total amount of the sell event
    /// - if R = 1/2 (portfolio halved its value), the capital gain is -100% of the total amount of the sell event
    /// 
    /// At the next iteration, the quantity S / R is deducted from the portfolio acquisition value, to keep track of the
    /// fraction of initial capital that has been sold. This quantity is stored in CryptoFractionOfInitialCapitalBase.
    /// </summary> 
    decimal CryptoPortfolioAcquisitionValueBase = 0m,

    /// <summary>
    /// As explained in the documentation of PortfolioAcquisitionValueBase, the capital gain for crypto is calculated
    /// as G = S - S / R - F, where R is PortfolioValue[time of the sell] / CryptoPortfolioAcquisitionValueBase.
    /// The quantity FIC = S / R is the Fraction of the Initial Capital in S and it corresponds to the the part of S 
    /// that comes from the initial capital, and is not considered capital gain.
    /// 
    /// This formula for R is, however, only correct at the first sell event. At the next sell event, the formula for R
    /// is slightly more complex, as it needs to deduct the previous FIC from S: 
    /// R = PortfolioValue[time of the sell] / (CryptoPortfolioAcquisitionValueBase - CryptoFractionOfInitialCapitalBase)
    /// 
    /// This is because, after the first sell event, the value of the portfolio has decreased by the sell amount.
    /// 
    /// This is repeated at every sell event of a crypto, and the CryptoFractionOfInitialCapitalBase keeps incrementing,
    /// and it's deducted from the PortfolioAcquisitionValueBase for the calculation of R at every sell event.
    /// </summary>
    decimal CryptoFractionOfInitialCapitalBase = 0m)
{
    // private static readonly Basics basics = new(); // TODO: remove it after checking where ToString is used

    public string ToString(Basics basics) =>
        $"{TotalQuantity.R(basics)} shares => {TotalAmountBase.R(basics)} {basics.BaseCurrency}, " +
        $"+V = CUMP {PlusValueCumpBase.R(basics)} {basics.BaseCurrency}, PEPS {PlusValuePepsBase.R(basics)} {basics.BaseCurrency}, CRYP {PlusValueCryptoBase.R(basics)} {basics.BaseCurrency}, " +
        $"-V = CUMP {MinusValueCumpBase.R(basics)} {basics.BaseCurrency}, PEPS {MinusValuePepsBase.R(basics)} {basics.BaseCurrency}, CRYP {MinusValueCryptoBase.R(basics)} {basics.BaseCurrency}, " +
        $"Dividends = {NetDividendsBase.R(basics)} {basics.BaseCurrency} + WHT {WhtDividendsBase.R(basics)} {basics.BaseCurrency} = {GrossDividendsBase.R(basics)} {basics.BaseCurrency}, " +
        $"Interests = {NetInterestsBase.R(basics)} {basics.BaseCurrency} + WHT {WhtInterestsBase.R(basics)} {basics.BaseCurrency} = {GrossInterestsBase.R(basics)} {basics.BaseCurrency}";

    public override string ToString() =>
        $"{TotalQuantity} shares => {TotalAmountBase} SomeCurrency, " +
        $"+V = CUMP {PlusValueCumpBase}, PEPS {PlusValuePepsBase}, CRYP {PlusValueCryptoBase}, " +
        $"-V = CUMP {MinusValueCumpBase}, PEPS {MinusValuePepsBase}, CRYP {MinusValueCryptoBase}, " +
        $"Dividends = {NetDividendsBase} + WHT {WhtDividendsBase} = {GrossDividendsBase}, " +
        $"Interests = {NetInterestsBase} + WHT {WhtInterestsBase} = {GrossInterestsBase}";
}

public record OutWriters(TextWriter Default, TextWriter Form2047Writer, TextWriter Form2086Writer)
{
    public OutWriters() : this(TextWriter.Null, TextWriter.Null, TextWriter.Null) { }
}

delegate TickerState TickerAction(
    Event tickerEvent, IList<Event> tickerEvents, int eventIndex, TickerState tickerState, OutWriters outWriters);

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
class MetricAttribute(string description, bool aggregateByCountry = false) : Attribute
{
    public string Description { get; } = description;
    public bool AggregateByCountry { get; } = aggregateByCountry;
}