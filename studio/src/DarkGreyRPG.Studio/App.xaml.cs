using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using DarkGreyRPG.Studio.Settings;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ICrashLogService? _crashLogService;
    private MainWindow? _mainWindow;
    private bool _globalHandlersRegistered;

    static App()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("windir")))
        {
            Environment.SetEnvironmentVariable(
                "windir",
                Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows");
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService(
            Environment.GetEnvironmentVariable("DARKGREYRPG_STUDIO_SETTINGS_PATH"));
        _crashLogService = new CrashLogService(settingsService.SettingsPath);
        RegisterGlobalExceptionHandlers();
        var themeSettings = new ThemeSettingsViewModel(settingsService, ApplyTheme);
        _mainWindow = new MainWindow(themeSettings, settingsService, _crashLogService);
        MainWindow = _mainWindow;
        _mainWindow.Show();
    }

    private void ApplyTheme(ThemePreference preference) =>
        ThemeMode = preference switch
        {
            ThemePreference.Light => System.Windows.ThemeMode.Light,
            ThemePreference.Dark => System.Windows.ThemeMode.Dark,
            _ => System.Windows.ThemeMode.System,
        };

    private void RegisterGlobalExceptionHandlers()
    {
        if (_globalHandlersRegistered)
        {
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _globalHandlersRegistered = true;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        var recoverable = ShellViewModel.IsRecoverableGlobalUiException(args.Exception);
        LogGlobalException(args.Exception, "Application.DispatcherUnhandledException");
        ReportToShell(args.Exception, "Application.DispatcherUnhandledException");
        // Do not claim to recover exceptions which may have left application
        // state inconsistent. WPF will terminate for those exceptions.
        args.Handled = recoverable;
    }

    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs args)
    {
        var exception = args.ExceptionObject as Exception
            ?? new Exception($"Unhandled exception object: {args.ExceptionObject}");
        LogGlobalException(exception, "AppDomain.CurrentDomain.UnhandledException");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        var recoverable = args.Exception.Flatten().InnerExceptions.All(ShellViewModel.IsRecoverableGlobalUiException);
        LogGlobalException(args.Exception, "TaskScheduler.UnobservedTaskException");
        if (recoverable)
        {
            args.SetObserved();
        }
    }

    private void LogGlobalException(Exception exception, string context)
    {
        try
        {
            _crashLogService?.Log(
                exception,
                context,
                _mainWindow?.GetCrashLogDetails());
        }
        catch (Exception)
        {
            // Logging must never become another unhandled exception.
        }
    }

    private void ReportToShell(Exception exception, string context)
    {
        try
        {
            _mainWindow?.Shell.ReportUnhandledUiException(exception, context, alreadyLogged: true);
        }
        catch (Exception)
        {
            // The global boundary is deliberately the final safety net.
        }
    }
}

