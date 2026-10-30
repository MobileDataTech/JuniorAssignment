namespace PumpTelemetry.Core;

public sealed record AlertRecord(
    TelemetryAlertType Type,
    string DeviceId,
    DateTimeOffset Timestamp,
    string Message,
    double? Pressure,
    AlertSeverity Severity)
{
    public static AlertRecord CreateAlertRecord(TelemetryAlert alert)
    {
        return new AlertRecord(
            alert.Type,
            alert.DeviceId,
            alert.Timestamp,
            alert.Message,
            alert.Reading?.Pressure,
            GetSeverity(alert.Type));
    }

    // Assumed mapping: pressure is a safety issue, silence is a possible outage,
    // out-of-order sequence is a data-quality problem.
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
}
