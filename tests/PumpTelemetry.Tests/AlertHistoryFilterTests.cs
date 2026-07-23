using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryFilterTests
{
    private static readonly DateTimeOffset BaseTimestamp =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

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
