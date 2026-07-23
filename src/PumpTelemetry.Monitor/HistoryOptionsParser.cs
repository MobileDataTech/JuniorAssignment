namespace PumpTelemetry.Monitor;

using PumpTelemetry.Core;
using System.Globalization;

/// <summary>
/// Parses the command-line option that controls startup alert history output.
/// </summary>
internal static class HistoryOptionsParser
{
    /// <summary>
    /// Parses no option as the supplied default, <c>--last=N</c> as a recent-alert limit,
    /// <c>--all</c> as complete history, <c>--severity=LEVEL</c> as a severity filter,
    /// or <c>--from=TIMESTAMP</c> and <c>--to=TIMESTAMP</c> as an inclusive time range.
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

        selection = new HistorySelection(
            ShowAll: false,
            Count: defaultCount,
            Severity: null,
            From: null,
            To: null);

        if (arguments.Length == 0)
        {
            return true;
        }

        if (arguments.Any(IsTimeRangeOption))
        {
            return TryParseTimeRange(arguments, out selection);
        }

        if (arguments.Length != 1)
        {
            return false;
        }

        if (string.Equals(arguments[0], "--all", StringComparison.Ordinal))
        {
            selection = new HistorySelection(
                ShowAll: true,
                Count: 0,
                Severity: null,
                From: null,
                To: null);
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

            selection = new HistorySelection(
                ShowAll: false,
                Count: 0,
                Severity: severity,
                From: null,
                To: null);
            return true;
        }

        const string optionPrefix = "--last=";

        if (!arguments[0].StartsWith(optionPrefix, StringComparison.Ordinal)
            || !int.TryParse(arguments[0][optionPrefix.Length..], out var count)
            || count < 0)
        {
            return false;
        }

        selection = new HistorySelection(
            ShowAll: false,
            Count: count,
            Severity: null,
            From: null,
            To: null);
        return true;
    }

    /// <summary>
    /// Parses one or both time-range bounds and rejects duplicates, unrelated options,
    /// malformed timestamps, and reversed ranges.
    /// </summary>
    private static bool TryParseTimeRange(
        string[] arguments,
        out HistorySelection selection)
    {
        selection = new HistorySelection(
            ShowAll: false,
            Count: 0,
            Severity: null,
            From: null,
            To: null);

        if (arguments.Length is < 1 or > 2)
        {
            return false;
        }

        DateTimeOffset? from = null;
        DateTimeOffset? to = null;

        foreach (var argument in arguments)
        {
            if (argument.StartsWith("--from=", StringComparison.Ordinal))
            {
                if (from.HasValue || !TryParseTimestamp(argument["--from=".Length..], out var value))
                {
                    return false;
                }

                from = value;
            }
            else if (argument.StartsWith("--to=", StringComparison.Ordinal))
            {
                if (to.HasValue || !TryParseTimestamp(argument["--to=".Length..], out var value))
                {
                    return false;
                }

                to = value;
            }
            else
            {
                return false;
            }
        }

        if (from > to)
        {
            return false;
        }

        selection = new HistorySelection(
            ShowAll: false,
            Count: 0,
            Severity: null,
            From: from,
            To: to);
        return true;
    }

    /// <summary>
    /// Determines whether an argument starts with a supported time-range option name.
    /// </summary>
    private static bool IsTimeRangeOption(string argument)
    {
        return argument.StartsWith("--from=", StringComparison.Ordinal)
            || argument.StartsWith("--to=", StringComparison.Ordinal);
    }

    /// <summary>
    /// Parses a timestamp using invariant culture and normalizes timestamps without an explicit
    /// offset as UTC.
    /// </summary>
    private static bool TryParseTimestamp(string value, out DateTimeOffset timestamp)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out timestamp);
    }
}
