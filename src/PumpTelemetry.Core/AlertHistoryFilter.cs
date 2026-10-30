namespace PumpTelemetry.Core;

public static class AlertHistoryFilter
{
    public static IReadOnlyList<AlertRecord> ApplyFilters(
        IEnumerable<AlertRecord> history,
        AlertSeverity? severityFilter,
        int? last,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null)
    {
        if (last.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(last.Value);
        }

        var filtered = history;

        if (severityFilter.HasValue)
        {
            filtered = filtered.Where(a => a.Severity == severityFilter.Value);
        }

        if (since.HasValue)
        {
            filtered = filtered.Where(a => a.Timestamp >= since.Value);
        }

        if (until.HasValue)
        {
            filtered = filtered.Where(a => a.Timestamp <= until.Value);
        }

        if (last.HasValue)
        {
            filtered = filtered.TakeLast(last.Value);
        }

        return filtered.ToList();
    }
}
