using System.IO;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ProjectCreationDialogViewModel : ObservableObject
{
    private string _fullDestination;
    private string _id;
    private string _displayName;

    private ProjectCreationDialogViewModel(
        string fullDestination,
        string id,
        string displayName)
    {
        _fullDestination = fullDestination;
        _id = id;
        _displayName = displayName;
        ApplySuggestionCommand = new RelayCommand(ApplySuggestion, () => HasSuggestion);
    }

    public string Title => "新建项目";

    public string ActionText => "创建项目";

    public RelayCommand ApplySuggestionCommand { get; }

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

    public string DestinationDirectory => TryGetFullPath(FullDestination.Trim(), out var fullPath)
        ? fullPath : string.Empty;

    public string NormalizedSuggestion => ProjectIdentity.IsValid(Id) ? Id : ActorValidator.NormalizeId(Id);

    public bool HasSuggestion =>
        NormalizedSuggestion.Length > 0 &&
        !string.Equals(Id, NormalizedSuggestion, StringComparison.Ordinal);

    public string ValidationText
    {
        get
        {
            List<string> messages = string.IsNullOrWhiteSpace(Id)
                ? ["项目 ID 不能为空。"]
                : ProjectIdentity.IsValid(Id) ? [] : ["项目 ID 只能包含英文字母、数字、下划线、点或连字符，且以字母或数字开头；大小写敏感。"];

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
            ? StudioStoragePaths.Default.Projects
            : initialParentDirectory.Trim();
        var destination = Path.Combine(Path.GetFullPath(parent), "Project");
        for (var suffix = 2; Directory.Exists(destination) || File.Exists(destination); suffix++)
            destination = Path.Combine(Path.GetFullPath(parent), "Project_" + suffix);
        return new(destination, "DarkGreyRPGProject", "DarkGrey RPG 项目");
    }

    private void ApplySuggestion() => Id = NormalizedSuggestion;

    private IReadOnlyList<string> GetDestinationValidationMessages()
    {
        if (string.IsNullOrWhiteSpace(FullDestination)) return ["项目路径不能为空。"];
        return TryGetFullPath(FullDestination.Trim(), out _) ? [] : ["请填写有效的完整项目路径。"];
    }

    private static bool TryGetFullPath(string path, out string fullPath)
    {
        try
        {
            if (!Path.IsPathFullyQualified(path))
            {
                fullPath = string.Empty;
                return false;
            }
            fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath)!;
            var segments = fullPath[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Any(segment => segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            {
                fullPath = string.Empty;
                return false;
            }
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
