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

        var paths = StudioStoragePaths.Default;
        try
        {
            paths.Initialize();
            Environment.SetEnvironmentVariable("TEMP", paths.Temp);
            Environment.SetEnvironmentVariable("TMP", paths.Temp);
        }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Studio 无法写入本地数据目录：{paths.Data}\n请将完整程序文件夹移到可写位置后重试。\n{exception.Message}",
                "Studio 本地数据目录不可用", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        var settingsService = new SettingsService(storagePaths: paths);
        _crashLogService = new CrashLogService(logsDirectory: paths.Logs);
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

