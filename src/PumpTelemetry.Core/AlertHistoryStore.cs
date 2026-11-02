using System.Text.Json;
using System.Text.Json.Serialization;

namespace PumpTelemetry.Core;

// One store instance serializes writes from both pipeline alert producers.
public sealed class AlertHistoryStore(string path) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Store readable alert names, such as HighPressure, instead of enum numbers.
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<AlertRecord>> LoadAsync()
    {
        await _gate.WaitAsync();
        try { return await ReadAsync(); }
        finally { _gate.Release(); }
    }

    public async Task AppendAsync(AlertRecord record)
    {
        await _gate.WaitAsync();
        var temporaryPath = Path.GetFullPath(path) + ".tmp";
        try
        {
            // Keep the existing history and add this alert to the JSON array.
            var records = await ReadAsync();
            records.Add(record);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            // Finish writing the replacement before touching the current history file.
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(records, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<AlertRecord>> ReadAsync()
    {
        // The first run has no history file yet. Invalid existing JSON still raises an error.
        if (!File.Exists(path)) return new();
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<AlertRecord>>(json, JsonOptions)
            ?? throw new JsonException("Alert history must contain a JSON array.");
    }

    public void Dispose() => _gate.Dispose();
}

public sealed record AlertRecord(
    DateTimeOffset Timestamp,
    TelemetryAlertType Type,
    string DeviceId,
    double? Value,
    double? Threshold,
    string Unit,
    string Message)
{
    // Map each alert to its relevant value and threshold; null means unavailable.
    public static AlertRecord FromAlert(TelemetryAlert alert, TelemetryPipelineOptions options) =>
        new(alert.Timestamp, alert.Type, alert.DeviceId,
            alert.Type switch
            {
                TelemetryAlertType.HighPressure => alert.Reading?.Pressure,
                TelemetryAlertType.OutOfOrderSequence => alert.Reading?.SequenceNumber,
                _ => null
            },
            alert.Type switch
            {
                TelemetryAlertType.HighPressure => options.PressureThreshold,
                TelemetryAlertType.DeviceSilent => options.SilentTimeout.TotalSeconds,
                _ => null
            },
            alert.Type switch
            {
                TelemetryAlertType.HighPressure => "pressure",
                TelemetryAlertType.DeviceSilent => "seconds silent",
                _ => "sequence"
            }, alert.Message);
}
