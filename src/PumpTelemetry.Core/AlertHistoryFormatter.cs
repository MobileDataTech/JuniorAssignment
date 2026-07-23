namespace PumpTelemetry.Core;

/// <summary>
/// Converts telemetry alerts into a consistent display string.
/// </summary>
public static class AlertHistoryFormatter
{
    /// <summary>
    /// Formats one alert for historical or live monitor output.
    /// </summary>
    /// <param name="alert">The telemetry alert to format.</param>
    /// <returns>
    /// A string containing the alert timestamp, type, device identifier, and message.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="alert"/> is <see langword="null"/>.
    /// </exception>
    public static string Format(TelemetryAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);

        return $"[{alert.Timestamp:HH:mm:ss.fff}] {alert.Type} {alert.DeviceId}: {alert.Message}";
    }
}
