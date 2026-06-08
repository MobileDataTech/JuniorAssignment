using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryStoreTests : IDisposable
{
    private readonly string _file =
        Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");

    // --- Persistence ---

    [Fact]
    public async Task AppendAsync_PersistsRecordToDisk()
    {
        var store = new AlertHistoryStore(_file);
        await store.AppendAsync(MakeRecord("pump-01", AlertSeverity.High));

        var reloaded = new AlertHistoryStore(_file);
        var record = Assert.Single(reloaded.Records);

        Assert.Equal("pump-01", record.DeviceId);
        Assert.Equal(AlertSeverity.High, record.Severity);
    }

    [Fact]
    public async Task AppendAsync_AccumulatesMultipleRecords()
    {
        var store = new AlertHistoryStore(_file);

        await store.AppendAsync(MakeRecord("pump-01", AlertSeverity.High));
        await store.AppendAsync(MakeRecord("pump-02", AlertSeverity.Low));

        var reloaded = new AlertHistoryStore(_file);

        Assert.Equal(2, reloaded.Records.Count);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_StartsWithEmptyHistory()
    {
        var store = new AlertHistoryStore(
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json"));

        Assert.Empty(store.Records);
    }

    [Fact]
    public void Load_WhenFileIsCorrupt_StartsWithEmptyHistory()
    {
        File.WriteAllText(_file, "not valid json {{{{");

        var store = new AlertHistoryStore(_file);

        Assert.Empty(store.Records);
    }

    // --- Filtering ---

    [Fact]
    public async Task Filter_BySeverity_ReturnsMatchingRecordsOnly()
    {
        var store = new AlertHistoryStore(_file);

        await store.AppendAsync(MakeRecord("pump-01", AlertSeverity.High));
        await store.AppendAsync(MakeRecord("pump-02", AlertSeverity.Low));
        await store.AppendAsync(MakeRecord("pump-03", AlertSeverity.High));

        var results = store.Filter(severity: AlertSeverity.High).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, record => Assert.Equal(AlertSeverity.High, record.Severity));
    }

    [Fact]
    public async Task Filter_LastN_ReturnsMostRecentN()
    {
        var store = new AlertHistoryStore(_file);
        var start = DateTimeOffset.Parse("2026-06-08T12:00:00Z");

        for (var i = 0; i < 5; i++)
        {
            await store.AppendAsync(
                MakeRecord($"pump-{i:00}", AlertSeverity.Low, start.AddMinutes(i)));
        }

        var results = store.Filter(last: 2).ToList();

        Assert.Equal(2, results.Count);
        Assert.Equal("pump-03", results[0].DeviceId);
        Assert.Equal("pump-04", results[1].DeviceId);
    }

    [Fact]
    public async Task Filter_Since_ExcludesRecordsBeforeCutoff()
    {
        var store = new AlertHistoryStore(_file);
        var cutoff = DateTimeOffset.Parse("2026-06-08T12:00:00Z");

        await store.AppendAsync(
            MakeRecord("pump-old", AlertSeverity.High, cutoff.AddHours(-2)));
        await store.AppendAsync(
            MakeRecord("pump-recent", AlertSeverity.High, cutoff.AddMinutes(-5)));

        var results = store.Filter(since: cutoff.AddHours(-1)).ToList();

        var record = Assert.Single(results);

        Assert.Equal("pump-recent", record.DeviceId);
    }

    [Fact]
    public async Task Filter_NoArgs_ReturnsAllRecords()
    {
        var store = new AlertHistoryStore(_file);

        await store.AppendAsync(MakeRecord("pump-01", AlertSeverity.High));
        await store.AppendAsync(MakeRecord("pump-02", AlertSeverity.Low));

        Assert.Equal(2, store.Filter().ToList().Count);
    }

    public void Dispose()
    {
        if (File.Exists(_file))
        {
            File.Delete(_file);
        }
    }

    private static AlertRecord MakeRecord(
        string deviceId,
        AlertSeverity severity,
        DateTimeOffset? timestamp = null)
    {
        return new AlertRecord(
            timestamp ?? DateTimeOffset.UtcNow,
            deviceId,
            TelemetryAlertType.HighPressure,
            severity,
            "Test message",
            SensorValue: 125.0);
    }
}