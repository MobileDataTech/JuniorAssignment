namespace PumpTelemetry.Monitor;

using PumpTelemetry.Core;

/// <summary>
/// Describes whether startup output should include all alerts, one severity, or a recent count.
/// </summary>
/// <param name="ShowAll">
/// <see langword="true"/> to display every saved alert; otherwise, only <paramref name="Count"/>
/// alerts are displayed.
/// </param>
/// <param name="Count">
/// The maximum number of recent alerts to display when <paramref name="ShowAll"/> is
/// <see langword="false"/>.
/// </param>
/// <param name="Severity">
/// The severity to display, or <see langword="null"/> when history is not filtered by severity.
/// </param>
internal sealed record HistorySelection(
    bool ShowAll,
    int Count,
    TelemetryAlertSeverity? Severity);
