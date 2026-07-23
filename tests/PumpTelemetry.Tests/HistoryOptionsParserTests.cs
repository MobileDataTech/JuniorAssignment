using PumpTelemetry.Core;
using PumpTelemetry.Monitor;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class HistoryOptionsParserTests
{
    private const int DefaultCount = 10;
    private static readonly DateTimeOffset BaseTimestamp =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TryParse_WhenNoArguments_SelectsDefaultCount()
    {
        var success = HistoryOptionsParser.TryParse(
            Array.Empty<string>(),
            DefaultCount,
            out var selection);

        Assert.True(success);
        Assert.False(selection.ShowAll);
        Assert.Equal(DefaultCount, selection.Count);
        Assert.Null(selection.Severity);
    }

    [Fact]
    public void TryParse_WhenLastIsValid_SelectsRequestedCount()
    {
        var success = HistoryOptionsParser.TryParse(
            new[] { "--last=20" },
            DefaultCount,
            out var selection);

        Assert.True(success);
        Assert.False(selection.ShowAll);
        Assert.Equal(20, selection.Count);
        Assert.Null(selection.Severity);
    }

    [Fact]
    public void TryParse_WhenAllIsSpecified_SelectsAllHistory()
    {
        var success = HistoryOptionsParser.TryParse(
            new[] { "--all" },
            DefaultCount,
            out var selection);

        Assert.True(success);
        Assert.True(selection.ShowAll);
        Assert.Null(selection.Severity);
    }

    [Theory]
    [InlineData("--severity=Warning", TelemetryAlertSeverity.Warning)]
    [InlineData("--severity=critical", TelemetryAlertSeverity.Critical)]
    public void TryParse_WhenSeverityIsValid_SelectsRequestedSeverity(
        string argument,
        TelemetryAlertSeverity expectedSeverity)
    {
        var success = HistoryOptionsParser.TryParse(
            new[] { argument },
            DefaultCount,
            out var selection);

        Assert.True(success);
        Assert.False(selection.ShowAll);
        Assert.Equal(expectedSeverity, selection.Severity);
    }

    [Theory]
    [InlineData("--last=-1")]
    [InlineData("--last=not-a-number")]
    [InlineData("--severity=")]
    [InlineData("--severity=Unknown")]
    [InlineData("--severity=1")]
    public void TryParse_WhenOptionIsInvalid_ReturnsFalse(string argument)
    {
        var success = HistoryOptionsParser.TryParse(
            new[] { argument },
            DefaultCount,
            out _);

        Assert.False(success);
    }

    [Fact]
    public void TryParse_WhenArgumentIsUnknown_ReturnsFalse()
    {
        var success = HistoryOptionsParser.TryParse(
            new[] { "--unknown" },
            DefaultCount,
            out _);

        Assert.False(success);
    }

    [Fact]
    public void TryParse_WhenMultipleOptionsAreSpecified_ReturnsFalse()
    {
        var success = HistoryOptionsParser.TryParse(
            new[] { "--all", "--last=20" },
            DefaultCount,
            out _);

        Assert.False(success);
    }

    [Fact]
    public void AllSelection_ReturnsEverySavedAlertNewestFirst()
    {
        var alerts = new[]
        {
            Alert("pump-01", BaseTimestamp),
            Alert("pump-02", BaseTimestamp.AddMinutes(1)),
            Alert("pump-03", BaseTimestamp.AddMinutes(2))
        };
        HistoryOptionsParser.TryParse(new[] { "--all" }, DefaultCount, out var selection);

        var result = AlertHistoryFilter.GetRecent(
            alerts,
            selection.ShowAll ? alerts.Length : selection.Count);

        Assert.Equal(new[] { alerts[2], alerts[1], alerts[0] }, result);
    }

    [Fact]
    public void SeveritySelection_ReturnsMatchingSavedAlertsNewestFirst()
    {
        var alerts = new[]
        {
            Alert("pump-01", BaseTimestamp, TelemetryAlertType.HighPressure),
            Alert("pump-02", BaseTimestamp.AddMinutes(1), TelemetryAlertType.OutOfOrderSequence),
            Alert("pump-03", BaseTimestamp.AddMinutes(2), TelemetryAlertType.DeviceSilent)
        };
        HistoryOptionsParser.TryParse(
            new[] { "--severity=Critical" },
            DefaultCount,
            out var selection);

        var matchingAlerts = AlertHistoryFilter.GetBySeverity(alerts, selection.Severity!.Value);
        var result = AlertHistoryFilter.GetRecent(matchingAlerts, matchingAlerts.Count);

        Assert.Equal(new[] { alerts[2], alerts[0] }, result);
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
