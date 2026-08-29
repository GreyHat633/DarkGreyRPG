using System.Collections.ObjectModel;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels;

/// <summary>Identity editor for a canonical Session or Task.</summary>
public class CanonicalResourceIdentityDialogViewModel : ObservableObject
{
    private string _id;
    private string _displayName;

    public CanonicalResourceIdentityDialogViewModel(
        GraphResourceKind resourceKind,
        string suggestedId,
        string? displayName = null)
    {
        EnsureSupportedKind(resourceKind);
        ResourceKind = resourceKind;
        _id = suggestedId ?? string.Empty;
        _displayName = displayName ?? DefaultDisplayName(resourceKind);
        ApplySuggestionCommand = new RelayCommand(ApplySuggestion, () => HasSuggestion);
    }

    public GraphResourceKind ResourceKind { get; }
    public GraphResourceKind Kind => ResourceKind;
    public string TypeLabel => EnglishLabel(ResourceKind);
    public string ChineseTypeLabel => ChineseLabel(ResourceKind);
    public string Title => $"新建 {TypeLabel}";
    public string ActionText => "创建";
    public string Description => $"创建独立的新{ChineseTypeLabel}，并归入当前故事。";
    public RelayCommand ApplySuggestionCommand { get; }

    public string Id
    {
        get => _id;
        set
        {
            if (!SetProperty(ref _id, value ?? string.Empty)) return;
            RaiseValidationProperties();
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set
        {
            if (!SetProperty(ref _displayName, value ?? string.Empty)) return;
            RaiseValidationProperties();
        }
    }

    public string NormalizedSuggestion => ActorValidator.NormalizeId(Id);
    public bool HasSuggestion => NormalizedSuggestion.Length > 0
        && !string.Equals(Id, NormalizedSuggestion, StringComparison.Ordinal);

    public string ValidationText
    {
        get
        {
            var messages = ActorValidator.ValidateId(Id, ActorIdPolicy.NewResource)
                .Where(issue => issue.Severity == ValidationSeverity.Error)
                .Select(issue => issue.Message)
                .ToList();
            if (string.IsNullOrWhiteSpace(DisplayName)) messages.Add("显示名称不能为空。");
            return string.Join(Environment.NewLine, messages);
        }
    }

    public bool CanConfirm => ValidationText.Length == 0;

    public static CanonicalResourceIdentityDialogViewModel ForCreate(
        GraphResourceKind resourceKind,
        string suggestedId)
        => new(resourceKind, suggestedId);

    public static string EnglishLabel(GraphResourceKind resourceKind) => resourceKind switch
    {
        GraphResourceKind.Session => "Session",
        GraphResourceKind.Task => "Task",
        _ => throw UnsupportedKind(resourceKind),
    };

    public static string ChineseLabel(GraphResourceKind resourceKind) => resourceKind switch
    {
        GraphResourceKind.Session => "会话",
        GraphResourceKind.Task => "任务",
        _ => throw UnsupportedKind(resourceKind),
    };

    public static void EnsureSupportedKind(GraphResourceKind resourceKind)
    {
        if (resourceKind is not (GraphResourceKind.Session or GraphResourceKind.Task))
            throw UnsupportedKind(resourceKind);
    }

    private static string DefaultDisplayName(GraphResourceKind resourceKind) => resourceKind switch
    {
        GraphResourceKind.Session => "新会话",
        GraphResourceKind.Task => "新任务",
        _ => throw UnsupportedKind(resourceKind),
    };

    private static ArgumentOutOfRangeException UnsupportedKind(GraphResourceKind resourceKind)
        => new(nameof(resourceKind), resourceKind, "Canonical Story dialogs support only Session and Task resources.");

    private void ApplySuggestion() => Id = NormalizedSuggestion;

    private void RaiseValidationProperties()
    {
        OnPropertyChanged(nameof(NormalizedSuggestion));
        OnPropertyChanged(nameof(HasSuggestion));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(CanConfirm));
        ApplySuggestionCommand.RaiseCanExecuteChanged();
    }
}

