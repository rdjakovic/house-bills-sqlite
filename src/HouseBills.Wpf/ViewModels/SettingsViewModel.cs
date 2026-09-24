using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using HouseBills.Application.Backups;
using HouseBills.Application.Common;
using HouseBills.Domain;
using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Platform;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.Theming;

using Microsoft.Extensions.Logging;

namespace HouseBills.Wpf.ViewModels;

/// <summary>
/// User settings. Changing the language or theme applies it immediately and saves it for the next start. Also backs up
/// and restores the data.
/// </summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly ILocalizationService _localization;
    private readonly IThemeService _themes;
    private readonly IDatabaseBackup _backup;
    private readonly IClock _clock;
    private readonly IStartupRegistration _startup;
    private readonly ILogger<SettingsViewModel> _logger;
    private bool _loadingReminderSetting;

    public SettingsViewModel(
        ILocalizationService localization,
        IThemeService themes,
        IDatabaseBackup backup,
        IClock clock,
        IStartupRegistration startup,
        IDialogService dialogs,
        ILogger<SettingsViewModel> logger)
        : base(dialogs, logger)
    {
        _localization = localization;
        _themes = themes;
        _backup = backup;
        _clock = clock;
        _startup = startup;
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

    public string BackupInfo => string.Format(LocalizedStrings.FormattingCulture, Strings.Settings_BackupInfo, _backup.AutomaticBackupsToKeep);

    public string BackupFolder => _backup.BackupFolder;

    public string RemindInfo => string.Format(LocalizedStrings.FormattingCulture, Strings.Settings_RemindInfo, Bill.DueSoonDays);

    /// <summary>Run the reminder check at Windows sign-in; mirrors the actual Windows setting.</summary>
    [ObservableProperty]
    public partial bool RemindAtSignIn { get; set; }

    public override Task OnNavigatedToAsync()
    {
        SelectedLanguage = _localization.Current;
        SelectedTheme = _themes.Current;
        LoadReminderSetting();
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
        OnPropertyChanged(nameof(BackupInfo));
        OnPropertyChanged(nameof(RemindInfo));
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

    partial void OnRemindAtSignInChanged(bool value)
    {
        if (_loadingReminderSetting)
        {
            return;
        }

        try
        {
            if (value)
            {
                _startup.Enable();
            }
            else
            {
                _startup.Disable();
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            _logger.LogError(ex, "Changing the sign-in reminder failed.");
            Dialogs.ShowError(Strings.Settings_ReminderSaveFailed);
            LoadReminderSetting();
        }
    }

    /// <summary>Shows the real state: the user can also turn the entry off in Task Manager.</summary>
    private void LoadReminderSetting()
    {
        _loadingReminderSetting = true;
        try
        {
            RemindAtSignIn = _startup.IsEnabled;
        }
        finally
        {
            _loadingReminderSetting = false;
        }
    }

    [RelayCommand]
    private async Task BackUpNowAsync(CancellationToken cancellationToken)
    {
        var suggestedName = $"HouseBills-{_clock.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.db";
        if (Dialogs.PickBackupSaveLocation(suggestedName) is not { } path)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _backup.BackupToAsync(path, cancellationToken);
            Dialogs.ShowInfo(string.Format(LocalizedStrings.FormattingCulture, Strings.Settings_BackupDone, path));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Backup on request failed.");
            Dialogs.ShowError(Strings.Settings_BackupFailed);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestoreAsync(CancellationToken cancellationToken)
    {
        if (Dialogs.PickBackupToOpen(_backup.BackupFolder) is not { } path
            || !Dialogs.Confirm(Strings.Settings_RestoreConfirmTitle, string.Format(LocalizedStrings.FormattingCulture, Strings.Settings_RestoreConfirm, System.IO.Path.GetFileName(path))))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _backup.RestoreAsync(path, cancellationToken);
            if (result.IsSuccess)
            {
                Dialogs.ShowInfo(Strings.Settings_RestoreDone);
            }
            else
            {
                Dialogs.ShowError(result.Error!.Message);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Restore failed.");
            Dialogs.ShowError(Strings.Settings_RestoreFailed);
        }
        finally
        {
            IsBusy = false;
        }
    }
}