using CommunityToolkit.Mvvm.Messaging;

using HouseBills.Application;
using HouseBills.Application.RecurringBills;
using HouseBills.Infrastructure;
using HouseBills.Infrastructure.Preferences;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Services;
using HouseBills.Wpf.Theming;
using HouseBills.Wpf.ViewModels;
using HouseBills.Wpf.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HouseBills.Wpf.Hosting;

/// <summary>Composition root: the only place in the UI project that knows about Infrastructure.</summary>
internal static class HostBuilderExtensions
{
    public static IHost CreateHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Logging.AddDailyFileLogging(builder.Configuration);

        builder.Services.AddApplication();
        builder.Services.AddOptions<BillingOptions>().BindConfiguration(BillingOptions.SectionName);

        // Local SQLite mode. For API mode, swap this for an Api.Client registration.
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddOptions<UserPreferencesOptions>().BindConfiguration("UserPreferences");

        builder.Services.AddPresentation();
        return builder.Build();
    }

    private static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IThemeService, ThemeService>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<BillsViewModel>();
        services.AddSingleton<RecurringBillsViewModel>();
        services.AddSingleton<PayeesViewModel>();
        services.AddSingleton<CategoriesViewModel>();
        services.AddSingleton<ReportsViewModel>();
        services.AddSingleton<SettingsViewModel>();

        services.AddSingleton<MainWindow>();
        services.AddTransient<StartupWindow>();
        return services;
    }
}