using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

using HouseBills.Application.Common;
using HouseBills.Presentation.Resources;
using HouseBills.Wpf.Hosting;
using HouseBills.Wpf.Localization;
using HouseBills.Wpf.Theming;
using HouseBills.Wpf.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HouseBills.Wpf;

public partial class App : System.Windows.Application
{
    /// <summary>Quick starts finish before this, so the startup window doesn't flash.</summary>
    private static readonly TimeSpan StartupWindowDelay = TimeSpan.FromMilliseconds(700);

    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterGlobalExceptionHandlers();

        try
        {
            _host = HostBuilderExtensions.CreateHost(e.Args);
            await _host.StartAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or Microsoft.Extensions.Options.OptionsValidationException)
        {
            // The host (and therefore logging) is not available; report configuration errors directly.
            MessageBox.Show(
                $"{Strings.Error_ConfigInvalid}{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "HouseBills",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // Before any window is shown, so every window (including the startup window) uses the chosen language.
        var localization = _host.Services.GetRequiredService<ILocalizationService>();
        await localization.InitializeAsync(CancellationToken.None);
        DatePickerWatermark.Register();
        await _host.Services.GetRequiredService<IThemeService>().InitializeAsync(CancellationToken.None);

        // Bindings (dates, number input) format and parse with the chosen language's formats instead of WPF's en-US
        // default. This default can be set only once; later language changes update open windows directly.
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(localization.FormattingCulture.IetfLanguageTag)));

        var startupWindow = await InitializeDatabaseAsync(_host.Services);

        var window = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = window;
        window.Show();

        // Close only after the main window is shown: WPF makes the first window shown the MainWindow, and closing
        // the MainWindow would end the application (ShutdownMode=OnMainWindowClose).
        startupWindow?.Close();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Creates/upgrades the private SQLite database before the first page loads. If that takes longer than
    /// <see cref="StartupWindowDelay"/>, a "Starting HouseBills…" window is shown and returned so the caller closes it
    /// once the main window is up. On failure the main window still opens (pages then report the problem).
    /// </summary>
    private async Task<StartupWindow?> InitializeDatabaseAsync(IServiceProvider services)
    {
        // Off the UI thread: parts of it are synchronous (SQLite file I/O, EF model building) and would
        // otherwise keep the startup window from rendering.
        var initializer = services.GetRequiredService<IDatabaseInitializer>();
        var initialization = Task.Run(() => initializer.InitializeAsync(CancellationToken.None));
        StartupWindow? startupWindow = null;
        try
        {
            if (await Task.WhenAny(initialization, Task.Delay(StartupWindowDelay)) != initialization)
            {
                startupWindow = services.GetRequiredService<StartupWindow>();
                startupWindow.Show();
            }

            await initialization;
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Database initialization failed.");
            startupWindow?.Hide();
            MessageBox.Show(
                Strings.Error_DatabasePrepareFailed,
                "HouseBills",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        return startupWindow;
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger?.LogError(e.Exception, "Unhandled exception on the UI thread.");
        MessageBox.Show(Strings.Error_Unexpected, "HouseBills", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger?.LogError(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Logger?.LogCritical(e.ExceptionObject as Exception, "Unhandled exception; the application will terminate.");
        MessageBox.Show(Strings.Error_Unexpected, "HouseBills", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private ILogger? Logger => _host?.Services.GetService<ILogger<App>>();
}