using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Taxes;

public static class ProcessUtils
{
    public static void PrintEnvironmentAndSettings(TextWriter outWriter)
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
            outWriter.WriteLine($"- {filePath}: {FileUtils.CalculateMD5Digest(filePath)}");
        }

        outWriter.WriteLine(new string('=', 100));
    }

    internal /* for testing */ static string CommandOutput(string command, string? workingDirectory = null)
    {
        try
        {
            var (filename, arguments) = BuildShellCommand(command);

            ProcessStartInfo procStartInfo = new(filename, arguments)
            {
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            if (null != workingDirectory)
            {
                procStartInfo.WorkingDirectory = workingDirectory;
            }

            StringBuilder sb = new();
            Process proc = new()
            {
                StartInfo = procStartInfo
            };

            proc.OutputDataReceived += (sender, e) => sb.AppendLine(e.Data);
            proc.ErrorDataReceived += (sender, e) => sb.AppendLine(e.Data);

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"{sb}, exit code = {proc.ExitCode}");

            return sb.ToString();
        }
        catch (Exception objException)
        {
            return $"Error in command: {command}, {objException.Message}";
        }
    }

    [ExcludeFromCodeCoverage]
    private static (string filename, string arguments) BuildShellCommand(string command) => 
        (Environment.OSVersion.Platform) switch
        {
            PlatformID.Unix or PlatformID.MacOSX => ("bash", "-c " + command),
            PlatformID.Win32NT => ("cmd", "/c " + command),
            _ => throw new PlatformNotSupportedException(),
        };
}
