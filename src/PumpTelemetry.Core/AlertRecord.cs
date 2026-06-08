namespace PumpTelemetry.Core;

public enum AlertSeverity { Low, Medium, High }

public sealed record AlertRecord(
    DateTimeOffset Timestamp,
    string DeviceId,
    TelemetryAlertType AlertType,
    AlertSeverity Severity,
    string Message,
    double? SensorValue
);