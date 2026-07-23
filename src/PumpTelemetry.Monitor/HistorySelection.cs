namespace PumpTelemetry.Monitor;

/// <summary>
/// Describes whether startup output should include all saved alerts or a limited recent count.
/// </summary>
/// <param name="ShowAll">
/// <see langword="true"/> to display every saved alert; otherwise, only <paramref name="Count"/>
/// alerts are displayed.
/// </param>
/// <param name="Count">
/// The maximum number of recent alerts to display when <paramref name="ShowAll"/> is
/// <see langword="false"/>.
/// </param>
internal sealed record HistorySelection(bool ShowAll, int Count);
