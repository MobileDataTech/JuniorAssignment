using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryFilterTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);

    private static readonly TelemetryPipelineOptions Options = new()
    {
        PressureThreshold = 120,
        SilentTimeout = TimeSpan.FromSeconds(5)
    };

    [Theory]
    [InlineData(TelemetryAlertType.HighPressure, AlertSeverity.High)]
    [InlineData(TelemetryAlertType.DeviceSilent, AlertSeverity.Medium)]
    [InlineData(TelemetryAlertType.OutOfOrderSequence, AlertSeverity.Low)]
    public void FromAlert_MapsAlertTypeToSeverity(TelemetryAlertType type, AlertSeverity expected)
    {
        var record = AlertRecord.FromAlert(
            new TelemetryAlert(type, "pump-01", Start, "message"),
            Options);

        Assert.Equal(expected, record.Severity);
    }

    [Fact]
    public void FromAlert_ForHighPressure_RecordsPressureAndThreshold()
    {
        var reading = new TelemetryReading("pump-01", Start, 125, 70, 4);

        var record = AlertRecord.FromAlert(
            new TelemetryAlert(TelemetryAlertType.HighPressure, "pump-01", Start, "high", reading),
            Options);

        Assert.Equal(125, record.Pressure);
        Assert.Equal(4, record.SequenceNumber);
        Assert.Equal(120, record.PressureThreshold);
        Assert.Null(record.SilentTimeoutSeconds);
    }

    [Fact]
    public void FromAlert_ForSilentDevice_RecordsSilentTimeoutOnly()
    {
        var record = AlertRecord.FromAlert(
            new TelemetryAlert(TelemetryAlertType.DeviceSilent, "pump-01", Start, "silent"),
            Options);

        Assert.Equal(5, record.SilentTimeoutSeconds);
        Assert.Null(record.Pressure);
        Assert.Null(record.SequenceNumber);
        Assert.Null(record.PressureThreshold);
    }

    [Fact]
    public void Filter_WithDefaults_ReturnsLastTen()
    {
        var records = Enumerable.Range(0, 15).Select(i => Record(i, AlertSeverity.High));

        var result = AlertHistoryStore.Filter(records);

        Assert.Equal(
            Enumerable.Range(5, 10).Select(i => $"pump-{i:00}"),
            result.Select(r => r.DeviceId));
    }

    [Fact]
    public void Filter_WithSeverityAndLast_ReturnsMostRecentMatches()
    {
        var records = new[]
        {
            Record(0, AlertSeverity.High),
            Record(1, AlertSeverity.Low),
            Record(2, AlertSeverity.High),
            Record(3, AlertSeverity.Medium),
            Record(4, AlertSeverity.High)
        };

        var result = AlertHistoryStore.Filter(records, AlertSeverity.High, last: 2);

        Assert.Equal(new[] { "pump-02", "pump-04" }, result.Select(r => r.DeviceId));
    }

    [Fact]
    public void Filter_WhenFewerRecordsThanLast_ReturnsAll()
    {
        var result = AlertHistoryStore.Filter(new[] { Record(0, AlertSeverity.Low) }, last: 50);

        Assert.Single(result);
    }

    [Fact]
    public void Filter_WhenLastIsNotPositive_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => AlertHistoryStore.Filter(Array.Empty<AlertRecord>(), last: 0));
    }

    private static AlertRecord Record(int index, AlertSeverity severity)
    {
        return new AlertRecord(
            Start.AddMinutes(index),
            TelemetryAlertType.HighPressure,
            severity,
            $"pump-{index:00}",
            "message");
    }
}
