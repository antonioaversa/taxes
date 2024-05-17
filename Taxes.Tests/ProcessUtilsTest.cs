using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Taxes.Tests;

[TestClass]
public class ProcessUtilsTest
{
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
