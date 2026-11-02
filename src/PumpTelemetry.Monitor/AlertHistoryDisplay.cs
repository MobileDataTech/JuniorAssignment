using PumpTelemetry.Core;

namespace PumpTelemetry.Monitor;

public static class AlertHistoryDisplay
{
    public static int ParseLimit(string[] args)
    {
        // Accept --history and --last=N; reject typos instead of silently ignoring them.
        var limit = 10;
        foreach (var arg in args)
        {
            if (arg == "--history") continue;
            if (!arg.StartsWith("--last=", StringComparison.Ordinal) ||
                !int.TryParse(arg[7..], out limit) || limit <= 0)
                throw new ArgumentException("Usage: [--history] [--last=<positive integer>]");
        }
        return limit;
    }

    public static void Write(TextWriter output, IEnumerable<AlertRecord> records, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        // Select by timestamp because file order may differ from when alerts occurred.
        var recent = records.OrderByDescending(record => record.Timestamp).Take(limit).ToArray();
        output.WriteLine($"Alert history (latest {limit}, newest first):");
        if (recent.Length == 0) output.WriteLine("No saved alerts.");
        foreach (var record in recent)
            output.WriteLine($"[{record.Timestamp:O}] {record.Type} {record.DeviceId}: " +
                $"value={record.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"}, " +
                $"threshold={record.Threshold?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "n/a"} " +
                $"({record.Unit}). {record.Message}");
    }
}
