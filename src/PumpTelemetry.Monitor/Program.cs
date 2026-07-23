using PumpTelemetry.Core;
using PumpTelemetry.Monitor;
using System.Threading.Channels;

// The monitor is the composition root: it loads saved history, configures the telemetry
// pipeline, starts the simulated devices, and coordinates graceful shutdown.
const int defaultHistoryCount = 10;

// Validate command-line input before opening history or starting background work.
if (!HistoryOptionsParser.TryParse(args, defaultHistoryCount, out var historySelection))
{
    Console.Error.WriteLine(
        "Invalid arguments. Usage: PumpTelemetry.Monitor [--last=N | --all | --severity=Warning|Critical | --from=TIMESTAMP [--to=TIMESTAMP] | --to=TIMESTAMP], where N is zero or greater and timestamps use ISO 8601.");
    return;
}

// Keep history in a predictable location relative to where the monitor is launched.
var historyFilePath = Path.Combine(Environment.CurrentDirectory, "alerts.json");
IAlertHistoryStore historyStore = new JsonAlertHistoryStore(historyFilePath);
var savedAlerts = await historyStore.LoadAsync().ConfigureAwait(false);
var severityAlerts = historySelection.Severity.HasValue
    ? AlertHistoryFilter.GetBySeverity(savedAlerts, historySelection.Severity.Value)
    : savedAlerts;
var rangeAlerts = historySelection.From.HasValue || historySelection.To.HasValue
    ? AlertHistoryFilter.GetByTimeRange(
        severityAlerts,
        historySelection.From,
        historySelection.To)
    : severityAlerts;
var displayedAlerts = AlertHistoryFilter.GetRecent(
    rangeAlerts,
    historySelection.ShowAll
        || historySelection.Severity.HasValue
        || historySelection.From.HasValue
        || historySelection.To.HasValue
        ? rangeAlerts.Count
        : historySelection.Count);

// Display saved alerts before live telemetry output begins.
Console.WriteLine(
    GetHistoryHeading(historySelection));

if (savedAlerts.Count == 0)
{
    Console.WriteLine("No saved alert history found.");
}
else if (displayedAlerts.Count == 0)
{
    Console.WriteLine("No alerts selected.");
}
else
{
    foreach (var alert in displayedAlerts)
    {
        Console.WriteLine(AlertHistoryFormatter.Format(alert));
    }
}

// Detection thresholds and polling intervals are configured at the application boundary.
var options = new TelemetryPipelineOptions
{
    PressureThreshold = 120,
    SilentTimeout = TimeSpan.FromSeconds(5),
    SilentCheckInterval = TimeSpan.FromSeconds(1)
};

// The single injected sink persists and displays every alert type emitted by the pipeline.
await using var pipeline = new TelemetryPipeline(options, async (alert, cancellationToken) =>
{
    try
    {
        await historyStore.SaveAsync(alert, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        // Persistence failures are visible, but must not interrupt live telemetry monitoring.
        Console.Error.WriteLine($"Failed to save alert history: {exception.Message}");
    }

    try
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        // Use the same display format for live alerts and saved alert history.
        Console.WriteLine(AlertHistoryFormatter.Format(alert));
    }
    finally
    {
        Console.ResetColor();
    }
});

// Ctrl+C requests cooperative cancellation so device tasks and the pipeline can stop cleanly.
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    shutdown.Cancel();
};

pipeline.Start(shutdown.Token);

// Fixed seeds make the five simulated pump streams reproducible between runs.
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
// Cancellation and channel completion are both expected during graceful shutdown.
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

// Describe the selected startup history view without duplicating filtering behavior.
static string GetHistoryHeading(HistorySelection selection)
{
    if (selection.Severity.HasValue)
    {
        return $"Alert history with severity {selection.Severity}:";
    }

    if (selection.From.HasValue || selection.To.HasValue)
    {
        var from = selection.From?.ToString("O") ?? "the beginning";
        var to = selection.To?.ToString("O") ?? "now";
        return $"Alert history from {from} to {to}:";
    }

    return selection.ShowAll
        ? "Saved alert history:"
        : $"Recent alert history (up to {selection.Count}):";
}
