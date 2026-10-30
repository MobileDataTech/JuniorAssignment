using PumpTelemetry.Core;
using System.Threading.Channels;

var options = new TelemetryPipelineOptions
{
    PressureThreshold = 120,
    SilentTimeout = TimeSpan.FromSeconds(5),
    SilentCheckInterval = TimeSpan.FromSeconds(1)
};

var historyStore = new AlertHistoryStore("alerts.jsonl");

var history = await historyStore.LoadAsync();

// Optional history filters: --severity=low|medium|high, --since=<date/time>, --until=<date/time>, --last=<count>.
// Any filter left out is not applied, so with no flags the whole history is shown.
AlertSeverity? severityFilter = null;
DateTimeOffset? since = null;
DateTimeOffset? until = null;
int? last = null;

foreach (var arg in args)
{
    if (arg.StartsWith("--severity=", StringComparison.OrdinalIgnoreCase))
    {
        var severityStr = arg.Substring("--severity=".Length);
        if (Enum.TryParse<AlertSeverity>(severityStr, ignoreCase: true, out var severityParsed) && Enum.IsDefined(severityParsed))
        {
            severityFilter = severityParsed;
        }
        else
        {
            Console.WriteLine($"Invalid severity filter: {severityStr}");
        }
    }

    if (arg.StartsWith("--last=", StringComparison.OrdinalIgnoreCase))
    {
        var lastStr = arg.Substring("--last=".Length);
        if (int.TryParse(lastStr, out var lastParsed) && lastParsed > 0)
        {
            last = lastParsed;
        }
        else
        {
            Console.WriteLine($"Invalid last filter: {lastStr}");
        }
    }

    if (arg.StartsWith("--since=", StringComparison.OrdinalIgnoreCase))
    {
        var sinceStr = arg.Substring("--since=".Length);
        if (TryParseTime(sinceStr, out var sinceParsed))
        {
            since = sinceParsed;
        }
        else
        {
            Console.WriteLine($"Invalid since filter: {sinceStr} (try 2026-10-30 or \"2026-10-30 14:30\")");
        }
    }

    if (arg.StartsWith("--until=", StringComparison.OrdinalIgnoreCase))
    {
        var untilStr = arg.Substring("--until=".Length);
        if (TryParseTime(untilStr, out var untilParsed))
        {
            until = untilParsed;
        }
        else
        {
            Console.WriteLine($"Invalid until filter: {untilStr} (try 2026-10-30 or \"2026-10-30 14:30\")");
        }
    }
}

if (since.HasValue && until.HasValue && since > until)
{
    Console.WriteLine("Note: --since is after --until, so no alerts can match.");
}

var toShow = AlertHistoryFilter.ApplyFilters(history, severityFilter, last, since, until);

var filterParts = new List<string>
{
    severityFilter.HasValue ? $"{severityFilter.Value} severity" : "all severities"
};
if (since.HasValue)
{
    filterParts.Add($"since {since.Value.ToUniversalTime():yyyy-MM-dd HH:mm}");
}
if (until.HasValue)
{
    filterParts.Add($"until {until.Value.ToUniversalTime():yyyy-MM-dd HH:mm}");
}
filterParts.Add(last.HasValue ? $"last {last}" : "no limit");
var filterDescription = string.Join(", ", filterParts);

Console.WriteLine($"Loaded {history.Count} saved alert(s). Showing {toShow.Count} ({filterDescription}):");
foreach (var past in toShow)
{
    Console.WriteLine(
        $"  [{past.Timestamp:yyyy-MM-dd HH:mm:ss}] {past.Severity} {past.Type} {past.DeviceId}: {past.Message}");
}
Console.WriteLine();

await using var pipeline = new TelemetryPipeline(options, async (alert, ct) =>
{
    var record = AlertRecord.CreateAlertRecord(alert);

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(
        $"[{record.Timestamp:HH:mm:ss.fff}] {record.Severity} {record.Type} {record.DeviceId}: {record.Message}");
    Console.ResetColor();

    try
    {
        await historyStore.AppendAsync(record, ct).ConfigureAwait(false);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        // A failed save must not stop alert processing.
        Console.Error.WriteLine($"Could not save alert: {ex.Message}");
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

// Times typed without a zone are read as UTC, the same clock the alerts are saved and printed in.
// A date with no time means 00:00 UTC that day.
static bool TryParseTime(string text, out DateTimeOffset result)
{
    return DateTimeOffset.TryParse(
        text,
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AssumeUniversal,
        out result);
}