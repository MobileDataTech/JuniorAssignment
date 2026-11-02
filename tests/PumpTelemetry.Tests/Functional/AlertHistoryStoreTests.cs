using PumpTelemetry.Core;
using System.Text.Json;
using Xunit;

namespace PumpTelemetry.Tests;

[Trait("Category", "Functional")]
public sealed class AlertHistoryStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private string HistoryPath => Path.Combine(_directory, "alerts.json");
    private static AlertRecord Record(int minute) => new(
        DateTimeOffset.Parse("2026-01-01T00:00:00Z").AddMinutes(minute),
        TelemetryAlertType.HighPressure, $"pump-{minute}", 125, 120, "pressure", "Pressure exceeded threshold.");

    [Fact]
    public async Task MissingFileReturnsEmptyHistory()
    {
        using var store = new AlertHistoryStore(HistoryPath);
        Assert.Empty(await store.LoadAsync());
    }

    [Fact]
    public async Task ConcurrentAppendsSurviveReopeningWithoutLostRecords()
    {
        using (var store = new AlertHistoryStore(HistoryPath))
            await Task.WhenAll(Enumerable.Range(1, 20).Select(i => store.AppendAsync(Record(i))));
        using var reopened = new AlertHistoryStore(HistoryPath);
        var records = await reopened.LoadAsync();
        Assert.Equal(20, records.Count);
        Assert.Equal(Enumerable.Range(1, 20).Select(Record), records.OrderBy(r => r.Timestamp));
    }

    [Fact]
    public async Task CorruptHistoryIsReportedAndNeverOverwritten()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(HistoryPath, "broken JSON");
        using var store = new AlertHistoryStore(HistoryPath);
        await Assert.ThrowsAsync<JsonException>(() => store.LoadAsync());
        await Assert.ThrowsAsync<JsonException>(() => store.AppendAsync(Record(1)));
        Assert.Equal("broken JSON", await File.ReadAllTextAsync(HistoryPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
