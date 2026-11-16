using PumpTelemetry.Core;
using System.Threading.Channels;

// --- Alert history CLI: --severity=low|medium|high, --last=N ---
AlertSeverity? severityFilter = null;
var last = 10;

foreach (var arg in args)
{
    if (arg.StartsWith("--severity=", StringComparison.OrdinalIgnoreCase))
    {
        var value = arg["--severity=".Length..];
        if (!Enum.TryParse<AlertSeverity>(value, ignoreCase: true, out var parsed))
        {
            Console.Error.WriteLine("Invalid --severity. Use low, medium, or high.");
            return;
        }

        severityFilter = parsed;
    }
    else if (arg.StartsWith("--last=", StringComparison.OrdinalIgnoreCase))
    {
        var value = arg["--last=".Length..];
        if (!int.TryParse(value, out last) || last <= 0)
        {
            Console.Error.WriteLine("Invalid --last. Use a positive integer.");
            return;
        }
    }
    else
    {
        Console.Error.WriteLine($"Unknown argument: {arg}");
        return;
    }
}

// --- Load and display recent alert history before monitoring starts ---
var historyStore = new AlertHistoryStore("alert-history.jsonl");
var history = historyStore.Load();
var shown = AlertHistoryStore.Filter(history, severityFilter, last);

Console.WriteLine($"Alert history: {shown.Count} shown ({history.Count} saved in total)");
foreach (var record in shown)
{
    var pressure = record.Pressure is null ? "" : $" pressure={record.Pressure:F1}";
    var sequence = record.SequenceNumber is null ? "" : $" sequence={record.SequenceNumber}";
    var threshold = record.PressureThreshold is null ? "" : $" threshold={record.PressureThreshold:F1}";
    var silent = record.SilentTimeoutSeconds is null ? "" : $" silentTimeout={record.SilentTimeoutSeconds}s";

    Console.WriteLine(
        $"  [{record.Timestamp:yyyy-MM-dd HH:mm:ss}] {record.Severity} {record.Type} {record.DeviceId}{pressure}{sequence}{threshold}{silent} | {record.Message}");
}

Console.WriteLine();

var options = new TelemetryPipelineOptions
{
    PressureThreshold = 120,
    SilentTimeout = TimeSpan.FromSeconds(5),
    SilentCheckInterval = TimeSpan.FromSeconds(1)
};

// Persist each alert, then print live console output (save failure does not stop monitoring).
await using var pipeline = new TelemetryPipeline(options, (alert, _) =>
{
    try
    {
        var record = AlertRecord.FromAlert(alert, options);
        historyStore.Append(record);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"Could not save alert history: {ex.Message}");
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(
        $"[{alert.Timestamp:HH:mm:ss.fff}] {alert.Type} {alert.DeviceId}: {alert.Message}");
    Console.ResetColor();
    return ValueTask.CompletedTask;
});

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    shutdown.Cancel();
};

pipeline.Start(shutdown.Token);

var devices = Enumerable.Range(1, 5)
    .Select(i => new DeviceSimulator($"pump-{i:00}", seed: i * 17))
    .ToArray();

var deviceTasks = devices
    .Select(device => Task.Run(
        () => device.RunAsync((reading, ct) => new ValueTask(pipeline.SubmitAsync(reading, ct)), shutdown.Token),
        CancellationToken.None))
    .ToArray();

Console.WriteLine("Pump telemetry monitor running. Saving alerts to alert-history.jsonl. Press Ctrl+C to stop.");

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
