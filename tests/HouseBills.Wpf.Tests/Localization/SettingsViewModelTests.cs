using HouseBills.Application.Backups;
using HouseBills.Application.Common;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Platform;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.Theming;
using HouseBills.Wpf.ViewModels;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace HouseBills.Wpf.Tests.Localization;

public sealed class SettingsViewModelTests
{
    private static readonly LanguageOption English = new("en", "English");
    private static readonly LanguageOption Serbian = new("sr-Latn-RS", "Srpski");

    private readonly ILocalizationService _localization = Substitute.For<ILocalizationService>();
    private readonly IThemeService _themes = Substitute.For<IThemeService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IDatabaseBackup _backup = Substitute.For<IDatabaseBackup>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IStartupRegistration _startup = Substitute.For<IStartupRegistration>();

    public SettingsViewModelTests()
    {
        _localization.Languages.Returns([English, Serbian]);
        _localization.Current.Returns(English);
        _themes.Current.Returns(AppTheme.System);
        _clock.Today.Returns(new DateOnly(2026, 9, 24));
        _backup.BackupFolder.Returns(@"C:\Backups");
    }

    [Fact]
    public void Constructor_Always_SelectsCurrentLanguageWithoutChangingIt()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedLanguage.ShouldBe(English);
        viewModel.Languages.ShouldBe([English, Serbian]);
        _localization.DidNotReceiveWithAnyArgs().SetLanguageAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SelectedLanguage_Changed_SwitchesLanguage()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedLanguage = Serbian;
        await viewModel.ChangeLanguageCommand.ExecutionTask!;

