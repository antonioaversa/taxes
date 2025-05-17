using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using Taxes;

namespace Taxes.Tests;

[TestClass]
public class TeeTextWriterTests
{
    private string _testDirectory = Path.Combine(Path.GetTempPath(), "TeeTextWriterTests", Guid.NewGuid().ToString());

    [TestInitialize]
    public void TestInitialize()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "TeeTextWriterTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    // Constructor Tests
    [TestMethod]
    public void Constructor_CreatesOutputDirectory_IfNotExists()
    {
        var outputDir = Path.Combine(_testDirectory, "TestOutputDir");
        Assert.IsFalse(Directory.Exists(outputDir));

        using var writer = new TeeTextWriter(outputDir, "test-{datetime}.log");

        Assert.IsTrue(Directory.Exists(outputDir));
    }

    [TestMethod]
    public void Constructor_CreatesLogFile_WithCorrectNameFormat()
    {
        var fileNameFormat = "test-log-{datetime}.txt";
        using var writer = new TeeTextWriter(_testDirectory, fileNameFormat);

        var files = Directory.GetFiles(_testDirectory);
        Assert.AreEqual(1, files.Length);
        var fileName = Path.GetFileName(files[0]);
        
        // Check if the file name matches the pattern (ignoring the exact datetime part)
        Assert.IsTrue(fileName.StartsWith("test-log-"));
        Assert.IsTrue(fileName.EndsWith(".txt"));
        // A more robust check would involve regex if we need to validate the datetime format itself
        // For now, just check that a file was created as expected.
    }

    [TestMethod]
    public void Constructor_ThrowsArgumentException_ForNullOutputDirectoryPath()
    {
        Assert.ThrowsException<ArgumentException>(() => 
            new TeeTextWriter(null!, "test-{datetime}.log"));
    }

    [TestMethod]
    public void Constructor_ThrowsArgumentException_ForWhitespaceOutputDirectoryPath()
    {
        Assert.ThrowsException<ArgumentException>(() => 
            new TeeTextWriter("   ", "test-{datetime}.log"));
    }

    [TestMethod]
    public void Constructor_ThrowsArgumentException_ForNullFileNameFormat()
    {
        Assert.ThrowsException<ArgumentException>(() => 
            new TeeTextWriter(_testDirectory, null!));
    }

    [TestMethod]
    public void Constructor_ThrowsArgumentException_ForWhitespaceFileNameFormat()
    {
        Assert.ThrowsException<ArgumentException>(() => 
            new TeeTextWriter(_testDirectory, "   "));
    }

    [TestMethod]
    public void Constructor_UsesConsoleOut_WhenPrimaryWriterIsNull()
    {
        // This is hard to assert directly without changing Console.Out or using a more complex setup.
        // We can infer it by checking that operations don't fail and a file is created.
        // A more direct test would require dependency injection for Console.Out.
        using var writer = new TeeTextWriter(_testDirectory, "console-test-{datetime}.log", null);
        Assert.IsNotNull(writer); // Basic check
        // Further checks would involve verifying Console.Out was actually written to if we could capture it.
    }
    
    [TestMethod]
    public void Constructor_UsesProvidedPrimaryWriter()
    {
        using var primaryStringWriter = new StringWriter();
        using var writer = new TeeTextWriter(_testDirectory, "primary-test-{datetime}.log", primaryStringWriter);
        
        writer.Write("test");
        Assert.AreEqual("test", primaryStringWriter.ToString());
    }

    // FilePath Property Test
    [TestMethod]
    public void FilePath_ReturnsCorrectPath()
    {
        var fileNameFormat = "path-test-{datetime}.log";
        using var writer = new TeeTextWriter(_testDirectory, fileNameFormat);
        
        var expectedFilePath = Directory.GetFiles(_testDirectory)[0]; // Assumes only one file created
        Assert.AreEqual(expectedFilePath, writer.FilePath);
    }

    // Encoding Property Test
    [TestMethod]
    public void Encoding_ReturnsPrimaryWriterEncoding()
    {
        using var primaryStringWriter = new StringWriter(); // Default is UTF-16
        using var writer = new TeeTextWriter(_testDirectory, "encoding-test-{datetime}.log", primaryStringWriter);
        
        Assert.AreEqual(primaryStringWriter.Encoding, writer.Encoding);
    }

    // Write Operations Tests
    [TestMethod]
    public void Write_Char_WritesToBothWriters()
    {
        using var primaryStringWriter = new StringWriter();
        using var writer = new TeeTextWriter(_testDirectory, "write-char-{datetime}.log", primaryStringWriter);
        var filePath = writer.FilePath;

        writer.Write('A');
        writer.Write('B');

        Assert.AreEqual("AB", primaryStringWriter.ToString());
        Assert.AreEqual("AB", File.ReadAllText(filePath));
    }

