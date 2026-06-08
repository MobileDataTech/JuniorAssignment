using System.Text.Json;
using System.Text.Json.Serialization;

namespace PumpTelemetry.Core;

public sealed class AlertHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly List<AlertRecord> _records = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AlertHistoryStore(string filePath = "alert-history.json")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _filePath = filePath;
        Load();
    }

    public IReadOnlyList<AlertRecord> Records
    {
        get
        {
            lock (_records)
            {
                return _records.ToArray();
            }
        }
    }

    public async Task AppendAsync(AlertRecord record, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            _records.Add(record);

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                _filePath,
                JsonSerializer.Serialize(_records, JsonOptions),
                ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public IEnumerable<AlertRecord> Filter(
        AlertSeverity? severity = null,
        int? last = null,
        DateTimeOffset? since = null)
    {
        if (last is <= 0)
        {
            return Enumerable.Empty<AlertRecord>();
        }

        IEnumerable<AlertRecord> query = Records;

        if (severity is not null)
        {
            query = query.Where(record => record.Severity == severity);
        }

        if (since is not null)
        {
            query = query.Where(record => record.Timestamp >= since);
        }

        query = query.OrderBy(record => record.Timestamp);

        if (last is not null)
        {
            query = query.TakeLast(last.Value);
        }

        return query;
    }

    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var loaded = JsonSerializer.Deserialize<List<AlertRecord>>(json, JsonOptions);

            if (loaded is not null)
            {
                _records.AddRange(loaded);
            }
        }
        catch
        {
            // If the file is corrupt or unreadable, start with empty history
            // so the monitor can still run.
        }
    }
}