using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryStoreTests : IDisposable
{
    // Each test gets its own temp file so tests never touch the real alerts.jsonl.
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"alerts-{Guid.NewGuid():N}.jsonl");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ReturnsEmpty()
    {
        var store = new AlertHistoryStore(_path);

        var loaded = await store.LoadAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task AppendAsync_ThenLoadAsync_RoundTripsRecordsInOrder()
    {
        var store = new AlertHistoryStore(_path);
        var first = Record("pump-01", TelemetryAlertType.HighPressure, AlertSeverity.High, pressure: 125.5);
        var second = Record("pump-02", TelemetryAlertType.OutOfOrderSequence, AlertSeverity.Low);

        await store.AppendAsync(first);
        await store.AppendAsync(second);
        var loaded = await store.LoadAsync();

        // Records compare by value, so this checks every field of every record.
        Assert.Equal(new[] { first, second }, loaded);
    }

    [Fact]
    public async Task LoadAsync_PreservesMissingPressureAsNull()
    {
        var store = new AlertHistoryStore(_path);
        await store.AppendAsync(Record("pump-01", TelemetryAlertType.DeviceSilent, AlertSeverity.Medium, pressure: null));

        var loaded = await store.LoadAsync();

        Assert.Null(Assert.Single(loaded).Pressure);
    }

    [Fact]
    public async Task AppendAsync_WritesEnumsAsNamesNotNumbers()
    {
        var store = new AlertHistoryStore(_path);

        await store.AppendAsync(Record("pump-01", TelemetryAlertType.HighPressure, AlertSeverity.High, pressure: 130));

        var text = await File.ReadAllTextAsync(_path);
        Assert.Contains("\"HighPressure\"", text);
        Assert.Contains("\"High\"", text);
    }

    [Fact]
    public async Task AppendAsync_WritesOneRecordPerLine()
    {
        var store = new AlertHistoryStore(_path);

        await store.AppendAsync(Record("pump-01"));
        await store.AppendAsync(Record("pump-02"));

        Assert.Equal(2, (await File.ReadAllLinesAsync(_path)).Length);
    }

    [Fact]
    public async Task LoadAsync_SkipsCorruptAndBlankLines_AndKeepsTheGoodOnes()
    {
        var store = new AlertHistoryStore(_path);
        await store.AppendAsync(Record("pump-01"));
        await File.AppendAllTextAsync(_path, "not json" + Environment.NewLine + Environment.NewLine);
        await store.AppendAsync(Record("pump-02"));

        var loaded = await store.LoadAsync();

        Assert.Equal(new[] { "pump-01", "pump-02" }, loaded.Select(r => r.DeviceId));
    }

    [Fact]
    public async Task LoadAsync_WhenEveryLineIsCorrupt_ReturnsEmptyInsteadOfThrowing()
    {
        await File.WriteAllTextAsync(_path, "garbage" + Environment.NewLine + "{ \"Type\": " + Environment.NewLine);
        var store = new AlertHistoryStore(_path);

        var loaded = await store.LoadAsync();

        Assert.Empty(loaded);
    }

    [Fact]
    public async Task AppendAsync_WhenCalledConcurrently_SavesEveryRecordIntact()
    {
        var store = new AlertHistoryStore(_path);

        var writes = Enumerable.Range(1, 50)
            .Select(i => Task.Run(() => store.AppendAsync(Record($"pump-{i:00}")).AsTask()));
        await Task.WhenAll(writes);

        var loaded = await store.LoadAsync();
        Assert.Equal(50, loaded.Count);
        Assert.Equal(50, loaded.Select(r => r.DeviceId).Distinct().Count());
    }

    private static AlertRecord Record(
        string deviceId,
        TelemetryAlertType type = TelemetryAlertType.OutOfOrderSequence,
        AlertSeverity severity = AlertSeverity.Low,
        double? pressure = null)
    {
        return new AlertRecord(type, deviceId, DateTimeOffset.UtcNow, "message", pressure, severity);
    }
}
