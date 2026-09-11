using System.IO;
using System.Windows;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Settings;
using Microsoft.Win32;

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

    public string? PickExportPath(string storyId, string suggestedDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedDirectory);

        var fullSuggestedDirectory = ResolveInitialDirectory(
            _settingsService?.Load().LastExportDirectory,
            suggestedDirectory);
        var dialog = new SaveFileDialog
        {
            Title = "导出故事包",
            Filter = "DarkGrey RPG 故事包 (*.dgrs)|*.dgrs",
            DefaultExt = ".dgrs",
            AddExtension = true,
            FileName = DisplayFileName(storyId),
            InitialDirectory = fullSuggestedDirectory,
            OverwritePrompt = true,
            CheckPathExists = true,
            ValidateNames = true,
        };

        var owner = _ownerProvider();
        var accepted = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (accepted != true) return null;

        RememberDirectory(dialog.FileName);
        return dialog.FileName;
    }

    public static string DisplayFileName(string storyId)
    {
        if (!DgrResourceId.IsCompatibleId(storyId))
        {
            throw new ArgumentException("Story ID is not a compatible DGR resource ID.", nameof(storyId));
        }

        return storyId.Replace(':', '.') + ".dgrs";
    }

    internal static string ResolveInitialDirectory(string? rememberedDirectory, string fallbackDirectory)
    {
        if (!string.IsNullOrWhiteSpace(rememberedDirectory))
        {
            try
            {
                var remembered = Path.GetFullPath(rememberedDirectory);
                if (Directory.Exists(remembered)) return remembered;
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
        }

        foreach (var candidate in new[] { fallbackDirectory, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                var directory = new DirectoryInfo(Path.GetFullPath(candidate));
                for (var current = directory; current is not null; current = current.Parent)
                {
                    if (current.Exists) return current.FullName;
                }
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
        }

        return Environment.CurrentDirectory;
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
