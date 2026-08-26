using System.ComponentModel;
using System.Runtime.CompilerServices;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Actors;

public sealed class ActorDocument : INotifyPropertyChanged
{
    private string _id;
    private string _displayName;
    private string _notes;
    private string _homeStoryId;
    private IReadOnlyList<string> _tags;
    private string? _sourcePath;
    private bool _isNew;
    private ActorSnapshot? _savedSnapshot;
    private IReadOnlyList<ValidationIssue> _validationIssues = [];

    private ActorDocument(ActorResource resource, string? sourcePath, bool isNew)
    {
        _id = resource.Id;
        _displayName = resource.DisplayName;
        _notes = resource.Notes;
        _homeStoryId = resource.HomeStoryId ?? "uncategorized";
        _tags = ActorValidator.NormalizeTags(resource.Tags);
        _sourcePath = sourcePath;
        _isNew = isNew;
        _savedSnapshot = isNew ? null : CaptureSnapshot();
        Revalidate();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value ?? string.Empty);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetField(ref _displayName, value ?? string.Empty);
    }

    public string Notes
    {
        get => _notes;
        set => SetField(ref _notes, value ?? string.Empty);
    }

    public string HomeStoryId
    {
        get => _homeStoryId;
        set => SetField(ref _homeStoryId, value ?? string.Empty);
    }

    public IReadOnlyList<string> Tags => _tags;

    public string? SourcePath
    {
        get => _sourcePath;
        private set => SetField(ref _sourcePath, value, refreshState: false);
    }

    public bool IsNew
    {
        get => _isNew;
        private set => SetField(ref _isNew, value, refreshState: false);
    }

    public bool IsDirty => IsNew || _savedSnapshot != CaptureSnapshot();

    public IReadOnlyList<ValidationIssue> ValidationIssues => _validationIssues;

    public IReadOnlyList<ValidationIssue> ValidationErrors =>
        _validationIssues.Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();

    public static ActorDocument CreateNew(string id, string displayName = "新角色") =>
        new(
            new ActorResource
            {
                Id = id,
                DisplayName = displayName,
                Notes = string.Empty,
                Tags = [],
            },
            sourcePath: null,
            isNew: true);

    public static ActorDocument FromResource(ActorResource resource, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        return new(resource, Path.GetFullPath(sourcePath), isNew: false);
    }

    public void SetTags(IEnumerable<string>? tags)
    {
        var normalized = ActorValidator.NormalizeTags(tags);
        if (_tags.SequenceEqual(normalized, StringComparer.Ordinal))
        {
            return;
        }

        _tags = normalized;
        OnPropertyChanged(nameof(Tags));
        RefreshState();
    }

    public ActorResource ToResource() => new()
    {
        SchemaVersion = ActorResource.CurrentSchemaVersion,
        Id = Id,
        DisplayName = DisplayName,
        Notes = Notes,
        Tags = [.. Tags],
        HomeStoryId = HomeStoryId,
    };

    public void Revalidate()
    {
        _validationIssues = ActorValidator.Validate(
            ToResource(),
            IsNew ? ActorIdPolicy.NewResource : ActorIdPolicy.ExistingResource);
        OnPropertyChanged(nameof(ValidationIssues));
        OnPropertyChanged(nameof(ValidationErrors));
    }

    internal void MarkSaved(string sourcePath)
    {
        SourcePath = Path.GetFullPath(sourcePath);
        IsNew = false;
        _savedSnapshot = CaptureSnapshot();
        Revalidate();
        OnPropertyChanged(nameof(IsDirty));
    }

    private void RefreshState()
    {
        Revalidate();
        OnPropertyChanged(nameof(IsDirty));
    }

    private void SetField<T>(ref T field, T value, bool refreshState = true, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        if (refreshState)
        {
            RefreshState();
        }
    }

    private ActorSnapshot CaptureSnapshot() => new(Id, DisplayName, Notes, HomeStoryId, string.Join("\u001f", Tags));

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private sealed record ActorSnapshot(string Id, string DisplayName, string Notes, string HomeStoryId, string Tags);
}
