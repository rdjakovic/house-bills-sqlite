using CommunityToolkit.Mvvm.Messaging;

using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.ViewModels;

using NSubstitute;

namespace HouseBills.Wpf.Tests;

public sealed class MainViewModelTests
{
    [Fact]
    public void Items_Always_HaveSettingsAsTheOnlyFooterItem()
    {
        var viewModel = new MainViewModel(Substitute.For<INavigationService>(), new StrongReferenceMessenger());

        viewModel.Items.Where(i => i.IsFooter).ShouldHaveSingleItem().Title.ShouldBe("Settings");
    }

    [Fact]
    public void Receive_LanguageChanged_RefreshesMenuTitles()
    {
        var messenger = new StrongReferenceMessenger();
        var viewModel = new MainViewModel(Substitute.For<INavigationService>(), messenger);
        var refreshed = new List<string?>();
        viewModel.Items[0].PropertyChanged += (_, e) => refreshed.Add(e.PropertyName);

        messenger.Send(new LanguageChangedMessage(new LanguageOption("sr-Latn-RS", "Srpski")));

        refreshed.ShouldBe([nameof(NavigationItem.Title)]);
    }

    [Fact]
    public void Start_Always_OpensOverviewFirst()
    {
        var navigation = Substitute.For<INavigationService>();
        var viewModel = new MainViewModel(navigation, new StrongReferenceMessenger());

        viewModel.Start();

        viewModel.SelectedItem.ShouldNotBeNull().PageType.ShouldBe(typeof(OverviewViewModel));
        navigation.Received(1).NavigateToAsync<OverviewViewModel>();
    }

    [Fact]
    public void CurrentPageChanged_PageOpenedByAnotherPage_HighlightsItsMenuEntryWithoutNavigatingAgain()
    {
        var navigation = Substitute.For<INavigationService>();
        var viewModel = new MainViewModel(navigation, new StrongReferenceMessenger());
        var settings = new SettingsViewModel(
            Substitute.For<ILocalizationService>(),
            Substitute.For<HouseBills.Wpf.Theming.IThemeService>(),
            Substitute.For<HouseBills.Application.Backups.IDatabaseBackup>(),
            Substitute.For<HouseBills.Application.Common.IClock>(),
            Substitute.For<IDialogService>(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsViewModel>.Instance);
        navigation.CurrentPage.Returns(settings);

        navigation.CurrentPageChanged += Raise.Event();

        viewModel.CurrentPage.ShouldBe(settings);
        viewModel.SelectedItem.ShouldNotBeNull().PageType.ShouldBe(typeof(SettingsViewModel));
        navigation.DidNotReceive().NavigateToAsync<SettingsViewModel>();
    }
}