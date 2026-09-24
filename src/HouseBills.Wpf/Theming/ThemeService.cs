using System.Windows;

using HouseBills.Application.Preferences;

using Microsoft.Win32;

namespace HouseBills.Wpf.Theming;

/// <summary>
/// Switches WPF's Fluent theme between light, dark and the Windows setting, together with the app's own status colors.
/// </summary>
internal sealed class ThemeService : IThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly IUserPreferencesStore _preferences;

    public ThemeService(IUserPreferencesStore preferences)
    {
        _preferences = preferences;

        // Fluent follows a Windows light/dark switch by itself in System mode; the status colors must follow too.
        if (System.Windows.Application.Current is not null)
        {
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.General && Current == AppTheme.System)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(ApplyStatusColors);
                }
            };
        }
    }

    public AppTheme Current { get; private set; } = AppTheme.System;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var saved = (await _preferences.LoadAsync(cancellationToken)).Theme;
        Apply(Enum.TryParse<AppTheme>(saved, ignoreCase: true, out var theme) && Enum.IsDefined(theme) ? theme : AppTheme.System);
    }

    public async Task SetThemeAsync(AppTheme theme, CancellationToken cancellationToken)
    {
        if (theme == Current)
        {
            return;
        }

        Apply(theme);

        var saved = await _preferences.LoadAsync(cancellationToken);
        await _preferences.SaveAsync(saved with { Theme = theme.ToString() }, cancellationToken);
    }

    private void Apply(AppTheme theme)
    {
        Current = theme;

        // No Application in unit tests. Setting ThemeMode swaps the Fluent resources; styles that use
        // DynamicResource brushes follow, and window title bars switch too.
        if (System.Windows.Application.Current is { } application)
        {
#pragma warning disable WPF0001 // ThemeMode is marked experimental; it is the supported way to switch the Fluent theme.
            application.ThemeMode = theme switch
            {
                AppTheme.Light => ThemeMode.Light,
                AppTheme.Dark => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
#pragma warning restore WPF0001
        }

        ApplyStatusColors();
    }

    /// <summary>Windows' "app mode" setting; light if it can't be read.</summary>
    private static bool WindowsUsesDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int useLight && useLight == 0;
    }

    private void ApplyStatusColors()
    {
        if (System.Windows.Application.Current is { } application)
        {
            var dark = Current == AppTheme.Dark || (Current == AppTheme.System && WindowsUsesDarkMode());
            StatusColors.Apply(application.Resources, dark);
        }
    }
}