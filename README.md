# Pump Telemetry Monitor

A small C#/.NET application that simulates telemetry from multiple industrial pump devices and detects abnormal operating conditions.

## How to run

Requires the .NET 8 SDK or later.

```powershell
dotnet run --project src/PumpTelemetry.Monitor
```

Press `Ctrl+C` to stop the monitor gracefully.

## How to test

```powershell
dotnet test
```

## Architecture choices

The solution is split into:

- `PumpTelemetry.Core`: telemetry models, simulated device producers, processing pipeline, and alert detection.
- `PumpTelemetry.Monitor`: console entry point that wires devices, pipeline, and console alert output together.
- `PumpTelemetry.Tests`: xUnit tests for threshold, sequence, silence, and concurrent submission behavior.

### Alert history storage contract

`IAlertHistoryStore` defines the two operations required by alert history: loading saved
`TelemetryAlert` instances and saving one generated alert. Both operations are asynchronous
and accept a `CancellationToken` so they can participate in the monitor's shutdown flow.

The contract is storage-independent so the monitor can use JSON persistence without coupling
alert generation to a file format. It also allows tests to substitute an in-memory store while
the production application uses a file-backed implementation.

`JsonAlertHistoryStore` is the local file implementation. It receives its history file path
through the constructor, stores alerts in a JSON array, and writes alert types as readable enum
names. Saving an alert retains the existing entries before adding the new one. File operations
are asynchronous, and access through one store instance is serialized so simultaneous alert
writes do not corrupt history.

Loading a path that does not exist returns an empty history. Invalid JSON instead produces an
`InvalidDataException` that identifies the affected file, leaving the existing content untouched
for diagnosis.

The console app starts five simulated devices. Each device emits a `TelemetryReading` every 100-500ms with a device id, timestamp, pressure, temperature, and sequence number. The simulator occasionally produces a pressure spike and an out-of-order sequence so the alert paths can be seen during a short run.

## Concurrency approach

The processing pipeline uses a bounded `Channel<TelemetryReading>` as an asynchronous fan-in queue. Multiple device tasks can publish concurrently, and the channel capacity provides backpressure if producers outpace alert processing for long enough. A single reader consumes the channel so per-device sequence checks are deterministic and based on channel arrival order.

Shutdown is idempotent: completing the channel writer and cancelling the silent-device monitor is safe to request more than once. Queued readings drain before shutdown completes. The silent-device monitor runs on a `PeriodicTimer` and is cancelled during shutdown.

## Alerts

The pipeline emits console alerts for:

- pressure above the configured threshold,
- out-of-order sequence numbers,
- devices silent for more than 5 seconds.

Silent-device detection starts tracking a device after its first reading, because the monitor has no external device registry in this small assignment.
