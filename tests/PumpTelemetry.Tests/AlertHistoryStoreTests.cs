using System.Runtime.CompilerServices;
using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryStoreTests
{
    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsEmpty()
    {
        var store = new AlertHistoryStore(NonExistentPath());
        var records = store.Load();
        Assert.Empty(records);
    }

    [Fact]
    public void AppendThenLoad_ReturnsRecordsInOrder()
    {
        var path = NewTempPath();
        try
        {
            var store = new AlertHistoryStore(path);
            var first = Record(TelemetryAlertType.HighPressure, "pump-01", 125.5);
            var second = Record(TelemetryAlertType.DeviceSilent, "pump-02", null);

            store.Append(first);
            store.Append(second);

            var loaded = store.Load();

            Assert.Equal(2, loaded.Count);
            Assert.Equal(first, loaded[0]);
            Assert.Equal(second, loaded[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ApplyFilter_WithLast_ReturnsOnlyMostRecent()
    {
        var records = new[]
        {
            Record(TelemetryAlertType.HighPressure, "pump-01", 121),
            Record(TelemetryAlertType.HighPressure, "pump-02", 122),
            Record(TelemetryAlertType.HighPressure, "pump-03", 123),
        };

        var filtered = AlertHistoryStore.ApplyFilter(records, last: 2);

        Assert.Equal(2, filtered.Count);
        Assert.Equal("pump-02", filtered[0].DeviceId);
        Assert.Equal("pump-03", filtered[1].DeviceId);
    }

    [Fact]
    public void ApplyFilter_WithSeverity_ReturnsOnlyMatchingType()
    {
        var records = new[]
        {
            Record(TelemetryAlertType.HighPressure, "pump-01", 125),
            Record(TelemetryAlertType.DeviceSilent, "pump-02", null),
            Record(TelemetryAlertType.OutOfOrderSequence, "pump-03", null),
        };

        var filtered = AlertHistoryStore.ApplyFilter(records, severity: "high");
        var filtered_2 = AlertHistoryStore.ApplyFilter(records, severity: "silent");
        
        var single = Assert.Single(filtered);
        var single2 = Assert.Single(filtered_2);

        Assert.Equal(TelemetryAlertType.HighPressure, single.Type);
        Assert.Equal(TelemetryAlertType.DeviceSilent, single2.Type);

        
    }

    private static AlertRecord Record(TelemetryAlertType type, string deviceId, double? value) =>
        new(DateTimeOffset.UtcNow, type, deviceId, value, $"{type} test message");

    private static string NewTempPath() =>
        Path.Combine(Path.GetTempPath(), $"alert-history-test-{Guid.NewGuid():N}.jsonl");

    private static string NonExistentPath() =>
        Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.jsonl");

}