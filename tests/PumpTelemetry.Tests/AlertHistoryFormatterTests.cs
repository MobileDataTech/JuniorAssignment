using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryFormatterTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 7, 23, 12, 34, 56, 789, TimeSpan.Zero);

    [Fact]
    public void Format_HighPressureAlert_IncludesRequiredFieldsAndThresholdMessage()
    {
        var reading = new TelemetryReading(
            "pump-01",
            Timestamp,
            Pressure: 125,
            Temperature: 70,
            SequenceNumber: 42);
        var alert = new TelemetryAlert(
            TelemetryAlertType.HighPressure,
            reading.DeviceId,
            reading.Timestamp,
            "Pressure 125.0 exceeded threshold 120.0.",
            reading);

        var result = AlertHistoryFormatter.Format(alert);

        Assert.Equal(
            "[12:34:56.789] HighPressure pump-01: Pressure 125.0 exceeded threshold 120.0.",
            result);
    }

    [Fact]
    public void Format_OutOfOrderSequenceAlert_IncludesRequiredFieldsAndSequenceValues()
    {
        var reading = new TelemetryReading(
            "pump-02",
            Timestamp,
            Pressure: 90,
            Temperature: 65,
            SequenceNumber: 9);
        var alert = new TelemetryAlert(
            TelemetryAlertType.OutOfOrderSequence,
            reading.DeviceId,
            reading.Timestamp,
            "Sequence 9 arrived after 10.",
            reading);

        var result = AlertHistoryFormatter.Format(alert);

        Assert.Equal(
            "[12:34:56.789] OutOfOrderSequence pump-02: Sequence 9 arrived after 10.",
            result);
    }

    [Fact]
    public void Format_DeviceSilentAlertWithNullReading_IncludesRequiredFieldsAndMessage()
    {
        var alert = new TelemetryAlert(
            TelemetryAlertType.DeviceSilent,
            "pump-03",
            Timestamp,
            "Device has been silent for more than 5 seconds.",
            Reading: null);

        var result = AlertHistoryFormatter.Format(alert);

        Assert.Equal(
            "[12:34:56.789] DeviceSilent pump-03: Device has been silent for more than 5 seconds.",
            result);
    }
}
