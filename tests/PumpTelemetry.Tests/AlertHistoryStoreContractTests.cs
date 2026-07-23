using PumpTelemetry.Core;
using Xunit;

namespace PumpTelemetry.Tests;

public sealed class AlertHistoryStoreContractTests
{
    [Fact]
    public async Task Store_WhenAlertIsSaved_CanLoadSavedAlert()
    {
        IAlertHistoryStore store = new InMemoryAlertHistoryStore();
        var alert = new TelemetryAlert(
            TelemetryAlertType.HighPressure,
            "pump-01",
            DateTimeOffset.UtcNow,
            "Pressure exceeded threshold.");

        await store.SaveAsync(alert);
        var alerts = await store.LoadAsync();

        Assert.Equal(alert, Assert.Single(alerts));
    }

    [Fact]
    public async Task Store_WhenCancellationIsRequested_ObservesCancellation()
    {
        IAlertHistoryStore store = new InMemoryAlertHistoryStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.LoadAsync(cancellation.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => store.SaveAsync(
                new TelemetryAlert(
                    TelemetryAlertType.DeviceSilent,
                    "pump-01",
                    DateTimeOffset.UtcNow,
                    "Device is silent."),
                cancellation.Token));
    }

    private sealed class InMemoryAlertHistoryStore : IAlertHistoryStore
    {
        private readonly List<TelemetryAlert> _alerts = new();

        public Task<IReadOnlyList<TelemetryAlert>> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<TelemetryAlert>>(_alerts.ToArray());
        }

        public Task SaveAsync(TelemetryAlert alert, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _alerts.Add(alert);
            return Task.CompletedTask;
        }
    }
}
