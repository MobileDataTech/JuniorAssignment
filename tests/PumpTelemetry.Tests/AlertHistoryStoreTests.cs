using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "pump-telemetry-tests",
        Guid.NewGuid().ToString("N"));

    private string HistoryFile => Path.Combine(_directory, "alert-history.jsonl");

    public AlertHistoryStoreTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsEmptyHistory()
    {
        var store = new AlertHistoryStore(HistoryFile);

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Append_ThenLoad_RoundTripsAllFields()
    {
        var record = new AlertRecord(
            new DateTimeOffset(2026, 11, 12, 20, 5, 1, TimeSpan.Zero),
            TelemetryAlertType.HighPressure,
            AlertSeverity.High,
            "pump-01",
            "Pressure 125.0 exceeded threshold 120.0.",
            Pressure: 125,
            SequenceNumber: 4,
            PressureThreshold: 120);

        new AlertHistoryStore(HistoryFile).Append(record);
        var history = new AlertHistoryStore(HistoryFile).Load();

        Assert.Equal(record, Assert.Single(history));
    }

    [Fact]
    public void Append_WritesOneJsonLinePerAlert()
    {
        var store = new AlertHistoryStore(HistoryFile);

        store.Append(Record("pump-01"));
        store.Append(Record("pump-02"));

        var lines = File.ReadAllLines(HistoryFile);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"Type\":\"HighPressure\"", lines[0]);
        Assert.Contains("\"Severity\":\"High\"", lines[0]);
        Assert.Contains("\"DeviceId\":\"pump-02\"", lines[1]);
    }

    [Fact]
    public void Load_SkipsBlankAndMalformedLines()
    {
        var store = new AlertHistoryStore(HistoryFile);
        store.Append(Record("pump-01"));
        File.AppendAllText(
            HistoryFile,
            "\nnot json\nnull\n{}\n{\"Type\":\"NoSuchType\"}\n{\"Timestamp\":\"2026-\n");
        store.Append(Record("pump-02"));

        var history = store.Load();

        Assert.Equal(new[] { "pump-01", "pump-02" }, history.Select(r => r.DeviceId));
    }

    [Fact]
    public void Append_WhenCalledConcurrently_KeepsEveryAlert()
    {
        var store = new AlertHistoryStore(HistoryFile);

        Parallel.For(1, 101, i => store.Append(Record($"pump-{i:000}")));

        var history = store.Load();
        Assert.Equal(100, history.Select(r => r.DeviceId).Distinct().Count());
    }

    [Fact]
    public async Task Pipeline_WithStoreAsAlertSink_PersistsAlertsAcrossRestart()
    {
        var options = new TelemetryPipelineOptions { PressureThreshold = 100 };
        var store = new AlertHistoryStore(HistoryFile);

        await using (var pipeline = new TelemetryPipeline(
            options,
            (alert, _) =>
            {
                store.Append(AlertRecord.FromAlert(alert, options));
                return ValueTask.CompletedTask;
            }))
        {
            pipeline.Start();
            await pipeline.SubmitAsync(new TelemetryReading("pump-01", DateTimeOffset.UtcNow, 125, 70, 10));
            await pipeline.SubmitAsync(new TelemetryReading("pump-01", DateTimeOffset.UtcNow, 90, 70, 9));
            await pipeline.StopAsync();
        }

        var history = new AlertHistoryStore(HistoryFile).Load();

        Assert.Collection(
            history,
            record =>
            {
                Assert.Equal(TelemetryAlertType.HighPressure, record.Type);
                Assert.Equal(AlertSeverity.High, record.Severity);
                Assert.Equal(125, record.Pressure);
                Assert.Equal(100, record.PressureThreshold);
            },
            record =>
            {
                Assert.Equal(TelemetryAlertType.OutOfOrderSequence, record.Type);
                Assert.Equal(AlertSeverity.Low, record.Severity);
                Assert.Equal(9, record.SequenceNumber);
                Assert.Null(record.PressureThreshold);
            });
    }

    [Fact]
    public async Task Pipeline_WhenDeviceGoesSilent_PersistsSilentTimeout()
    {
        var options = new TelemetryPipelineOptions { SilentTimeout = TimeSpan.FromSeconds(5) };
        var store = new AlertHistoryStore(HistoryFile);
        await using var pipeline = new TelemetryPipeline(
            options,
            (alert, _) =>
            {
                store.Append(AlertRecord.FromAlert(alert, options));
                return ValueTask.CompletedTask;
            });

        var lastSeen = DateTimeOffset.UtcNow;
        await pipeline.ProcessReadingAsync(new TelemetryReading("pump-01", lastSeen, 90, 70, 1));
        await pipeline.CheckSilentDevicesAsync(lastSeen.AddSeconds(6));

        var record = Assert.Single(store.Load());
        Assert.Equal(TelemetryAlertType.DeviceSilent, record.Type);
        Assert.Equal(AlertSeverity.Medium, record.Severity);
        Assert.Equal(5, record.SilentTimeoutSeconds);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static AlertRecord Record(string deviceId)
    {
        return new AlertRecord(
            DateTimeOffset.UtcNow,
            TelemetryAlertType.HighPressure,
            AlertSeverity.High,
            deviceId,
            "Pressure exceeded threshold.",
            Pressure: 125,
            SequenceNumber: 7,
            PressureThreshold: 120);
    }
}
