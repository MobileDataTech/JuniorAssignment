namespace PumpTelemetry.Core;

public sealed record AlertRecord(
    DateTimeOffset Timestamp,
    string AlertType,
    string DeviceId,
    double? SensorValue,
    double? Threshold,
    string Message)
{
    public static AlertRecord FromAlert(TelemetryAlert alert, double? pressureThreshold = null)
    {
        var sensorValue = alert.Type switch
        {
            TelemetryAlertType.HighPressure => alert.Reading?.Pressure,
            TelemetryAlertType.OutOfOrderSequence => alert.Reading?.SequenceNumber,
            _ => null
        };

        var threshold = alert.Type == TelemetryAlertType.HighPressure
            ? pressureThreshold
            : null;

        return new AlertRecord(
            alert.Timestamp,
            alert.Type.ToString(),
            alert.DeviceId,
            sensorValue,
            threshold,
            alert.Message);
    }
}
