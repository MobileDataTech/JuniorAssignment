using System.Text.Json;
using System.Text.Json.Serialization;

namespace PumpTelemetry.Core;

/// Append-only alert history, persisted as JSON Lines (one JSON object per line)
/// so each alert is a cheap append. Append/Load are safe to call from any thread.
public sealed class AlertHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = {new JsonStringEnumConverter() } // Ensures that enum values are serialized as strings otherwise they would be e.g. Type: 0 instead of Type: "HighPressure"
    };

    private readonly string _filePath;
    private readonly object _gate = new();

    public AlertHistoryStore(string filePath)
    {
        _filePath = filePath;
    }

    public void Append(AlertRecord record)
    {
        var line = JsonSerializer.Serialize(record, JsonOptions);
        // The pipeline emits alerts from two threads (the channel reader and the
        // silent-device monitor), so appends are serialized to avoid interleaved
        // or failed concurrent writes to the file.
        lock (_gate)
        {
            File.AppendAllText(_filePath, line + Environment.NewLine);
        }
    }

    public IReadOnlyList<AlertRecord> Load()
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<AlertRecord>();
        }

        var records = new List<AlertRecord>();
        foreach (var line in File.ReadLines(_filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var record = JsonSerializer.Deserialize<AlertRecord>(line, JsonOptions); 
                if (record is not null)
                {
                    records.Add(record);
                }
            }
            catch (JsonException)
            {
                //Skip a partially written or corrupted line, instead of throwing an exception and stopping the load process. This is important for robustness, especially in scenarios where the application might crash or be terminated while writing to the file.
            }
        }
        return records;
    }

    public static IReadOnlyList<AlertRecord> ApplyFilter(
        IEnumerable<AlertRecord> records,
        string? severity = null,
        int? last = null)
    {
        IEnumerable<AlertRecord> result = records;

        if (!string.IsNullOrWhiteSpace(severity))
        {
            result = result.Where(r => 
                r.Type.ToString().Contains(severity, StringComparison.OrdinalIgnoreCase)); // Substring match so a short "high" matches "HighPressure" (case-insensitive).

        }
        if (last is > 0)
        {
            result = result.TakeLast(last.Value); // takes the last N (last) most recent entries - since we enter in chronological order
        }

        return result.ToList();
    }

}

