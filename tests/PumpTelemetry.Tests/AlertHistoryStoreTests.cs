using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryStoreTests
{
    [Fact]
    public async Task SaveAsync_WhenRecordIsSaved_LoadAsyncReturnsPersistedAlert()
    {
        var filePath = CreateTempFilePath();
        var store = new AlertHistoryStore(filePath);
        var alert = new AlertRecord(
            DateTimeOffset.Parse("2026-06-08T12:00:00Z"),
            nameof(TelemetryAlertType.HighPressure),
            "pump-01",
            SensorValue: 130.5,
            Threshold: 120,
            "Pressure exceeded threshold.");

        try
        {
            await store.SaveAsync(alert);
            var loadedAlerts = await store.LoadAsync();

            var loadedAlert = Assert.Single(loadedAlerts);
            Assert.Equal(alert.Timestamp, loadedAlert.Timestamp);
            Assert.Equal(alert.AlertType, loadedAlert.AlertType);
            Assert.Equal(alert.DeviceId, loadedAlert.DeviceId);
            Assert.Equal(alert.SensorValue, loadedAlert.SensorValue);
            Assert.Equal(alert.Threshold, loadedAlert.Threshold);
            Assert.Equal(alert.Message, loadedAlert.Message);
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void FromAlert_WhenAlertIncludesReading_CapturesSensorValueAndThreshold()
    {
        var reading = new TelemetryReading(
            "pump-01",
            DateTimeOffset.Parse("2026-06-08T12:00:00Z"),
            Pressure: 130.5,
            Temperature: 75,
            SequenceNumber: 10);
        var alert = new TelemetryAlert(
            TelemetryAlertType.HighPressure,
            reading.DeviceId,
            reading.Timestamp,
            "Pressure exceeded threshold.",
            reading);

        var record = AlertRecord.FromAlert(alert, pressureThreshold: 120);

        Assert.Equal(nameof(TelemetryAlertType.HighPressure), record.AlertType);
        Assert.Equal(reading.DeviceId, record.DeviceId);
        Assert.Equal(reading.Pressure, record.SensorValue);
        Assert.Equal(120, record.Threshold);
    }

    [Fact]
    public void ApplyFilter_WhenTypeAndLastAreProvided_ReturnsMostRecentMatchingAlerts()
    {
        var records = new[]
        {
            Record(TelemetryAlertType.HighPressure, minutesAgo: 3),
            Record(TelemetryAlertType.OutOfOrderSequence, minutesAgo: 2),
            Record(TelemetryAlertType.HighPressure, minutesAgo: 1)
        };

        var filtered = AlertHistoryStore.ApplyFilter(
            records,
            new AlertHistoryFilter(nameof(TelemetryAlertType.HighPressure), Last: 1));

        var alert = Assert.Single(filtered);
        Assert.Equal(nameof(TelemetryAlertType.HighPressure), alert.AlertType);
        Assert.Equal(records[2].Timestamp, alert.Timestamp);
    }

    private static AlertRecord Record(TelemetryAlertType type, int minutesAgo)
    {
        return new AlertRecord(
            DateTimeOffset.UtcNow.AddMinutes(-minutesAgo),
            type.ToString(),
            "pump-01",
            SensorValue: 100 + minutesAgo,
            Threshold: 120,
            $"{type} alert.");
    }

    private static string CreateTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"alert-history-{Guid.NewGuid():N}.json");
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
