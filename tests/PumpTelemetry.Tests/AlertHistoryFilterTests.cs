using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryFilterTests
{
    [Fact]
    public void ApplyFilters_WithNoSeverityAndNoLast_ReturnsEverything()
    {
        var history = new[]
        {
            Record("a", AlertSeverity.High),
            Record("b", AlertSeverity.Medium),
            Record("c", AlertSeverity.Low)
        };

        var result = AlertHistoryFilter.ApplyFilters(history, severityFilter: null, last: null);

        Assert.Equal(new[] { "a", "b", "c" }, result.Select(r => r.DeviceId));
    }

    [Fact]
    public void ApplyFilters_WithOnlyLast_ReturnsTheMostRecentOnesInOrder()
    {
        var history = new[] { Record("a"), Record("b"), Record("c"), Record("d"), Record("e") };

        var result = AlertHistoryFilter.ApplyFilters(history, severityFilter: null, last: 2);

        Assert.Equal(new[] { "d", "e" }, result.Select(r => r.DeviceId));
    }

    [Fact]
    public void ApplyFilters_WithOnlySeverityAndNoLast_ReturnsAllOfThatSeverity()
    {
        var history = new[]
        {
            Record("a", AlertSeverity.High),
            Record("b", AlertSeverity.Low),
            Record("c", AlertSeverity.High),
            Record("d", AlertSeverity.Medium)
        };

        var result = AlertHistoryFilter.ApplyFilters(history, AlertSeverity.High, last: null);

        Assert.Equal(new[] { "a", "c" }, result.Select(r => r.DeviceId));
    }

    [Fact]
    public void ApplyFilters_FiltersSeverityBeforeTakingLast()
    {
        // The last 2 alerts overall are Low, but the last 2 *High* alerts are a and b.
        var history = new[]
        {
            Record("a", AlertSeverity.High),
            Record("b", AlertSeverity.High),
            Record("c", AlertSeverity.Low),
            Record("d", AlertSeverity.Low)
        };

        var result = AlertHistoryFilter.ApplyFilters(history, AlertSeverity.High, last: 2);

        Assert.Equal(new[] { "a", "b" }, result.Select(r => r.DeviceId));
    }

    [Fact]
    public void ApplyFilters_WhenLastExceedsHistory_ReturnsEverything()
    {
        var history = new[] { Record("a"), Record("b") };

        var result = AlertHistoryFilter.ApplyFilters(history, severityFilter: null, last: 50);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ApplyFilters_WhenNothingMatchesSeverity_ReturnsEmpty()
    {
        var history = new[] { Record("a", AlertSeverity.Low) };

        var result = AlertHistoryFilter.ApplyFilters(history, AlertSeverity.High, last: 10);

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyFilters_WithEmptyHistory_ReturnsEmpty()
    {
        var result = AlertHistoryFilter.ApplyFilters(Array.Empty<AlertRecord>(), AlertSeverity.High, last: 5);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplyFilters_WhenLastIsNotPositive_Throws(int last)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AlertHistoryFilter.ApplyFilters(Array.Empty<AlertRecord>(), severityFilter: null, last: last));
    }

    [Fact]
    public void ApplyFilters_WithSince_KeepsAlertsAtOrAfterThatTime()
    {
        var history = new[]
        {
            Record("a", at: Hour(1)),
            Record("b", at: Hour(2)),
            Record("c", at: Hour(3))
        };

        var result = AlertHistoryFilter.ApplyFilters(history, null, null, since: Hour(2));

        Assert.Equal(new[] { "b", "c" }, result.Select(r => r.DeviceId));
    }

    [Fact]
    public void ApplyFilters_WithUntil_KeepsAlertsAtOrBeforeThatTime()
    {
        var history = new[]
        {
            Record("a", at: Hour(1)),
            Record("b", at: Hour(2)),
            Record("c", at: Hour(3))
        };

        var result = AlertHistoryFilter.ApplyFilters(history, null, null, until: Hour(2));

        Assert.Equal(new[] { "a", "b" }, result.Select(r => r.DeviceId));
    }

    [Fact]
    public void ApplyFilters_WithSinceAndUntil_KeepsOnlyAlertsInsideTheRange()
    {
        var history = new[]
        {
            Record("a", at: Hour(1)),
            Record("b", at: Hour(2)),
            Record("c", at: Hour(3)),
            Record("d", at: Hour(4))
        };

        var result = AlertHistoryFilter.ApplyFilters(history, null, null, since: Hour(2), until: Hour(3));

        Assert.Equal(new[] { "b", "c" }, result.Select(r => r.DeviceId));
    }

    [Fact]
    public void ApplyFilters_WhenSinceIsAfterUntil_ReturnsEmpty()
    {
        var history = new[] { Record("a", at: Hour(2)) };

        var result = AlertHistoryFilter.ApplyFilters(history, null, null, since: Hour(3), until: Hour(1));

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyFilters_CombinesSeverityTimeRangeAndLast()
    {
        var history = new[]
        {
            Record("a", AlertSeverity.High, Hour(1)),   // too early
            Record("b", AlertSeverity.High, Hour(2)),
            Record("c", AlertSeverity.Low, Hour(3)),    // wrong severity
            Record("d", AlertSeverity.High, Hour(4)),
            Record("e", AlertSeverity.High, Hour(5)),
            Record("f", AlertSeverity.High, Hour(9))    // too late
        };

        var result = AlertHistoryFilter.ApplyFilters(history, AlertSeverity.High, last: 2, since: Hour(2), until: Hour(6));

        Assert.Equal(new[] { "d", "e" }, result.Select(r => r.DeviceId));
    }

    private static readonly DateTimeOffset Midnight = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Hour(int hour) => Midnight.AddHours(hour);

    private static AlertRecord Record(
        string deviceId,
        AlertSeverity severity = AlertSeverity.Low,
        DateTimeOffset? at = null)
    {
        return new AlertRecord(
            TelemetryAlertType.OutOfOrderSequence,
            deviceId,
            at ?? DateTimeOffset.UtcNow,
            "message",
            null,
            severity);
    }
}
