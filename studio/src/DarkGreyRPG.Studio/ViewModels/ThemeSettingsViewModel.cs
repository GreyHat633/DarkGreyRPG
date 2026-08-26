using DarkGreyRPG.Studio.Settings;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ThemeSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly Action<ThemePreference> _applyTheme;
    private StudioSettings _settings = new();
    private ThemePreference _selectedTheme;
    private string? _persistenceError;

    public ThemeSettingsViewModel(
        ISettingsService settingsService,
        Action<ThemePreference> applyTheme)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _applyTheme = applyTheme ?? throw new ArgumentNullException(nameof(applyTheme));

        try
        {
            _settings = _settingsService.Load();
            _selectedTheme = Normalize(_settings.Theme);
        }
        catch (SettingsPersistenceException exception)
        {
            _selectedTheme = ThemePreference.System;
            PersistenceError = exception.Message;
        }

        _applyTheme(_selectedTheme);
        UseSystemThemeCommand = new RelayCommand(() => SelectedTheme = ThemePreference.System);
        UseLightThemeCommand = new RelayCommand(() => SelectedTheme = ThemePreference.Light);
        UseDarkThemeCommand = new RelayCommand(() => SelectedTheme = ThemePreference.Dark);
    }

    public IReadOnlyList<ThemePreference> ThemeOptions { get; } =
    [
        ThemePreference.System,
        ThemePreference.Light,
        ThemePreference.Dark,
    ];

    public RelayCommand UseSystemThemeCommand { get; }

    public RelayCommand UseLightThemeCommand { get; }

    public RelayCommand UseDarkThemeCommand { get; }

    public ThemePreference SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            value = Normalize(value);
            if (!SetProperty(ref _selectedTheme, value))
            {
                return;
            }

            _applyTheme(value);
            PersistTheme(value);
        }
    }

    public string? PersistenceError
    {
        get => _persistenceError;
        private set
        {
            if (SetProperty(ref _persistenceError, value))
            {
                OnPropertyChanged(nameof(HasPersistenceError));
            }
        }
    }

    public bool HasPersistenceError => !string.IsNullOrWhiteSpace(PersistenceError);

    public StudioSettings CurrentSettings => _settings;

    private static ThemePreference Normalize(ThemePreference preference) =>
        Enum.IsDefined(preference) ? preference : ThemePreference.System;

    private void PersistTheme(ThemePreference preference)
    {
        try
        {
            _settings = _settings with { Theme = preference };
            _settingsService.Save(_settings);
            PersistenceError = null;
        }
        catch (SettingsPersistenceException exception)
        {
            PersistenceError = exception.Message;
        }
    }
}
