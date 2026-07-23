using System.Text.Json;
using System.Text.Json.Serialization;

namespace PumpTelemetry.Core;

/// <summary>
/// Persists telemetry alert history as a JSON array in a local file.
/// </summary>
/// <remarks>
/// Alert types are written as readable enum names. Access through a store instance is
/// serialized so concurrent reads and writes do not modify the file at the same time.
/// </remarks>
public sealed class JsonAlertHistoryStore : IAlertHistoryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Creates a JSON alert history store for the specified file.
    /// </summary>
    /// <param name="filePath">The path of the JSON file used to store alert history.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="filePath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="filePath"/> is empty or contains only whitespace.
    /// </exception>
    public JsonAlertHistoryStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    /// <summary>
    /// Loads every alert currently stored in the history file.
    /// </summary>
    /// <param name="cancellationToken">Stops the asynchronous file operation when cancellation is requested.</param>
    /// <returns>
    /// A read-only list of saved alerts, or an empty list when the history file does not exist.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the history file does not contain a valid JSON alert array.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public async Task<IReadOnlyList<TelemetryAlert>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Adds one alert to the existing history and writes the complete history to the JSON file.
    /// </summary>
    /// <param name="alert">The generated telemetry alert to save.</param>
    /// <param name="cancellationToken">Stops the asynchronous file operation when cancellation is requested.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="alert"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    /// Thrown when an existing history file does not contain a valid JSON alert array.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    public async Task SaveAsync(
        TelemetryAlert alert,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var alerts = (await LoadCoreAsync(cancellationToken).ConfigureAwait(false)).ToList();
            alerts.Add(alert);

            var json = JsonSerializer.Serialize(alerts, SerializerOptions);
            await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Reads and deserializes the history while the caller holds the file-access semaphore.
    /// </summary>
    /// <param name="cancellationToken">Stops the asynchronous read when cancellation is requested.</param>
    /// <returns>
    /// The deserialized alerts, or an empty list when the history file does not exist.
    /// </returns>
    /// <exception cref="InvalidDataException">
    /// Thrown when the file contents cannot be deserialized as an alert collection.
    /// </exception>
    private async Task<IReadOnlyList<TelemetryAlert>> LoadCoreAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return Array.Empty<TelemetryAlert>();
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);

        try
        {
            return JsonSerializer.Deserialize<List<TelemetryAlert>>(json, SerializerOptions)
                ?? throw CreateInvalidJsonException();
        }
        catch (JsonException exception)
        {
            throw CreateInvalidJsonException(exception);
        }
    }

    /// <summary>
    /// Creates the consistent exception returned when persisted history is not valid JSON.
    /// </summary>
    /// <param name="innerException">The JSON parsing error that caused the failure, when available.</param>
    /// <returns>An error that identifies the invalid alert history file.</returns>
    private InvalidDataException CreateInvalidJsonException(Exception? innerException = null)
    {
        return new InvalidDataException(
            $"Alert history file '{_filePath}' contains invalid JSON.",
            innerException);
    }
}
