using System.Text.Json;
using System.Text.Json.Serialization;

namespace PumpTelemetry.Core;

public sealed class AlertHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AlertHistoryStore(string filePath)
    {
        _filePath = filePath;
    }

    public async ValueTask AppendAsync(AlertRecord record, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine;

        // The alert sink can run on two background tasks at once, so writes take turns.
        // lock can't be held across an await, hence SemaphoreSlim.
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(_filePath, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Reads every saved alert, oldest first. A missing file means no history yet.
    /// Lines that can't be parsed are skipped so one bad write never blocks startup.
    /// </summary>
    public async ValueTask<IReadOnlyList<AlertRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<AlertRecord>();

        // Same lock as AppendAsync, so we never read a half-written line.
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return records;
            }

            var lines = await File.ReadAllLinesAsync(_filePath, cancellationToken).ConfigureAwait(false);

            foreach (var line in lines)
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
                    // Corrupt line (for example a crash mid-write): skip it and keep loading.
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }

        return records;
    }
}
