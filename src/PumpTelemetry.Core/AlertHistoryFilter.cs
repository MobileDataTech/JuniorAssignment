namespace PumpTelemetry.Core;

/// <summary>
/// Provides pure operations for selecting alerts from saved history.
/// </summary>
public static class AlertHistoryFilter
{
    /// <summary>
    /// Selects alerts whose timestamps fall within an inclusive time range.
    /// </summary>
    /// <param name="alerts">The alerts to filter.</param>
    /// <param name="from">
    /// The optional earliest timestamp to include, or <see langword="null"/> for no lower bound.
    /// </param>
    /// <param name="to">
    /// The optional latest timestamp to include, or <see langword="null"/> for no upper bound.
    /// </param>
    /// <returns>A read-only list containing alerts within the requested time range.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="alerts"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="from"/> is later than <paramref name="to"/>.
    /// </exception>
    public static IReadOnlyList<TelemetryAlert> GetByTimeRange(
        IEnumerable<TelemetryAlert> alerts,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        ArgumentNullException.ThrowIfNull(alerts);

        if (from > to)
        {
            throw new ArgumentException("The start of the time range cannot be later than the end.");
        }

        return alerts
            .Where(alert => !from.HasValue || alert.Timestamp >= from.Value)
            .Where(alert => !to.HasValue || alert.Timestamp <= to.Value)
            .ToArray();
    }

    /// <summary>
    /// Selects alerts that have the requested severity.
    /// </summary>
    /// <param name="alerts">The alerts to filter.</param>
    /// <param name="severity">The severity that returned alerts must have.</param>
    /// <returns>A read-only list containing only alerts with the requested severity.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="alerts"/> is <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<TelemetryAlert> GetBySeverity(
        IEnumerable<TelemetryAlert> alerts,
        TelemetryAlertSeverity severity)
    {
        ArgumentNullException.ThrowIfNull(alerts);

        return alerts
            .Where(alert => alert.Severity == severity)
            .ToArray();
    }

    /// <summary>
    /// Selects up to the requested number of alerts, ordered from newest to oldest.
    /// </summary>
    /// <param name="alerts">The alerts to filter.</param>
    /// <param name="count">
    /// The maximum number of alerts to return. A value of zero returns an empty list.
    /// </param>
    /// <returns>
    /// A read-only list containing at most <paramref name="count"/> alerts in descending
    /// timestamp order.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="alerts"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="count"/> is negative.
    /// </exception>
    public static IReadOnlyList<TelemetryAlert> GetRecent(
        IEnumerable<TelemetryAlert> alerts,
        int count)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        return alerts
            .OrderByDescending(alert => alert.Timestamp)
            .Take(count)
            .ToArray();
    }
}
