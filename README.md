# Revolut taxes calculator for France

The software can be used to **simulate approximated taxes on financial assets**, according to French Law, for a given 
period of time. 

It is designed to process the data exported from the Revolut app, but can be easily adapted to other
sources of data.

> [!CAUTION]
> It can be used in simulations for personal use, and it doesn't provide any guarantee of correctness.
> It is provided as-is and shouldn't be used as a substitute for professional tools or advice.

It currently deals with the calculation of the following asset classes:
- stocks (and ETFs of stocks)
- crypto-currencies

> [!WARNING]
> It has not been designed to process bonds, ETFs of bonds, or other financial assets, such as options, futures, etc.
> It also doesn't provide any support for the calculation of taxes on income, such as salaries, pensions, etc.
> It doesn't offer support in the compilation of the tax declaration, nor in its submission.

Check the [Prerequirements section](#Prerequirements) for more details on the prerequisites to use the software.

Check the [Build section](#Build) for more details on how to build and execute the software.

Check the [Input and output section](#Input-and-output) for more details on how to use the software.

Then check the [Setup section](#Setup) for more details on how to configure the software before its use.

## Prerequirements

The software is written in C# and requires the .NET 9.0 runtime to be installed on the machine where it is executed.

The latest version of .NET 9.0 for all major Operating Systems (Linux, MacOS, Windows) can be downloaded from the 
[official site](https://dotnet.microsoft.com/download/dotnet/9.0).

## Build

The software can be built using the `dotnet` command-line tool on the three major Operating Systems.

Once this repository has been cloned and the prerequisites have been installed, the software can be built and run
by opening a terminal and executing the following commands starting from the root directory of the project:

```shell
cd Taxes
dotnet build
dotnet run
```

## Input and output

It is designed to be used in **batch mode** (no UI provided).

It requires the following input:
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
  - the software will use the FX Rate as defined in other reports, or the closest FX Rate available following, when the
    ECB didn't provide an FX Rate for the date of a ticker event
  - as an alternative for stock events, the FX Rate specified on each event can be used during calculation
	- that is available in Revolut exported data for stock events only, not for crypto events
	- however, BCE FX Rates should be used whenever available, so that simulation results are as close to actual taxes
	  as possible
 
The output is emitted to the standard output. It shows the processing of the events, and the calculation of the taxes
step-by-step, giving the state of the portfolio and the taxes due after each event.

Example of output:

```text
PROCESS TSM [US8740391003]
0: 2022-09-22 15:09:50 BuyMarket 15 shares at 75.74 USD/share => 1136.08 USD (FXRate = 0.9884)
        Total Buy Price (USD) = 1136.08
        Total Buy Price (EUR) = 1149.4132
        Shares Buy Price (USD) = 1136.10
        Shares Buy Price (EUR) = 1149.4334
        PerShare Buy Price (USD) = 75.74
        PerShare Buy Price (EUR) = 76.6289
        Buy Fees (USD) = 0.02
        Buy Fees (EUR) = 0.0202
        Ticker State: 15 shares => 1149.4132 EUR, +V = CUMP 0 EUR, PEPS 0 EUR, CRYP 0 EUR, -V = CUMP 0 EUR, PEPS 0 EUR, CRYP 0 EUR, Dividends = 0 EUR + WHT 0 EUR = 0 EUR

1: 2023-01-17 05:29:46 Dividend => 5.30 USD (FXRate = 1.0843)
        Net Dividend (USD) = 5.30
        Net Dividend (EUR) = 4.8879
        WHT Dividend (EUR) = 0.8626
        Gross Dividend (EUR) = 5.7505
        Ticker State: 15 shares => 1149.4132 EUR, +V = CUMP 0 EUR, PEPS 0 EUR, CRYP 0 EUR, -V = CUMP 0 EUR, PEPS 0 EUR, CRYP 0 EUR, Dividends = 4.8879 EUR + WHT 0.8626 EUR = 5.7505 EUR

2: 2023-01-24 18:50:20 SellMarket 15 shares at 94.21 USD/share => 1413.13 USD (FXRate = 1.0858)
        Total Sell Price (USD) = 1413.13
        Shares Sell Price (USD) = 1413.15
        PerShare Average Buy Price (EUR) = 76.6275
        Total Average Buy Price (EUR) = 1149.4132
        PerShare Sell Price (EUR) = 86.7655
        Shares Sell Price (EUR) = 1301.4828
        Total Sell Price (EUR) = 1301.4644
        Sell Fees (EUR) = 0.0184
        Plus Value CUMP (EUR) = 152.0512
        PEPS Remaining Quantity to match: 15 => FIND Buy Event
        PEPS Buy Event 2022-09-22 15:09:50 BuyMarket 15 shares at 75.74 USD/share => 1136.08 USD (FXRate = 0.9884) at index 0 bought entirely => move to next
        PEPS Remaining Quantity to match: 0 => DONE
        Plus Value PEPS (EUR) = 152.0107
        Portfolio Current Value not known => Skipping Crypto +/- value calculation...
        Ticker State: 0 shares => 0 EUR, +V = CUMP 152.0512 EUR, PEPS 152.0107 EUR, CRYP 0 EUR, -V = CUMP 0 EUR, PEPS 0 EUR, CRYP 0 EUR, Dividends = 4.8879 EUR + WHT 0.8626 EUR = 5.7505 EUR
```

At the end of the processing, the software emits a summary of the taxes due, for each asset class, for the entire period:

```text
Total Plus Value CUMP (EUR) = 2855.3576
Total Plus Value PEPS (EUR) = 3542.9491
Total Plus Value CRYPTO (EUR) = 0
Total Minus Value CUMP (EUR) = 591.6265
Total Minus Value PEPS (EUR) = 1671.8474
Total Minus Value CRYPTO (EUR) = 0
Total Net Dividends (EUR) = 252.9329
Total WHT Dividends (EUR) = 44.6352
Total Gross Dividends (EUR) = 297.5682
Total Plus Value CUMP (EUR) = 0
Total Plus Value PEPS (EUR) = 0
Total Plus Value CRYPTO (EUR) = 0
Total Minus Value CUMP (EUR) = 0
Total Minus Value PEPS (EUR) = 0
Total Minus Value CRYPTO (EUR) = 0
Total Net Dividends (EUR) = 0
Total WHT Dividends (EUR) = 0
Total Gross Dividends (EUR) = 0
```

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
- define `Positions`, used in reporting of calculation results, as a string-to-object dictionary, mapping the Ticker of 
  a financial asset, to the Country and ISIN of that asset. Both are mandatory. 
  - e.g. `{ "AAPL" : { "Country": "US", "ISIN": "US0378331005" }, ... }`
  - `FR` is added to the list for all those products that are not considered "foreign" and are not subject to 
    withholding tax, such as crypto and security lending programs
  - for the time being only `US` and `IE` stocks are supported, for the calculation of withholding tax on dividends
- define events file paths: 
  - `StockEventsFilePaths`, as the list of paths of files containing ticker events for stocks
	- Remark: each file path can also be a blob pattern
	- Remark: files are processed in increasing lexicographic order of their name
	- e.g. `["stocks_2022.csv", "stocks_2023.csv"]` 
    - data extraction from Revolut and format described in the [Setup stock events section](#Setup-stock-events)
  - `CryptoEventsFilePaths`, as the list of paths of files containing ticker events for crypto
	- Remark: each file path can also be a blob pattern
	- Remark: files are processed in increasing lexicographic order of their name
	- e.g. `["crypto_2022_*.csv", "crypto_2023_*.csv"]` 
    - data extraction from Revolut and format described in the [Setup crypto events section](#Setup-crypto-events)
- define crypto portfolio settings:
  - `CryptoPortfolioValuesFilePath`, as the path of the file containing the value of the entire crypto portfolio
    for each relevant day 
  - data extraction from Revolut and format described in the [Setup crypto portfolio values section](#Setup-crypto-portfolio-values) 
- define FX Rates settings: 
  - `FXRatesFilePath`, as the path of the file that contains the FX Rates
  - data extraction from the web and format described in the [Setup FX Rates section](#Setup-FX-Rates)

### Setup stock events

The stock events are exported from the Revolut app in CSV format.

An example of valid stock events file is the following:

```texts
Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
2022-03-30T23:48:44.882381Z,,CASH TOP-UP,,,"$3,000",USD,1.12
2022-05-02T13:32:24.217636Z,TSLA,BUY - MARKET,1.018999,$861.63,$878,USD,01.06
2022-06-02T06:41:50.336664Z,,CUSTODY FEE,,,($1.35),USD,01.07
2022-06-06T05:20:46.594417Z,AMZN,STOCK SPLIT,11.49072,,$0,USD,01.08
2022-06-10T04:28:02.657456Z,MSFT,DIVIDEND,,,$10.54,USD,01.07
2022-06-23T14:30:37.660417Z,CVNA,SELL - LIMIT,15,$26.62,$398.23,USD,01.06
2022-06-29T13:45:38.161874Z,CVNA,BUY - LIMIT,10,$23.02,$231.25,USD,01.05
2022-07-07T15:37:02.604183Z,QCOM,SELL - MARKET,8,$132.29,"$1,055.65",USD,01.02
2022-07-19T06:01:55.845058Z,GSK,STOCK SPLIT,-1.8,,$0,USD,01.02
2023-01-01T00:00:00.000000Z,,RESET,,,$0,USD,1.0584
2023-11-26T21:54:34.023429Z,,CASH WITHDRAWAL,,,"($3,500)",USD,1.0968
2023-12-11T14:34:06.497Z,PFE,BUY - LIMIT,17,$28.50 ,$485.71 ,USD,1.0765
2023-12-18T14:37:36.664Z,ORCL,BUY - MARKET,20,$104.24 ,"$2,084.90 ",USD,1.0947
```

### Setup crypto events

The crypto events are exported from the Revolut app in CSV format.

Revolut has changed the CSV export format in 2025, and the new format is not compatible with the old one.

The software supports both pre-2025 and 2025 CSV file formats.

#### Pre-2025 CSV file format

An example of valid crypto events file in pre-2025 CSV file format is the following:

```text
Type,Product,Started Date,Completed Date,Description,Amount,Currency,Fiat amount,Fiat amount (inc. fees),Fee,Base currency,State,Balance
EXCHANGE,Current,2022-03-30 22:23:01,2022-03-30 22:23:01,Exchanged to SOL,2.000000,SOL,214.83,218.05,3.22,EUR,COMPLETED,2.000000
EXCHANGE,Current,2022-04-06 22:16:57,2022-04-06 22:16:57,Exchanged to SOL,0.500000,SOL,53.90,54.71,0.81,EUR,COMPLETED,2.500000
EXCHANGE,Current,2022-04-18 00:23:02,2022-04-18 00:23:02,Exchanged to SOL,0.500000,SOL,46.61,47.31,0.70,EUR,COMPLETED,3.000000
TRANSFER,Current,2022-05-19 10:09:31,2022-05-19 10:09:31,Balance migration to another region or legal entity,-3.000000,SOL,-139.89,-139.89,0.00,EUR,COMPLETED,
TRANSFER,Current,2022-05-19 10:09:31,2022-05-19 10:09:31,Balance migration to another region or legal entity,3.000000,SOL,140.18,140.18,0.00,EUR,COMPLETED,
EXCHANGE,Current,2022-10-21 09:14:13,2022-10-21 09:14:13,Exchanged to SOL,10.000000,SOL,280.11,284.28,4.17,EUR,COMPLETED,13.000000
```

When exporting the data from the Revolut app, make sure to select all the crypto option, to get all the events of all
crypto currencies.

Also, it seems that old transactions for crypto currencies that are not available anymore on Revolut, such as NuCypher,
are not exported anymore. In this case, make sure you can integrate the report with the missing transactions, or the 
software will not be able to calculate the taxes for those.

The exported file is in xlsx format, and should be converted to CSV using a spreadsheet software, such as Excel. 
Before making the conversion, ensure that:
- the dates (`Started Date`, `Completed Date`) are in the format `yyyy-MM-dd HH:mm:ss`
- the numeric values (`Amount`, `Fiat amount`, `Fiat amount (inc. fees)`, `Fee`, `Balance`) have `.` as decimal 
  separator, no thousands separator and a very high precision after the decimal separator (e.g. 10-digit)

#### 2025 CSV file format

An example of valid crypto events file in 2025 CSV file format is the following:

```text
Symbol,Type,Quantity,Price,Value,Fees,Date
BTC,Buy,0.02,"EUR 62,654.96","EUR 1,253.10",EUR 12.41,"Mar 15, 2024, 12:05:32 PM"
BTC,Sell,0.02,"EUR 62,671.63","EUR 1,253.4426",EUR 12.41,"Mar 8, 2024, 9:32:39 PM"
```

The file can be generated by:
- opening the Revolut App
- going to the Cryto section
- More -> Documents
- Select "Account statement"
- Select "Excel" as export format
- Select the appropriate period type: e.g. "Tax year"
- Select the appropriate period: e.g. "2024"
- Click on "Generate"
- Open with Excel, or any other application able to visualize the CSV

Unlike the pre-2025 format, the 2025 format includes all the crypto currencies in a single file, including the ones 
that are not owned or available anymore on Revolut.

Moreover, now Revolut allows to exchange crypto currencies against any FIAT currency, and not just against the base
one. The FIAT currency bought/sold against crypto is specified as prefix in the `Price`, `Value`, and `Fees` columns.
The software expect that the three currencies for a given transaction are the same. However, different transaction
can be made against different FIAT currencies.

### Setup crypto portfolio values

Unlike stocks, crypto taxes calculation require knowning the value of the entire crypto portfolio after each taxable 
event, not just the sell price of the specific crypto sold.

As of the day of writing this, there is no option to extract this data from the Revolut app into a file, so the value 
of the portfolio should be checked manually in the UI of the app and entered in a file respecting the following format:

```text
Date,PortfolioValue,Currency
2022-03-30,2261.4,USD
2022-03-31,2756.6,USD
2022-06-15,8422.8,USD
2022-06-21,8670.8,USD
2022-06-24,8942.1,USD
2022-06-27,9996.7,USD
2022-06-28,10414,USD
```

If the currency is the same as the base currency (as defined in `Basics.json`), then the FX Rate is not needed, and the
as the software will use `1` as FX Rate for any date, including weekends and in general dates when the ECB doesn't
provide official exchange rates.

If the currency is different from the base currency, then a FX Rate is needed for each date, and the software will use 
the FX Rate as defined in other reports, or the closest FX Rate available following, when the ECB didn't provide an FX 
Rate for the date of a ticker event.

### Setup FX Rates

The FX Rates are provided by the ECB only for working days. It's OK: the software will use the FX Rate as defined in
other reports, or the closest FX Rate available following, when the ECB didn't provide an FX Rate for the date of a 
ticker event.

Updated data can be retrieved at [this link](https://www.banque-france.fr/statistiques/taux-et-cours/les-taux-de-change-salle-des-marches/parites-quotidiennes) 
on the web-site of the Banque de France.

Under the `Taux de change (parit�s quotidiennes) dd MMM yyyy` page, there is the possibility to download the data in
CSV format, for all conversions. The downloaded CSV file has a complete history of FX Rates for every day in the past, 
and for all currencies.

When using the multi-currency FX Rates option, the file can be used as-is, without any modification.

### Reset events

The software requires a reset event at the beginning of each tax period (i.e. typically the fiscal year), to reset 
the state of the calculation of capital gains, losses, dividends, etc. That applies to both stocks and crypto.

A reset event resets to 0 all counters related to the calculation of capital gains, losses, dividends, etc.

A reset event, however, is different from simply removing all the events of the previous year and start fresh, as it 
allows to keep the history of all the events, and match, for example, sell events within the year of interest to buy
events in previous years, according CUMP and PEPS methodologies.

#### Stocks reset event

A reset event for stocks looks like the following:

```text
Date,Ticker,Type,Quantity,Price per share,Total Amount,Currency,FX Rate
2023-01-01T00:00:00.000000Z,,RESET,,,,USD,1.0584
```

The `Date` is mandatory since it places the reset event in time w.r.t. other events.
Also `Currency` and `FX Rate` are mandatory, although for technical reasons only, as they are not used in the 
processing of the reset event. 

#### Crypto reset event

A reset event for crypto in 2025 CSV format looks like the following:

```text
Symbol,Type,Quantity,Price,Value,Fees,Date
,Reset,,,,,"Jan 1, 2024, 12:00:00 AM"
```

Apart from `Type`, the only other mandatory field is `Date`, since it places the reset event in time w.r.t. 
other events.
