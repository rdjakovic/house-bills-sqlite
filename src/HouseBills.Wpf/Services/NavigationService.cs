using HouseBills.Wpf.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace HouseBills.Wpf.Services;

internal sealed class NavigationService(IServiceProvider services) : INavigationService
{
    public PageViewModel? CurrentPage { get; private set; }

    public event EventHandler? CurrentPageChanged;

    public async Task NavigateToAsync<TPage>(Action<TPage>? prepare = null)
        where TPage : PageViewModel
    {
        var page = services.GetRequiredService<TPage>();
        prepare?.Invoke(page);
        CurrentPage = page;
        CurrentPageChanged?.Invoke(this, EventArgs.Empty);
        await page.OnNavigatedToAsync();
    }
}