using Microsoft.Win32;

namespace HouseBills.Wpf.Platform;

/// <summary>
/// Uses the current user's Run key (no administrator rights needed). The entry appears in Task Manager's startup apps,
/// where the user can also turn it off.
/// </summary>
internal sealed class WindowsStartupRegistration : IStartupRegistration
{
    internal const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName = "HouseBills";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void Enable()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, Command);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public void RefreshIfEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key?.GetValue(ValueName) is string current && current != Command)
        {
            key.SetValue(ValueName, Command);
        }
    }

    private static string Command => $"\"{Environment.ProcessPath}\" {CommandLine.RemindArgument}";
}