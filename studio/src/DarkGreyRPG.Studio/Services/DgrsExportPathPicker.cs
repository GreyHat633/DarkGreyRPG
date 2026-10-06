using System.IO;
using System.Windows;
using DarkGreyRPG.Studio.Settings;

namespace DarkGreyRPG.Studio.Services;

public sealed class DgrsExportPathPicker : IDgrsExportPathPicker
{
    private readonly Func<Window?> _ownerProvider;
    private readonly ISettingsService? _settingsService;

    public DgrsExportPathPicker(Func<Window?> ownerProvider, ISettingsService? settingsService = null)
    {
        _ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
        _settingsService = settingsService;
    }

    public string? PickExportPath(string displayName, string suggestedDirectory)
        => PickPath(DisplayFileName(displayName), suggestedDirectory, false);

    public string? PickGroupExportPath(string displayName, string suggestedDirectory)
    {
        return PickPath(DisplayFileName(displayName, group: true), suggestedDirectory, true);
    }

    private string? PickPath(string fileName, string suggestedDirectory, bool group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedDirectory);

        var fullSuggestedDirectory = ResolveInitialDirectory(
            _settingsService?.Load().LastExportDirectory,
            suggestedDirectory);
        var owner = _ownerProvider();
        while (true)
        {
            var directory = FixedNameExportDialog.SelectDirectory(owner, fileName, fullSuggestedDirectory,
                $"{(group ? "导出完整故事组" : "导出故事包")}：{fileName} — 选择导出位置");
            if (directory is null) return null;
            var target = Path.Combine(directory, fileName);
            if (File.Exists(target))
            {
                var message = $"文件已存在：{target}\n是否替换？";
                var answer = owner is null
                    ? MessageBox.Show(message, "确认替换", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
                    : MessageBox.Show(owner, message, "确认替换", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes)
                {
                    fullSuggestedDirectory = directory;
                    continue;
                }
            }
            RememberDirectory(target);
            return target;
        }
    }

    public static string DisplayFileName(string displayName, bool group = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var invalid = Path.GetInvalidFileNameChars();
        var stem = string.Concat(displayName.Select(character => invalid.Contains(character) ? '_' : character)).Trim().TrimEnd('.', ' ');
        if (stem.Length == 0) stem = group ? "故事组" : "故事";
        // Windows reserves device names even when a filename has an extension.
        var firstPart = stem.Split('.')[0].TrimEnd(' ');
        if (firstPart.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || firstPart.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || firstPart.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || firstPart.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (firstPart.Length == 4 && (firstPart.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                || firstPart.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && "123456789¹²³".Contains(firstPart[3]))) stem = "_" + stem;
        if (stem.Length > 240)
        {
            stem = stem[..(char.IsHighSurrogate(stem[239]) ? 239 : 240)].TrimEnd('.', ' ');
        }
        return stem + (group ? ".dgrs.g" : ".dgrs");
    }

    internal static string ResolveInitialDirectory(string? rememberedDirectory, string fallbackDirectory)
    {
        if (!string.IsNullOrWhiteSpace(rememberedDirectory))
        {
            try
            {
                var remembered = ResolveMovedDirectory(rememberedDirectory);
                if (Directory.Exists(remembered)) return remembered;
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
        }

        foreach (var candidate in new[] { fallbackDirectory, StudioStoragePaths.Default.Exports })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                var directory = new DirectoryInfo(ResolveMovedDirectory(candidate));
                for (var current = directory; current is not null; current = current.Parent)
                {
                    if (current.Exists) return current.FullName;
                }
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
        }

        return StudioStoragePaths.Default.Root;
    }

    private static string ResolveMovedDirectory(string path)
    {
        var original = Path.GetFullPath(path);
        if (Directory.Exists(original)) return original;
        var parts = original.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            parts[i] = parts[i] switch
            {
                "DarkGrey_RPG" => "DarkGreyRPG",
                "darkgrey_rpg_story_packages" => Path.Combine("DarkGreyRPG", "StoryPackages"),
                "darkgrey_rpg_project" => Path.Combine("DarkGreyRPG", "Project"),
                "darkgrey_rpg_media_cache" => Path.Combine("DarkGreyRPG", "Cache"),
                _ => parts[i],
            };
            var moved = string.Join(Path.DirectorySeparatorChar, parts);
            if (Directory.Exists(moved)) return moved;
        }
        return original;
    }

    private void RememberDirectory(string selectedPath)
    {
        if (_settingsService is null) return;
        var directory = Path.GetDirectoryName(Path.GetFullPath(selectedPath));
        if (string.IsNullOrWhiteSpace(directory)) return;

        try
        {
            var latest = _settingsService.Load();
            _settingsService.Save(latest with { LastExportDirectory = directory });
        }
        catch (SettingsPersistenceException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
