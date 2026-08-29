using System.Collections.ObjectModel;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ResourceIdentityDialogViewModel : ObservableObject
{
    private string _id;
    private string _displayName;

    private ResourceIdentityDialogViewModel(
        ProjectResourceType type,
        string title,
        string actionText,
        string description,
        string id,
        string displayName)
    {
        Type = type;
        Title = title;
        ActionText = actionText;
        Description = description;
        _id = id;
        _displayName = displayName;
        ApplySuggestionCommand = new RelayCommand(ApplySuggestion, () => HasSuggestion);
    }

    public ProjectResourceType Type { get; }
    public string TypeLabel => Label(Type);
    public string Title { get; }
    public string ActionText { get; }
    public string Description { get; }
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
    public bool HasSuggestion => NormalizedSuggestion.Length > 0 &&
                                 !string.Equals(Id, NormalizedSuggestion, StringComparison.Ordinal);
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

    public static ResourceIdentityDialogViewModel ForCreate(ProjectResourceType type, string suggestedId) =>
        new(
            type,
            $"新建 {Label(type)}",
            "创建",
            type == ProjectResourceType.Story
                ? "在当前项目中新建一条独立故事。"
                : $"创建独立的新{ChineseLabel(type)}，并归入当前故事。",
            suggestedId,
            type switch
            {
                ProjectResourceType.Dialogue => "新对话",
                ProjectResourceType.Quest => "新任务",
                ProjectResourceType.Story => "新故事",
                _ => throw new ArgumentOutOfRangeException(nameof(type)),
            });

    public static ResourceIdentityDialogViewModel ForImport(
        ProjectResourceType type,
        string sourceDisplayName,
        string suggestedId) =>
        new(
            type,
            $"导入 {Label(type)} 副本",
            "创建副本",
            $"将以“{sourceDisplayName}”为模板创建新的独立资源。后续修改不会影响原资源。",
            suggestedId,
            sourceDisplayName);

    internal static string Label(ProjectResourceType type) => type switch
    {
        ProjectResourceType.Dialogue => "Dialogue",
        ProjectResourceType.Quest => "Quest",
        ProjectResourceType.Story => "Story",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    internal static string ChineseLabel(ProjectResourceType type) => type switch
    {
        ProjectResourceType.Dialogue => "对话",
        ProjectResourceType.Quest => "任务",
        ProjectResourceType.Story => "故事",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

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

public sealed class ResourceCreationChoiceViewModel : ObservableObject
{
    private ResourceCreationMode _selectedMode = ResourceCreationMode.Blank;

    public ResourceCreationChoiceViewModel(ProjectResourceType type, string storyDisplayName)
    {
        Type = type;
        StoryDisplayName = string.IsNullOrWhiteSpace(storyDisplayName) ? "当前故事" : storyDisplayName;
    }

    public ProjectResourceType Type { get; }
    public string TypeLabel => ResourceIdentityDialogViewModel.Label(Type);
    public string ChineseTypeLabel => ResourceIdentityDialogViewModel.ChineseLabel(Type);
    public string Title => $"创建{ChineseTypeLabel}";
    public string StoryDisplayName { get; }
    public ResourceCreationMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (!SetProperty(ref _selectedMode, value)) return;
            OnPropertyChanged(nameof(IsBlank));
            OnPropertyChanged(nameof(IsImportAsNew));
        }
    }
    public bool IsBlank
    {
        get => SelectedMode == ResourceCreationMode.Blank;
        set { if (value) SelectedMode = ResourceCreationMode.Blank; }
    }
    public bool IsImportAsNew
    {
        get => SelectedMode == ResourceCreationMode.ImportAsNew;
        set { if (value) SelectedMode = ResourceCreationMode.ImportAsNew; }
    }
}

public sealed class ResourcePickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<ResourceDescriptor> _resources;
    private string _searchText = string.Empty;
    private ResourceDescriptor? _selectedResource;

    public ResourcePickerViewModel(
        ProjectResourceType type,
        IReadOnlyList<ResourceDescriptor> resources,
        ResourcePickerMode mode,
        string storyDisplayName)
    {
        Type = type;
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        Mode = mode;
        StoryDisplayName = string.IsNullOrWhiteSpace(storyDisplayName) ? "当前故事" : storyDisplayName;
        RefreshFilter();
    }

    public ProjectResourceType Type { get; }
    public ResourcePickerMode Mode { get; }
    public string StoryDisplayName { get; }
    public string ChineseTypeLabel => ResourceIdentityDialogViewModel.ChineseLabel(Type);
    public string Title => Mode == ResourcePickerMode.Reference
        ? $"引用已有{ChineseTypeLabel}"
        : $"导入已有{ChineseTypeLabel}";
    public string ActionText => Mode == ResourcePickerMode.Reference ? "引用" : "下一步";
    public string Explanation => Mode == ResourcePickerMode.Reference
        ? $"选择项目中的现有{ChineseTypeLabel}链接到“{StoryDisplayName}”。引用共享同一份资源，任何位置的修改都会同步。"
        : $"选择一个{ChineseTypeLabel}作为“{StoryDisplayName}”中新资源的模板。将创建独立 ID 和文件，后续修改互不影响。";
    public string SearchAutomationName => $"搜索{ChineseTypeLabel}";
    public ObservableCollection<ResourceDescriptor> FilteredResources { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value ?? string.Empty)) RefreshFilter(); }
    }
    public ResourceDescriptor? SelectedResource
    {
        get => _selectedResource;
        set { if (SetProperty(ref _selectedResource, value)) OnPropertyChanged(nameof(CanConfirm)); }
    }
    public bool CanConfirm => SelectedResource is not null;
    public bool HasCandidates => _resources.Count > 0;
    public string EmptyText => Mode == ResourcePickerMode.Reference
        ? $"没有可引用的{ChineseTypeLabel}。"
        : $"项目中还没有可作为模板的{ChineseTypeLabel}。";

    private void RefreshFilter()
    {
        var query = SearchText.Trim();
        FilteredResources.Clear();
        foreach (var resource in _resources.Where(resource =>
                     query.Length == 0 ||
                     resource.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     resource.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
        {
            FilteredResources.Add(resource);
        }
    }
}

public sealed class ResourceReferencesViewModel
{
    public ResourceReferencesViewModel(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references)
    {
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        References = references ?? throw new ArgumentNullException(nameof(references));
    }

    public ResourceDescriptor Resource { get; }
    public IReadOnlyList<ResourceDescriptor> References { get; }
    public string Title => $"“{Resource.DisplayName}”的引用";
    public string Summary => References.Count == 0
        ? "当前没有其它故事引用这个资源。"
        : $"以下 {References.Count} 个故事仍引用这个资源：";
}
