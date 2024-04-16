using System.Globalization;
using System.Text.RegularExpressions;
namespace Taxes;

using static Basics;

/// <summary>
/// Defines the FX Rates by currency and day.
/// FX Rates are stored in a dictionary where:
/// - the key is the symbol of the currency (e.g. USD)
/// - and the value is another dictionary where:
///   - the key is the day (DateTime where only the date part is relevant)
///   - and the value is the FX Rate, for that currency, that day.
/// </summary>
public partial record FxRates(Dictionary<string, Dictionary<DateTime, decimal>> Rates)
{
    private static readonly string[] FxRatesHeaderLinesFirstWord = 
        [ "Titre", "Code série", "Unité", "Magnitude", "Méthode", "Source" ];

    public IDictionary<DateTime, decimal> this[string currency] => Rates[currency];
    public decimal this[string currency, DateTime date] => Rates[currency][date];

    /// <summary>
    /// Parses the FX Rates for a single currency from a file with the provided path.
    /// The file needs to have the following format: 
    /// - each line represents the FX Rate for a given day
    /// - the line format is: "M/d/yyyy\tFXRate"
    /// - the file can have comment lines starting with "//"
    /// - the FX Rate must be a positive number in the default culture
    /// </summary>
    public static FxRates ParseSingleCurrencyFromFile(string currency, string path) => 
        ParseSingleCurrencyFromContent(currency, File.ReadAllLines(path));

