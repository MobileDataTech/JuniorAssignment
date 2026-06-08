using PumpTelemetry.Core;
using System.Threading.Channels;

var historyFilePath = GetOptionValue(args, "--history-file") ?? "alert-history.json";
var historyFilter = new AlertHistoryFilter(
    AlertType: GetOptionValue(args, "--type"),
    Last: GetIntOptionValue(args, "--last") ?? 10);
var historyOnly = HasOption(args, "--history-only");
var historyStore = new AlertHistoryStore(historyFilePath);

var options = new TelemetryPipelineOptions
{
    PressureThreshold = 120,
    SilentTimeout = TimeSpan.FromSeconds(5),
    SilentCheckInterval = TimeSpan.FromSeconds(1)
};

await DisplayAlertHistoryAsync(historyStore, historyFilter);

if (historyOnly)
{
    return;
}

await using var pipeline = new TelemetryPipeline(
    options,
    (alert, cancellationToken) => SaveAndPrintAlertAsync(
        alert,
        historyStore,
        options.PressureThreshold,
        cancellationToken));

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
Console.WriteLine($"Alert history file: {historyFilePath}");

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

static async Task DisplayAlertHistoryAsync(
    AlertHistoryStore historyStore,
    AlertHistoryFilter filter,
    CancellationToken cancellationToken = default)
{
    var savedAlerts = await historyStore.LoadAsync(cancellationToken).ConfigureAwait(false);
    var filteredAlerts = AlertHistoryStore.ApplyFilter(savedAlerts, filter);

    Console.WriteLine("Recent alert history:");

    if (filteredAlerts.Count == 0)
    {
        Console.WriteLine("  No saved alerts found.");
        Console.WriteLine();
        return;
    }

    foreach (var alert in filteredAlerts)
    {
        var valueText = alert.SensorValue.HasValue
            ? $" value={alert.SensorValue.Value:F1}"
            : string.Empty;
        var thresholdText = alert.Threshold.HasValue
            ? $" threshold={alert.Threshold.Value:F1}"
            : string.Empty;

        Console.WriteLine(
            $"  [{alert.Timestamp:yyyy-MM-dd HH:mm:ss}] {alert.AlertType} {alert.DeviceId}{valueText}{thresholdText}: {alert.Message}");
    }

    Console.WriteLine();
}

static async ValueTask SaveAndPrintAlertAsync(
    TelemetryAlert alert,
    AlertHistoryStore historyStore,
    double pressureThreshold,
    CancellationToken cancellationToken)
{
    var record = AlertRecord.FromAlert(alert, pressureThreshold);
    await historyStore.SaveAsync(record, cancellationToken).ConfigureAwait(false);

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(
        $"[{alert.Timestamp:HH:mm:ss.fff}] {alert.Type} {alert.DeviceId}: {alert.Message}");
    Console.ResetColor();
}

static bool HasOption(string[] args, string optionName)
{
    return args.Any(arg => string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase));
}

static string? GetOptionValue(string[] args, string optionName)
{
    var prefix = $"{optionName}=";
    var match = args.FirstOrDefault(arg => arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    return match is null ? null : match[prefix.Length..];
}

static int? GetIntOptionValue(string[] args, string optionName)
{
    var value = GetOptionValue(args, optionName);
    return int.TryParse(value, out var parsed) ? parsed : null;
}
