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

## Alert history

Every alert raised during a run is appended to a local history file, `alert-history.jsonl`
(JSON Lines — one alert per line), created in the directory you run from. When the monitor
starts, it loads that file and prints the saved history before live monitoring begins, so
alerts from previous runs are visible.

Each stored alert records its timestamp, type, device id, the triggering sensor value
(pressure, where applicable), and the alert message.

### Filtering the history view

Two optional command-line filters narrow what the startup history shows:

- `--last=<N>` — show only the N most recent alerts.
- `--severity=<text>` — show only alerts whose type contains `<text>` (case-insensitive), e.g. `--severity=high`.

They can be combined. Pass them after `--` so the arguments reach the app rather than `dotnet run`:

```powershell
dotnet run --project src/PumpTelemetry.Monitor -- --last=10
dotnet run --project src/PumpTelemetry.Monitor -- --severity=high --last=5
```

The filters affect only the displayed history; every alert is still persisted. Delete
`alert-history.jsonl` to reset the history.