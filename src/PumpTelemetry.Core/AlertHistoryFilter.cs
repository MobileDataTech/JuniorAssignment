namespace PumpTelemetry.Core;

public sealed record AlertHistoryFilter(string? AlertType = null, int? Last = null);
