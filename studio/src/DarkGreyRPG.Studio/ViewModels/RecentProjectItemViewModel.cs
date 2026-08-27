using System.IO;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class RecentProjectItemViewModel
{
    public RecentProjectItemViewModel(string projectDirectory, Action open)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ArgumentNullException.ThrowIfNull(open);
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        DisplayName = Path.GetFileName(ProjectDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = ProjectDirectory;
        ProjectFile = Path.Combine(ProjectDirectory, "project.json");
        OpenCommand = new RelayCommand(open, () => Directory.Exists(ProjectDirectory) && File.Exists(ProjectFile));
    }

    public string DisplayName { get; }

    public string ProjectDirectory { get; }

    public string ProjectFile { get; }

    public RelayCommand OpenCommand { get; }
}
