namespace PumpTelemetry.Monitor;

/// <summary>
/// Parses the command-line option that controls startup alert history output.
/// </summary>
internal static class HistoryOptionsParser
{
    /// <summary>
    /// Parses no option as the supplied default, <c>--last=N</c> as a recent-alert limit,
    /// or <c>--all</c> as complete history.
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

        selection = new HistorySelection(ShowAll: false, Count: defaultCount);

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
            selection = new HistorySelection(ShowAll: true, Count: 0);
            return true;
        }

        const string optionPrefix = "--last=";

        if (!arguments[0].StartsWith(optionPrefix, StringComparison.Ordinal)
            || !int.TryParse(arguments[0][optionPrefix.Length..], out var count)
            || count < 0)
        {
            return false;
        }

        selection = new HistorySelection(ShowAll: false, Count: count);
        return true;
    }
}
