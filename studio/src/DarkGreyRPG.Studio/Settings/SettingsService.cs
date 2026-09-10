using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

namespace DarkGreyRPG.Studio.Settings;

/// <summary>
/// Persists Studio preferences independently of the RPG project files.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private const string SettingsDirectoryName = "DarkGreyRPG";
    private const string StudioDirectoryName = "Studio";
    private const string SettingsFileName = "settings.json";

    public SettingsService(string? settingsPath = null)
    {
        SettingsPath = string.IsNullOrWhiteSpace(settingsPath)
            ? GetDefaultSettingsPath()
            : settingsPath;
    }

    public string SettingsPath { get; }

    public StudioSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return CreateDefaultSettings();
        }

        try
        {
            using var stream = File.OpenRead(SettingsPath);
            using var document = JsonDocument.Parse(stream);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("schema_version", out var schemaVersion)
                || schemaVersion.ValueKind != JsonValueKind.Number
                || !schemaVersion.TryGetInt32(out var version)
                || version != StudioSettings.CurrentSchemaVersion)
            {
                return CreateDefaultSettings();
            }

            var settings = document.RootElement.Deserialize<StudioSettings>(SerializerOptions);
            if (settings is null || !IsSupportedTheme(settings.Theme))
            {
                return CreateDefaultSettings();
            }

            return Normalize(settings);
        }
        catch (Exception exception) when (IsRecoverableLoadFailure(exception))
        {
            return CreateDefaultSettings();
        }
    }

    public void Save(StudioSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.GlobalNamespace is not null
            && !DarkGreyRPG.Studio.Core.Identity.DgrResourceId.IsValidNamespace(settings.GlobalNamespace))
            throw new ArgumentException("Global Namespace is invalid; it must be preserved exactly without normalization.", nameof(settings));

        if (!IsSupportedTheme(settings.Theme))
        {
            throw new ArgumentOutOfRangeException(nameof(settings), settings.Theme, "The theme preference is not supported.");
        }

        var temporaryPath = SettingsPath + ".tmp";

        try
        {
            var fullSettingsPath = Path.GetFullPath(SettingsPath);
            var directory = Path.GetDirectoryName(fullSettingsPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new IOException("The settings path has no parent directory.");
            }

            Directory.CreateDirectory(directory);

            // A previous interrupted write must not become the next saved file.
            DeleteTemporaryFileIfPresent(temporaryPath);

            var persistedSettings = Normalize(settings);
            var json = JsonSerializer.Serialize(persistedSettings, SerializerOptions);
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // The temporary file is in the same directory, so replacement is atomic
            // from the point of view of readers on the same volume.
            File.Move(temporaryPath, SettingsPath, overwrite: true);
        }
        catch (Exception exception)
        {
            throw new SettingsPersistenceException(
                $"Failed to save Studio settings to '{SettingsPath}'. {exception.Message}",
                exception);
        }
        finally
        {
            DeleteTemporaryFileIfPresent(temporaryPath);
        }
    }

    private static string GetDefaultSettingsPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            SettingsDirectoryName,
            StudioDirectoryName,
            SettingsFileName);
    }

    private static StudioSettings CreateDefaultSettings() => new()
    {
        SchemaVersion = StudioSettings.CurrentSchemaVersion,
        Theme = ThemePreference.System,
    };

    private static StudioSettings Normalize(StudioSettings settings) => settings with
    {
        SchemaVersion = StudioSettings.CurrentSchemaVersion,
        WindowWidth = NormalizeDimension(settings.WindowWidth, 900, 7680, StudioSettings.DefaultWindowWidth),
        WindowHeight = NormalizeDimension(settings.WindowHeight, 560, 4320, StudioSettings.DefaultWindowHeight),
        ResourceBrowserWidth = NormalizeDimension(settings.ResourceBrowserWidth, 180, 400, StudioSettings.DefaultResourceBrowserWidth),
        StoryResourceLibraryWidth = NormalizeDimension(settings.StoryResourceLibraryWidth, StudioSettings.StoryResourceLibraryMinWidth, StudioSettings.StoryResourceLibraryMaxWidth, StudioSettings.DefaultStoryResourceLibraryWidth),
        BottomPanelHeight = NormalizeDimension(settings.BottomPanelHeight, 120, 520, StudioSettings.DefaultBottomPanelHeight),
        LastProject = string.IsNullOrWhiteSpace(settings.LastProject)
            ? null
            : Path.GetFullPath(settings.LastProject.Trim()),
        RecentProjects = NormalizeRecentProjects(settings.RecentProjects),
    };

    private static IReadOnlyList<string> NormalizeRecentProjects(IEnumerable<string>? recentProjects)
    {
        if (recentProjects is null)
        {
            return Array.Empty<string>();
        }

        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var projectPath in recentProjects)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(projectPath.Trim());
            }
            catch (ArgumentException)
            {
                continue;
            }
            catch (NotSupportedException)
            {
                continue;
            }

            if (!seen.Add(fullPath))
            {
                continue;
            }

            normalized.Add(fullPath);
            if (normalized.Count == 10)
            {
                break;
            }
        }

        return normalized;
    }

    private static double NormalizeDimension(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) && value >= minimum && value <= maximum ? value : fallback;

    private static bool IsSupportedTheme(ThemePreference theme)
    {
        return theme is ThemePreference.System or ThemePreference.Light or ThemePreference.Dark;
    }

    private static bool IsRecoverableLoadFailure(Exception exception)
    {
        return exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or NotSupportedException
            or InvalidDataException;
    }

    private static void DeleteTemporaryFileIfPresent(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (IOException)
        {
            // Cleanup must not hide the original save result.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup must not hide the original save result.
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = false,
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}
