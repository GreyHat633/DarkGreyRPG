using System.Collections.ObjectModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.ViewModels;

/// <summary>Stable route identifiers used by the Story workspace and its XAML bindings.</summary>
public static class StoryWorkspaceRoutes
{
    public const string Actors = "Actors";
    public const string Dialogues = "Dialogues";
    public const string Quests = "Quests";
    public const string Flow = "Flow";
}

public sealed record StoryRouteViewModel(string Page, string Title);

public enum StoryMembershipKind
{
    Owned,
    Referenced,
}

/// <summary>One Story Actor membership, retaining ownership and resolved Actor metadata.</summary>
public sealed class StoryActorMembershipViewModel : ObservableObject
{
    public StoryActorMembershipViewModel(
        string id,
        StoryMembershipKind kind,
        ActorResourceInfo? actor,
        string? homeStoryDisplayName = null)
    {
        Id = id;
        Kind = kind;
        Actor = actor;
        HomeStoryDisplayName = homeStoryDisplayName;
    }

    public string Id { get; }
    public StoryMembershipKind Kind { get; }
    public string MembershipKind => Kind == StoryMembershipKind.Owned ? "本剧情" : "引用";
    public string? HomeStoryDisplayName { get; }
    public string SourceLabel => IsOwned ? "本剧情" : $"来自：{HomeStoryDisplayName ?? "未知剧情"}";
    public bool IsOwned => Kind == StoryMembershipKind.Owned;
    public bool IsReferenced => Kind == StoryMembershipKind.Referenced;
    public ActorResourceInfo? Actor { get; }
    public bool IsResolved => Actor is not null;
    public bool IsMissing => !IsResolved;
    public string DisplayName => Actor?.DisplayName ?? $"缺失角色：{Id}";
    public string SourcePath => Actor?.SourcePath ?? string.Empty;
    public IReadOnlyList<string> Tags => Actor?.Tags ?? [];
    public string MembershipTooltip => IsMissing
        ? $"缺失角色：{Id}"
        : IsReferenced ? $"引用资源\n来源剧情：{HomeStoryDisplayName ?? "未知剧情"}" : $"{DisplayName}\n{Id}";
}

public sealed class StoryActorsViewModel : ObservableObject
{
    private string _searchText = string.Empty;
    private StoryActorMembershipViewModel? _selectedMembership;

    public StoryActorsViewModel(
        StoryResource story,
        IReadOnlyList<ActorResourceInfo> actors,
        IReadOnlyDictionary<string, string>? homeStoryNames = null)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(actors);
        Story = story;
        var actorById = actors.ToDictionary(actor => actor.Id, StringComparer.Ordinal);

        homeStoryNames ??= new Dictionary<string, string>(StringComparer.Ordinal);
        AddMemberships(story.OwnedResources.Actors, StoryMembershipKind.Owned, actorById, homeStoryNames);
        AddMemberships(
            story.ReferencedResources.Actors.Where(id => !story.OwnedResources.Actors.Contains(id, StringComparer.Ordinal)),
            StoryMembershipKind.Referenced,
            actorById,
            homeStoryNames);
        SortMemberships();
        RefreshFilter();
    }

    public StoryResource Story { get; }
    public ObservableCollection<StoryActorMembershipViewModel> Memberships { get; } = [];
    public ObservableCollection<StoryActorMembershipViewModel> FilteredMemberships { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty)) RefreshFilter();
        }
    }

    public StoryActorMembershipViewModel? SelectedMembership
    {
        get => _selectedMembership;
        set => SetProperty(ref _selectedMembership, value);
    }

    public int OwnedCount => Memberships.Count(member => member.IsOwned);
    public int ReferencedCount => Memberships.Count(member => member.IsReferenced);
    public bool HasMissingActors => Memberships.Any(member => !member.IsResolved);

    private void AddMemberships(
        IEnumerable<string> ids,
        StoryMembershipKind kind,
        IReadOnlyDictionary<string, ActorResourceInfo> actorById,
        IReadOnlyDictionary<string, string> homeStoryNames)
    {
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            actorById.TryGetValue(id, out var actor);
            homeStoryNames.TryGetValue(id, out var homeStoryName);
            Memberships.Add(new StoryActorMembershipViewModel(id, kind, actor, homeStoryName));
        }
    }

    private void RefreshFilter()
    {
        var query = SearchText.Trim();
        FilteredMemberships.Clear();
        foreach (var membership in Memberships.Where(item =>
                     query.Length == 0 ||
                     item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     item.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                     item.Tags.Any(tag => tag.Contains(query, StringComparison.CurrentCultureIgnoreCase))))
        {
            FilteredMemberships.Add(membership);
        }

        if (_selectedMembership is not null && !FilteredMemberships.Contains(_selectedMembership))
            SelectedMembership = null;
    }

    private void SortMemberships()
    {
        var sorted = Memberships
            .OrderBy(item => item.IsReferenced ? 1 : 0)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        Memberships.Clear();
        foreach (var item in sorted) Memberships.Add(item);
    }
}