    /// <summary>
    /// Parses the FX Rates for multiple currencies from a file with the provided path.
    /// The format of the file is the same as the one used by the Banque de France.
    /// URI: https://www.banque-france.fr/statistiques/taux-et-cours/les-taux-de-change-salle-des-marches/parites-quotidiennes
    /// </summary>
    /// <example>
    /// Titre :;Dollar australien (AUD);Lev bulgare (BGN);Real brésilien (BRL);Dollar canadien (CAD);Franc suisse (CHF);Yuan renminbi chinois (CNY);Livre chypriote (CYP);Couronne tchèque (CZK);Couronne danoise (DKK);Couronne estonienne (EEK);Livre sterling (GBP);Dollar de Hong Kong (HKD);Kuna croate (HRK);Forint hongrois (HUF);Roupie indonésienne (IDR);Sheqel israélien (ILS);Roupie Indienne (100 paise);Couronne islandaise (ISK);Yen japonais (JPY);Won coréen (KRW);Litas lituanien (LTL);Lats letton (LVL);Livre maltaise (MTL);Peso méxicain (MXN);Ringgit malaisien (MYR);Couronne norvégienne (NOK);Dollar neo-zélandais (NZD);Peso philippin (PHP);Zloty polonais (PLN);Leu roumain (RON);Rouble russe (RUB);Couronne suédoise (SEK);Dollar de Singapour (SGD);Tolar slovène (SIT);Couronne slovaque (SKK);Baht thaïlandais (THB);Livre turque (TRY);Dollar des Etats-Unis (USD);Rand sud-africain (ZAR)
    /// Code série :; EXR.D.AUD.EUR.SP00.A;EXR.D.BGN.EUR.SP00.A;EXR.D.BRL.EUR.SP00.A;EXR.D.CAD.EUR.SP00.A;EXR.D.CHF.EUR.SP00.A;EXR.D.CNY.EUR.SP00.A;EXR.D.CYP.EUR.SP00.A;EXR.D.CZK.EUR.SP00.A;EXR.D.DKK.EUR.SP00.A;EXR.D.EEK.EUR.SP00.A;EXR.D.GBP.EUR.SP00.A;EXR.D.HKD.EUR.SP00.A;EXR.D.HRK.EUR.SP00.A;EXR.D.HUF.EUR.SP00.A;EXR.D.IDR.EUR.SP00.A;EXR.D.ILS.EUR.SP00.A;EXR.D.INR.EUR.SP00.A;EXR.D.ISK.EUR.SP00.A;EXR.D.JPY.EUR.SP00.A;EXR.D.KRW.EUR.SP00.A;EXR.D.LTL.EUR.SP00.A;EXR.D.LVL.EUR.SP00.A;EXR.D.MTL.EUR.SP00.A;EXR.D.MXN.EUR.SP00.A;EXR.D.MYR.EUR.SP00.A;EXR.D.NOK.EUR.SP00.A;EXR.D.NZD.EUR.SP00.A;EXR.D.PHP.EUR.SP00.A;EXR.D.PLN.EUR.SP00.A;EXR.D.RON.EUR.SP00.A;EXR.D.RUB.EUR.SP00.A;EXR.D.SEK.EUR.SP00.A;EXR.D.SGD.EUR.SP00.A;EXR.D.SIT.EUR.SP00.A;EXR.D.SKK.EUR.SP00.A;EXR.D.THB.EUR.SP00.A;EXR.D.TRY.EUR.SP00.A;EXR.D.USD.EUR.SP00.A;EXR.D.ZAR.EUR.SP00.A
    /// Unité :; Dollar Australien(AUD); Lev Nouveau(BGN); Real Bresilien(BRL); Dollar Canadien(CAD); Franc Suisse(CHF); Yuan Ren Min Bi(CNY); Livre Cypriote(CYP); Couronne Tcheque(CZK); Couronne Danoise(DKK); Couronne d`Estonie(EEK); Livre Sterling(GBP); Dollar de Hong Kong(HKD); Kuna Croate(HRK); Forint(HUF); Rupiah(IDR); Nouveau Israeli Shekel(ILS); Roupie Indienne(INR); Couronne Islandaise(ISK); Yen(JPY); Won(KRW); Litas Lituanien(LTL); Lats Letton(LVL); Livre Maltaise(MTL); Nouveau Peso Mexicain(MXN); Ringgit de Malaisie(MYR); Couronne Norvegienne(NOK); Dollar Neo-Zelandais(NZD); Peso Philippin(PHP); Zloty(PLN); Nouveau Ron(RON); Rouble Russe(RUB) (RUB);Couronne Suedoise(SEK); Dollar de Singapour(SGD); Tolar(SIT); Couronne Slovaque(SKK); Baht(THB); Nouvelle Livre Turque(TRY); Dollar des Etats-Unis(USD); Rand(ZAR)
    /// Magnitude :;Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0); Unités(0)
    /// Méthode d'observation :;Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Moyenne de la période (A);Fin de période (E);Fin de période (E);Moyenne de la période (A);Fin de période (E);Fin de période (E);Moyenne de la période (A);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Moyenne de la période (A);Moyenne de la période (A);Moyenne de la période (A);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E);Moyenne de la période (A);Moyenne de la période (A);Fin de période (E);Fin de période (E);Fin de période (E);Fin de période (E)
    /// Source :;BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0);BCE(Banque Centrale Européenne) (4F0)
    /// 04/04/2024;1,6446;1,9558;5,4751;1,4652;0,9846;7,8501;;25,322;7,4589;;0,85788;8,4957;;391,55;17233,19;4,0339;90,5116;150,3;164,69;1462,46;;;;17,9675;5,1433;11,6125;1,7998;61,243;4,2955;4,9687;;11,5105;1,4628;;;39,881;34,6065;1,0852;20,2704
    /// 03/04/2024;1,6539;1,9558;5,4681;1,4626;0,9792;7,8023;;25,352;7,4589;;0,85713;8,4421;;393,2;17196,78;4,0165;90,0055;150,1;163,66;1456,04;;;;17,8782;5,1273;11,658;1,8054;60,817;4,2968;4,9687;;11,575;1,4571;;;39,584;34,4418;1,0783;20,2667
    /// 02/04/2024;1,6522;1,9558;5,4114;1,4577;0,9765;7,7779;;25,361;7,4582;;0,8551;8,4148;;395,63;17103,38;3,9826;89,649;150,1;163,01;1453,23;;;;17,849;5,1106;11,708;1,804;60,506;4,2938;4,9699;;11,5575;1,4535;;;39,395;34,6033;1,0749;20,2399
    /// 01/04/2024;-;-;-;-;-;-;;-;-;;-;-;;-;-;-;-;-;-;-;;;;-;-;-;-;-;-;-;;-;-;;;-;-;-;-
    /// 31/03/2024;-;-;-;-;-;-;;-;-;;-;-;;-;-;-;-;-;-;-;;;;-;-;-;-;-;-;-;;-;-;;;-;-;-;-
    /// </example>
    public static FxRates ParseMultiCurrenciesFromFile(string path) => 
        ParseMultiCurrenciesFromContent(File.ReadAllLines(path));

