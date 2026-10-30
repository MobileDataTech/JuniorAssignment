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
- `PumpTelemetry.Tests`: xUnit tests for threshold, sequence, silence, and concurrent submission behavior, plus alert history saving, loading, severity mapping, and filtering.

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

Every alert is saved to `alerts.jsonl` in the folder the monitor is started from, as it happens. When the monitor starts it loads that file and prints the saved alerts before live monitoring begins. If the file does not exist yet, the history is simply empty.

Each alert is stored as one line of JSON:

| Field | Meaning |
| --- | --- |
| `Timestamp` | When the alert was raised (UTC). |
| `Type` | `HighPressure`, `DeviceSilent`, or `OutOfOrderSequence`. |
| `Severity` | `High`, `Medium`, or `Low`. |
| `DeviceId` | The pump that raised it, e.g. `pump-03`. |
| `Message` | The alert text. |
| `Pressure` | The pressure reading that triggered it. `null` for alerts with no reading, such as a silent device. |

Severity is not part of the original alert, so it is derived from the alert type:

| Alert type | Severity | Reasoning |
| --- | --- | --- |
| `HighPressure` | High | A measured, confirmed hazard. |
| `DeviceSilent` | Medium | Visibility is lost, but nothing is confirmed wrong. |
| `OutOfOrderSequence` | Low | The data arrived; only its ordering is suspect. |

### Filtering history

Pass filters after `--` to narrow what is printed at startup. With no filters, the whole history is shown. Filters can be combined and given in any order.

| Flag | Meaning |
| --- | --- |
| `--severity=low\|medium\|high` | Only alerts of that severity. |
| `--last=N` | Only the `N` most recent alerts that passed the other filters. |
| `--since=<date/time>` | Only alerts at or after this time. |
| `--until=<date/time>` | Only alerts at or before this time. |

```powershell
dotnet run --project src/PumpTelemetry.Monitor -- --severity=high
dotnet run --project src/PumpTelemetry.Monitor -- --severity=high --last=5
dotnet run --project src/PumpTelemetry.Monitor -- --since="2026-10-30 14:00" --until="2026-10-30 16:30"
```

The first line printed shows how many alerts were loaded and which filters were applied, for example:

```
Loaded 160 saved alert(s). Showing 2 (all severities, since 2026-10-30 22:00, until 2026-10-30 22:20, last 2):
```

About times:

- `--since` and `--until` are read as UTC, the same clock the alerts are shown in. Both ends are inclusive.
- A date with no time means 00:00 on that date. `--until=2026-10-30` therefore stops at the start of Oct 30. To include the whole day, use `--until=2026-10-31` or `--until="2026-10-30 23:59:59"`.
- An invalid value prints a message and that filter is ignored. The same applies to `--severity=banana` or `--last=0`.

### Where the code lives

- `AlertRecord` / `AlertSeverity` (Core): the saved shape of an alert and the severity mapping.
- `AlertHistoryStore` (Core): saves (`AppendAsync`) and loads (`LoadAsync`) the file.
- `AlertHistoryFilter` (Core): `ApplyFilters`, which applies severity, time range, then `last`.
- `Program.cs` (Monitor): reads the flags, loads and prints the history, and saves alerts from the existing alert callback.

`TelemetryPipeline` itself is unchanged.

### Assumptions

- Alerts are stored as JSON Lines (one JSON object per line) so saving is a cheap append and one damaged line cannot lose the rest of the history. Unreadable lines are skipped when loading.
- The value field is the pressure reading. The threshold is not stored separately; it appears in the message text.
- If the file cannot be written (for example, it is locked), a message is printed and alert processing continues.
- The history file grows without limit; there is no rotation. Printing everything at startup gets long on a large file, so use `--last=N`.
- Saves are serialized inside one running monitor. Two monitors writing the same file is not supported.
