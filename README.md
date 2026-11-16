# Pump Telemetry Monitor

A small C#/.NET application that simulates telemetry from multiple industrial pump devices and detects abnormal operating conditions.

## How to run

Requires the .NET 8 SDK or later.

```powershell
dotnet run --project src/PumpTelemetry.Monitor
```

Press `Ctrl+C` to stop the monitor gracefully.

**For alert history filters (`--last`, `--severity`), see [Alert history](#alert-history) below.**

## How to test

```powershell
dotnet test
```

## Architecture choices

The solution is split into:

- `PumpTelemetry.Core`: telemetry models, simulated device producers, processing pipeline, alert detection, and alert history persistence (`AlertRecord`, `AlertHistoryStore`).
- `PumpTelemetry.Monitor`: console entry point that wires devices, pipeline, console alert output, history load/display, and CLI filters.
- `PumpTelemetry.Tests`: xUnit tests for threshold, sequence, silence, concurrent submission, and alert history persistence/filtering.

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

Generated alerts are appended to `alert-history.jsonl` in the current working directory.

When the monitor starts, it loads that file and prints recent history before live monitoring begins. New alerts from the current run are appended to the same file as they are raised.

### Saved fields

Each history record includes timestamp (UTC), alert type, severity, device / pump ID, and message. When relevant, it also stores pressure, sequence number, pressure threshold, or silent timeout seconds.

### Severity mapping

Severity is derived from alert type:

- `HighPressure` → High
- `DeviceSilent` → Medium
- `OutOfOrderSequence` → Low

### Filters

Startup history can be narrowed with CLI options. Pass them after `--` so `dotnet run` forwards them to the app:

```powershell
# Show the 5 most recent saved alerts, then start monitoring
dotnet run --project src/PumpTelemetry.Monitor -- --last=5

# Show only high-severity alerts (up to 10 by default)
dotnet run --project src/PumpTelemetry.Monitor -- --severity=high

# Combine filters
dotnet run --project src/PumpTelemetry.Monitor -- --severity=high --last=10
```

When both filters are provided, severity filtering is applied first, then the most recent N matching alerts are kept.

### Notes

- Valid `--severity` values: `low`, `medium`, `high` (case-insensitive). `--last` must be a positive integer (default `10`).
- Filters only change what is displayed at startup. They do not delete alerts from the file.
- A missing history file means empty history (first run). The history file is never trimmed; delete `alert-history.jsonl` to clear it.
