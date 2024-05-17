﻿using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Taxes;

PrintEnvironmentAndSettings(Console.Out);

var basics = new Basics();
var fxRatesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.FXRatesFilePath);
var fxRatesReader = new FxRatesReader(basics);
var fxRates = fxRatesReader.ParseMultiCurrenciesFromFile(fxRatesFilePath);
var cryptoPortfolioValuesFilePath = Path.Combine(basics.ReportsDirectoryPath, basics.CryptoPortfolioValuesFilePath);
var cryptoPortfolioValues = new CryptoPortfolioValues(basics, fxRates, basics.CryptoPortfolioValuesCurrency, cryptoPortfolioValuesFilePath);

var stockEventsReader = new StockEventsReader(basics);
var stockEvents = basics.StockEventsFilePaths
    .SelectMany(pattern => Directory.GetFiles(basics.ReportsDirectoryPath, pattern))
    .Order()
    .SelectMany(filePath => stockEventsReader.Parse(filePath, fxRates))
    .ToList();
ProcessEvents(stockEvents, basics, cryptoPortfolioValues);

var cryptoEventsReader = new CryptoEventsReader(basics);
var cryptoEvents = basics.CryptoEventsFilePaths
    .SelectMany(pattern => Directory.GetFiles(basics.ReportsDirectoryPath, pattern))
    .Order()
    .SelectMany(cryptoEventsReader.Parse)
    .ToList();
ProcessEvents(cryptoEvents, basics, cryptoPortfolioValues);

static void ProcessEvents(IList<Event> events, Basics basics, CryptoPortfolioValues cryptoPortfolioValues)
{
    var tickerProcessing = new TickerProcessing(basics, cryptoPortfolioValues);

    // Taken into account in each ticker
    var nonTickerRelatedEvents = (
        from e in events
        where string.IsNullOrWhiteSpace(e.Ticker)
        select e)
        .ToList();
    var eventsByTicker = (
        from e in events
        group e by e.Ticker into g
        orderby g.Key
        select (ticker: g.Key, tickerEvents: g.Concat(nonTickerRelatedEvents).OrderBy(e1 => e1.Date).ToArray()))
        .ToList();

    var tickerStates = (
        from e in eventsByTicker
        select tickerProcessing.ProcessTicker(e.ticker, e.tickerEvents, Console.Out))
        .ToList();

    tickerStates.PrintAggregatedMetrics(Console.Out, basics);
}

static void PrintEnvironmentAndSettings(TextWriter outWriter) 
{
    outWriter.WriteLine("ENVIRONMENT AND SETTINGS");
    outWriter.WriteLine();
    outWriter.WriteLine($"Date and time: {DateTime.Now}");
    outWriter.WriteLine($"Machine name: {Environment.MachineName}");
    outWriter.WriteLine($"User name: {Environment.UserName}");
    outWriter.WriteLine($"Current working directory: {Environment.CurrentDirectory}");
    outWriter.WriteLine($"Command line parameters: {string.Join(' ', Environment.GetCommandLineArgs())}");
    outWriter.WriteLine($"Commit hash: {CommandOutput("git rev-parse HEAD").Trim()}");
    outWriter.WriteLine($"Modified files: {CommandOutput("git diff")}");
    outWriter.WriteLine("MD5 digest of files in Reports folder:");
    foreach (var filePath in Directory.GetFiles("Reports"))
    {
        Console.WriteLine($"- {filePath}: {FileUtils.CalculateMD5Digest(filePath)}");
    }

    outWriter.WriteLine(new string('=', 100));
}

static string CommandOutput(string command, string? workingDirectory = null)
{
    try
    {
        ProcessStartInfo procStartInfo = new("cmd", "/c " + command);

        procStartInfo.RedirectStandardError = procStartInfo.RedirectStandardInput = procStartInfo.RedirectStandardOutput = true;
        procStartInfo.UseShellExecute = false;
        procStartInfo.CreateNoWindow = true;
        if (null != workingDirectory)
        {
            procStartInfo.WorkingDirectory = workingDirectory;
        }

        Process proc = new Process();
        proc.StartInfo = procStartInfo;

        StringBuilder sb = new StringBuilder();
        proc.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
        {
            sb.AppendLine(e.Data);
        };
        proc.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
        {
            sb.AppendLine(e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit();
        return sb.ToString();
    }
    catch (Exception objException)
    {
        return $"Error in command: {command}, {objException.Message}";
    }
}
