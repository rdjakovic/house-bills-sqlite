using System.Windows;

using HouseBills.Application.Preferences;

namespace HouseBills.Wpf.Theming;

/// <summary>Switches WPF's Fluent theme between light, dark and the Windows setting.</summary>
internal sealed class ThemeService(IUserPreferencesStore preferences) : IThemeService
{
    public AppTheme Current { get; private set; } = AppTheme.System;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var saved = (await preferences.LoadAsync(cancellationToken)).Theme;
        Apply(Enum.TryParse<AppTheme>(saved, ignoreCase: true, out var theme) && Enum.IsDefined(theme) ? theme : AppTheme.System);
    }

    public async Task SetThemeAsync(AppTheme theme, CancellationToken cancellationToken)
    {
        if (theme == Current)
        {
            return;
        }

        Apply(theme);

        var saved = await preferences.LoadAsync(cancellationToken);
        await preferences.SaveAsync(saved with { Theme = theme.ToString() }, cancellationToken);
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
    }
}