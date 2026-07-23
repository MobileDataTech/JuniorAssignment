using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class JsonAlertHistoryStoreTests : IDisposable
{
    private readonly string _directoryPath =
        Path.Combine(Path.GetTempPath(), $"pump-telemetry-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmptyHistory()
    {
        var store = CreateStore();

        var alerts = await store.LoadAsync();

        Assert.Empty(alerts);
    }

    [Fact]
    public async Task SaveAsync_WhenAlertIsSaved_RoundTripsAlert()
    {
        var store = CreateStore();
        var reading = new TelemetryReading(
            "pump-01",
            DateTimeOffset.UtcNow,
            Pressure: 125,
            Temperature: 70,
            SequenceNumber: 42);
        var alert = new TelemetryAlert(
            TelemetryAlertType.HighPressure,
            reading.DeviceId,
            reading.Timestamp,
            "Pressure exceeded threshold.",
            reading);

        await store.SaveAsync(alert);
        var savedAlert = Assert.Single(await store.LoadAsync());

        Assert.Equal(alert, savedAlert);
    }

    [Fact]
    public async Task SaveAsync_WhenMultipleAlertsAreSaved_PreservesAllAlerts()
    {
        var store = CreateStore();
        var first = Alert(TelemetryAlertType.HighPressure, "pump-01");
        var second = Alert(TelemetryAlertType.OutOfOrderSequence, "pump-02");

        await store.SaveAsync(first);
        await store.SaveAsync(second);

        Assert.Equal(new[] { first, second }, await store.LoadAsync());
    }

    [Fact]
    public async Task SaveAsync_WritesAlertTypeAsReadableEnumName()
    {
        var filePath = CreateFilePath();
        var store = new JsonAlertHistoryStore(filePath);

        await store.SaveAsync(Alert(TelemetryAlertType.DeviceSilent, "pump-03"));
        var json = await File.ReadAllTextAsync(filePath);

        Assert.Contains("\"Type\": \"DeviceSilent\"", json);
        Assert.Contains("\"Severity\": \"Critical\"", json);
    }

    [Fact]
    public async Task SaveAsync_WhenReadingIsNull_RoundTripsAlert()
    {
        var store = CreateStore();
        var alert = Alert(TelemetryAlertType.DeviceSilent, "pump-04");

        await store.SaveAsync(alert);
        var savedAlert = Assert.Single(await store.LoadAsync());

        Assert.Equal(alert, savedAlert);
        Assert.Null(savedAlert.Reading);
    }

    [Fact]
    public async Task LoadAsync_WhenJsonIsMalformed_ThrowsClearError()
    {
        var filePath = CreateFilePath();
        await File.WriteAllTextAsync(filePath, "{ not valid json");
        var store = new JsonAlertHistoryStore(filePath);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync());

        Assert.Contains(filePath, exception.Message);
        Assert.Contains("invalid JSON", exception.Message);
        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
    }

    [Fact]
    public async Task SaveAsync_WhenExistingJsonIsMalformed_DoesNotOverwriteHistory()
    {
        const string malformedJson = "{ not valid json";
        var filePath = CreateFilePath();
        await File.WriteAllTextAsync(filePath, malformedJson);
        var store = new JsonAlertHistoryStore(filePath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => store.SaveAsync(Alert(TelemetryAlertType.HighPressure, "pump-01")));

        Assert.Equal(malformedJson, await File.ReadAllTextAsync(filePath));
    }

    [Fact]
    public async Task SaveAsync_WhenCalledConcurrently_PreservesEveryAlert()
    {
        var store = CreateStore();
        var alerts = Enumerable.Range(1, 25)
            .Select(index => Alert(TelemetryAlertType.HighPressure, $"pump-{index:00}"))
            .ToArray();

        await Task.WhenAll(alerts.Select(alert => store.SaveAsync(alert)));
        var savedAlerts = await store.LoadAsync();

        Assert.Equal(alerts.Length, savedAlerts.Count);
        Assert.All(alerts, alert => Assert.Contains(alert, savedAlerts));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directoryPath))
        {
            Directory.Delete(_directoryPath, recursive: true);
        }
    }

    private JsonAlertHistoryStore CreateStore()
    {
        return new JsonAlertHistoryStore(CreateFilePath());
    }

    private string CreateFilePath()
    {
        Directory.CreateDirectory(_directoryPath);
        return Path.Combine(_directoryPath, "alerts.json");
    }

    private static TelemetryAlert Alert(TelemetryAlertType type, string deviceId)
    {
        return new TelemetryAlert(
            type,
            deviceId,
            DateTimeOffset.UtcNow,
            "Test alert.");
    }
}
