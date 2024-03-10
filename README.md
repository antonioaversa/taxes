# taxes
Taxes

## Setup reports directory
Update the input reports under the `Reports` directory.
Those reports are necessary for the calculation of the taxes.

### Setup basics

Make sure that `Basics.json` is up-to-date:
- define `Precision` for financial calculation, as a decimal number. E.g. `0.01`, for two digits precision.
- define `BaseCurrency`, used as target currency for all financial calculations. E.g. `EUR`, for EURO.
- define `Rounding`, used to round figure in calculations. Acceptable values are:
  - `Fixed_(?<numberOfDigits>\d+)`
  - `Fixed_(?<numberOfDigits>\d+)_(?<resolutionAroundZero>[\d\.]+)` 
- define `ISINs`, used in reporting of calculation results, as a string-to-string dictionary, mapping the Ticker of a
  financial asset, to the ISIN of that asset. E.g. `{ "AAPL" : "US0378331005", ... }`.

### Setup BCE FX Rates

Make sure that `BCE-FXRate-<base_currency>-USD.txt` is up-to-date, i.e. it contains all the FX Rates defined by the ECB
between the base currency (e.g. `EUR`) and `USD` for a contiguous period of time including all dates of ticker events,
of any type (buy, sell, dividends, for both stocks and crypto).

The FX Rates are provided by the ECB only for working days. It's OK: the software will use the FX Rate as defined in
other reports, or the closest FX Rate available, when the ECB didn't provide an FX Rate for the date of a ticker event.

The file should define FX Rates in the following format:

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




