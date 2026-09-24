using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Services;

namespace HouseBills.Wpf.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IRecipient<LanguageChangedMessage>
{
    private readonly INavigationService _navigation;
    private bool _followingNavigation;

    public MainViewModel(INavigationService navigation, IMessenger messenger)
    {
        _navigation = navigation;
        _navigation.CurrentPageChanged += (_, _) => OnCurrentPageChanged();
        Items =
        [
            NavigationItem.For<OverviewViewModel>(() => Strings.Page_Overview),
            NavigationItem.For<BillsViewModel>(() => Strings.Page_Bills),
            NavigationItem.For<RecurringBillsViewModel>(() => Strings.Page_RecurringBills),
            NavigationItem.For<PayeesViewModel>(() => Strings.Page_Payees),
            NavigationItem.For<CategoriesViewModel>(() => Strings.Page_Categories),
            NavigationItem.For<ReportsViewModel>(() => Strings.Page_Reports),
            NavigationItem.For<SettingsViewModel>(() => Strings.Page_Settings, isFooter: true),
        ];
        messenger.RegisterAll(this);
    }

    public IReadOnlyList<NavigationItem> Items { get; }

    [ObservableProperty]
    public partial NavigationItem? SelectedItem { get; set; }

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; set; }

    /// <summary>Opens the first page. Called once the window is shown.</summary>
    public void Start()
    {
        SelectedItem ??= Items[0];
    }

    public void Receive(LanguageChangedMessage message)
    {
        foreach (var item in Items)
        {
            item.RefreshTitle();
        }
    }

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value is not null && !_followingNavigation)
        {
            NavigateCommand.Execute(value);
        }
    }

    /// <summary>
    /// Shows the new page and, when a page opened another one (e.g. Overview → Bills), highlights its menu entry
    /// without navigating again.
    /// </summary>
    private void OnCurrentPageChanged()
    {
        CurrentPage = _navigation.CurrentPage;
        var item = Items.FirstOrDefault(i => i.PageType == CurrentPage?.GetType());
        if (item is null || item == SelectedItem)
        {
            return;
        }

        _followingNavigation = true;
        try
        {
            SelectedItem = item;
        }
        finally
        {
            _followingNavigation = false;
        }
    }

    [RelayCommand]
    private Task NavigateAsync(NavigationItem item) => item.NavigateAsync(_navigation);
}