using DarkGreyRPG.Studio.Settings;
using System.Text.Json;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class SettingsServiceTests
{
    [TestMethod]
    public void DefaultSettingsPath_ResolvesToAppDataDarkGreyRpgStudioSettingsJson()
    {
        var service = new SettingsService();

        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DarkGreyRPG",
            "Studio",
            "settings.json");

        Assert.AreEqual(expected, service.SettingsPath);
    }

    [TestMethod]
    public void Load_WhenFileIsMissing_ReturnsDefaults()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var service = new SettingsService(settingsPath);

            var settings = service.Load();

            Assert.AreEqual(StudioSettings.CurrentSchemaVersion, settings.SchemaVersion);
            Assert.AreEqual(ThemePreference.System, settings.Theme);
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    [DataRow(ThemePreference.System)]
    [DataRow(ThemePreference.Light)]
    [DataRow(ThemePreference.Dark)]
    public void SaveAndLoad_RoundTripsTheme(ThemePreference theme)
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var service = new SettingsService(settingsPath);

            service.Save(new StudioSettings { Theme = theme });
            var loaded = service.Load();

            Assert.AreEqual(theme, loaded.Theme);
            Assert.AreEqual(StudioSettings.CurrentSchemaVersion, loaded.SchemaVersion);
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void SaveAndLoad_RoundTripsLayoutAndLastProject()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var project = Path.Combine(Path.GetDirectoryName(settingsPath)!, "project");
            var service = new SettingsService(settingsPath);
            service.Save(new StudioSettings
            {
                Theme = ThemePreference.Dark,
                WindowWidth = 1440,
                WindowHeight = 900,
                WindowMaximized = true,
                ResourceBrowserWidth = 320,
                BottomPanelHeight = 240,
                LastProject = project,
            });

            var loaded = service.Load();

            Assert.AreEqual(1440, loaded.WindowWidth);
            Assert.AreEqual(900, loaded.WindowHeight);
            Assert.IsTrue(loaded.WindowMaximized);
            Assert.AreEqual(320, loaded.ResourceBrowserWidth);
            Assert.AreEqual(240, loaded.BottomPanelHeight);
            Assert.AreEqual(Path.GetFullPath(project), loaded.LastProject);
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void SaveAndLoad_RoundTripsRecentProjects()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var directory = Path.GetDirectoryName(settingsPath)!;
            var relativeProject = Path.Combine("projects", "relative");
            var absoluteProject = Path.Combine(directory, "projects", "absolute");
            var service = new SettingsService(settingsPath);

            service.Save(new StudioSettings
            {
                RecentProjects = [relativeProject, absoluteProject],
            });

            var loaded = service.Load();

            CollectionAssert.AreEqual(
                new[] { Path.GetFullPath(relativeProject), Path.GetFullPath(absoluteProject) },
                loaded.RecentProjects.ToArray());
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Load_SchemaV1WithoutRecentProjectsUsesEmptyHistory()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{ \"schema_version\": 1, \"theme\": \"Dark\" }");

            var loaded = new SettingsService(settingsPath).Load();

            Assert.AreEqual(ThemePreference.Dark, loaded.Theme);
            CollectionAssert.AreEqual(Array.Empty<string>(), loaded.RecentProjects.ToArray());
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Load_NormalizesRecentProjectsByTrimmingResolvingDeduplicatingAndCapping()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var directory = Path.GetDirectoryName(settingsPath)!;
            var first = Path.Combine(directory, "first");
            var second = Path.Combine(directory, "second");
            var additional = Enumerable.Range(0, 10)
                .Select(index => Path.Combine(directory, $"additional-{index}"))
                .ToArray();
            var entries = new List<string?>
            {
                " ",
                null,
                $"  {first}  ",
                first.ToUpperInvariant(),
                $" {second} ",
            };
            entries.AddRange(additional);
            var json = JsonSerializer.Serialize(new
            {
                schema_version = 1,
                theme = "System",
                recent_projects = entries,
            });
            File.WriteAllText(settingsPath, json);

            var loaded = new SettingsService(settingsPath).Load();

            var expected = new[] { first, second }
                .Concat(additional.Take(8))
                .Select(Path.GetFullPath)
                .ToArray();
            CollectionAssert.AreEqual(expected, loaded.RecentProjects.ToArray());
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void SaveAndLoad_PreservesM1LayoutBoundaryValues()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var service = new SettingsService(settingsPath);
            service.Save(new StudioSettings
            {
                WindowWidth = 900,
                WindowHeight = 560,
                ResourceBrowserWidth = 180,
                BottomPanelHeight = 520,
            });

            var loaded = service.Load();

            Assert.AreEqual(900, loaded.WindowWidth);
            Assert.AreEqual(560, loaded.WindowHeight);
            Assert.AreEqual(180, loaded.ResourceBrowserWidth);
            Assert.AreEqual(520, loaded.BottomPanelHeight);
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Load_NormalizesUnsafeLayoutValuesToDefaults()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(settingsPath, """{ "schema_version": 1, "theme": "System", "window_width": 10, "window_height": 20, "resource_browser_width": 999, "bottom_panel_height": -1 }""");

            var loaded = new SettingsService(settingsPath).Load();

            Assert.AreEqual(StudioSettings.DefaultWindowWidth, loaded.WindowWidth);
            Assert.AreEqual(StudioSettings.DefaultWindowHeight, loaded.WindowHeight);
            Assert.AreEqual(StudioSettings.DefaultResourceBrowserWidth, loaded.ResourceBrowserWidth);
            Assert.AreEqual(StudioSettings.DefaultBottomPanelHeight, loaded.BottomPanelHeight);
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Save_WritesReadableJsonWithSchemaVersionAndStringTheme()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var service = new SettingsService(settingsPath);

            service.Save(new StudioSettings { Theme = ThemePreference.Dark });

            var json = File.ReadAllText(settingsPath);
            StringAssert.Contains(json, "\"schema_version\": 1");
            StringAssert.Contains(json, "\"theme\": \"Dark\"");
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Load_WhenFileContainsInvalidJson_ReturnsDefaults()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, "{ this is not valid json !!!");

            var settings = new SettingsService(settingsPath).Load();

            Assert.AreEqual(ThemePreference.System, settings.Theme);
            Assert.AreEqual(StudioSettings.CurrentSchemaVersion, settings.SchemaVersion);
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Load_WhenThemeIsNotDefined_ReturnsDefaults()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, """{ "schema_version": 1, "theme": "Neon" }""");

            var settings = new SettingsService(settingsPath).Load();

            Assert.AreEqual(ThemePreference.System, settings.Theme);
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Load_WhenSchemaVersionIsMissing_ReturnsDefaults()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{ \"theme\": \"Dark\" }");

            var settings = new SettingsService(settingsPath).Load();

            Assert.AreEqual(ThemePreference.System, settings.Theme);
            Assert.AreEqual(StudioSettings.CurrentSchemaVersion, settings.SchemaVersion);
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Load_WhenSchemaVersionIsUnsupported_ReturnsDefaults()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{ \"schema_version\": 2, \"theme\": \"Dark\" }");

            var settings = new SettingsService(settingsPath).Load();

            Assert.AreEqual(ThemePreference.System, settings.Theme);
            Assert.AreEqual(StudioSettings.CurrentSchemaVersion, settings.SchemaVersion);
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Load_WhenThemeIsNumeric_ReturnsDefaults()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            File.WriteAllText(settingsPath, "{ \"schema_version\": 1, \"theme\": 2 }");

            var settings = new SettingsService(settingsPath).Load();

            Assert.AreEqual(ThemePreference.System, settings.Theme);
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Save_CleansUpTemporaryFile()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, "stale");

            new SettingsService(settingsPath).Save(new StudioSettings { Theme = ThemePreference.Light });

            Assert.IsFalse(File.Exists(temporaryPath));
            Assert.IsTrue(File.Exists(settingsPath));
        }
        finally
        {
            DeleteTempDirectory(settingsPath);
        }
    }

    [TestMethod]
    public void Save_WhenDirectoryCannotBeCreated_ThrowsSettingsPersistenceExceptionWithReadableMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), "DarkGreyRPG.SettingsTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var blocker = Path.Combine(root, "blocker");
            File.WriteAllText(blocker, "file, not a directory");
            var settingsPath = Path.Combine(blocker, "settings.json");

            var service = new SettingsService(settingsPath);
            var exception = Assert.Throws<SettingsPersistenceException>(
                () => service.Save(new StudioSettings { Theme = ThemePreference.Dark }));

            StringAssert.Contains(exception.Message, settingsPath);
            Assert.IsNotNull(exception.InnerException);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string CreateTempSettingsPath()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "DarkGreyRPG.SettingsTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }

    private static void DeleteTempDirectory(string settingsPath)
    {
        var directory = Path.GetDirectoryName(settingsPath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
