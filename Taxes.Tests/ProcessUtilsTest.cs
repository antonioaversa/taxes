using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Taxes.Tests;

[TestClass]
public class ProcessUtilsTest
{
    [TestMethod]
    public void PrintEnvironmentAndSettings()
    {
        using var writer = new StringWriter();
        ProcessUtils.PrintEnvironmentAndSettings(writer, "dummy.log");
        var output = writer.ToString();
        Assert.IsTrue(output.Contains("Date and time: "));
        Assert.IsTrue(output.Contains("Machine name: "));
        Assert.IsTrue(output.Contains("User name: "));
        Assert.IsTrue(output.Contains("AppContext base directory: "));
        Assert.IsTrue(output.Contains("Current working directory: "));
        Assert.IsTrue(output.Contains("Command line parameters: "));
        Assert.IsTrue(output.Contains("Git commit hash: "));
        Assert.IsTrue(output.Contains("## Git Modified Files"));
        Assert.IsTrue(output.Contains("MD5 digest of files in Reports folder:"));
    }

    [TestMethod]
    public void CommandOutput_WithValidCommandAndDefaultWorkDirectory()
    {
        var output = ProcessUtils.CommandOutput("whoami");
        Assert.IsFalse(output.StartsWith("Error in command: "));
    }

    [TestMethod]
    public void CommandOutput_WithInvalidCommandAndDefaultWorkDirectory()
    {
        var output = ProcessUtils.CommandOutput("/dddd an invalid command");
        Assert.IsTrue(output.StartsWith("Error in command: "));
    }

    [TestMethod]
    public void CommandOutput_WithValidCommandAndCustomWorkDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(tempDirectory);
            var output = ProcessUtils.CommandOutput("whoami", tempDirectory);
            Assert.IsFalse(output.StartsWith("Error in command: "));
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
        }

    }
}
