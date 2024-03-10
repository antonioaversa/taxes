# Taxes calculator

## Setup
Update all the files under the `Reports` directory.

Those files are necessary input for the calculation of the taxes.

### Setup basics

Make sure that `Basics.json` is up-to-date:
- define `Rounding`, used to round figure in calculations. Acceptable values are:
  - `Fixed_(?<numberOfDigits>\d+)`
	- e.g. `Fixed_2`, for rounding with `2` digits after the decimal sign
  - `Fixed_(?<numberOfDigits>\d+)_(?<resolutionAroundZero>[\d\.]+)` 
	- e.g. `Fixed_4_0.0005`, for rounding with `4` decimals and resolution around zero of `0.0005`
- define `Precision` for financial calculation, as a strictly-positive decimal number
	- e.g. `0.01`, for two digits precision
- define `BaseCurrency`, used as target currency for all financial calculations
	- for the time being only `EUR`, for EURO, is supported
- define `ISINs`, used in reporting of calculation results, as a string-to-string dictionary, mapping the Ticker of a
  financial asset, to the ISIN of that asset. E.g. `{ "AAPL" : "US0378331005", ... }`
	- for the time being only `US` stocks are supported
- define `StockEventsFilePaths`, as the list of paths of files containing ticker events for stocks
	- Remark: each file path can also be a blob pattern
	- Remark: files are processed in increasing lexicographic order of their name
	- e.g. `["stocks_2022.csv", "stocks_2023.csv"]` 
- define `CryptoEventsFilePaths`, as the list of paths of files containing ticker events for crypto
	- Remark: each file path can also be a blob pattern
	- Remark: files are processed in increasing lexicographic order of their name
	- e.g. `["crypto_2022_*.csv", "crypto_2023_*.csv"]` 

### Setup BCE FX Rates

Make sure that `BCE-FXRate-<base_currency>-USD.txt` is up-to-date, i.e. it contains all the FX Rates defined by the ECB
between the base currency `EUR` and `USD` for a contiguous period of time including all dates of ticker events, of any 
type (buy, sell, dividends, for both stocks and crypto).

The FX Rates are provided by the ECB only for working days. It's OK: the software will use the FX Rate as defined in
other reports, or the closest FX Rate available, when the ECB didn't provide an FX Rate for the date of a ticker event.

Updated data can be retrieved at [this link](https://www.banque-france.fr/statistiques/taux-et-cours/les-taux-de-change-salle-des-marches/parites-quotidiennes) 
on the web-site of the Banque de France.

Under the `Taux de change (parités quotidiennes) dd MMM yyyy` page, there is the possibility to download the data in
CSV format, for all conversions. The downloaded CSV file has a complete history of FX Rates for every day in the past, 
and for all currencies. This CSV file should then be then trimmed, retaining only the FX Rate between `USD` and `EUR`.

The resulting file should define FX Rates in the following format:

```
<date>\t<decimalNumber>
```

Where:
- `<date>` is in the format `d/MM/yyyy`	
- `<decimalNumber>` uses `.` as decimal separator, and no thousands separator

For example:

```
4/14/2023	1.1057 
4/13/2023	1.1015
4/12/2023	1.0922
4/11/2023	1.0905
4/10/2023	-
4/9/2023	-
4/8/2023	-
```

Comments are allowed, in the following format:

```
// Titre :	Dollar des Etats-Unis (USD)
// Code s rie :	EXR.D.USD.EUR.SP00.A
// Unit  :	Dollar des Etats-Unis (USD)
// Magnitude :	Unit s (0)
// M thode d'observation :	Fin de p riode (E)
// Source :	BCE (Banque Centrale Europ enne) (4F0)
```

### Setup stock events

Files reporting ticker events for stocks (buy, sell, dividends, split, etc.) should be defined in 

TODO

### Setup crypto events
TODO

## Processing
TODO