        await _localization.Received(1).SetLanguageAsync(Serbian, Arg.Any<CancellationToken>());
        _dialogs.DidNotReceiveWithAnyArgs().ShowError(default!);
    }

    [Fact]
    public async Task SelectedLanguage_SavingFails_ShowsError()
    {
        _localization.SetLanguageAsync(Serbian, Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("disk full"));
        var viewModel = CreateViewModel();

        viewModel.SelectedLanguage = Serbian;
        await viewModel.ChangeLanguageCommand.ExecutionTask!;

        _dialogs.Received(1).ShowError(Arg.Is<string>(m => !m.Contains("disk full")));
    }

    [Fact]
    public void Constructor_Always_SelectsCurrentThemeWithoutChangingIt()
    {
        _themes.Current.Returns(AppTheme.Dark);

        var viewModel = CreateViewModel();

        viewModel.SelectedTheme.ShouldBe(AppTheme.Dark);
        viewModel.Themes.ShouldBe([AppTheme.System, AppTheme.Light, AppTheme.Dark]);
        _themes.DidNotReceiveWithAnyArgs().SetThemeAsync(default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SelectedTheme_Changed_SwitchesTheme()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedTheme = AppTheme.Light;
        await viewModel.ChangeThemeCommand.ExecutionTask!;

        await _themes.Received(1).SetThemeAsync(AppTheme.Light, Arg.Any<CancellationToken>());
        _dialogs.DidNotReceiveWithAnyArgs().ShowError(default!);
    }

    [Fact]
    public async Task SelectedTheme_SavingFails_ShowsError()
    {
        _themes.SetThemeAsync(AppTheme.Dark, Arg.Any<CancellationToken>()).ThrowsAsync(new UnauthorizedAccessException("denied"));
        var viewModel = CreateViewModel();

        viewModel.SelectedTheme = AppTheme.Dark;
        await viewModel.ChangeThemeCommand.ExecutionTask!;

        _dialogs.Received(1).ShowError(Arg.Is<string>(m => !m.Contains("denied")));
    }

    [Fact]
    public async Task BackUpNow_LocationChosen_BacksUpThereAndConfirms()
    {
        _dialogs.PickBackupSaveLocation("HouseBills-2026-09-24.db").Returns(@"D:\HouseBills-2026-09-24.db");
        var viewModel = CreateViewModel();

        await viewModel.BackUpNowCommand.ExecuteAsync(null);

        await _backup.Received(1).BackupToAsync(@"D:\HouseBills-2026-09-24.db", Arg.Any<CancellationToken>());
        _dialogs.Received(1).ShowInfo(Arg.Is<string>(m => m.Contains(@"D:\HouseBills-2026-09-24.db")));
        viewModel.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task BackUpNow_DialogCancelled_DoesNothing()
    {
        _dialogs.PickBackupSaveLocation(Arg.Any<string>()).Returns((string?)null);
        var viewModel = CreateViewModel();

        await viewModel.BackUpNowCommand.ExecuteAsync(null);

        await _backup.DidNotReceiveWithAnyArgs().BackupToAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BackUpNow_WriteFails_ShowsFriendlyError()
    {
        _dialogs.PickBackupSaveLocation(Arg.Any<string>()).Returns(@"E:\backup.db");
        _backup.BackupToAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("device not ready"));
        var viewModel = CreateViewModel();

        await viewModel.BackUpNowCommand.ExecuteAsync(null);

        _dialogs.Received(1).ShowError(Arg.Is<string>(m => !m.Contains("device not ready")));
        viewModel.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task Restore_NotConfirmed_DoesNotRestore()
    {
        _dialogs.PickBackupToOpen(@"C:\Backups").Returns(@"C:\Backups\old.db");
        _dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(false);
        var viewModel = CreateViewModel();

        await viewModel.RestoreCommand.ExecuteAsync(null);

        await _backup.DidNotReceiveWithAnyArgs().RestoreAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Restore_Confirmed_RestoresAndConfirms()
    {
        _dialogs.PickBackupToOpen(@"C:\Backups").Returns(@"C:\Backups\old.db");
        _dialogs.Confirm(Arg.Any<string>(), Arg.Is<string>(m => m.Contains("old.db"))).Returns(true);
        _backup.RestoreAsync(@"C:\Backups\old.db", Arg.Any<CancellationToken>()).Returns(Result.Success());
        var viewModel = CreateViewModel();

        await viewModel.RestoreCommand.ExecuteAsync(null);

        await _backup.Received(1).RestoreAsync(@"C:\Backups\old.db", Arg.Any<CancellationToken>());
        _dialogs.Received(1).ShowInfo(Arg.Any<string>());
    }

    [Fact]
    public async Task Restore_NotABackup_ShowsServiceMessage()
    {
        _dialogs.PickBackupToOpen(Arg.Any<string>()).Returns(@"C:\notes.txt");
        _dialogs.Confirm(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _backup.RestoreAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Failure(Error.Validation("Not a backup.")));
        var viewModel = CreateViewModel();

        await viewModel.RestoreCommand.ExecuteAsync(null);

        _dialogs.Received(1).ShowError("Not a backup.");
        _dialogs.DidNotReceiveWithAnyArgs().ShowInfo(default!);
    }

    [Fact]
    public async Task OnNavigatedToAsync_ReminderEnabledInWindows_ShowsItCheckedWithoutWritingIt()
    {
        _startup.IsEnabled.Returns(true);
        var viewModel = CreateViewModel();

        await viewModel.OnNavigatedToAsync();

        viewModel.RemindAtSignIn.ShouldBeTrue();
        _startup.DidNotReceive().Enable();
        viewModel.RemindInfo.ShouldContain("7");
    }

    [Fact]
    public void RemindAtSignIn_Toggled_EnablesThenDisables()
    {
        var viewModel = CreateViewModel();

        viewModel.RemindAtSignIn = true;
        viewModel.RemindAtSignIn = false;

        Received.InOrder(() =>
        {
            _startup.Enable();
            _startup.Disable();
        });
    }

    [Fact]
    public void RemindAtSignIn_WriteFails_ShowsErrorAndShowsRealState()
    {
        _startup.When(s => s.Enable()).Do(_ => throw new UnauthorizedAccessException("denied"));
        _startup.IsEnabled.Returns(false);
        var viewModel = CreateViewModel();

        viewModel.RemindAtSignIn = true;

        _dialogs.Received(1).ShowError(Arg.Is<string>(m => !m.Contains("denied")));
        viewModel.RemindAtSignIn.ShouldBeFalse();
    }

    private SettingsViewModel CreateViewModel() => new(_localization, _themes, _backup, _clock, _startup, _dialogs, NullLogger<SettingsViewModel>.Instance);
}