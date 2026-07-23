namespace PumpTelemetry.Core;

public enum TelemetryAlertType
{
    HighPressure,
    OutOfOrderSequence,
    DeviceSilent
}

/// <summary>
/// Describes the operational importance of a telemetry alert.
/// </summary>
public enum TelemetryAlertSeverity
{
    /// <summary>
    /// Identifies an abnormal condition that should be reviewed.
    /// </summary>
    Warning,

    /// <summary>
    /// Identifies a condition that may require immediate attention.
    /// </summary>
    Critical
}

/// <summary>
/// Represents an abnormal condition detected from pump telemetry.
/// </summary>
public sealed record TelemetryAlert(
    TelemetryAlertType Type,
    string DeviceId,
    DateTimeOffset Timestamp,
    string Message,
    TelemetryReading? Reading = null)
{
    /// <summary>
    /// Gets the severity derived consistently from the alert type.
    /// </summary>
    /// <remarks>
    /// High pressure and device silence are critical because they can indicate unsafe operation
    /// or loss of monitoring. An out-of-order sequence is a warning because it indicates a data
    /// quality problem while the device is still reporting.
    /// </remarks>
    public TelemetryAlertSeverity Severity => Type switch
    {
        TelemetryAlertType.OutOfOrderSequence => TelemetryAlertSeverity.Warning,
        _ => TelemetryAlertSeverity.Critical
    };
}
