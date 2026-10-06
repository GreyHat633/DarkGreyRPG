using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using DarkGreyRPG.Studio.Settings;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ThemeMenuCloseoutTests
{
    public TestContext TestContext { get; set; } = null!;

    [STATestMethod]
    public void OpenPopupKeepsWindowThemeCommandsBoundAndPersistsAllOptions()
    {
        // Fluent Application and its dispatcher are process-wide. Use a fresh
        // test host for this top-level popup so preceding STA render workloads
        // cannot prevent its dispatcher from reaching Loaded.
        const string isolatedVariable = "DGR_THEME_MENU_CLOSEOUT_ISOLATED";
        if (Environment.GetEnvironmentVariable(isolatedVariable) != "1")
        {
            var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (string.IsNullOrEmpty(dotnet))
                dotnet = Path.Combine(Directory.GetParent(RuntimeEnvironment.GetRuntimeDirectory())!.Parent!.Parent!.FullName, "dotnet.exe");
            Assert.IsTrue(File.Exists(dotnet), "The selected test runtime must have its dotnet host.");
            var start = new ProcessStartInfo(dotnet) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("vstest");
            start.ArgumentList.Add(typeof(ThemeMenuCloseoutTests).Assembly.Location);
            start.ArgumentList.Add("--TestCaseFilter:FullyQualifiedName=" + typeof(ThemeMenuCloseoutTests).FullName + "." + nameof(OpenPopupKeepsWindowThemeCommandsBoundAndPersistsAllOptions));
            start.Environment[isolatedVariable] = "1";
            using var child = Process.Start(start)!;
            var output = child.StandardOutput.ReadToEndAsync();
            var error = child.StandardError.ReadToEndAsync();
            if (!child.WaitForExit(60000)) { child.Kill(entireProcessTree: true); Assert.Fail("Isolated popup test timed out."); }
            var diagnostics = output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
            TestContext.WriteLine(diagnostics);
            Assert.AreEqual(0, child.ExitCode, diagnostics);
            return;
        }
        var application = new App();
        application.InitializeComponent();
        var directory = Path.Combine(AppContext.BaseDirectory, "temp", "theme-menu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var service = new SettingsService(Path.Combine(directory, "settings.json"));
        var applied = new List<ThemePreference>();
        var theme = new ThemeSettingsViewModel(service, applied.Add);
        var window = new MainWindow(theme, service);
        try
        {
            window.Show();
            window.UpdateLayout();
            var outer = (Grid)window.Content;
            var menu = outer.Children.OfType<Grid>().Single().Children.OfType<Menu>().Single();
            var view = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "视图(_V)"));
            foreach (var (label, preference, command) in new[]
            {
                ("浅色", ThemePreference.Light, theme.UseLightThemeCommand),
                ("深色", ThemePreference.Dark, theme.UseDarkThemeCommand),
                ("跟随系统", ThemePreference.System, theme.UseSystemThemeCommand),
            })
            {
                view.IsSubmenuOpen = true;
                window.UpdateLayout();
                var item = view.Items.OfType<MenuItem>().Single(item => Equals(item.Header, label));
                Assert.AreSame(command, item.Command, "Popup binding must resolve the actual Window property.");
                Assert.IsTrue(item.Command!.CanExecute(null));
                item.Command.Execute(null);
                view.IsSubmenuOpen = false;
                Assert.AreEqual(preference, theme.SelectedTheme);
                Assert.AreEqual(preference, service.Load().Theme);
            }
            CollectionAssert.AreEqual(new[] { ThemePreference.Dark, ThemePreference.Light, ThemePreference.Dark, ThemePreference.System }, applied);
        }
        finally
        {
            window.Close();
            Directory.Delete(directory, true);
        }
    }
}
