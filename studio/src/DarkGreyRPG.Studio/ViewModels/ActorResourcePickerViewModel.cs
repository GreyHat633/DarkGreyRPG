using System.Collections.ObjectModel;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ActorResourcePickerViewModel : ObservableObject
{
    private readonly IReadOnlyList<ActorResourceInfo> _actors;
    private string _searchText = string.Empty;
    private ActorResourceInfo? _selectedActor;

    public ActorResourcePickerViewModel(
        IReadOnlyList<ActorResourceInfo> actors,
        ActorPickerMode mode,
        string storyDisplayName)
    {
        ArgumentNullException.ThrowIfNull(actors);
        _actors = actors;
        Mode = mode;
        StoryDisplayName = string.IsNullOrWhiteSpace(storyDisplayName) ? "当前故事" : storyDisplayName;
        RefreshFilter();
    }

    public ActorPickerMode Mode { get; }
    public string StoryDisplayName { get; }
    public string Title => Mode == ActorPickerMode.Reference ? "引用已有角色" : "导入已有角色";
    public string ActionText => Mode == ActorPickerMode.Reference ? "引用" : "下一步";
    public string Explanation => Mode == ActorPickerMode.Reference
        ? $"选择项目中的现有角色链接到“{StoryDisplayName}”。引用共享同一个资源，任何位置的修改都会同步。"
        : $"选择一个角色作为“{StoryDisplayName}”中新资源的模板。将创建独立 ID 和文件，后续修改互不影响。";

    public ObservableCollection<ActorResourceInfo> FilteredActors { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty)) RefreshFilter();
        }
    }

    public ActorResourceInfo? SelectedActor
    {
        get => _selectedActor;
        set
        {
            if (SetProperty(ref _selectedActor, value)) OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public bool CanConfirm => SelectedActor is not null;
    public bool HasCandidates => _actors.Count > 0;
    public string EmptyText => Mode == ActorPickerMode.Reference
        ? "没有可引用的角色。"
        : "项目中还没有可作为模板的角色。";

    private void RefreshFilter()
    {
        var query = SearchText.Trim();
        FilteredActors.Clear();
        foreach (var actor in _actors.Where(actor =>
                     query.Length == 0 ||
                     actor.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     actor.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                     actor.Tags.Any(tag => tag.Contains(query, StringComparison.CurrentCultureIgnoreCase))))
        {
            FilteredActors.Add(actor);
        }
    }
}
