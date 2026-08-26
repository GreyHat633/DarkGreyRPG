using System.Configuration;
using System.Data;
using System.Windows;
using DarkGreyRPG.Studio.Settings;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
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
        var themeSettings = new ThemeSettingsViewModel(settingsService, ApplyTheme);
        var mainWindow = new MainWindow(themeSettings, settingsService);
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void ApplyTheme(ThemePreference preference) =>
        ThemeMode = preference switch
        {
            ThemePreference.Light => System.Windows.ThemeMode.Light,
            ThemePreference.Dark => System.Windows.ThemeMode.Dark,
            _ => System.Windows.ThemeMode.System,
        };
}

