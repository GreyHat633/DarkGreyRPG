using DarkGreyRPG.Studio.Settings;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ThemeSettingsViewModelTests
{
    [TestMethod]
    public void Constructor_LoadsAndAppliesSavedThemeWithoutWritingSettings()
    {
        var service = new RecordingSettingsService(ThemePreference.Dark);
        var applied = new List<ThemePreference>();

        var viewModel = new ThemeSettingsViewModel(service, applied.Add);

        Assert.AreEqual(ThemePreference.Dark, viewModel.SelectedTheme);
        CollectionAssert.AreEqual(new[] { ThemePreference.Dark }, applied);
        Assert.AreEqual(0, service.SaveCount);
        Assert.IsFalse(viewModel.HasPersistenceError);
    }

    [TestMethod]
    public void SelectingTheme_AppliesAndPersistsImmediately()
    {
        var service = new RecordingSettingsService(ThemePreference.System);
        var applied = new List<ThemePreference>();
        var viewModel = new ThemeSettingsViewModel(service, applied.Add);

        viewModel.SelectedTheme = ThemePreference.Light;
        viewModel.SelectedTheme = ThemePreference.Dark;

        CollectionAssert.AreEqual(
            new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark },
            applied);
        Assert.AreEqual(2, service.SaveCount);
        Assert.AreEqual(ThemePreference.Dark, service.LastSaved?.Theme);
    }

    [TestMethod]
    public void SelectingTheme_PreservesExistingLayoutSettings()
    {
        var initial = new StudioSettings { Theme = ThemePreference.System, WindowWidth = 1440, LastProject = @"E:\Projects\Rpg" };
        var service = new RecordingSettingsService(initial);
        var viewModel = new ThemeSettingsViewModel(service, _ => { });

        viewModel.SelectedTheme = ThemePreference.Dark;

        Assert.AreEqual(1440, service.LastSaved?.WindowWidth);
        Assert.AreEqual(initial.LastProject, service.LastSaved?.LastProject);
    }

    [TestMethod]
    public void SelectingCurrentTheme_DoesNotApplyOrSaveAgain()
    {
        var service = new RecordingSettingsService(ThemePreference.Light);
        var applied = new List<ThemePreference>();
        var viewModel = new ThemeSettingsViewModel(service, applied.Add);

        viewModel.SelectedTheme = ThemePreference.Light;

        CollectionAssert.AreEqual(new[] { ThemePreference.Light }, applied);
        Assert.AreEqual(0, service.SaveCount);
    }

    [TestMethod]
    public void SaveFailure_KeepsImmediateThemeAndExposesReadableError()
    {
        var service = new RecordingSettingsService(ThemePreference.System)
        {
            SaveException = CreatePersistenceException("Unable to save theme settings."),
        };
        var applied = new List<ThemePreference>();
        var viewModel = new ThemeSettingsViewModel(service, applied.Add);

        viewModel.SelectedTheme = ThemePreference.Dark;

        Assert.AreEqual(ThemePreference.Dark, viewModel.SelectedTheme);
        CollectionAssert.Contains(applied, ThemePreference.Dark);
        Assert.IsTrue(viewModel.HasPersistenceError);
        StringAssert.Contains(viewModel.PersistenceError, "Unable to save theme settings.");
    }

    [TestMethod]
    public void LoadFailure_UsesSystemThemeAndExposesReadableError()
    {
        var service = new RecordingSettingsService(ThemePreference.Dark)
        {
            LoadException = CreatePersistenceException("Unable to load theme settings."),
        };
        var applied = new List<ThemePreference>();

        var viewModel = new ThemeSettingsViewModel(service, applied.Add);

        Assert.AreEqual(ThemePreference.System, viewModel.SelectedTheme);
        CollectionAssert.AreEqual(new[] { ThemePreference.System }, applied);
        Assert.IsTrue(viewModel.HasPersistenceError);
        StringAssert.Contains(viewModel.PersistenceError, "Unable to load theme settings.");
    }

    [TestMethod]
    public void ThemeOptions_ContainsSystemLightAndDarkInDisplayOrder()
    {
        var viewModel = new ThemeSettingsViewModel(
            new RecordingSettingsService(ThemePreference.System),
            _ => { });

        CollectionAssert.AreEqual(
            new[] { ThemePreference.System, ThemePreference.Light, ThemePreference.Dark },
            viewModel.ThemeOptions.ToArray());
    }

    [TestMethod]
    public void ThemeCommandsApplyAndPersistTheirRequestedTheme()
    {
        var service = new RecordingSettingsService(ThemePreference.System);
        var viewModel = new ThemeSettingsViewModel(service, _ => { });

        viewModel.UseDarkThemeCommand.Execute(null);
        Assert.AreEqual(ThemePreference.Dark, viewModel.SelectedTheme);
        viewModel.UseLightThemeCommand.Execute(null);
        Assert.AreEqual(ThemePreference.Light, viewModel.SelectedTheme);
        viewModel.UseSystemThemeCommand.Execute(null);

        Assert.AreEqual(ThemePreference.System, viewModel.SelectedTheme);
        Assert.AreEqual(3, service.SaveCount);
    }

    private static SettingsPersistenceException CreatePersistenceException(string message) =>
        new(message, new IOException("Test failure."));

    private sealed class RecordingSettingsService : ISettingsService
    {
        private readonly StudioSettings _initialSettings;

        public RecordingSettingsService(ThemePreference initialTheme)
            : this(new StudioSettings { Theme = initialTheme })
        {
        }

        public RecordingSettingsService(StudioSettings initialSettings)
        {
            _initialSettings = initialSettings;
        }

        public string SettingsPath => "test-settings.json";

        public int SaveCount { get; private set; }

        public StudioSettings? LastSaved { get; private set; }

        public SettingsPersistenceException? LoadException { get; init; }

        public SettingsPersistenceException? SaveException { get; init; }

        public StudioSettings Load() =>
            LoadException is null
                ? _initialSettings
                : throw LoadException;

        public void Save(StudioSettings settings)
        {
            SaveCount++;
            if (SaveException is not null)
            {
                throw SaveException;
            }

            LastSaved = settings;
        }
    }
}
