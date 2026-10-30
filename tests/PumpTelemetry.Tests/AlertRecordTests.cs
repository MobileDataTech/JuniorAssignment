using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertRecordTests
{
    [Theory]
    [InlineData(TelemetryAlertType.HighPressure, AlertSeverity.High)]
    [InlineData(TelemetryAlertType.DeviceSilent, AlertSeverity.Medium)]
    [InlineData(TelemetryAlertType.OutOfOrderSequence, AlertSeverity.Low)]
    public void GetSeverity_MapsEachAlertType(TelemetryAlertType type, AlertSeverity expected)
    {
        Assert.Equal(expected, AlertRecord.GetSeverity(type));
    }

    [Fact]
    public void GetSeverity_WhenTypeIsUnknown_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AlertRecord.GetSeverity((TelemetryAlertType)99));
    }

    [Fact]
    public void CreateAlertRecord_CopiesFieldsAndTakesPressureFromReading()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var reading = new TelemetryReading("pump-01", timestamp, Pressure: 131.5, Temperature: 70, SequenceNumber: 4);
        var alert = new TelemetryAlert(TelemetryAlertType.HighPressure, "pump-01", timestamp, "too high", reading);

        var record = AlertRecord.CreateAlertRecord(alert);

        Assert.Equal(TelemetryAlertType.HighPressure, record.Type);
        Assert.Equal("pump-01", record.DeviceId);
        Assert.Equal(timestamp, record.Timestamp);
        Assert.Equal("too high", record.Message);
        Assert.Equal(131.5, record.Pressure);
        Assert.Equal(AlertSeverity.High, record.Severity);
    }

    [Fact]
    public void CreateAlertRecord_WhenAlertHasNoReading_HasNullPressureAndStillGetsSeverity()
    {
        var alert = new TelemetryAlert(TelemetryAlertType.DeviceSilent, "pump-02", DateTimeOffset.UtcNow, "silent");

        var record = AlertRecord.CreateAlertRecord(alert);

        Assert.Null(record.Pressure);
        Assert.Equal(AlertSeverity.Medium, record.Severity);
    }
}
