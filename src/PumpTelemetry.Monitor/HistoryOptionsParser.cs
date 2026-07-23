namespace PumpTelemetry.Monitor;

using PumpTelemetry.Core;

/// <summary>
/// Parses the command-line option that controls startup alert history output.
/// </summary>
internal static class HistoryOptionsParser
{
    /// <summary>
    /// Parses no option as the supplied default, <c>--last=N</c> as a recent-alert limit,
    /// <c>--all</c> as complete history, or <c>--severity=LEVEL</c> as a severity filter.
    /// </summary>
    /// <param name="arguments">The command-line arguments to parse.</param>
    /// <param name="defaultCount">The recent-alert count used when no argument is supplied.</param>
    /// <param name="selection">The parsed history selection when this method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the arguments represent a supported selection; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="arguments"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="defaultCount"/> is negative.
    /// </exception>
    public static bool TryParse(
        string[] arguments,
        int defaultCount,
        out HistorySelection selection)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentOutOfRangeException.ThrowIfNegative(defaultCount);

        selection = new HistorySelection(ShowAll: false, Count: defaultCount, Severity: null);

        if (arguments.Length == 0)
        {
            return true;
        }

        if (arguments.Length != 1)
        {
            return false;
        }

        if (string.Equals(arguments[0], "--all", StringComparison.Ordinal))
        {
            selection = new HistorySelection(ShowAll: true, Count: 0, Severity: null);
            return true;
        }

        const string severityPrefix = "--severity=";

        if (arguments[0].StartsWith(severityPrefix, StringComparison.Ordinal))
        {
            var severityText = arguments[0][severityPrefix.Length..];

            if (!Enum.TryParse<TelemetryAlertSeverity>(
                    severityText,
                    ignoreCase: true,
                    out var severity)
                || !Enum.GetNames<TelemetryAlertSeverity>()
                    .Contains(severityText, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            selection = new HistorySelection(ShowAll: false, Count: 0, Severity: severity);
            return true;
        }

        const string optionPrefix = "--last=";

        if (!arguments[0].StartsWith(optionPrefix, StringComparison.Ordinal)
            || !int.TryParse(arguments[0][optionPrefix.Length..], out var count)
            || count < 0)
        {
            return false;
        }

        selection = new HistorySelection(ShowAll: false, Count: count, Severity: null);
        return true;
    }
}