public sealed class StoryResourceMembershipViewModel
{
    public StoryResourceMembershipViewModel(
        string id,
        StoryMembershipKind kind,
        ResourceDescriptor? descriptor,
        string? homeStoryDisplayName = null,
        bool isDraft = false,
        string? draftDisplayName = null,
        ProjectResourceType resourceType = ProjectResourceType.Dialogue)
    {
        Id = id;
        Kind = kind;
        Descriptor = descriptor;
        HomeStoryDisplayName = homeStoryDisplayName;
        IsDraft = isDraft;
        DraftDisplayName = draftDisplayName;
        ResourceType = resourceType;
    }

    public string Id { get; }
    public StoryMembershipKind Kind { get; }
    public string MembershipKind => IsDraft ? "草稿" : Kind == StoryMembershipKind.Owned ? "本剧情" : "引用";
    public bool IsOwned => !IsDraft && Kind == StoryMembershipKind.Owned;
    public bool IsReferenced => Kind == StoryMembershipKind.Referenced;
    public bool IsDraft { get; }
    public string? DraftDisplayName { get; }
    public ProjectResourceType ResourceType { get; }
    public ResourceDescriptor? Descriptor { get; }
    public string? HomeStoryDisplayName { get; }
    public bool IsResolved => Descriptor is not null || IsDraft;
    public bool IsMissing => !IsResolved;
    public string DisplayName => IsDraft ? DraftDisplayName ?? Id : Descriptor?.DisplayName ?? $"缺失资源：{Id}";
    public string SourcePath => Descriptor?.Path ?? string.Empty;
    public string DraftBadge => IsDraft ? "未保存" : string.Empty;
    public string MembershipTooltip => IsMissing
        ? $"缺失资源：{Id}"
        : IsDraft ? $"{(ResourceType == ProjectResourceType.Quest ? "任务" : "对话")}草稿\n{DisplayName}\n尚未保存到正式资源库"
        : IsReferenced ? $"引用资源\n来源剧情：{HomeStoryDisplayName ?? "未知剧情"}" : $"{DisplayName}\n{Id}";
}

public abstract class StoryResourceMembershipListViewModel : ObservableObject
{
    private string _searchText = string.Empty;
    private StoryResourceMembershipViewModel? _selectedItem;

    protected StoryResourceMembershipListViewModel(
        StoryResource story,
        ProjectResourceType resourceType,
        IReadOnlyList<ResourceDescriptor> descriptors,
        IReadOnlyDictionary<string, string>? homeStoryNames = null,
        IReadOnlyList<DialogueDocument>? dialogueDrafts = null,
        IReadOnlyList<QuestDocument>? questDrafts = null)
    {
        Story = story;
        var descriptorById = descriptors
            .Where(descriptor => descriptor.Type == resourceType)
            .ToDictionary(descriptor => descriptor.Id, StringComparer.Ordinal);
        var owned = resourceType == ProjectResourceType.Dialogue
            ? story.OwnedResources.Dialogues
            : story.OwnedResources.Quests;
        var referenced = resourceType == ProjectResourceType.Dialogue
            ? story.ReferencedResources.Dialogues
            : story.ReferencedResources.Quests;

        homeStoryNames ??= new Dictionary<string, string>(StringComparer.Ordinal);
        Add(owned, StoryMembershipKind.Owned, descriptorById, homeStoryNames, resourceType);
        Add(referenced.Where(id => !owned.Contains(id, StringComparer.Ordinal)), StoryMembershipKind.Referenced, descriptorById, homeStoryNames, resourceType);
        if (resourceType == ProjectResourceType.Dialogue)
        {
            foreach (var draft in dialogueDrafts ?? [])
            {
                if (draft.IsNewDraft && !Items.Any(item => item.Id == draft.Id))
                    Items.Add(new StoryResourceMembershipViewModel(draft.Id, StoryMembershipKind.Owned, null, null, true, draft.DisplayName));
            }
        }
        else
        {
            foreach (var draft in questDrafts ?? [])
            {
                if (draft.IsNewDraft && !Items.Any(item => item.Id == draft.Id))
                    Items.Add(new StoryResourceMembershipViewModel(draft.Id, StoryMembershipKind.Owned, null, null, true, draft.DisplayName, resourceType));
            }
        }
        SortItems();
        RefreshFilter();
    }

