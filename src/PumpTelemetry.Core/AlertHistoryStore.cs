using System.Text.Json;

namespace PumpTelemetry.Core;

public sealed class AlertHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public AlertHistoryStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Alert history file path is required.", nameof(filePath));
        }

        _filePath = filePath;
    }

    public async Task<IReadOnlyList<AlertRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await LoadWithoutLockAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(AlertRecord record, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var records = await LoadWithoutLockAsync(cancellationToken).ConfigureAwait(false);
            var updatedRecords = records.Concat(new[] { record }).ToArray();
            var directory = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, updatedRecords, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public static IReadOnlyList<AlertRecord> ApplyFilter(
        IEnumerable<AlertRecord> records,
        AlertHistoryFilter filter)
    {
        var query = records;

        if (!string.IsNullOrWhiteSpace(filter.AlertType))
        {
            query = query.Where(record =>
                string.Equals(record.AlertType, filter.AlertType, StringComparison.OrdinalIgnoreCase));
        }

        query = query.OrderByDescending(record => record.Timestamp);

        if (filter.Last is > 0)
        {
            query = query.Take(filter.Last.Value);
        }

        return query.ToArray();
    }

    private async Task<IReadOnlyList<AlertRecord>> LoadWithoutLockAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);

        if (stream.Length == 0)
        {
            return [];
        }

        var records = await JsonSerializer.DeserializeAsync<AlertRecord[]>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return records ?? [];
    }
}
