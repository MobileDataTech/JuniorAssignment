namespace PumpTelemetry.Core;

/// <summary>
/// Provides storage-independent access to persisted telemetry alert history.
/// </summary>
/// <remarks>
/// The monitor depends on this contract so alert handling can be tested without
/// requiring a particular file format or storage mechanism.
/// </remarks>
public interface IAlertHistoryStore
{
    /// <summary>
    /// Loads all previously saved telemetry alerts.
    /// </summary>
    /// <param name="cancellationToken">Stops the asynchronous load operation when cancellation is requested.</param>
    /// <returns>The saved alerts, or an empty collection when no history exists.</returns>
    Task<IReadOnlyList<TelemetryAlert>> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves one telemetry alert to history.
    /// </summary>
    /// <param name="alert">The generated alert to persist.</param>
    /// <param name="cancellationToken">Stops the asynchronous save operation when cancellation is requested.</param>
    Task SaveAsync(TelemetryAlert alert, CancellationToken cancellationToken = default);
}
