using System.Text.Json;
using System.Text.Json.Serialization;

namespace PumpTelemetry.Core;

// Append-only JSON Lines history file (one AlertRecord per line).
public sealed class AlertHistoryStore
{
    private readonly string _filePath;
    private readonly object _gate = new();

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public AlertHistoryStore(string filePath)
    {
        _filePath = filePath;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        // Store enums as names like "HighPressure", not numbers.
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    // Append one alert; lock because two pipeline workers can alert at once.
    public void Append(AlertRecord record)
    {
        var line = JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine;

        lock (_gate)
        {
            File.AppendAllText(_filePath, line);
        }
    }

    // Load all saved alerts; missing file => empty; bad lines are skipped.
    public IReadOnlyList<AlertRecord> Load()
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<AlertRecord>();
        }

        var records = new List<AlertRecord>();

        foreach (var line in File.ReadAllLines(_filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var record = JsonSerializer.Deserialize<AlertRecord>(line, JsonOptions);
                if (record is not null && !string.IsNullOrWhiteSpace(record.DeviceId))
                {
                    records.Add(record);
                }
            }
            catch (JsonException)
            {
                // Skip corrupt/partial lines so startup can continue.
            }
        }

        return records;
    }

    // Narrow history by optional severity, then keep the last N entries.
    public static IReadOnlyList<AlertRecord> Filter(
        IEnumerable<AlertRecord> records,
        AlertSeverity? severity = null,
        int last = 10)
    {
        if (last <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(last), "last must be positive.");
        }

        IEnumerable<AlertRecord> query = records;

        if (severity is not null)
        {
            query = query.Where(record => record.Severity == severity);
        }

        return query.TakeLast(last).ToList();
    }
}
