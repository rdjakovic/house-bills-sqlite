namespace HouseBills.Wpf.Theming;

/// <summary>The color theme: which one is active, and switching between them.</summary>
public interface IThemeService
{
    AppTheme Current { get; }

    /// <summary>Applies the saved theme, or <see cref="AppTheme.System"/> if none is saved.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Switches the theme of all windows immediately and saves the choice.</summary>
    Task SetThemeAsync(AppTheme theme, CancellationToken cancellationToken);
}