using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace DarkGreyRPG.Studio.Services;

public sealed class DgrsExportPathPicker(Func<Window?> ownerProvider) : IDgrsExportPathPicker
{
    private readonly Func<Window?> _ownerProvider = ownerProvider
        ?? throw new ArgumentNullException(nameof(ownerProvider));

    public string? PickExportPath(string storyId, string suggestedDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedDirectory);

        var fullSuggestedDirectory = Path.GetFullPath(suggestedDirectory);
        var dialog = new SaveFileDialog
        {
            Title = "导出故事包",
            Filter = "DarkGrey RPG 故事包 (*.dgrs)|*.dgrs",
            DefaultExt = ".dgrs",
            AddExtension = true,
            FileName = DarkGreyRPG.Studio.Core.Identity.DgrResourceId.PackageFileName(storyId),
            InitialDirectory = FindExistingDirectory(fullSuggestedDirectory),
            OverwritePrompt = true,
            CheckPathExists = true,
            ValidateNames = true,
        };

        var owner = _ownerProvider();
        var accepted = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        return accepted == true ? dialog.FileName : null;
    }

    private static string FindExistingDirectory(string directory)
    {
        for (var candidate = new DirectoryInfo(directory); candidate is not null; candidate = candidate.Parent)
        {
            if (candidate.Exists) return candidate.FullName;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }
}
