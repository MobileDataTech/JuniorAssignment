using PumpTelemetry.Core;
using System.Threading.Channels;

// parsing optional CLI filters
string? severity = null;
int? last = null;

foreach (var arg in args)
{
    if (arg.StartsWith("--severity="))
    {
        severity = arg.Substring("--severity=".Length);
    }
    else if (arg.StartsWith("--last="))
    {
        if (int.TryParse(arg.Substring("--last=".Length), out var parsed))
        {
            last = parsed;
        }
    }
}

// Load history saved by previous runs and print a filtered view on startup.
// Filters narrow what is shown only; every alert is still persisted (below).
var historyStore = new AlertHistoryStore("alert-history.jsonl");
var history = AlertHistoryStore.ApplyFilter(historyStore.Load(), severity, last);

Console.WriteLine($"=== Alert History ({history.Count} shown) ===");
foreach (var record in history)
{
    Console.WriteLine(
        $"[{record.Timestamp:yyyy-MM-dd HH:mm:ss}] {record.Type} {record.DeviceId}: {record.Message}");
}
Console.WriteLine("=== End of history ===");
Console.WriteLine();

var options = new TelemetryPipelineOptions
{
    PressureThreshold = 120,
    SilentTimeout = TimeSpan.FromSeconds(5),
    SilentCheckInterval = TimeSpan.FromSeconds(1)
};

await using var pipeline = new TelemetryPipeline(options, (alert, _) =>
{
    historyStore.Append(AlertRecord.FromAlert(alert)); // each alert is both printed and persisted
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
