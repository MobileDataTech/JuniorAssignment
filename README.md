# Pump Telemetry Monitor

A small C#/.NET application that simulates telemetry from multiple industrial pump devices and detects abnormal operating conditions.

## How to run

Requires the .NET 8 SDK or later.

```powershell
dotnet run --project src/PumpTelemetry.Monitor
```

Press `Ctrl+C` to stop the monitor gracefully.

## Alert History Dashboard

The monitor persists generated alerts to a local JSON file named `alert-history.json`.

When the monitor starts, it loads saved alert history from this file and displays matching history records in the console before live monitoring begins. New alerts generated during the current run are also appended to the same history file.

Each saved alert record includes:

- timestamp
- device or pump ID
- alert type
- severity
- message
- sensor value when available

Severity is assigned from the alert type:

- `HighPressure` -> `High`
- `DeviceSilent` -> `High`
- `OutOfOrderSequence` -> `Medium`

`DeviceSilent` alerts do not include a sensor value because there is no current telemetry reading when the device 
is silent.

## How to test

```powershell
dotnet test
```

## Alert History Filters

The monitor supports simple command-line filters for the startup history display.

Show only the last 5 saved alerts:

```powershell
dotnet run --project src/PumpTelemetry.Monitor -- --last=5
```

Show only high severity alerts:

```powershell
dotnet run --project src/PumpTelemetry.Monitor -- --severity=High
```

Show alerts from the last 24 hours:

```powershell
dotnet run --project src/PumpTelemetry.Monitor -- --since-hours=24
```

Filters can also be combined:

```powershell
dotnet run --project src/PumpTelemetry.Monitor -- --severity=High --last=10
```

The filters only affect what is displayed in the console at startup. They do not delete older alerts from `alert-history.json`.

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
