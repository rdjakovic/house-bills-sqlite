using HouseBills.Wpf.Localization;
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

    public SettingsViewModelTests()
    {
        _localization.Languages.Returns([English, Serbian]);
        _localization.Current.Returns(English);
        _themes.Current.Returns(AppTheme.System);
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

    private SettingsViewModel CreateViewModel() => new(_localization, _themes, _dialogs, NullLogger<SettingsViewModel>.Instance);
}