    internal /* for testing */ static FxRates ParseSingleCurrencyFromContent(string currency, string[] lines)
    {
        var currencyFxRates = new Dictionary<DateTime, decimal>();

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("//"))
            {
                Console.WriteLine($"Skipping comment line '{line}'...");
                continue;
            }
            var fields = line.Split('\t');
            if (fields.Length != 2)
                throw new InvalidDataException($"Invalid line: '{line}'");
            if (!DateTime.TryParseExact(fields[0], "M/d/yyyy", DefaultCulture, DateTimeStyles.AssumeLocal, out var date))
                throw new InvalidDataException($"Invalid line: '{line}'");
            if (currencyFxRates.ContainsKey(date))
                throw new InvalidDataException($"Multiple FX Rates for day {date}");

            if (fields[1] == "-")
            {
                Console.WriteLine($"Skipping no-data line: '{line}'...");
                continue;
            }
            if (!decimal.TryParse(fields[1], NumberStyles.Currency, DefaultCulture, out var fxRate))
                throw new InvalidDataException($"Invalid line: '{line}'");
            if (fxRate <= 0)
                throw new InvalidDataException($"Invalid line: '{line}'");
            currencyFxRates[date] = fxRate;
        }

        return new(new Dictionary<string, Dictionary<DateTime, decimal>>() { [currency] = currencyFxRates });
    }

    internal /* for testing */ static FxRates ParseMultiCurrenciesFromContent(IList<string> lines)
    {
        if (lines.Count < FxRatesHeaderLinesFirstWord.Length || 
            FxRatesHeaderLinesFirstWord.Where((s, i) => !lines[i].Trim().StartsWith(s)).Any())
            throw new InvalidDataException("Invalid header lines");

        var currencies = (
            from field in lines[0].Split(';')[1..]
            let match = CurrencyRegex().Match(field)
            where match.Success
            select match.Groups["currency"].Value).ToList();

        if (currencies.Count == 0)
            throw new InvalidDataException("Invalid or no currencies found in header");

        var currencyFxRates = currencies.ToDictionary(k => k, v => new Dictionary<DateTime, decimal>());

        for (int lineIndex = FxRatesHeaderLinesFirstWord.Length; lineIndex < lines.Count; lineIndex++)
        {
            var lineFields = lines[lineIndex].Split(';');
            if (lineFields.Length != currencies.Count + 1)
                throw new InvalidDataException($"Invalid data line: '{lines[lineIndex]}'");
            
            var day = DateTime.ParseExact(lineFields[0], "dd/MM/yyyy", DefaultCulture);
            for (int fieldIndex = 1; fieldIndex < lineFields.Length; fieldIndex++)
            {
                if (lineFields[fieldIndex] == "-")
                    continue;

                if (lineFields[fieldIndex] == string.Empty)
                    continue;

                var currency = currencies[fieldIndex - 1];
                var fxRate = decimal.Parse(lineFields[fieldIndex], CultureInfo.GetCultureInfo("fr-FR"));
                if (currencyFxRates[currency].ContainsKey(day))
                    throw new InvalidDataException($"Multiple FX Rates for currency {currency} and day {day}");
                
                currencyFxRates[currency][day] = fxRate;
            }
        }
        return new(currencyFxRates);
    }

    // Example: Dollar australien (AUD) => currency = AUD
    // Special case: Roupie Indienne (100 paise) => currency = 100 paise
    [GeneratedRegex(@"^[^\(]+\((?<currency>[A-Za-z0-9\s]+)\)$")]
    private static partial Regex CurrencyRegex();
}
