namespace Taxes;

public static class Basics
{
    public const decimal Precision = 0.01m;

    public const string BaseCurrency = "EUR";

    public static readonly Dictionary<string, string> ISIN = new()
    {
        ["AAPL"] = "US0378331005",
        ["AMD"] = "US0079031078",
        ["AMZN"] = "US0231351067",
        ["BEP"] = "BMG162581083",
        ["CFLT"] = "US20717M1036",
        ["COIN"] = "US19260Q1076",
        ["CVNA"] = "US1468691027",
        ["GOOGL"] = "US02079K3059",
        ["GSK"] = "US37733W2044",
        ["GSK.WI"] = "US37733W2044",
        ["INTC"] = "US0",
        ["HLN"] = "US4055521003",
        ["JKS"] = "US47759T1007",
        ["KO"] = "US1912161007",
        ["LLY"] = "US5324571083",
        ["META"] = "US30303M1027",
        ["MSFT"] = "US5949181045",
        ["NVDA"] = "US0",
        ["NVO"] = "US6701002056",
        ["ORCL"] = "US0",
        ["OXY"] = "US6745991058",
        ["PFE"] = "US7170811035",
        ["QCOM"] = "US7475251036",
        ["TSLA"] = "US88160R1014",
        ["TSM"] = "US8740391003",

        ["CRYPTO"] = "CRYPTO",
        ["AAVE"] = "CRYPTO_AAVE",
        ["ADA"] = "CRYPTO_ADA",
        ["BNT"] = "CRYPTO_BNT",
        ["BTC"] = "CRYPTO_BTC",
        ["DOGE"] = "CRYPTO_DOGE",
        ["ETH"] = "CRYPTO_ETH",
        ["NMR"] = "CRYPTO_NMR",
        ["NU"] = "CRYPTO_NU",
        ["PERP"] = "CRYPTO_PERP",
        ["RLC"] = "CRYPTO_RLC",
        ["SKL"] = "CRYPTO_SKL",
        ["SNX"] = "CRYPTO_SNX",
        ["SOL"] = "CRYPTO_SOL",
        ["STORJ"] = "CRYPTO_STORJ",
        ["ZRX"] = "CRYPTO_ZRX",

    };

    public static decimal R(this decimal value) =>
        //Math.Round(value, 2);
        Math.Abs(Math.Round(value, 4)) < 0.0005m ? 0m : Math.Round(value, 4);

    public static decimal WitholdingTaxFor(string isin) => 
        isin switch
        {
            string s when s.StartsWith("US") && s[2] - '0' <= 9 => 0.15m,
            string s when s.StartsWith("BMG") && s[3] - '0' <= 9 => 0.15m,
            string s => throw new NotSupportedException($"Unknown WHT for {s}"),
        };
}