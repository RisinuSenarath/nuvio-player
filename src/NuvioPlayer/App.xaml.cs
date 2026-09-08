using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NuvioPlayer.Interfaces;
using NuvioPlayer.Services;
using NuvioPlayer.ViewModels;
using NuvioPlayer.Views;

namespace NuvioPlayer;

public partial class App : Application
{
    private static IServiceProvider? _serviceProvider;
    public static IServiceProvider Services => _serviceProvider ?? throw new InvalidOperationException("App services not initialized.");

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        _serviceProvider = serviceCollection.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Nuvio Player starting up...");

        var singleInstanceService = _serviceProvider.GetRequiredService<ISingleInstanceService>();
        if (!singleInstanceService.TryAcquireSingleInstance())
        {
            logger.LogInformation("Another instance is already running. Forwarding {Count} arguments...", e.Args.Length);
            await singleInstanceService.SendArgsToRunningInstanceAsync(e.Args);
            Shutdown(0);
            return;
        }

        // Start listening for arguments from subsequent instances
        singleInstanceService.StartListening(args =>
        {
            _ = Dispatcher.BeginInvoke(async () =>
            {
                if (MainWindow != null)
                {
                    if (MainWindow.WindowState == WindowState.Minimized)
                    {
                        MainWindow.WindowState = WindowState.Normal;
                    }
                    MainWindow.Activate();
                    MainWindow.Topmost = true;
                    MainWindow.Topmost = false;
                    MainWindow.Focus();
                }

                var commandLine = _serviceProvider.GetRequiredService<ICommandLineService>();
                var mediaFiles = commandLine.ParseMediaPaths(args);
                if (mediaFiles.Count > 0)
                {
                    var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
                    await mainVm.HandleMediaPathsAsync(mediaFiles);
                }
            });
        });

        // Load settings
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        await settingsService.LoadAsync();

        // Initialize SQLite Database
        var databaseService = _serviceProvider.GetRequiredService<IDatabaseService>();
        await databaseService.InitializeAsync();

        // Show MainWindow
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();

        // Process any initial command-line arguments
        if (e.Args.Length > 0)
        {
            var commandLine = _serviceProvider.GetRequiredService<ICommandLineService>();
            var initialMedia = commandLine.ParseMediaPaths(e.Args);
            if (initialMedia.Count > 0)
            {
                var mainVm = _serviceProvider.GetRequiredService<MainViewModel>();
                _ = mainVm.HandleMediaPathsAsync(initialMedia);
            }
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NuvioPlayer", "logs", "nuvio.log");

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(new NuvioPlayer.Helpers.FileLoggerProvider(logPath));
        });

        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IMediaPlayerService, VlcMediaPlayerService>();
        services.AddSingleton<IPlaylistService, PlaylistService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<ICommandLineService, CommandLineService>();
        services.AddSingleton<ISingleInstanceService, SingleInstanceService>();
        services.AddSingleton<IFileAssociationService, WindowsFileAssociationService>();

        // ViewModels
        services.AddSingleton<PlaylistViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider != null)
        {
            var logger = _serviceProvider.GetService<ILogger<App>>();
            logger?.LogInformation("Nuvio Player exiting, saving state...");

            var singleInstanceService = _serviceProvider.GetService<ISingleInstanceService>();
            singleInstanceService?.Release();

            var settingsService = _serviceProvider.GetService<ISettingsService>();
            if (settingsService != null)
            {
                await settingsService.SaveAsync();
            }

            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logger = _serviceProvider?.GetService<ILogger<App>>();
        logger?.LogError(e.Exception, "Unhandled UI exception caught");

        MessageBox.Show(
            $"An unexpected error occurred: {e.Exception.Message}\n\nNuvio Player will attempt to continue.",
            "Nuvio Player — Error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var logger = _serviceProvider?.GetService<ILogger<App>>();
        if (e.ExceptionObject is Exception ex)
        {
            logger?.LogCritical(ex, "Fatal domain unhandled exception");
        }
    }
}
