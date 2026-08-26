using System.IO;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ProjectCreationDialogViewModel : ObservableObject
{
    private string _parentDirectory;
    private string _projectFolderName;
    private string _fullDestination;
    private string _id;
    private string _displayName;

    private ProjectCreationDialogViewModel(
        string parentDirectory,
        string projectFolderName,
        string fullDestination,
        string id,
        string displayName)
    {
        _parentDirectory = parentDirectory;
        _projectFolderName = projectFolderName;
        _fullDestination = fullDestination;
        _id = id;
        _displayName = displayName;
        ApplySuggestionCommand = new RelayCommand(ApplySuggestion, () => HasSuggestion);
    }

    public string Title => "新建项目";

    public string ActionText => "创建项目";

    public RelayCommand ApplySuggestionCommand { get; }

    public string ParentDirectory
    {
        get => _parentDirectory;
        set
        {
            if (SetProperty(ref _parentDirectory, value ?? string.Empty))
            {
                RaiseDestinationProperties();
            }
        }
    }

    public string ProjectFolderName
    {
        get => _projectFolderName;
        set
        {
            if (SetProperty(ref _projectFolderName, value ?? string.Empty))
            {
                RaiseDestinationProperties();
            }
        }
    }

    public string FullDestination
    {
        get => _fullDestination;
        set
        {
            if (SetProperty(ref _fullDestination, value ?? string.Empty))
            {
                RaiseDestinationProperties();
            }
        }
    }

    public string Id
    {
        get => _id;
        set
        {
            if (SetProperty(ref _id, value ?? string.Empty))
            {
                RaiseIdentityProperties();
            }
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (SetProperty(ref _displayName, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(ValidationText));
                OnPropertyChanged(nameof(CanConfirm));
            }
        }
    }

    public string DestinationDirectory
    {
        get
        {
            var fullDestination = FullDestination.Trim();
            if (fullDestination.Length > 0)
            {
                return TryGetFullPath(fullDestination, out var fullPath) ? fullPath : string.Empty;
            }

            var parent = ParentDirectory.Trim();
            var folderName = ProjectFolderName.Trim();
            if (parent.Length == 0 || folderName.Length == 0)
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(Path.Combine(parent, folderName));
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
            {
                return string.Empty;
            }
        }
    }

    public string NormalizedSuggestion => ActorValidator.NormalizeId(Id);

    public bool HasSuggestion =>
        NormalizedSuggestion.Length > 0 &&
        !string.Equals(Id, NormalizedSuggestion, StringComparison.Ordinal);

    public string ValidationText
    {
        get
        {
            var messages = string.IsNullOrWhiteSpace(Id)
                ? ["项目 ID 不能为空。"]
                : ActorValidator.ValidateId(Id, ActorIdPolicy.NewResource)
                    .Where(issue => issue.Severity == ValidationSeverity.Error)
                    .Select(issue => issue.Message)
                    .ToList();

            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                messages.Add("显示名称不能为空。");
            }

            messages.AddRange(GetDestinationValidationMessages());
            return string.Join(Environment.NewLine, messages);
        }
    }

    public bool CanConfirm => ValidationText.Length == 0;

    public static ProjectCreationDialogViewModel ForCreate(string? initialParentDirectory = null)
    {
        var parent = string.IsNullOrWhiteSpace(initialParentDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : initialParentDirectory.Trim();
        return new(parent, "darkgrey_rpg_project", string.Empty, "darkgrey_rpg_project", "DarkGrey RPG 项目");
    }

    private void ApplySuggestion() => Id = NormalizedSuggestion;

    private IReadOnlyList<string> GetDestinationValidationMessages()
    {
        if (FullDestination.Trim().Length > 0)
        {
            return TryGetFullPath(FullDestination.Trim(), out _)
                ? []
                : ["完整目标路径无效。"];
        }

        var messages = new List<string>();
        if (string.IsNullOrWhiteSpace(ParentDirectory))
        {
            messages.Add("父文件夹不能为空。");
        }
        else if (!TryGetFullPath(ParentDirectory.Trim(), out _))
        {
            messages.Add("父文件夹路径无效。");
        }

        if (string.IsNullOrWhiteSpace(ProjectFolderName))
        {
            messages.Add("项目文件夹名不能为空。");
        }
        else if (!IsValidFolderName(ProjectFolderName.Trim()))
        {
            messages.Add("项目文件夹名必须是单个有效的文件夹名。");
        }

        return messages;
    }

    private static bool IsValidFolderName(string folderName)
    {
        if (folderName is "." or ".." || Path.IsPathRooted(folderName))
        {
            return false;
        }

        return folderName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               folderName.IndexOf(Path.DirectorySeparatorChar) < 0 &&
               folderName.IndexOf(Path.AltDirectorySeparatorChar) < 0;
    }

    private static bool TryGetFullPath(string path, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    private void RaiseDestinationProperties()
    {
        OnPropertyChanged(nameof(DestinationDirectory));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(CanConfirm));
    }

    private void RaiseIdentityProperties()
    {
        OnPropertyChanged(nameof(NormalizedSuggestion));
        OnPropertyChanged(nameof(HasSuggestion));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(CanConfirm));
        ApplySuggestionCommand.RaiseCanExecuteChanged();
    }
}
