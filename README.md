# Pump Telemetry Monitor

A small C#/.NET application that simulates telemetry from multiple industrial pump devices and detects abnormal operating conditions.

## How to run

Requires the .NET 8 SDK or later.

```powershell
dotnet run --project src/PumpTelemetry.Monitor
```

Press `Ctrl+C` to stop the monitor gracefully.

## Alert history dashboard

The monitor now persists generated alerts to a local JSON file named `alert-history.json`
by default. When the monitor starts, it loads saved alerts and prints a recent alert
history before live telemetry begins.

Examples:

```powershell
dotnet run --project src/PumpTelemetry.Monitor
dotnet run --project src/PumpTelemetry.Monitor -- --history-only --last=5
dotnet run --project src/PumpTelemetry.Monitor -- --history-only --type=HighPressure
dotnet run --project src/PumpTelemetry.Monitor -- --history-only --type=HighPressure --last=5
dotnet run --project src/PumpTelemetry.Monitor -- --history-file=./data/alerts.json
```

Supported options:

- `--last=N`: show the most recent `N` saved alerts.
- `--type=AlertType`: filter history by alert type, such as `HighPressure`,
  `OutOfOrderSequence`, or `DeviceSilent`.
- `--history-only`: display saved history and exit without starting live monitoring.
- `--history-file=PATH`: read and write alert history from a custom JSON file path.

Persisted alert records include timestamp, alert type, device id, message, and the
available sensor value or pressure threshold when applicable.

## How to test

```powershell
dotnet test
```

## Architecture choices

The solution is split into:

- `PumpTelemetry.Core`: telemetry models, simulated device producers, processing pipeline, and alert detection.
- `PumpTelemetry.Monitor`: console entry point that wires devices, pipeline, and console alert output together.
- `PumpTelemetry.Tests`: xUnit tests for threshold, sequence, silence, and concurrent submission behavior.

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
