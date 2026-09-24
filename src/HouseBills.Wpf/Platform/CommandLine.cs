namespace HouseBills.Wpf.Platform;

/// <summary>Command-line arguments HouseBills handles itself (they are not passed on to configuration).</summary>
public static class CommandLine
{
    /// <summary>Check for bills that need attention, show a notification if any, and exit without opening a window.</summary>
    public const string RemindArgument = "--remind";

    /// <summary>The link a reminder notification opens (<c>housebills:open</c>); it just starts the app.</summary>
    public const string ProtocolScheme = "housebills";

    public static bool IsReminderCheck(IEnumerable<string> args) =>
        args.Any(a => string.Equals(a, RemindArgument, StringComparison.OrdinalIgnoreCase));

    /// <summary>The arguments meant for configuration: without the ones above.</summary>
    public static string[] ConfigurationArguments(IEnumerable<string> args) =>
        args.Where(a => !string.Equals(a, RemindArgument, StringComparison.OrdinalIgnoreCase)
                        && !a.StartsWith(ProtocolScheme + ":", StringComparison.OrdinalIgnoreCase))
            .ToArray();
}