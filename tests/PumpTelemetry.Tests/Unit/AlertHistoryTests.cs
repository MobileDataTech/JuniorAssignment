using PumpTelemetry.Core;
using PumpTelemetry.Monitor;
using Xunit;

namespace PumpTelemetry.Tests;

[Trait("Category", "Unit")]
public sealed class AlertHistoryTests
{
    private static AlertRecord Record(int minute) => new(
        DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddMinutes(minute),
        TelemetryAlertType.HighPressure, $"pump-{minute}", 125, 120, "pressure", "Pressure exceeded threshold.");

    [Fact]
    public void DisplayLimitsHistoryAndOrdersByTimestamp()
    {
        using var output = new StringWriter();
        AlertHistoryDisplay.Write(output, new[] { Record(1), Record(3), Record(2) }, 2);
        var text = output.ToString();
        Assert.DoesNotContain("pump-1", text);
        Assert.True(text.IndexOf("pump-3", StringComparison.Ordinal) < text.IndexOf("pump-2", StringComparison.Ordinal));
        Assert.Contains("HighPressure", text);
        Assert.Contains("value=125, threshold=120", text);
        Assert.Contains("2026-01-01T00:03:00", text);
    }

    [Fact]
    public void EmptyHistoryHasHelpfulMessage()
    {
        using var output = new StringWriter();
        AlertHistoryDisplay.Write(output, Array.Empty<AlertRecord>(), 10);
        Assert.Contains("No saved alerts.", output.ToString());
    }

    [Theory]
    [InlineData("--last=0")]
    [InlineData("--last=-1")]
    [InlineData("--last=abc")]
    [InlineData("--unknown")]
    public void InvalidArgumentsAreRejected(string arg) =>
        Assert.Throws<ArgumentException>(() => AlertHistoryDisplay.ParseLimit(new[] { arg }));

    [Fact]
    public void HistoryAndLimitArgumentsAreAccepted()
    {
        Assert.Equal(10, AlertHistoryDisplay.ParseLimit(Array.Empty<string>()));
        Assert.Equal(3, AlertHistoryDisplay.ParseLimit(new[] { "--history", "--last=3" }));
    }

    [Fact]
    public void AlertMappingCapturesValuesAndSilentThreshold()
    {
        var options = new TelemetryPipelineOptions();
        var reading = new TelemetryReading("pump-1", DateTimeOffset.UtcNow, 150, 70, 4);
        var pressure = AlertRecord.FromAlert(new(TelemetryAlertType.HighPressure, reading.DeviceId,
            reading.Timestamp, "High", reading), options);
        Assert.Equal(150d, pressure.Value);
        Assert.Equal(options.PressureThreshold, pressure.Threshold);
        var silent = AlertRecord.FromAlert(new(TelemetryAlertType.DeviceSilent, reading.DeviceId,
            reading.Timestamp, "Silent"), options);
        Assert.Null(silent.Value);
        Assert.Equal(options.SilentTimeout.TotalSeconds, silent.Threshold);
    }

}
