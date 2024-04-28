# Taxes calculator

The software can be used to simulate approximated taxes on financial assets, according to French Law, for a given 
period of time. It can be used in simulations for personal use, and it doesn't provide any guarantee of correctness.

It is provided as-is and shouldn't be used as a substitute for professional tools or advice.

It currently deals with the calculation of the following asset classes:
- stocks (and ETFs of stocks)
- crypto-currencies

It has not been designed to process bonds, ETFs of bonds, or other financial assets, such as options, futures, etc.
It also doesn't provide any support for the calculation of taxes on income, such as salaries, pensions, etc.
It doesn't offer support in the compilation of the tax declaration, nor in its submission.

It is designed to be used in **batch mode**, and it requires the following input:
- **Basics.json**, a JSON file containing the basic setup of the software
  - where settings like rounding, precision, base currency, ISINs, etc. are configured
  - also where the paths of input files containing list of events are specified (see next point)
- **one or more ordered list of stock events**, each list in a CSV, according to the Revolut export format
  - events can be ticker events, such as buy, sell, dividends and splits, or portfolio events, such as deposits and withdrawals
  - multiple lists are supported to be able to keep one list (that is one CSV file) per year
	- keeping different lists per year is useful but not mandatory
	- when data for multiple years is provided, it's important to have a reset event at the beginning of each year, 
	  to reset the state of the calculation of capital gains, losses, dividends, etc.
  - if more than one list is provided, the software will merge them in a single list of events, ordered by date
- **one or more ordered list of crypto events**, each list in a CSV, according to the Revolut export format
  - the Revolut export format for crypto events is different from the one for stock events
  - events can be crypto-specific events (`EXCHANGE`), or portfolio events (`TRANSFER`) 
  - multiple lists are supported to be able to keep one list (that is one CSV file) per year and per crypto
	- keeping different lists per year and per crypto is useful but not mandatory
	- as for stocks, when data for multiple years is provided, it's important to have a reset event at the beginning of 
	  each year, to reset the state of the calculation of crypto gains and losses
  - as for stocks, if more than one list is provided, the software will merge them in a single list of events, ordered
	by date
- **BCE FX Rates**, a semicolon-separated-values file in French-local containing the FX Rates (Foreign eXchange Rates)
  between the base currency (specified in the basics) and any other currency appearing in the events
  - the exchange rate is determined by the ECB and provided by the Banque de France on their web-site
  - the file should contain all FX Rates for a contiguous period of time including all dates of ticker events
  - the software will use the FX Rate as defined in other reports, or the closest FX Rate available, when the ECB 
	didn't provide an FX Rate for the date of a ticker event
  - as an alternative for stock events, the FX Rate specified on each event can be used
	- that is available in Revolut exported data for stock events only, not for crypto events
 
The output is emitted on the standard output. It shows the processing of the events, and the calculation of the taxes
step-by-step, giving the state of the portfolio and the taxes due after each event.

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