/// <summary>Searchable picker for canonical Session or Task references.</summary>
public class CanonicalResourcePickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<GraphResourceInfo> _resources;
    private string _searchText = string.Empty;
    private GraphResourceInfo? _selectedResource;

    public CanonicalResourcePickerViewModel(
        GraphResourceKind resourceKind,
        IReadOnlyList<GraphResourceInfo> candidates,
        string storyDisplayName)
    {
        CanonicalResourceIdentityDialogViewModel.EnsureSupportedKind(resourceKind);
        ResourceKind = resourceKind;
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Any(candidate => candidate is null))
            throw new ArgumentException("Canonical reference candidates cannot contain null entries.", nameof(candidates));
        if (candidates.Any(candidate => candidate.ResourceKind != resourceKind))
            throw new ArgumentException("Canonical reference candidates must match the requested resource kind.", nameof(candidates));
        _resources = candidates.ToArray();
        StoryDisplayName = string.IsNullOrWhiteSpace(storyDisplayName) ? "当前故事" : storyDisplayName.Trim();
        RefreshFilter();
    }

    public GraphResourceKind ResourceKind { get; }
    public GraphResourceKind Kind => ResourceKind;
    public string ChineseTypeLabel => CanonicalResourceIdentityDialogViewModel.ChineseLabel(ResourceKind);
    public string TypeLabel => CanonicalResourceIdentityDialogViewModel.EnglishLabel(ResourceKind);
    public string Title => $"引用已有{ChineseTypeLabel}";
    public string ActionText => "引用";
    public string StoryDisplayName { get; }
    public string Explanation => $"选择项目中的现有{ChineseTypeLabel}链接到“{StoryDisplayName}”。引用共享同一份资源，任何位置的修改都会同步。";
    public string SearchAutomationName => $"搜索{ChineseTypeLabel}";
    public ObservableCollection<GraphResourceInfo> FilteredResources { get; } = [];
    public IReadOnlyList<GraphResourceInfo> Candidates => _resources;
    public bool HasCandidates => _resources.Count > 0;
    public bool HasFilteredCandidates => FilteredResources.Count > 0;
    public bool IsEmpty => !HasFilteredCandidates;
    public string EmptyText => HasCandidates ? $"没有匹配的{ChineseTypeLabel}。" : $"没有可引用的{ChineseTypeLabel}。";

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty)) RefreshFilter();
        }
    }

    public GraphResourceInfo? SelectedResource
    {
        get => _selectedResource;
        set
        {
            if (!SetProperty(ref _selectedResource, value)) return;
            OnPropertyChanged(nameof(SelectedCandidate));
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public GraphResourceInfo? SelectedCandidate
    {
        get => SelectedResource;
        set => SelectedResource = value;
    }

    public bool CanConfirm => SelectedResource is not null;

    private void RefreshFilter()
    {
        var query = SearchText.Trim();
        FilteredResources.Clear();
        foreach (var resource in _resources.Where(resource => query.Length == 0
            || resource.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
            || resource.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            FilteredResources.Add(resource);
        if (_selectedResource is not null && !FilteredResources.Contains(_selectedResource))
            SelectedResource = null;
        OnPropertyChanged(nameof(HasFilteredCandidates));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }
}

// Explicit graph-prefixed aliases keep the canonical terminology available to
// callers while preserving the concise names used by the existing dialogs.
public sealed class CanonicalGraphResourceIdentityDialogViewModel : CanonicalResourceIdentityDialogViewModel
{
    public CanonicalGraphResourceIdentityDialogViewModel(GraphResourceKind resourceKind, string suggestedId, string? displayName = null)
        : base(resourceKind, suggestedId, displayName) { }
}

public sealed class CanonicalGraphResourcePickerViewModel : CanonicalResourcePickerViewModel
{
    public CanonicalGraphResourcePickerViewModel(GraphResourceKind resourceKind, IReadOnlyList<GraphResourceInfo> candidates, string storyDisplayName)
        : base(resourceKind, candidates, storyDisplayName) { }
}
