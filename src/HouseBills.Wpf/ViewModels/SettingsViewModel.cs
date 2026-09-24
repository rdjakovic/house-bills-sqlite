using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.Theming;

using Microsoft.Extensions.Logging;

namespace HouseBills.Wpf.ViewModels;

/// <summary>User settings. Changing the language or theme applies it immediately and saves it for the next start.</summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly ILocalizationService _localization;
    private readonly IThemeService _themes;
    private readonly ILogger<SettingsViewModel> _logger;

    public SettingsViewModel(ILocalizationService localization, IThemeService themes, IDialogService dialogs, ILogger<SettingsViewModel> logger)
        : base(dialogs, logger)
    {
        _localization = localization;
        _themes = themes;
        _logger = logger;
        Languages = localization.Languages;
        SelectedLanguage = localization.Current;
        SelectedTheme = themes.Current;
    }

    public override string Title => Strings.Page_Settings;

    public IReadOnlyList<LanguageOption> Languages { get; }

    [ObservableProperty]
    public partial LanguageOption SelectedLanguage { get; set; }

    public IReadOnlyList<AppTheme> Themes { get; } = Enum.GetValues<AppTheme>();

    [ObservableProperty]
    public partial AppTheme SelectedTheme { get; set; }

    public override Task OnNavigatedToAsync()
    {
        SelectedLanguage = _localization.Current;
        SelectedTheme = _themes.Current;
        return Task.CompletedTask;
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        if (value != _themes.Current)
        {
            ChangeThemeCommand.Execute(value);
        }
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value != _localization.Current)
        {
            ChangeLanguageCommand.Execute(value);
        }
    }

    [RelayCommand]
    private async Task ChangeLanguageAsync(LanguageOption language, CancellationToken cancellationToken)
    {
        try
        {
            await _localization.SetLanguageAsync(language, cancellationToken);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            // The language is already applied for this session; only remembering it failed.
            _logger.LogError(ex, "Saving the language preference failed.");
            Dialogs.ShowError(Strings.Settings_SaveFailed);
        }

        OnPropertyChanged(nameof(Title));
    }

    [RelayCommand]
    private async Task ChangeThemeAsync(AppTheme theme, CancellationToken cancellationToken)
    {
        try
        {
            await _themes.SetThemeAsync(theme, cancellationToken);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            // The theme is already applied for this session; only remembering it failed.
            _logger.LogError(ex, "Saving the theme preference failed.");
            Dialogs.ShowError(Strings.Settings_SaveFailed);
        }
    }
}