    public StoryResource Story { get; }
    public ObservableCollection<StoryResourceMembershipViewModel> Items { get; } = [];
    public ObservableCollection<StoryResourceMembershipViewModel> FilteredItems { get; } = [];
    public int OwnedCount => Items.Count(item => item.IsOwned);
    public int ReferencedCount => Items.Count(item => item.IsReferenced);
    public bool HasMissingResources => Items.Any(item => !item.IsResolved);

    public void RemoveDraft(string id)
    {
        foreach (var item in Items.Where(item => item.IsDraft && item.Id == id).ToArray())
        {
            Items.Remove(item);
            FilteredItems.Remove(item);
            if (ReferenceEquals(SelectedItem, item)) SelectedItem = null;
        }
        OnPropertyChanged(nameof(OwnedCount));
    }

    public StoryResourceMembershipViewModel? PromoteDraft(string id, ResourceDescriptor descriptor)
    {
        var index = Items.IndexOf(Items.FirstOrDefault(item => item.IsDraft && item.Id == id)!);
        if (index < 0) return null;
        var promoted = new StoryResourceMembershipViewModel(id, StoryMembershipKind.Owned, descriptor);
        Items[index] = promoted;
        RefreshFilter();
        OnPropertyChanged(nameof(OwnedCount));
        return promoted;
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty)) RefreshFilter();
        }
    }

    public StoryResourceMembershipViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    private void Add(
        IEnumerable<string> ids,
        StoryMembershipKind kind,
        IReadOnlyDictionary<string, ResourceDescriptor> descriptorById,
        IReadOnlyDictionary<string, string> homeStoryNames,
        ProjectResourceType resourceType)
    {
        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            descriptorById.TryGetValue(id, out var descriptor);
            homeStoryNames.TryGetValue(id, out var homeStoryName);
            Items.Add(new StoryResourceMembershipViewModel(id, kind, descriptor, homeStoryName, resourceType: resourceType));
        }
    }

    private void SortItems()
    {
        var sorted = Items
            .OrderBy(item => item.IsDraft ? 0 : item.IsReferenced ? 2 : 1)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        Items.Clear();
        foreach (var item in sorted) Items.Add(item);
    }

    private void RefreshFilter()
    {
        var query = SearchText.Trim();
        FilteredItems.Clear();
        foreach (var item in Items.Where(item =>
                     query.Length == 0 ||
                     item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     item.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
        {
            FilteredItems.Add(item);
        }

        if (_selectedItem is not null && !FilteredItems.Contains(_selectedItem))
            SelectedItem = null;
    }
}

public sealed class StoryDialoguesViewModel : StoryResourceMembershipListViewModel
{
    public StoryDialoguesViewModel(StoryResource story, IReadOnlyList<ResourceDescriptor> descriptors, IReadOnlyDictionary<string, string>? homeStoryNames = null, IReadOnlyList<DialogueDocument>? dialogueDrafts = null)
        : base(story, ProjectResourceType.Dialogue, descriptors, homeStoryNames, dialogueDrafts) { }
}

public sealed class StoryQuestsViewModel : StoryResourceMembershipListViewModel
{
    public StoryQuestsViewModel(StoryResource story, IReadOnlyList<ResourceDescriptor> descriptors, IReadOnlyDictionary<string, string>? homeStoryNames = null, IReadOnlyList<QuestDocument>? questDrafts = null)
        : base(story, ProjectResourceType.Quest, descriptors, homeStoryNames, questDrafts: questDrafts) { }
}

public sealed record StoryFlowNodeViewModel(string Id, string Type, string Label, double X, double Y);
public sealed record StoryFlowConnectionViewModel(string From, string Output, string To);

public sealed class StoryFlowViewModel
{
    public StoryFlowViewModel(StoryResource story)
    {
        Story = story;
        Nodes = new ReadOnlyCollection<StoryFlowNodeViewModel>(story.Nodes.Select(node =>
            new StoryFlowNodeViewModel(
                node.Id,
                node.Type,
                node.Properties.TryGetValue("label", out var label) ? DisplayValue(label) : node.Type,
                node.Position.X,
                node.Position.Y)).ToList());
        Connections = new ReadOnlyCollection<StoryFlowConnectionViewModel>(story.Connections
            .Select(connection => new StoryFlowConnectionViewModel(connection.From, connection.Output, connection.To))
            .ToList());
    }

    public StoryResource Story { get; }
    public IReadOnlyList<StoryFlowNodeViewModel> Nodes { get; }
    public IReadOnlyList<StoryFlowConnectionViewModel> Connections { get; }
    public bool IsEmpty => Nodes.Count == 0 && Connections.Count == 0;
    public string Summary => $"{Nodes.Count} 个节点 · {Connections.Count} 条连接";

    private static string DisplayValue(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? string.Empty
        : value.GetRawText();
}

/// <summary>State and route selection for a selected Story. M3 routes are read-only except Actor editing.</summary>
public sealed class StoryWorkspaceViewModel : ObservableObject
{
    private StoryResource? _story;
    private StoryRouteViewModel _selectedRoute;

    public StoryWorkspaceViewModel()
    {
        Routes = new ReadOnlyCollection<StoryRouteViewModel>(
        [
            new(StoryWorkspaceRoutes.Actors, "角色"),
            new(StoryWorkspaceRoutes.Dialogues, "对话"),
            new(StoryWorkspaceRoutes.Quests, "任务"),
            new(StoryWorkspaceRoutes.Flow, "流程"),
        ]);
        _selectedRoute = Routes[0];
        SelectActorsCommand = new RelayCommand(() => SelectRoute(StoryWorkspaceRoutes.Actors), () => Story is not null);
        SelectDialoguesCommand = new RelayCommand(() => SelectRoute(StoryWorkspaceRoutes.Dialogues), () => Story is not null);
        SelectQuestsCommand = new RelayCommand(() => SelectRoute(StoryWorkspaceRoutes.Quests), () => Story is not null);
        SelectFlowCommand = new RelayCommand(() => SelectRoute(StoryWorkspaceRoutes.Flow), () => Story is not null);
    }

    public IReadOnlyList<StoryRouteViewModel> Routes { get; }
    public StoryResource? Story
    {
        get => _story;
        private set => SetProperty(ref _story, value);
    }

    public string StoryId => Story?.Id ?? string.Empty;
    public string StoryDisplayName => Story is null
        ? string.Empty
        : string.IsNullOrWhiteSpace(Story.DisplayName) ? Story.Title : Story.DisplayName;
    public bool HasStory => Story is not null;
    public StoryRouteViewModel SelectedRoute
    {
        get => _selectedRoute;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            SelectRoute(value.Page);
        }
    }

    public string CurrentRoute => SelectedRoute.Page;
    public object? CurrentPage => SelectedRoute.Page switch
    {
        StoryWorkspaceRoutes.Actors => Actors,
        StoryWorkspaceRoutes.Dialogues => Dialogues,
        StoryWorkspaceRoutes.Quests => Quests,
        StoryWorkspaceRoutes.Flow => Flow,
        _ => null,
    };

    public StoryActorsViewModel? Actors { get; private set; }
    public StoryDialoguesViewModel? Dialogues { get; private set; }
    public StoryQuestsViewModel? Quests { get; private set; }
    public StoryFlowViewModel? Flow { get; private set; }

    public RelayCommand SelectActorsCommand { get; }
    public RelayCommand SelectDialoguesCommand { get; }
    public RelayCommand SelectQuestsCommand { get; }
    public RelayCommand SelectFlowCommand { get; }

    /// <summary>Shell supplies the existing Actor unsaved-changes policy for route changes.</summary>
    public Func<bool>? CanLeaveActorsRoute { get; set; }

    /// <summary>Shell supplies a route-aware unsaved-changes policy for M5+ editors.</summary>
    public Func<string, bool>? CanLeaveRoute { get; set; }

    public void OpenStory(
        StoryResource story,
        IReadOnlyList<ActorResourceInfo> actors,
        IReadOnlyList<ResourceDescriptor>? descriptors = null,
        IReadOnlyDictionary<string, string>? actorHomeStoryNames = null,
        IReadOnlyDictionary<string, string>? dialogueHomeStoryNames = null,
        IReadOnlyDictionary<string, string>? questHomeStoryNames = null,
        IReadOnlyList<DialogueDocument>? dialogueDrafts = null,
        IReadOnlyList<QuestDocument>? questDrafts = null)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(actors);
        descriptors ??= [];
        Story = story;
        Actors = new StoryActorsViewModel(story, actors, actorHomeStoryNames);
        Dialogues = new StoryDialoguesViewModel(story, descriptors, dialogueHomeStoryNames, dialogueDrafts);
        Quests = new StoryQuestsViewModel(story, descriptors, questHomeStoryNames, questDrafts);
        Flow = new StoryFlowViewModel(story);
        OnPropertyChanged(nameof(StoryId));
        OnPropertyChanged(nameof(StoryDisplayName));
        OnPropertyChanged(nameof(HasStory));
        OnPropertyChanged(nameof(Actors));
        OnPropertyChanged(nameof(Dialogues));
        OnPropertyChanged(nameof(Quests));
        OnPropertyChanged(nameof(Flow));
        SelectRoute(StoryWorkspaceRoutes.Actors);
        RaiseRouteCommandStates();
    }

    public void CloseStory()
    {
        Story = null;
        Actors = null;
        Dialogues = null;
        Quests = null;
        Flow = null;
        OnPropertyChanged(nameof(StoryId));
        OnPropertyChanged(nameof(StoryDisplayName));
        OnPropertyChanged(nameof(HasStory));
        OnPropertyChanged(nameof(Actors));
        OnPropertyChanged(nameof(Dialogues));
        OnPropertyChanged(nameof(Quests));
        OnPropertyChanged(nameof(Flow));
        SelectRoute(StoryWorkspaceRoutes.Actors);
        RaiseRouteCommandStates();
    }

    public void SelectRoute(string page)
    {
        var route = Routes.FirstOrDefault(item => string.Equals(item.Page, page, StringComparison.Ordinal));
        if (route is null) throw new ArgumentException($"Unknown Story route '{page}'.", nameof(page));
        if (!HasStory && !string.Equals(page, StoryWorkspaceRoutes.Actors, StringComparison.Ordinal)) return;
        if (!string.Equals(SelectedRoute.Page, page, StringComparison.Ordinal) &&
            CanLeaveRoute is not null &&
            !CanLeaveRoute(SelectedRoute.Page))
        {
            return;
        }
        if (CanLeaveRoute is null &&
            SelectedRoute.Page == StoryWorkspaceRoutes.Actors &&
            !string.Equals(page, StoryWorkspaceRoutes.Actors, StringComparison.Ordinal) &&
            CanLeaveActorsRoute is not null &&
            !CanLeaveActorsRoute())
        {
            return;
        }
        if (!SetProperty(ref _selectedRoute, route, nameof(SelectedRoute))) return;
        OnPropertyChanged(nameof(CurrentRoute));
        OnPropertyChanged(nameof(CurrentPage));
    }

    private void RaiseRouteCommandStates()
    {
        SelectActorsCommand.RaiseCanExecuteChanged();
        SelectDialoguesCommand.RaiseCanExecuteChanged();
        SelectQuestsCommand.RaiseCanExecuteChanged();
        SelectFlowCommand.RaiseCanExecuteChanged();
    }
}
