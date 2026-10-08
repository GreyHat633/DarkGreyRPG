using System.Collections.ObjectModel;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

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
    public string ChineseTypeLabel => ProjectResourceLabels.ChineseLabel(Type);
    public string Title => "迁移故事内容";
    public string ActionText => "复制到目标";
    public string Explanation => $"将“{StoryDisplayName}”的内容追加到选中的故事，保留源故事和目标已有内容。";
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
    public string EmptyText => "请先创建另一个可编辑故事作为目标。";

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

internal static class ProjectResourceLabels
{
    public static string ChineseLabel(ProjectResourceType type) => type switch
    {
        ProjectResourceType.Story => "故事", ProjectResourceType.Session => "会话", ProjectResourceType.Task => "任务",
        ProjectResourceType.Actor => "角色", ProjectResourceType.Item => "个体物品", ProjectResourceType.ItemGroup => "集体物品",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
}
