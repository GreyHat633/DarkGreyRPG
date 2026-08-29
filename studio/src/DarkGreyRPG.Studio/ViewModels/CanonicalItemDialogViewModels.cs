using System.Collections.ObjectModel;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

/// <summary>Explicitly chooses whether a new resource is an Item or Item Group.</summary>
public sealed class CanonicalItemCreationChoiceViewModel : ObservableObject
{
    private ItemCreationMode _selectedMode = ItemCreationMode.Individual;

    public CanonicalItemCreationChoiceViewModel(string storyDisplayName)
    {
        StoryDisplayName = string.IsNullOrWhiteSpace(storyDisplayName) ? "当前故事" : storyDisplayName.Trim();
    }

    public string StoryDisplayName { get; }

    public ItemCreationMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (!Enum.IsDefined(value)) return;
            if (!SetProperty(ref _selectedMode, value)) return;
            OnPropertyChanged(nameof(IsIndividual));
            OnPropertyChanged(nameof(IsCollective));
        }
    }

    public bool IsIndividual
    {
        get => SelectedMode == ItemCreationMode.Individual;
        set { if (value) SelectedMode = ItemCreationMode.Individual; }
    }

    public bool IsCollective
    {
        get => SelectedMode == ItemCreationMode.Collective;
        set { if (value) SelectedMode = ItemCreationMode.Collective; }
    }

    public CanonicalStoryItemKind Kind => SelectedMode == ItemCreationMode.Individual
        ? CanonicalStoryItemKind.Individual
        : CanonicalStoryItemKind.Collective;
}

/// <summary>Identity and metadata editor for one canonical Item resource.</summary>
public sealed class CanonicalItemIdentityDialogViewModel : ObservableObject
{
    private string _id;
    private string _displayName;
    private string _tagsText;

    public CanonicalItemIdentityDialogViewModel(
        CanonicalStoryItemKind kind,
        string suggestedId,
        string? displayName = null,
        IEnumerable<string>? tags = null)
    {
        EnsureSupportedKind(kind);
        Kind = kind;
        _id = suggestedId ?? string.Empty;
        _displayName = displayName ?? DefaultDisplayName(kind);
        _tagsText = string.Join(", ", tags ?? []);
        ApplySuggestionCommand = new RelayCommand(ApplySuggestion, () => HasSuggestion);
    }

    public CanonicalStoryItemKind Kind { get; }
    public CanonicalStoryItemKind ItemKind => Kind;
    public string ChineseTypeLabel => Kind == CanonicalStoryItemKind.Individual ? "物品" : "物品组";
    public string TypeLabel => Kind == CanonicalStoryItemKind.Individual ? "Item" : "Item Group";
    public string Title => $"新建 {ChineseTypeLabel}";
    public string Description => Kind == CanonicalStoryItemKind.Individual
        ? "创建精确匹配一个物品身份的 Item ID，并归入当前故事。"
        : "创建可包含多个物品身份的 Group ID，并归入当前故事。";
    public string ActionText => "创建";
    public RelayCommand ApplySuggestionCommand { get; }

    public string Id
    {
        get => _id;
        set { if (SetProperty(ref _id, value ?? string.Empty)) RaiseValidationProperties(); }
    }

    public string DisplayName
    {
        get => _displayName;
        set { if (SetProperty(ref _displayName, value ?? string.Empty)) RaiseValidationProperties(); }
    }

    public string TagsText
    {
        get => _tagsText;
        set { if (SetProperty(ref _tagsText, value ?? string.Empty)) RaiseValidationProperties(); }
    }

    public IReadOnlyList<string> Tags => ParseTags(TagsText);
    public string NormalizedSuggestion => ItemValidator.NormalizeId(Id);
    public bool HasSuggestion => NormalizedSuggestion.Length > 0
        && !string.Equals(Id, NormalizedSuggestion, StringComparison.Ordinal);

