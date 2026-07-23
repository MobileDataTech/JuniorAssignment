using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class TelemetryAlertSeverityTests
{
    [Theory]
    [InlineData(TelemetryAlertType.HighPressure, TelemetryAlertSeverity.Critical)]
    [InlineData(TelemetryAlertType.OutOfOrderSequence, TelemetryAlertSeverity.Warning)]
    [InlineData(TelemetryAlertType.DeviceSilent, TelemetryAlertSeverity.Critical)]
    public void Severity_MapsAlertTypeToExpectedSeverity(
        TelemetryAlertType type,
        TelemetryAlertSeverity expectedSeverity)
    {
        var alert = new TelemetryAlert(
            type,
            "pump-01",
            DateTimeOffset.UtcNow,
            "Test alert.");

        Assert.Equal(expectedSeverity, alert.Severity);
    }
}