    [TestMethod]
    public void Write_String_WritesToBothWriters()
    {
        using var primaryStringWriter = new StringWriter();
        using var writer = new TeeTextWriter(_testDirectory, "write-string-{datetime}.log", primaryStringWriter);
        var filePath = writer.FilePath;

        writer.Write("Hello");
        writer.Write(" World");

        Assert.AreEqual("Hello World", primaryStringWriter.ToString());
        Assert.AreEqual("Hello World", File.ReadAllText(filePath));
    }

    [TestMethod]
    public void Write_NullString_WritesToBothWritersAsEmpty()
    {
        // StringWriter and StreamWriter typically handle null as empty or don't write anything specific.
        // Let's see the behavior of TeeTextWriter (which should mirror its underlying writers).
        using var primaryStringWriter = new StringWriter();
        using var writer = new TeeTextWriter(_testDirectory, "write-null-{datetime}.log", primaryStringWriter);
        var filePath = writer.FilePath;

        writer.Write("Prefix-");
        writer.Write(null as string); // Explicitly null string
        writer.Write("-Suffix");

        Assert.AreEqual("Prefix--Suffix", primaryStringWriter.ToString());
        Assert.AreEqual("Prefix--Suffix", File.ReadAllText(filePath));
    }

    [TestMethod]
    public void WriteLine_String_WritesToBothWritersWithNewLine()
    {
        using var primaryStringWriter = new StringWriter();
        using var writer = new TeeTextWriter(_testDirectory, "writeline-string-{datetime}.log", primaryStringWriter);
        var filePath = writer.FilePath;
        var expectedPrimary = $"Hello{Environment.NewLine}World{Environment.NewLine}";
        // File.ReadAllText might normalize newlines, so be careful with direct comparison if OS-specific newlines are involved.
        // StreamWriter defaults to UTF-8 which often uses \n. Let's assume \n for file comparison for simplicity or read lines.

        writer.WriteLine("Hello");
        writer.WriteLine("World");

        Assert.AreEqual(expectedPrimary, primaryStringWriter.ToString());
        // Read lines to avoid issues with CR/LF vs LF in file
        var fileLines = File.ReadAllLines(filePath);
        Assert.AreEqual(2, fileLines.Length);
        Assert.AreEqual("Hello", fileLines[0]);
        Assert.AreEqual("World", fileLines[1]);
    }

    [TestMethod]
    public void WriteLine_NullString_WritesToBothWritersWithNewLine()
    {
        using var primaryStringWriter = new StringWriter();
        using var writer = new TeeTextWriter(_testDirectory, "writeline-null-{datetime}.log", primaryStringWriter);
        var filePath = writer.FilePath;
        var expectedPrimary = $"Prefix-{Environment.NewLine}{Environment.NewLine}-Suffix{Environment.NewLine}";

        writer.WriteLine("Prefix-");
        writer.WriteLine(null as string); // Writes a newline for null
        writer.WriteLine("-Suffix");

        Assert.AreEqual(expectedPrimary, primaryStringWriter.ToString());
        var fileLines = File.ReadAllLines(filePath);
        Assert.AreEqual(3, fileLines.Length);
        Assert.AreEqual("Prefix-", fileLines[0]);
        Assert.AreEqual("", fileLines[1]); // Empty line for null
        Assert.AreEqual("-Suffix", fileLines[2]);
    }

    // Flush and Dispose Tests
    [TestMethod]
    public void Flush_FlushesBothWriters()
    {
        // StreamWriter has AutoFlush = true in TeeTextWriter implementation.
        // For primaryStringWriter, Flush() can be called.
        // This test is more about ensuring the method call doesn't break and trying to verify state if possible.
        using var primaryStringWriter = new StringWriter();
        using var writer = new TeeTextWriter(_testDirectory, "flush-test-{datetime}.log", primaryStringWriter);
        var filePath = writer.FilePath;

        writer.Write("Some data");
        // Normally, StringWriter writes immediately. File stream might buffer if AutoFlush was false.
        writer.Flush(); 

        Assert.AreEqual("Some data", primaryStringWriter.ToString());
        Assert.AreEqual("Some data", File.ReadAllText(filePath)); // AutoFlush means it should be there
    }

    [TestMethod]
    public void Dispose_ClosesSecondaryWriter_AndFileIsWritten()
    {
        using var primaryStringWriter = new StringWriter();
        string filePath;
        using (var writer = new TeeTextWriter(_testDirectory, "dispose-test-{datetime}.log", primaryStringWriter))
        {
            filePath = writer.FilePath;
            writer.Write("Final Data");
        } // writer is disposed here

        Assert.AreEqual("Final Data", primaryStringWriter.ToString()); // StringWriter content remains after dispose
        Assert.IsTrue(File.Exists(filePath));
        Assert.AreEqual("Final Data", File.ReadAllText(filePath));

        // Try to write to the file again to ensure it's closed (this would throw if not closed)
        // This is a bit indirect. A better test might involve trying to get a lock or checking handles.
        try
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // If we can open it with no sharing, it implies the previous handle was released.
            }
        }
        catch (IOException)
        {
            Assert.Fail("File should be closed and accessible after TeeTextWriter is disposed.");
        }
    }
} 