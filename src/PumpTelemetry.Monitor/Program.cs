using PumpTelemetry.Core;
using System.Threading.Channels;

var severityArg = GetArg(args, "--severity");
var lastArg = GetArg(args, "--last");
var sinceArg = GetArg(args, "--since-hours");

if (!TryParseOptions(severityArg, lastArg, sinceArg, out var severityFilter, out var lastN, out var since))
{
    return;
}

var history = new AlertHistoryStore("alert-history.json");
DisplayHistory(history, severityFilter, lastN, since);

var options = new TelemetryPipelineOptions
{
    PressureThreshold = 120,
    SilentTimeout = TimeSpan.FromSeconds(5),
    SilentCheckInterval = TimeSpan.FromSeconds(1)
};

await using var pipeline = new TelemetryPipeline(options, async (alert, ct) =>
{
    var record = new AlertRecord(
        Timestamp: alert.Timestamp,
        DeviceId: alert.DeviceId,
        AlertType: alert.Type,
        Severity: GetSeverity(alert.Type),
        Message: alert.Message,
        SensorValue: GetSensorValue(alert));

    await history.AppendAsync(record, ct);

    Console.ForegroundColor = GetConsoleColor(record.Severity);
    Console.WriteLine(FormatAlert(record));
    Console.ResetColor();
});

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

pipeline.Start(shutdown.Token);

var devices = Enumerable.Range(1, 5)
    .Select(i => new DeviceSimulator($"pump-{i:00}", seed: i * 17))
    .ToArray();

var deviceTasks = devices
    .Select(device => Task.Run(
        () => device.RunAsync(
            (reading, ct) => new ValueTask(pipeline.SubmitAsync(reading, ct)),
            shutdown.Token),
        CancellationToken.None))
    .ToArray();

Console.WriteLine("Pump telemetry monitor running. Press Ctrl+C to stop.");

try
{
    await Task.WhenAll(deviceTasks).ConfigureAwait(false);
}
catch (OperationCanceledException)
{
}
catch (ChannelClosedException)
{
}
finally
{
    await pipeline.StopAsync().ConfigureAwait(false);
}

static string? GetArg(string[] args, string name)
{
    var prefix = name + "=";
    var match = Array.Find(args, arg =>
        arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    return match is null ? null : match[prefix.Length..];
}

static bool TryParseOptions(
    string? severityArg,
    string? lastArg,
    string? sinceArg,
    out AlertSeverity? severity,
    out int? last,
    out DateTimeOffset? since)
{
    severity = null;
    last = null;
    since = null;

    if (severityArg is not null)
    {
        if (!Enum.TryParse<AlertSeverity>(
                severityArg,
                ignoreCase: true,
                out var parsedSeverity))
        {
            Console.WriteLine("Invalid --severity value. Use Low, Medium, or High.");
            return false;
        }

        severity = parsedSeverity;
    }

    if (lastArg is not null)
    {
        if (!int.TryParse(lastArg, out var parsedLast) || parsedLast <= 0)
        {
            Console.WriteLine("Invalid --last value. Use a positive whole number.");
            return false;
        }

        last = parsedLast;
    }

    if (sinceArg is not null)
    {
        if (!int.TryParse(sinceArg, out var parsedHours) || parsedHours <= 0)
        {
            Console.WriteLine("Invalid --since-hours value. Use a positive whole number.");
            return false;
        }

        since = DateTimeOffset.UtcNow.AddHours(-parsedHours);
    }

    return true;
}

static void DisplayHistory(
    AlertHistoryStore store,
    AlertSeverity? severity,
    int? last,
    DateTimeOffset? since)
{
    var records = store.Filter(severity, last, since).ToList();

    if (records.Count == 0)
    {
        Console.WriteLine("No saved alert history found for the selected filters.");
        Console.WriteLine();
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"=== Alert History ({records.Count} record{(records.Count == 1 ? "" : "s")}) ===");

    foreach (var record in records)
    {
        Console.ForegroundColor = GetConsoleColor(record.Severity);
        Console.WriteLine("  " + FormatAlert(record));
    }

    Console.ResetColor();
    Console.WriteLine(new string('=', 60));
    Console.WriteLine();
}

static AlertSeverity GetSeverity(TelemetryAlertType type)
{
    return type switch
    {
        TelemetryAlertType.HighPressure => AlertSeverity.High,
        TelemetryAlertType.DeviceSilent => AlertSeverity.High,
        TelemetryAlertType.OutOfOrderSequence => AlertSeverity.Medium,
        _ => AlertSeverity.Low
    };
}

static double? GetSensorValue(TelemetryAlert alert)
{
    return alert.Type switch
    {
        TelemetryAlertType.HighPressure => alert.Reading?.Pressure,
        TelemetryAlertType.OutOfOrderSequence => alert.Reading?.SequenceNumber,
        _ => null
    };
}

static ConsoleColor GetConsoleColor(AlertSeverity severity)
{
    return severity switch
    {
        AlertSeverity.High => ConsoleColor.Red,
        AlertSeverity.Medium => ConsoleColor.Yellow,
        _ => ConsoleColor.Gray
    };
}

static string FormatAlert(AlertRecord record)
{
    var sensorText = record.SensorValue is not null
        ? $" | value={record.SensorValue:F1}"
        : string.Empty;

    return $"[{record.Timestamp:yyyy-MM-dd HH:mm:ss}] [{record.Severity,-6}] {record.DeviceId,-10} {record.AlertType,-22} {record.Message}{sensorText}";
}