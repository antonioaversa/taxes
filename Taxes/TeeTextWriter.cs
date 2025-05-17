using System.Text;

namespace Taxes;

/// <summary>
/// A TextWriter implementation that writes to both a primary TextWriter (e.g., Console.Out)
/// and a secondary TextWriter (e.g., a log file).
/// </summary>
public class TeeTextWriter : TextWriter
{
    private readonly TextWriter _primaryWriter;
    private readonly StreamWriter _secondaryWriter;
    private readonly string _filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeeTextWriter"/> class.
    /// </summary>
    /// <param name="outputDirectoryPath">The directory where the log file will be created.</param>
    /// <param name="fileNameFormat">The format string for the log file name (e.g., "Taxes-yyyy-MM-dd-HH-mm-ss.log").</param>
    /// <param name="primaryWriter">The primary writer, defaults to Console.Out if null.</param>
    public TeeTextWriter(string outputDirectoryPath, string fileNameFormat, TextWriter? primaryWriter = null)
    {
        _primaryWriter = primaryWriter ?? Console.Out;

        if (string.IsNullOrWhiteSpace(outputDirectoryPath))
            throw new ArgumentException("Output directory path cannot be null or whitespace.", nameof(outputDirectoryPath));

        if (string.IsNullOrWhiteSpace(fileNameFormat))
            throw new ArgumentException("File name format cannot be null or whitespace.", nameof(fileNameFormat));

        Directory.CreateDirectory(outputDirectoryPath); // Ensure the directory exists

        var datetime = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
        var actualFileName = fileNameFormat.Replace("{datetime}", datetime);
        _filePath = Path.Combine(outputDirectoryPath, actualFileName);
        
        // Create a StreamWriter with UTF-8 encoding and that flushes automatically.
        _secondaryWriter = new StreamWriter(_filePath, append: false, encoding: Encoding.UTF8)
        {
            AutoFlush = true
        };
    }

    public string FilePath => _filePath;

    public override Encoding Encoding => _primaryWriter.Encoding;

    public override void Write(char value)
    {
        _primaryWriter.Write(value);
        _secondaryWriter.Write(value);
    }

    public override void Write(string? value)
    {
        _primaryWriter.Write(value);
        _secondaryWriter.Write(value);
    }

    public override void WriteLine(string? value)
    {
        _primaryWriter.WriteLine(value);
        _secondaryWriter.WriteLine(value);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _secondaryWriter.Dispose();
        }
        base.Dispose(disposing);
    }

    public override void Flush()
    {
        _primaryWriter.Flush();
        _secondaryWriter.Flush();
    }
} 