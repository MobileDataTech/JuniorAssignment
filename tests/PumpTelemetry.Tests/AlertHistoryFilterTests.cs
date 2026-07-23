using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryFilterTests
{
    private static readonly DateTimeOffset BaseTimestamp =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetByTimeRange_ReturnsAlertsWithinInclusiveBounds()
    {
        var before = Alert("pump-01", BaseTimestamp.AddMinutes(-1));
        var atStart = Alert("pump-02", BaseTimestamp);
        var inside = Alert("pump-03", BaseTimestamp.AddMinutes(1));
        var atEnd = Alert("pump-04", BaseTimestamp.AddMinutes(2));
        var after = Alert("pump-05", BaseTimestamp.AddMinutes(3));

        var result = AlertHistoryFilter.GetByTimeRange(
            new[] { before, atStart, inside, atEnd, after },
            BaseTimestamp,
            BaseTimestamp.AddMinutes(2));

        Assert.Equal(new[] { atStart, inside, atEnd }, result);
    }

    [Fact]
    public void GetByTimeRange_WhenStartIsMissing_AppliesOnlyEndBound()
    {
        var beforeEnd = Alert("pump-01", BaseTimestamp);
        var afterEnd = Alert("pump-02", BaseTimestamp.AddMinutes(2));

        var result = AlertHistoryFilter.GetByTimeRange(
            new[] { beforeEnd, afterEnd },
            from: null,
            to: BaseTimestamp.AddMinutes(1));

        Assert.Equal(new[] { beforeEnd }, result);
    }

    [Fact]
    public void GetByTimeRange_WhenEndIsMissing_AppliesOnlyStartBound()
    {
        var beforeStart = Alert("pump-01", BaseTimestamp);
        var afterStart = Alert("pump-02", BaseTimestamp.AddMinutes(2));

        var result = AlertHistoryFilter.GetByTimeRange(
            new[] { beforeStart, afterStart },
            from: BaseTimestamp.AddMinutes(1),
            to: null);

        Assert.Equal(new[] { afterStart }, result);
    }

    [Fact]
    public void GetByTimeRange_WhenStartIsAfterEnd_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => AlertHistoryFilter.GetByTimeRange(
                Array.Empty<TelemetryAlert>(),
                BaseTimestamp.AddMinutes(1),
                BaseTimestamp));
    }

    [Fact]
    public void GetBySeverity_ReturnsOnlyAlertsWithRequestedSeverity()
    {
        var criticalPressure = Alert(
            "pump-01",
            BaseTimestamp,
            TelemetryAlertType.HighPressure);
        var warningSequence = Alert(
            "pump-02",
            BaseTimestamp.AddMinutes(1),
            TelemetryAlertType.OutOfOrderSequence);
        var criticalSilent = Alert(
            "pump-03",
            BaseTimestamp.AddMinutes(2),
            TelemetryAlertType.DeviceSilent);

        var result = AlertHistoryFilter.GetBySeverity(
            new[] { criticalPressure, warningSequence, criticalSilent },
            TelemetryAlertSeverity.Critical);

        Assert.Equal(new[] { criticalPressure, criticalSilent }, result);
    }

    [Fact]
    public void GetRecent_OrdersAlertsNewestFirst()
    {
        var oldest = Alert("pump-01", BaseTimestamp);
        var newest = Alert("pump-03", BaseTimestamp.AddMinutes(2));
        var middle = Alert("pump-02", BaseTimestamp.AddMinutes(1));

        var result = AlertHistoryFilter.GetRecent(new[] { oldest, newest, middle }, 3);

        Assert.Equal(new[] { newest, middle, oldest }, result);
    }

    [Fact]
    public void GetRecent_LimitsResultsToRequestedCount()
    {
        var alerts = new[]
        {
            Alert("pump-01", BaseTimestamp),
            Alert("pump-02", BaseTimestamp.AddMinutes(1)),
            Alert("pump-03", BaseTimestamp.AddMinutes(2))
        };

        var result = AlertHistoryFilter.GetRecent(alerts, 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { alerts[2], alerts[1] }, result);
    }

    [Fact]
    public void GetRecent_WhenCountExceedsHistory_ReturnsAllAlerts()
    {
        var alerts = new[]
        {
            Alert("pump-01", BaseTimestamp),
            Alert("pump-02", BaseTimestamp.AddMinutes(1))
        };

        var result = AlertHistoryFilter.GetRecent(alerts, 10);

        Assert.Equal(new[] { alerts[1], alerts[0] }, result);
    }

    [Fact]
    public void GetRecent_WhenHistoryIsEmpty_ReturnsEmptyList()
    {
        var result = AlertHistoryFilter.GetRecent(Array.Empty<TelemetryAlert>(), 10);

        Assert.Empty(result);
    }

    [Fact]
    public void GetRecent_WhenCountIsZero_ReturnsEmptyList()
    {
        var result = AlertHistoryFilter.GetRecent(
            new[] { Alert("pump-01", BaseTimestamp) },
            0);

        Assert.Empty(result);
    }

    [Fact]
    public void GetRecent_WhenCountIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AlertHistoryFilter.GetRecent(Array.Empty<TelemetryAlert>(), -1));
    }

    private static TelemetryAlert Alert(
        string deviceId,
        DateTimeOffset timestamp,
        TelemetryAlertType type = TelemetryAlertType.HighPressure)
    {
        return new TelemetryAlert(
            type,
            deviceId,
            timestamp,
            "Test alert.");
    }
}
