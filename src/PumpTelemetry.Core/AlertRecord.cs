namespace PumpTelemetry.Core;

// Severity used for --severity filtering (assignment assumption).
public enum AlertSeverity
{
    Low,
    Medium,
    High
}

// Flattened alert shape saved to disk (separate from live TelemetryAlert).
public sealed record AlertRecord(
    DateTimeOffset Timestamp,
    TelemetryAlertType Type,
    AlertSeverity Severity,
    string DeviceId,
    string Message,
    double? Pressure = null,
    long? SequenceNumber = null,
    double? PressureThreshold = null,
    double? SilentTimeoutSeconds = null)
{
    public static AlertSeverity GetSeverity(TelemetryAlertType type)
    {
        return type switch
        {
            TelemetryAlertType.HighPressure => AlertSeverity.High,
            TelemetryAlertType.DeviceSilent => AlertSeverity.Medium,
            TelemetryAlertType.OutOfOrderSequence => AlertSeverity.Low,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown alert type.")
        };
    }

    // Map a live alert + pipeline options into a persistable record.
    public static AlertRecord FromAlert(TelemetryAlert alert, TelemetryPipelineOptions options)
    {
        return new AlertRecord(
            alert.Timestamp,
            alert.Type,
            GetSeverity(alert.Type),
            alert.DeviceId,
            alert.Message,
            alert.Reading?.Pressure,
            alert.Reading?.SequenceNumber,
            alert.Type == TelemetryAlertType.HighPressure ? options.PressureThreshold : null,
            alert.Type == TelemetryAlertType.DeviceSilent ? options.SilentTimeout.TotalSeconds : null);
    }
}
