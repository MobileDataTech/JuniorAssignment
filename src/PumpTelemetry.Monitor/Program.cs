using PumpTelemetry.Core;
using PumpTelemetry.Monitor;
using System.Text.Json;
using System.Threading.Channels;

// Validate options before reading history or starting the simulated pumps.
int limit;
try { limit = AlertHistoryDisplay.ParseLimit(args); }
catch (ArgumentException error)
{
    Console.Error.WriteLine(error.Message);
    Environment.ExitCode = 1;
    return;
}

// Reuse the history in the directory the user launched the app from.
var historyPath = Path.Combine(Environment.CurrentDirectory, "alert-history.json");
using var history = new AlertHistoryStore(historyPath);
try
{
    AlertHistoryDisplay.Write(Console.Out, await history.LoadAsync(), limit);
}
catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
{
    Console.Error.WriteLine($"Cannot load alert history at {historyPath}: {error.Message}");
    Environment.ExitCode = 1;
    return;
}
// History-only mode displays saved alerts without starting live monitoring.
if (args.Contains("--history")) return;

var options = new TelemetryPipelineOptions
{
    PressureThreshold = 120,
    SilentTimeout = TimeSpan.FromSeconds(5),
    SilentCheckInterval = TimeSpan.FromSeconds(1)
};

// The existing pipeline calls this sink whenever it detects an alert.
await using var pipeline = new TelemetryPipeline(options, async (alert, _) =>
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(
        $"[{alert.Timestamp:HH:mm:ss.fff}] {alert.Type} {alert.DeviceId}: {alert.Message}");
    Console.ResetColor();
    try
    {
        // Persist even while shutdown drains queued readings.
        await history.AppendAsync(AlertRecord.FromAlert(alert, options));
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
    {
        // Keep monitoring, but make the storage failure visible to users and scripts.
        Console.Error.WriteLine($"Could not save alert history: {error.Message}");
        Environment.ExitCode = 1;
    }
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
