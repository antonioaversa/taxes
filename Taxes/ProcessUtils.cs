using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.AccessControl;
using System.Text;

namespace Taxes;

public static class ProcessUtils
{
    public static string CommandOutput(string command, string? workingDirectory = null)
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
