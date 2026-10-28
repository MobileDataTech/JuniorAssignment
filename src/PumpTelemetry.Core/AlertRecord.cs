namespace PumpTelemetry.Core;

/// A flat, serializable snapshot of an alert for persistence — kept separate
/// from the domain TelemetryAlert so the on-disk format is decoupled from the model.

public sealed record AlertRecord(
    DateTimeOffset Timestamp,
    TelemetryAlertType Type, // HighPressure, OutOfOrderSequence, DeviceSilent
    string DeviceId,
    double? Value, // Pressure value for HighPressure alerts, null otherwise
    string Message )
{
    public static AlertRecord FromAlert(TelemetryAlert alert) =>
    //converts the live TelemetryAlert to a record that can be stored
        new(
            alert.Timestamp,
            alert.Type,
            alert.DeviceId,
            alert.Reading?.Pressure,
            alert.Message
        );
}