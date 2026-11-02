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

The monitor loads and displays the latest 10 alerts on startup and saves new alerts
in `alert-history.json` in the current working directory. Run commands from the
same directory to reuse that history. The file is ignored by Git.

```sh
# Display the latest 5 saved alerts and exit without starting simulated pumps
dotnet run --project src/PumpTelemetry.Monitor -- --history --last=5

# Display the latest 20 saved alerts, then start monitoring
dotnet run --project src/PumpTelemetry.Monitor -- --last=20
```

`--last` must be a positive integer; invalid arguments exit with an error.
History is displayed newest first by timestamp and includes the alert type, device,
value, threshold, units, and message. Sequence alerts contain the sequence value;
silent-device alerts contain the configured silence threshold in seconds.
Unavailable values are shown as `n/a`.

This small implementation assumes one monitor process per history file and modest
history size. Writes are serialized within the process and replace the JSON file
via a temporary file after each alert. History is retained without a size limit.
Unreadable or malformed history prevents startup so existing data is preserved;
repair or move the file before retrying. Save failures are printed to stderr and
set a nonzero exit code while live monitoring continues.

### Assumptions and limitations

- One monitor process uses each history file.
- History is modest in size: each save rewrites the file, and there is no automatic retention.
- History is stored in the current working directory and filtered by recent entries.

### Test suites

Tests follow this project's convention of unit → functional → integration,
with matching folders and xUnit `Category` traits:

- `Unit`: alert mapping, CLI parsing, history formatting/filtering, and pipeline detection/lifecycle using in-memory alert sinks.
- `Functional`: the history store with real temporary files, including concurrent writes, reopening, and corrupt-file preservation.
- `Integration`: launches the actual console application in a separate process and isolated temporary directory. Covers history-only mode, JSON loading/order/limits, invalid CLI options, corrupt history, and live alert persistence followed by history retrieval in a new process.

```sh
dotnet test                                 # all suites
dotnet test --filter 'Category=Unit'         # unit tests only
dotnet test --filter 'Category=Functional'   # history storage feature tests
dotnet test --filter 'Category=Integration'  # console application end to end
```

Integration tests use the SDK's .NET host and build the monitor automatically.
The live test waits up to 20 seconds for a persisted alert, then terminates the
monitor process and verifies history through a fresh process. It tests persistence
across process termination; it does not assert graceful Ctrl+C shutdown.
All test history files are isolated from your normal `alert-history.json`.
