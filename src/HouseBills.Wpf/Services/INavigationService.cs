using HouseBills.Wpf.ViewModels;

namespace HouseBills.Wpf.Services;

/// <summary>Switches the page shown in the main window.</summary>
public interface INavigationService
{
    PageViewModel? CurrentPage { get; }

    event EventHandler? CurrentPageChanged;

    /// <summary>Shows <typeparamref name="TPage"/> and lets it load its data.</summary>
    /// <param name="prepare">Runs before the page loads, e.g. to set its filters.</param>
    Task NavigateToAsync<TPage>(Action<TPage>? prepare = null)
        where TPage : PageViewModel;
}