    public string ValidationText
    {
        get
        {
            var messages = ItemValidator.ValidateId(Id)
                .Where(issue => issue.Severity == ValidationSeverity.Error)
                .Select(issue => issue.Message)
                .ToList();
            if (string.IsNullOrWhiteSpace(DisplayName)) messages.Add("显示名称不能为空。");
            if (Tags.Any(string.IsNullOrWhiteSpace)) messages.Add("标签不能为空。");
            if (Tags.Count != Tags.Distinct(StringComparer.Ordinal).Count()) messages.Add("标签不能重复。");
            return string.Join(Environment.NewLine, messages);
        }
    }

    public bool CanConfirm => ValidationText.Length == 0;

    public static CanonicalItemIdentityDialogViewModel ForCreate(
        CanonicalStoryItemKind kind,
        string suggestedId) => new(kind, suggestedId);

    public static void EnsureSupportedKind(CanonicalStoryItemKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported canonical Story item kind.");
    }

    private static string DefaultDisplayName(CanonicalStoryItemKind kind)
        => kind == CanonicalStoryItemKind.Individual ? "新物品" : "新物品组";

    private static IReadOnlyList<string> ParseTags(string value)
        => value.Split([',', '，', ';', '；', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(tag => tag.Trim())
            .ToArray();

    private void ApplySuggestion() => Id = NormalizedSuggestion;

    private void RaiseValidationProperties()
    {
        OnPropertyChanged(nameof(NormalizedSuggestion));
        OnPropertyChanged(nameof(HasSuggestion));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(ValidationText));
        OnPropertyChanged(nameof(CanConfirm));
        ApplySuggestionCommand.RaiseCanExecuteChanged();
    }
}

/// <summary>Searches existing Item IDs, names, and tags for Story references.</summary>
public sealed class CanonicalItemPickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<ItemResourceInfo> _resources;
    private string _searchText = string.Empty;
    private ItemResourceInfo? _selectedResource;

    public CanonicalItemPickerViewModel(
        IReadOnlyList<ItemResourceInfo> candidates,
        string storyDisplayName)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Any(candidate => candidate is null))
            throw new ArgumentException("Item reference candidates cannot contain null entries.", nameof(candidates));
        if (candidates.Any(candidate =>
                !string.Equals(candidate.Type, IndividualItemResource.ResourceType, StringComparison.Ordinal)
                && !string.Equals(candidate.Type, CollectiveItemResource.ResourceType, StringComparison.Ordinal)))
            throw new ArgumentException("Item reference candidates contain an unsupported type.", nameof(candidates));
        _resources = candidates.ToArray();
        StoryDisplayName = string.IsNullOrWhiteSpace(storyDisplayName) ? "当前故事" : storyDisplayName.Trim();
        RefreshFilter();
    }

    public string StoryDisplayName { get; }
    public string Title => "引用已有物品";
    public string Explanation => $"搜索并选择项目中的现有物品或物品组，链接到“{StoryDisplayName}”。引用不会复制或删除资源文件。";
    public string SearchAutomationName => "搜索物品或物品组";
    public ObservableCollection<ItemResourceInfo> FilteredResources { get; } = [];
    public ObservableCollection<ItemResourceInfo> FilteredItems => FilteredResources;
    public IReadOnlyList<ItemResourceInfo> Candidates => _resources;
    public bool HasCandidates => _resources.Count > 0;
    public bool HasFilteredCandidates => FilteredResources.Count > 0;
    public bool IsEmpty => !HasFilteredCandidates;
    public string EmptyText => HasCandidates ? "没有匹配的物品或物品组。" : "没有可引用的物品或物品组。";

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value ?? string.Empty)) RefreshFilter(); }
    }

    public ItemResourceInfo? SelectedResource
    {
        get => _selectedResource;
        set
        {
            if (!SetProperty(ref _selectedResource, value)) return;
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public ItemResourceInfo? SelectedItem
    {
        get => SelectedResource;
        set => SelectedResource = value;
    }

    public ItemResourceInfo? SelectedCandidate
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
            || resource.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || resource.Tags.Any(tag => tag.Contains(query, StringComparison.CurrentCultureIgnoreCase))))
            FilteredResources.Add(resource);
        if (_selectedResource is not null && !FilteredResources.Contains(_selectedResource)) SelectedResource = null;
        OnPropertyChanged(nameof(HasFilteredCandidates));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(EmptyText));
    }
}
