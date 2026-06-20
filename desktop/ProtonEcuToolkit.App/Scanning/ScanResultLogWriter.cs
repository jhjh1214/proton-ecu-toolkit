using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProtonEcuToolkit.Core.Scanning;

namespace ProtonEcuToolkit.App.Scanning;

/// <summary>
/// Appends every scan attempt to a timestamped JSONL file as it happens -
/// the raw evidence trail (README.md §"PID/CID discovery scanner"), not
/// just the positive hits shown in the live results table.
/// </summary>
public sealed class ScanResultLogWriter : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly StreamWriter _writer;

    public string FilePath { get; }

    public ScanResultLogWriter()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProtonEcuToolkit",
            "scan-results");
        Directory.CreateDirectory(dir);
        FilePath = Path.Combine(dir, $"scan-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.jsonl");
        _writer = new StreamWriter(FilePath, append: false) { AutoFlush = true };
    }

    public void WriteEntry(ScanResultEntry entry) => _writer.WriteLine(JsonSerializer.Serialize(entry, JsonOptions));

    public void Dispose() => _writer.Dispose();
}
