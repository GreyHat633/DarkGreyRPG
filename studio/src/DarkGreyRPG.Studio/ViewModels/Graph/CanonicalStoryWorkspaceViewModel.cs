using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public enum CanonicalStoryFolderKind
{
    Actors,
    Items,
    Sessions,
    Tasks,
}

public interface ICanonicalStoryTreeItem
{
    string Id { get; }
    string DisplayName { get; }
    string IdentityText => Id;
}

public sealed record CanonicalStoryActorItem(
    ActorResourceInfo Actor,
    CanonicalStoryWorkspaceMembershipKind MembershipKind = CanonicalStoryWorkspaceMembershipKind.Owned)
    : ICanonicalStoryTreeItem
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<CanonicalStoryActorItem, ActorPortraitState> PortraitStates = new();
    private ActorPortraitState _portraitState => PortraitStates.GetValue(this, item => new(item.Actor));
    public ActorResourceInfo PortraitSource => _portraitState.Source;
    public event EventHandler? PortraitsChanged
    {
        add => _portraitState.Changed += value;
        remove => _portraitState.Changed -= value;
    }
    public void UpdatePortraits(string? defaultPortrait, IReadOnlyList<ActorPortraitVariant> variants)
    {
        _portraitState.Source = Actor with { DefaultPortraitRef = defaultPortrait, PortraitVariants = variants.ToArray() };
        _portraitState.Notify(this);
    }
    // Keep the record's equality/hash stable while live media and subscribers change.
    // WPF selectors retain these resource records in their item lookup tables.
    private sealed class ActorPortraitState(ActorResourceInfo source)
    {
        public ActorResourceInfo Source = source;
        public event EventHandler? Changed;
        public void Notify(object sender) => Changed?.Invoke(sender, EventArgs.Empty);
    }
    public OfflineProviderResource? Provider { get; init; }
    public bool IsReadOnly => Provider is not null;
    public string ProviderPackageText => Provider?.PackageIdentity.ToString() ?? string.Empty;
    public string SourceText => IsReferenced ? "[引用] " : string.Empty;
    public string Id => Actor.Id;
    public string DisplayName => Actor.DisplayName;
    public string IdentityText => Actor.Type switch
    {
        IndividualActorResource.ResourceType => ResourceIdentityPresentation.Format("NPC_ID", Id),
        CollectiveActorResource.ResourceType => ResourceIdentityPresentation.Format("Group_ID", Id),
        _ => ResourceIdentityPresentation.Format("NPC_ID", Id),
    };
    public bool IsOwned => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Owned;
    public bool IsReferenced => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Referenced;
}

public sealed record CanonicalStoryItemItem(
    ItemResource Item,
    CanonicalStoryWorkspaceMembershipKind MembershipKind = CanonicalStoryWorkspaceMembershipKind.Owned)
    : ICanonicalStoryTreeItem
{
    public OfflineProviderResource? Provider { get; init; }
    public bool IsReadOnly => Provider is not null;
    public string ProviderPackageText => Provider?.PackageIdentity.ToString() ?? string.Empty;
    public string SourceText => IsReferenced ? "[引用] " : string.Empty;
    public string Id => Item.Id;
    public string DisplayName => Item.DisplayName;
    public string IdentityText => Type == IndividualItemResource.ResourceType
        ? ResourceIdentityPresentation.Format("Item_ID", Id)
        : ResourceIdentityPresentation.Format("Group_ID", Id);
    public string Type => Item.Type;
    public IReadOnlyList<string> Tags => Item.Tags;
    public bool IsOwned => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Owned;
    public bool IsReferenced => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Referenced;
}

public sealed class CanonicalStoryGraphItem : ObservableObject, ICanonicalStoryTreeItem
{
    public CanonicalStoryGraphItem(
        CanonicalGraphResourceEditorViewModel editor,
        CanonicalStoryWorkspaceMembershipKind membershipKind = CanonicalStoryWorkspaceMembershipKind.Owned,
        OfflineProviderResource? provider = null)
    {
        Editor = editor ?? throw new ArgumentNullException(nameof(editor));
        MembershipKind = membershipKind;
        Provider = provider;
        Editor.PropertyChanged += EditorOnPropertyChanged;
    }

    public CanonicalGraphResourceEditorViewModel Editor { get; }
    public CanonicalStoryWorkspaceMembershipKind MembershipKind { get; }
    public OfflineProviderResource? Provider { get; }
    public bool IsReadOnly => Provider is not null;
    public string ProviderPackageText => Provider?.PackageIdentity.ToString() ?? string.Empty;
    public string SourceText => IsReferenced ? "[引用] " : string.Empty;
    public string Id => Editor.Id;
    public string DisplayName => Editor.DisplayName;
    public string IdentityText => ResourceIdentityPresentation.Format(ResourceKind == GraphResourceKind.Session ? "Session_ID" : "Task_ID", Id);
    public GraphResourceKind ResourceKind => Editor.ResourceKind;
    public bool IsOwned => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Owned;
    public bool IsReferenced => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Referenced;

    private void EditorOnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(CanonicalGraphResourceEditorViewModel.DisplayName))
            OnPropertyChanged(nameof(DisplayName));
    }
}

public sealed record CanonicalStoryMissingItem(
    string Id,
    CanonicalStoryFolderKind FolderKind,
    CanonicalStoryWorkspaceMembershipKind MembershipKind,
    ValidationIssue Issue,
    string OrderHandle) : ICanonicalStoryTreeItem
{
    public string DisplayName => $"{Id}（缺失）";
    public bool IsOwned => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Owned;
    public bool IsReferenced => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Referenced;
}

public enum CanonicalStoryBreadcrumbKind
{
    Project,
    Story,
    Session,
    Task,
}

public sealed record CanonicalStoryBreadcrumb(
    string Id,
    string DisplayName,
    CanonicalStoryBreadcrumbKind Kind,
    GraphResourceKind? ResourceKind,
    bool IsFirst,
    bool IsCurrent);

public sealed record CanonicalStoryNodeFocusRequest(
    string NodeId,
    string? Field,
    long Sequence);

/// <summary>One expandable logical resource folder; toggling never changes the graph workspace.</summary>
public sealed class CanonicalStoryFolderViewModel : ObservableObject
{
    private bool _isExpanded = true;
    private readonly ObservableCollection<ICanonicalStoryTreeItem> _items;

    public CanonicalStoryFolderViewModel(
        CanonicalStoryFolderKind kind,
        string displayName,
        IEnumerable<ICanonicalStoryTreeItem> items)
    {
        Kind = kind;
        DisplayName = displayName;
        _items = new ObservableCollection<ICanonicalStoryTreeItem>((items ?? []).ToList());
        Items = new ReadOnlyObservableCollection<ICanonicalStoryTreeItem>(_items);
        ToggleCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
    }

    public CanonicalStoryFolderKind Kind { get; }
    public string DisplayName { get; }
    public IReadOnlyList<ICanonicalStoryTreeItem> Items { get; }
    public RelayCommand ToggleCommand { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    internal void SynchronizeItems(IReadOnlyList<ICanonicalStoryTreeItem> next)
    {
        for (var index = 0; index < next.Count; index++)
        {
            var item = next[index];
            if (index < _items.Count && ReferenceEquals(_items[index], item)) continue;
            var existingIndex = -1;
            for (var candidate = index + 1; candidate < _items.Count; candidate++)
            {
                if (!ReferenceEquals(_items[candidate], item)) continue;
                existingIndex = candidate;
                break;
            }
            if (existingIndex >= 0) _items.Move(existingIndex, index);
            else _items.Insert(index, item);
        }
        while (_items.Count > next.Count) _items.RemoveAt(_items.Count - 1);
    }

    internal bool Move(ICanonicalStoryTreeItem item, ICanonicalStoryTreeItem target)
    {
        var sourceIndex = _items.IndexOf(item);
        var targetIndex = _items.IndexOf(target);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex) return false;
        _items.Move(sourceIndex, targetIndex);
        return true;
    }
}

/// <summary>
/// Parallel 0.3.0.0 Story-first workspace state. Story Flow is always the home
/// graph; Actor selection is Inspector-only, while Session/Task require an
/// explicit open action to replace the middle graph temporarily.
/// </summary>
public sealed class CanonicalStoryWorkspaceViewModel : ObservableObject, IDisposable
{
    private CanonicalGraphResourceEditorViewModel _activeEditor;
    private ICanonicalStoryTreeItem? _selectedTreeItem;
    private CanonicalStoryFolderKind? _selectedFolderKind;
    private object? _inspectorSelection;
    private CanonicalNodeInspectorViewModel? _nodeInspector;
    private CanonicalStoryNodeFocusRequest? _storyNodeFocusRequest;
    private long _storyNodeFocusSequence;
    private string _parameterDropMessage = string.Empty;
    private string _projectId = "project";
    private string _projectDisplayName = "项目";
    private readonly CanonicalGraphLayoutStore? _layoutStore;
    private bool _disposed;
    private readonly Dictionary<(GraphResourceKind Kind, string Id), CanonicalGraphResourceEditorViewModel> _detachedEditors = [];

    public CanonicalStoryWorkspaceViewModel(
        GraphResourceEnvelope story,
        IEnumerable<ActorResourceInfo>? actors = null,
        IEnumerable<GraphResourceEnvelope>? sessions = null,
        IEnumerable<GraphResourceEnvelope>? tasks = null,
        IEnumerable<ItemResource>? items = null,
        CanonicalGraphLayoutStore? layoutStore = null)
    {
        ArgumentNullException.ThrowIfNull(story);
        if (story.ResourceKind != GraphResourceKind.Story)
            throw new ArgumentException("The Story-first workspace requires a story resource.", nameof(story));

        var actorResources = (actors ?? []).ToArray();
        if (actorResources.Any(actor => actor is null))
            throw new ArgumentException("Actor resources cannot contain null entries.", nameof(actors));
        var duplicateActor = actorResources.GroupBy(actor => actor.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateActor is not null)
            throw new ArgumentException($"Duplicate Actor ID '{duplicateActor.Key}'.", nameof(actors));
        var sessionResources = ValidateResources(sessions, GraphResourceKind.Session, nameof(sessions));
        var taskResources = ValidateResources(tasks, GraphResourceKind.Task, nameof(tasks));
        var createdEditors = new List<CanonicalGraphResourceEditorViewModel>();
        _layoutStore = layoutStore;
        try
        {
            StoryEditor = CreateEditor(story, layoutStore);
            createdEditors.Add(StoryEditor);
            SessionEditors = sessionResources.Select(resource =>
            {
                var editor = CreateEditor(resource, layoutStore);
                createdEditors.Add(editor);
                return editor;
            }).ToArray();
            TaskEditors = taskResources.Select(resource =>
            {
                var editor = CreateEditor(resource, layoutStore);
                createdEditors.Add(editor);
                return editor;
            }).ToArray();
        }
        catch
        {
            foreach (var editor in createdEditors) editor.Dispose();
            throw;
        }
        ActorItems = actorResources
            .Select(actor => new CanonicalStoryActorItem(actor))
            .ToArray();
        ItemItems = (items ?? [])
            .Select(item => new CanonicalStoryItemItem(item))
            .ToArray();
        SessionItems = SessionEditors.Select(editor => new CanonicalStoryGraphItem(editor)).ToArray();
        TaskItems = TaskEditors.Select(editor => new CanonicalStoryGraphItem(editor)).ToArray();
        Folders =
        [
            new(CanonicalStoryFolderKind.Actors, "角色", ActorItems),
            new(CanonicalStoryFolderKind.Items, "物品", ItemItems),
            new(CanonicalStoryFolderKind.Sessions, "会话", SessionItems),
            new(CanonicalStoryFolderKind.Tasks, "任务", TaskItems),
        ];

        _activeEditor = StoryEditor;
        _inspectorSelection = StoryEditor;
        foreach (var editor in AllEditors()) editor.PropertyChanged += OnEditorPropertyChanged;
        ReturnToStoryCommand = new RelayCommand(() => ReturnToStory(), () => IsLocalGraphOpen);
        OpenSelectedResourceCommand = new RelayCommand(
            () => OpenSelectedResource(),
            () => SelectedTreeItem is CanonicalStoryGraphItem);
        CreateSelectedResourceCommand = new RelayCommand(
            () => RequestCreateSelectedResource(),
            CanCreateSelectedResource);
        ReferenceSelectedResourceCommand = new RelayCommand(
            () => RequestReferenceSelectedResource(),
            CanReferenceSelectedResource);
        DeleteSelectedResourceCommand = new RelayCommand(
            () => RequestDeleteSelectedResource(),
            CanDeleteSelectedResource);
    }

    /// <summary>
    /// Adapts a loader snapshot without dropping unresolved membership entries
    /// or their owned/reference provenance.
    /// </summary>
    public CanonicalStoryWorkspaceViewModel(
        CanonicalStoryWorkspaceSnapshot snapshot,
        CanonicalGraphLayoutStore? layoutStore = null)
        : this(
            SnapshotStory(snapshot),
            SnapshotActors(snapshot),
            SnapshotGraphs(snapshot, GraphResourceKind.Session),
            SnapshotGraphs(snapshot, GraphResourceKind.Task),
            SnapshotItems(snapshot),
            layoutStore)
    {
        ValidationIssues = snapshot.ValidationIssues.ToArray();

        var actorsById = ActorItems.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var sessionsById = SessionItems.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var tasksById = TaskItems.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var individualItemsById = ItemItems.Where(item => item.Item is IndividualItemResource)
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var collectiveItemsById = ItemItems.Where(item => item.Item is CollectiveItemResource)
            .ToDictionary(item => item.Id, StringComparer.Ordinal);
        var missing = new List<CanonicalStoryMissingItem>();

        var actorFolderItems = AdaptEntries(
            snapshot.Actors,
            CanonicalStoryFolderKind.Actors,
            actorsById,
            missing,
            (item, membershipKind, provider) => new CanonicalStoryActorItem(item.Actor, membershipKind) { Provider = provider },
            id => id);
        var sessionFolderItems = AdaptEntries(
            snapshot.Sessions,
            CanonicalStoryFolderKind.Sessions,
            sessionsById,
            missing,
            (item, membershipKind, provider) => new CanonicalStoryGraphItem(item.Editor, membershipKind, provider),
            id => id);
        var individualFolderItems = AdaptEntries(
                snapshot.Items,
                CanonicalStoryFolderKind.Items,
                individualItemsById,
                missing,
                (item, membershipKind, provider) => new CanonicalStoryItemItem(item.Item, membershipKind) { Provider = provider },
                id => $"item:{id}");
        var groupFolderItems = AdaptEntries(
                snapshot.ItemGroups,
                CanonicalStoryFolderKind.Items,
                collectiveItemsById,
                missing,
                (item, membershipKind, provider) => new CanonicalStoryItemItem(item.Item, membershipKind) { Provider = provider },
                id => $"item_group:{id}");
        var taskFolderItems = AdaptEntries(
            snapshot.Tasks,
            CanonicalStoryFolderKind.Tasks,
            tasksById,
            missing,
            (item, membershipKind, provider) => new CanonicalStoryGraphItem(item.Editor, membershipKind, provider),
            id => id);

        var displayOrder = snapshot.Membership.DisplayOrder;
        actorFolderItems = ApplyDisplayOrder(actorFolderItems, displayOrder.Actors,
            item => OrderHandle(CanonicalStoryFolderKind.Actors, item));
        var itemFolderItems = ApplyDisplayOrder(individualFolderItems.Concat(groupFolderItems), displayOrder.Items,
            item => OrderHandle(CanonicalStoryFolderKind.Items, item));
        sessionFolderItems = ApplyDisplayOrder(sessionFolderItems, displayOrder.Sessions,
            item => OrderHandle(CanonicalStoryFolderKind.Sessions, item));
        taskFolderItems = ApplyDisplayOrder(taskFolderItems, displayOrder.Tasks,
            item => OrderHandle(CanonicalStoryFolderKind.Tasks, item));

        ActorItems = actorFolderItems.OfType<CanonicalStoryActorItem>().ToArray();
        ItemItems = itemFolderItems.OfType<CanonicalStoryItemItem>().ToArray();
        SessionItems = sessionFolderItems.OfType<CanonicalStoryGraphItem>().ToArray();
        TaskItems = taskFolderItems.OfType<CanonicalStoryGraphItem>().ToArray();
        MissingItems = missing.ToArray();
        Folders =
        [
            new(CanonicalStoryFolderKind.Actors, "角色", actorFolderItems),
            new(CanonicalStoryFolderKind.Items, "物品", itemFolderItems),
            new(CanonicalStoryFolderKind.Sessions, "会话", sessionFolderItems),
            new(CanonicalStoryFolderKind.Tasks, "任务", taskFolderItems),
        ];
    }

    public CanonicalGraphResourceEditorViewModel StoryEditor { get; }
    public IReadOnlyList<CanonicalGraphResourceEditorViewModel> SessionEditors { get; private set; }
    public IReadOnlyList<CanonicalGraphResourceEditorViewModel> TaskEditors { get; private set; }
    public IReadOnlyList<CanonicalStoryActorItem> ActorItems { get; private set; }
    public IReadOnlyList<CanonicalStoryItemItem> ItemItems { get; private set; }
    public IReadOnlyList<CanonicalStoryGraphItem> SessionItems { get; private set; }
    public IReadOnlyList<CanonicalStoryGraphItem> TaskItems { get; private set; }
    public IReadOnlyList<CanonicalStoryMissingItem> MissingItems { get; private set; } = [];
    public IReadOnlyList<CanonicalStoryFolderViewModel> Folders { get; }
    public IReadOnlyList<ValidationIssue> ValidationIssues { get; private set; } = [];
    public IReadOnlyList<ValidationIssue> LastAggregateAuthoringIssues { get; private set; } = [];
    public string ParameterDropMessage
    {
        get => _parameterDropMessage;
        private set
        {
            if (!SetProperty(ref _parameterDropMessage, value)) return;
            OnPropertyChanged(nameof(HasParameterDropMessage));
        }
    }
    public bool HasParameterDropMessage => !string.IsNullOrWhiteSpace(ParameterDropMessage);
    public IReadOnlyList<CanonicalGraphResourceEditorViewModel> Editors => AllEditors().ToArray();
    public bool IsWritableEditor(CanonicalGraphResourceEditorViewModel editor)
        => ReferenceEquals(editor, StoryEditor) || _detachedEditors.ContainsValue(editor) || SessionItems.Concat(TaskItems).Any(item => ReferenceEquals(item.Editor, editor) && !item.IsReadOnly);
    public bool HasDirtyEditors => AllEditors().Any(editor => IsWritableEditor(editor) && editor.IsDirty);
    public RelayCommand ReturnToStoryCommand { get; }
    public RelayCommand OpenSelectedResourceCommand { get; }
    public RelayCommand CreateSelectedResourceCommand { get; }
    public RelayCommand ReferenceSelectedResourceCommand { get; }
    public RelayCommand DeleteSelectedResourceCommand { get; }

    public Action<GraphResourceKind>? CreateResourceRequested { get; set; }
    public Action<GraphResourceKind>? ReferenceResourceRequested { get; set; }
    public Action? CreateActorRequested { get; set; }
    public Action? ReferenceActorRequested { get; set; }
    public Action? CreateItemRequested { get; set; }
    public Action? ReferenceItemRequested { get; set; }
    public Action<ICanonicalStoryTreeItem>? DeleteResourceRequested { get; set; }
    public Action<ICanonicalStoryTreeItem>? RenameResourceRequested { get; set; }
    public Func<CanonicalStoryFolderKind, IReadOnlyList<string>, bool>? ResourceOrderChangeRequested { get; set; }
    public Action? ReturnToProjectRequested { get; set; }
    /// <summary>Opens a provider-backed graph in a host-owned read-only viewer.</summary>
    public Action<CanonicalStoryGraphItem>? ReadOnlyResourceRequested { get; set; }

    public CanonicalGraphResourceEditorViewModel ActiveEditor
    {
        get => _activeEditor;
        private set
        {
            if (!SetProperty(ref _activeEditor, value)) return;
            OnPropertyChanged(nameof(ActiveGraphHost));
            OnPropertyChanged(nameof(CanEditActivePresentation));
            OnPropertyChanged(nameof(IsStoryFlowActive));
            OnPropertyChanged(nameof(IsLocalGraphOpen));
            OnPropertyChanged(nameof(Breadcrumbs));
            ReturnToStoryCommand.RaiseCanExecuteChanged();
        }
    }

    public bool CanEditActivePresentation => IsWritableEditor(ActiveEditor);
    public GraphEditorHostViewModel ActiveGraphHost => ActiveEditor.Host;
    public bool IsStoryFlowActive => ReferenceEquals(ActiveEditor, StoryEditor);
    public bool IsLocalGraphOpen => !IsStoryFlowActive;
    public object? InspectorSelection
    {
        get => _inspectorSelection;
        private set
        {
            if (!SetProperty(ref _inspectorSelection, value)) return;
            OnPropertyChanged(nameof(InspectorTitle));
            OnPropertyChanged(nameof(InspectorKindText));
            OnPropertyChanged(nameof(InspectorId));
            OnPropertyChanged(nameof(InspectorSaveStateText));
            OnPropertyChanged(nameof(InspectorValidationText));
            OnPropertyChanged(nameof(HasResourceInspectorDetails));
            OnPropertyChanged(nameof(CanEditActorPortrait));
            OnPropertyChanged(nameof(InspectorPortraitEditor));
            OnPropertyChanged(nameof(InspectorIdentityLabel));
            OnPropertyChanged(nameof(InspectorIdentityText));
            OnPropertyChanged(nameof(InspectorTagsText));
            OnPropertyChanged(nameof(InspectorTaskEditor));
            OnPropertyChanged(nameof(HasTaskDescription));
            OnPropertyChanged(nameof(IsTaskDescriptionReadOnly));
            OnPropertyChanged(nameof(InspectorOwnershipText));
            OnPropertyChanged(nameof(InspectorReferenceBadge));
            OnPropertyChanged(nameof(InspectorSourceDetailsText));
        }
    }

    /// <summary>Node Inspector projection for the currently selected graph node.</summary>
    public string? MediaProjectDirectory { get; set; }
    public CanonicalNodeInspectorViewModel? NodeInspector => _nodeInspector;
    public Action<CanonicalStoryActorItem>? EditActorPortraitRequested { get; set; }
    public Func<CanonicalStoryActorItem, ActorEditorViewModel?>? PortraitEditorFactory { get; set; }
    private readonly Dictionary<string, ActorEditorViewModel> _portraitEditors = new(StringComparer.Ordinal);
    public ActorEditorViewModel? InspectorPortraitEditor
    {
        get
        {
            if (InspectorSelection is not CanonicalStoryActorItem actor) return null;
            if (_portraitEditors.TryGetValue(actor.Id, out var editor)) return editor;
            editor = PortraitEditorFactory?.Invoke(actor);
            if (editor is not null)
            {
                _portraitEditors[actor.Id] = editor;
                editor.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName is nameof(ActorEditorViewModel.DefaultPortraitRef) or nameof(ActorEditorViewModel.PortraitVariants))
                        actor.UpdatePortraits(editor.DefaultPortraitRef, editor.PortraitVariants);
                };
                actor.UpdatePortraits(editor.DefaultPortraitRef, editor.PortraitVariants);
            }
            return editor;
        }
    }
    public bool CanEditActorPortrait => InspectorSelection is CanonicalStoryActorItem { IsReadOnly: false, Actor.Type: not null };
    public void EditActorPortrait()
    {
        if (CanEditActorPortrait && InspectorSelection is CanonicalStoryActorItem actor)
            EditActorPortraitRequested?.Invoke(actor);
    }
    public CanonicalNodeInspectorViewModel? Inspector => _nodeInspector;

    public string InspectorTitle => InspectorSelection switch
    {
        CanonicalNodeInspectorViewModel node => node.DisplayName,
        CanonicalStoryActorItem actor => actor.DisplayName,
        CanonicalStoryItemItem item => item.DisplayName,
        CanonicalStoryMissingItem missing => missing.DisplayName,
        CanonicalGraphResourceEditorViewModel editor => editor.DisplayName,
        _ => StoryEditor.DisplayName,
    };

    public string InspectorKindText => InspectorSelection switch
    {
        CanonicalNodeInspectorViewModel node => NodeKindText(node),
        CanonicalStoryActorItem { Actor.Type: CollectiveActorResource.ResourceType } => "角色组",
        CanonicalStoryActorItem => "角色",
        CanonicalStoryItemItem { Item: IndividualItemResource } => "物品",
        CanonicalStoryItemItem { Item: CollectiveItemResource } => "物品组",
        CanonicalStoryMissingItem { FolderKind: CanonicalStoryFolderKind.Actors } => "缺失角色",
        CanonicalStoryMissingItem { FolderKind: CanonicalStoryFolderKind.Items } => "缺失物品",
        CanonicalStoryMissingItem { FolderKind: CanonicalStoryFolderKind.Sessions } => "缺失会话",
        CanonicalStoryMissingItem { FolderKind: CanonicalStoryFolderKind.Tasks } => "缺失任务",
        CanonicalGraphResourceEditorViewModel { ResourceKind: GraphResourceKind.Session } => "会话",
        CanonicalGraphResourceEditorViewModel { ResourceKind: GraphResourceKind.Task } => "任务",
        _ => "Story",
    };

    public string InspectorId => InspectorSelection switch
    {
        CanonicalNodeInspectorViewModel node => node.NodeId,
        CanonicalStoryActorItem actor => actor.Id,
        CanonicalStoryItemItem item => item.Id,
        CanonicalStoryMissingItem missing => missing.Id,
        CanonicalStoryGraphItem graph => graph.Id,
        CanonicalGraphResourceEditorViewModel editor => editor.Id,
        _ => StoryEditor.Id,
    };

    public bool HasResourceInspectorDetails => InspectorSelection is CanonicalStoryActorItem
        or CanonicalStoryItemItem or CanonicalStoryGraphItem
        or CanonicalGraphResourceEditorViewModel { ResourceKind: GraphResourceKind.Session or GraphResourceKind.Task };

    public string InspectorIdentityLabel => InspectorSelection switch
    {
        CanonicalStoryActorItem { Actor.Type: CollectiveActorResource.ResourceType } => "Group_ID",
        CanonicalStoryActorItem => "NPC_ID",
        CanonicalStoryItemItem { Item: CollectiveItemResource } => "Group_ID",
        CanonicalStoryItemItem => "Item_ID",
        CanonicalStoryGraphItem { ResourceKind: GraphResourceKind.Session } or CanonicalGraphResourceEditorViewModel { ResourceKind: GraphResourceKind.Session } => "Session_ID",
        CanonicalStoryGraphItem { ResourceKind: GraphResourceKind.Task } or CanonicalGraphResourceEditorViewModel { ResourceKind: GraphResourceKind.Task } => "Task_ID",
        _ => "Resource_ID",
    };

    public string InspectorIdentityText => ResourceIdentityPresentation.Format(InspectorIdentityLabel, InspectorId);

    public CanonicalGraphResourceEditorViewModel? InspectorTaskEditor => InspectorSelection switch
    {
        CanonicalGraphResourceEditorViewModel { ResourceKind: GraphResourceKind.Task } editor => editor,
        CanonicalStoryGraphItem { ResourceKind: GraphResourceKind.Task } item => item.Editor,
        _ => null,
    };
    public bool HasTaskDescription => InspectorTaskEditor is not null;
    public bool IsTaskDescriptionReadOnly => InspectorTaskEditor is not { } editor || !IsWritableEditor(editor);

    public string InspectorTagsText => InspectorSelection switch
    {
        CanonicalStoryActorItem actor => actor.Actor.Tags.Count == 0 ? "—" : string.Join("、", actor.Actor.Tags),
        CanonicalStoryItemItem item => item.Tags.Count == 0 ? "—" : string.Join("、", item.Tags),
        CanonicalStoryGraphItem graph => graph.Editor.Tags.Count == 0 ? "—" : string.Join("、", graph.Editor.Tags),
        CanonicalGraphResourceEditorViewModel editor => editor.Tags.Count == 0 ? "—" : string.Join("、", editor.Tags),
        _ => "—",
    };

    public string InspectorOwnershipText => InspectorSelection switch
    {
        CanonicalGraphResourceEditorViewModel editor when SessionItems.Concat(TaskItems)
            .FirstOrDefault(item => ReferenceEquals(item.Editor, editor)) is { } resource
            => resource.IsReadOnly ? "[引用] 只读资源\n" + ShellViewModel.ProviderDescription(resource.Provider!)
                : resource.IsReferenced ? "[引用] " : string.Empty,
        CanonicalStoryActorItem { IsReadOnly: true } actor => "[引用] 只读资源\n" + ShellViewModel.ProviderDescription(actor.Provider!),
        CanonicalStoryItemItem { IsReadOnly: true } item => "[引用] 只读资源\n" + ShellViewModel.ProviderDescription(item.Provider!),
        CanonicalStoryGraphItem { IsReadOnly: true } graph => "[引用] 只读资源\n" + ShellViewModel.ProviderDescription(graph.Provider!),
        CanonicalStoryActorItem { IsReferenced: true } or CanonicalStoryItemItem { IsReferenced: true }
            or CanonicalStoryGraphItem { IsReferenced: true } => "引用",
        CanonicalStoryActorItem or CanonicalStoryItemItem or CanonicalStoryGraphItem => string.Empty,
        _ => string.Empty,
    };

    public string InspectorReferenceBadge => InspectorOwnershipText.Contains("引用", StringComparison.Ordinal)
        ? "[引用]" : string.Empty;
    public string InspectorSourceDetailsText => InspectorOwnershipText.Contains('\n')
        ? InspectorOwnershipText[(InspectorOwnershipText.IndexOf('\n') + 1)..] : string.Empty;

    public string InspectorSaveStateText => InspectorSelection switch
    {
        CanonicalNodeInspectorViewModel node => string.Empty,
        CanonicalGraphResourceEditorViewModel editor => editor.SaveStateText,
        CanonicalStoryMissingItem => "资源缺失",
        _ => string.Empty,
    };

    public string InspectorValidationText => InspectorSelection switch
    {
        CanonicalNodeInspectorViewModel node => string.Join(Environment.NewLine,
            node.ValidationIssues.Select(ValidationIssuePresentation.FormatCompact)),
        CanonicalGraphResourceEditorViewModel editor => editor.ValidationText,
        CanonicalStoryMissingItem missing => $"资源“{missing.Id}”缺失，请重新添加或解除该资源。{Environment.NewLine}请在“问题”面板查看详情。",
        _ => string.Empty,
    };

    private static string NodeKindText(CanonicalNodeInspectorViewModel node)
        => GraphNodeDefinitionRegistry.TryGet(node.Host.Scope, node.NodeType, out var definition)
            ? definition.DisplayName
            : "节点";

    public ICanonicalStoryTreeItem? SelectedTreeItem
    {
        get => _selectedTreeItem;
        private set
        {
            if (!SetProperty(ref _selectedTreeItem, value)) return;
            OnPropertyChanged(nameof(SelectedActor));
            OnPropertyChanged(nameof(SelectedItem));
            OnPropertyChanged(nameof(SelectedGraphResource));
            OnPropertyChanged(nameof(SelectedMissingResource));
            OpenSelectedResourceCommand.RaiseCanExecuteChanged();
            DeleteSelectedResourceCommand.RaiseCanExecuteChanged();
        }
    }

    public CanonicalStoryFolderKind? SelectedFolderKind
    {
        get => _selectedFolderKind;
        private set
        {
            if (!SetProperty(ref _selectedFolderKind, value)) return;
            OnPropertyChanged(nameof(SelectedResourceKind));
            CreateSelectedResourceCommand.RaiseCanExecuteChanged();
            ReferenceSelectedResourceCommand.RaiseCanExecuteChanged();
        }
    }

    public GraphResourceKind? SelectedResourceKind => SelectedFolderKind switch
    {
        CanonicalStoryFolderKind.Sessions => GraphResourceKind.Session,
        CanonicalStoryFolderKind.Tasks => GraphResourceKind.Task,
        _ => null,
    };

    public CanonicalStoryActorItem? SelectedActor => SelectedTreeItem as CanonicalStoryActorItem;
    public CanonicalStoryItemItem? SelectedItem => SelectedTreeItem as CanonicalStoryItemItem;
    public CanonicalStoryGraphItem? SelectedGraphResource => SelectedTreeItem as CanonicalStoryGraphItem;
    public CanonicalStoryMissingItem? SelectedMissingResource => SelectedTreeItem as CanonicalStoryMissingItem;

    public IReadOnlyList<CanonicalStoryBreadcrumb> Breadcrumbs => IsStoryFlowActive
        ? [ProjectBreadcrumb(), Breadcrumb(StoryEditor, isCurrent: true)]
        : [ProjectBreadcrumb(), Breadcrumb(StoryEditor, isCurrent: false), Breadcrumb(ActiveEditor, isCurrent: true)];

    public void ConfigureProjectBreadcrumb(string projectId, string displayName, Action returnToProject)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException("Project ID is required.", nameof(projectId));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Project display name is required.", nameof(displayName));
        _projectId = projectId;
        _projectDisplayName = displayName;
        ReturnToProjectRequested = returnToProject ?? throw new ArgumentNullException(nameof(returnToProject));
        OnPropertyChanged(nameof(Breadcrumbs));
    }

    public bool ActivateBreadcrumb(CanonicalStoryBreadcrumb breadcrumb)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(breadcrumb);
        if (breadcrumb.IsCurrent) return true;
        switch (breadcrumb.Kind)
        {
            case CanonicalStoryBreadcrumbKind.Project:
                ReturnToProjectRequested?.Invoke();
                return ReturnToProjectRequested is not null;
            case CanonicalStoryBreadcrumbKind.Story:
                return ReturnToStory();
            default:
                return false;
        }
    }

    public CanonicalStoryNodeFocusRequest? StoryNodeFocusRequest
    {
        get => _storyNodeFocusRequest;
        private set => SetProperty(ref _storyNodeFocusRequest, value);
    }

    public bool RequestStoryNodeFocus(string? nodeId, string? field = null)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(nodeId)
            || StoryEditor.Host.Nodes.Count(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)) != 1)
            return false;
        if (!ReturnToStory()) return false;
        StoryNodeFocusRequest = new(nodeId, field, ++_storyNodeFocusSequence);
        return true;
    }

    /// <summary>Routes the visual graph's node selection to the shared Inspector.</summary>
    public bool SelectGraphNode(GraphEditorNodeViewModel? node)
    {
        ThrowIfDisposed();
        if (node is null || !ActiveGraphHost.Nodes.Contains(node))
        {
            ClearGraphSelection();
            return false;
        }

        DisposeNodeInspector();
        _nodeInspector = new CanonicalNodeInspectorViewModel(ActiveGraphHost, node, ActorItems, ItemItems);
        _nodeInspector.PropertyChanged += OnNodeInspectorPropertyChanged;
        OnPropertyChanged(nameof(NodeInspector));
        OnPropertyChanged(nameof(Inspector));
        InspectorSelection = _nodeInspector;
        return true;
    }

    /// <summary>Restores the active resource Inspector after graph selection clears.</summary>
    public void ClearGraphSelection()
    {
        ThrowIfDisposed();
        DisposeNodeInspector();
        OnPropertyChanged(nameof(NodeInspector));
        OnPropertyChanged(nameof(Inspector));
        InspectorSelection = ActiveEditor;
    }

    /// <summary>Single-click selection changes only Inspector state.</summary>
    public bool SelectTreeItem(ICanonicalStoryTreeItem? item)
    {
        ThrowIfDisposed();
        if (item is not null && !Contains(item)) return false;
        SelectedTreeItem = item;
        SelectedFolderKind = item is null ? SelectedFolderKind : FolderFor(item);
        DisposeNodeInspector();
        OnPropertyChanged(nameof(NodeInspector));
        OnPropertyChanged(nameof(Inspector));
        InspectorSelection = item switch
        {
            CanonicalStoryActorItem actor => actor,
            CanonicalStoryItemItem itemResource => itemResource,
            CanonicalStoryGraphItem graph => graph.Editor,
            CanonicalStoryMissingItem missing => missing,
            _ => ActiveEditor,
        };
        return true;
    }

    public bool SelectFolder(CanonicalStoryFolderKind folderKind)
    {
        ThrowIfDisposed();
        if (!Enum.IsDefined(folderKind)) return false;
        SelectedFolderKind = folderKind;
        SelectedTreeItem = null;
        ClearGraphSelection();
        return true;
    }

    public bool RequestCreate(CanonicalStoryFolderKind folderKind)
    {
        if (!SelectFolder(folderKind) || !CanCreateSelectedResource()) return false;
        RequestCreateSelectedResource();
        return true;
    }

    public bool RequestReference(CanonicalStoryFolderKind folderKind)
    {
        if (!SelectFolder(folderKind) || !CanReferenceSelectedResource()) return false;
        RequestReferenceSelectedResource();
        return true;
    }

    public bool RequestDelete(ICanonicalStoryTreeItem item)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(item);
        if (!SelectTreeItem(item) || !CanDeleteSelectedResource() || DeleteResourceRequested is null)
            return false;
        DeleteResourceRequested(item);
        return true;
    }

    public bool RequestRename(ICanonicalStoryTreeItem item)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(item);
        if (item is CanonicalStoryMissingItem || IsProviderResource(item) || RenameResourceRequested is null || !SelectTreeItem(item))
            return false;
        RenameResourceRequested(item);
        return true;
    }

    /// <summary>Moves a resource inside its current folder without changing membership ownership.</summary>
    public bool CanReorderResource(ICanonicalStoryTreeItem item, ICanonicalStoryTreeItem target)
    {
        if (_disposed || item is null || target is null || ReferenceEquals(item, target)) return false;
        return Contains(item) && Contains(target) && FolderFor(item) == FolderFor(target);
    }

    /// <summary>Moves a resource inside its current folder without changing membership ownership.</summary>
    public bool ReorderResource(ICanonicalStoryTreeItem item, ICanonicalStoryTreeItem target)
        => ReorderResource(item, target, insertAfter: false);

    public bool ReorderResource(ICanonicalStoryTreeItem item, ICanonicalStoryTreeItem target, bool insertAfter)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(target);
        if (!CanReorderResource(item, target)) return false;
        var folderKind = FolderFor(item);
        var folder = Folders.Single(candidate => candidate.Kind == folderKind);
        var next = folder.Items.ToList();
        if (!next.Remove(item)) return false;
        var targetIndex = next.IndexOf(target);
        if (targetIndex < 0) return false;
        next.Insert(targetIndex + (insertAfter ? 1 : 0), item);
        var handles = next.Select(candidate => OrderHandle(folderKind, candidate)).ToArray();
        if (ResourceOrderChangeRequested is not null
            && !ResourceOrderChangeRequested(folderKind, handles)) return false;
        folder.SynchronizeItems(next);
        SynchronizeTypedFolderOrder(folderKind, folder.Items);
        return true;
    }

    /// <summary>Double-click/Enter behavior for a selected Session or Task.</summary>
    public bool OpenSelectedResource()
        => SelectedGraphResource is { } item && OpenGraphResource(item);

    public bool OpenGraphResource(CanonicalStoryGraphItem item)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(item);
        if (!Contains(item)) return false;
        if (item.IsReadOnly)
        {
            ReadOnlyResourceRequested?.Invoke(item);
            return ReadOnlyResourceRequested is not null;
        }
        if (ReferenceEquals(ActiveEditor, item.Editor)) return true;
        SelectedTreeItem = item;
        ActiveEditor = item.Editor;
        ClearGraphSelection();
        InspectorSelection = item.Editor;
        return true;
    }

    public bool ReturnToStory()
    {
        ThrowIfDisposed();
        if (IsStoryFlowActive) return true;
        ActiveEditor = StoryEditor;
        ClearGraphSelection();
        InspectorSelection = StoryEditor;
        return true;
    }

    /// <summary>
    /// Only resolved Session/Task members can be placed, and only while the
    /// Story Flow editor owns the middle canvas.
    /// </summary>
    public bool CanPlaceAggregate(CanonicalStoryGraphItem? item)
    {
        ThrowIfDisposed();
        return IsStoryFlowActive && item is not null && Contains(item)
            && item.ResourceKind is GraphResourceKind.Session or GraphResourceKind.Task;
    }

    /// <summary>
    /// Builds a detached bound aggregate and commits it through the Story host
    /// as one graph edit. Rejection never changes graph or layout state.
    /// </summary>
    public bool PlaceAggregate(CanonicalStoryGraphItem? item, string? placementNodeId, double x, double y)
    {
        ThrowIfDisposed();
        if (!IsStoryFlowActive)
            return FailAggregate(new("graph.aggregate.drop.story_flow.required",
                "Resources can only be placed on the active Story Flow.", "drop_target"));
        if (item is null || !Contains(item)
            || item.ResourceKind is not (GraphResourceKind.Session or GraphResourceKind.Task))
            return FailAggregate(new("graph.aggregate.drop.resource.required",
                "A resolved Session or Task member is required.", "resource"));
        if (!double.IsFinite(x) || !double.IsFinite(y))
            return FailAggregate(new("graph.aggregate.drop.position.invalid",
                "Aggregate node position must be finite.", "position"));

        var result = CanonicalAggregateNodeFactory.Create(
            StoryEditor.Host.Graph,
            item.Editor.CreateSnapshot(),
            placementNodeId);
        LastAggregateAuthoringIssues = result.Issues.ToArray();
        OnPropertyChanged(nameof(LastAggregateAuthoringIssues));
        if (!result.IsSuccess || result.Candidate is not { } candidate)
            return false;
        if (!StoryEditor.Host.AddNode(candidate))
        {
            LastAggregateAuthoringIssues = StoryEditor.Host.LastValidationIssues.ToArray();
            OnPropertyChanged(nameof(LastAggregateAuthoringIssues));
            return false;
        }

        StoryEditor.Host.SetNodePosition(candidate.Id, x, y);
        LastAggregateAuthoringIssues = [];
        OnPropertyChanged(nameof(LastAggregateAuthoringIssues));
        return true;
    }

    /// <summary>
    /// Applies a resource identity to one compatible author-facing node parameter.
    /// It never changes tree selection or the current Inspector context.
    /// </summary>
    public bool ApplyResourceToNodeParameter(GraphEditorNodeViewModel? node, ICanonicalStoryTreeItem? item)
    {
        ThrowIfDisposed();
        if (node is null || item is null || !ActiveGraphHost.Nodes.Contains(node) || !Contains(item))
            return FailParameterDrop("无法将该资源拖放到当前节点。");

        bool changed;
        switch (item)
        {
            case CanonicalStoryActorItem actor:
                changed = ApplyActorToNode(node, actor);
                break;
            case CanonicalStoryItemItem itemResource:
                changed = ApplyItemToNode(node, itemResource);
                break;
            default:
                return FailParameterDrop("该资源只能放置为 Story Flow 聚合节点。");
        }

        if (!changed) return false;
        ParameterDropMessage = $"已将“{item.DisplayName}”应用到“{node.DisplayName}”。";
        return true;
    }

    public void ClearParameterDropMessage() => ParameterDropMessage = string.Empty;

    private bool ApplyActorToNode(GraphEditorNodeViewModel node, CanonicalStoryActorItem actor)
    {
        if (ActiveGraphHost.Scope == GraphScope.Session && node.Type == "line")
            return CommitParameterDrop(() => ActiveGraphHost.SetNodeProperty(
                node.NodeId, "speaker_actor_id", JsonSerializer.SerializeToElement(actor.Id)));

        if (ActiveGraphHost.Scope == GraphScope.Task && node.Type == CanonicalTaskObjectiveSchema.NodeType)
        {
            var type = ReadNodeString(node, CanonicalTaskObjectiveSchema.TypeProperty);
            var property = type switch
            {
                CanonicalTaskObjectiveSchema.KillEntity => CanonicalTaskObjectiveSchema.EntityProperty,
                CanonicalTaskObjectiveSchema.InteractActor => CanonicalTaskObjectiveSchema.ActorIdProperty,
                _ => null,
            };
            if (property is not null)
                return CommitParameterDrop(() => ActiveGraphHost.SetNodeProperty(
                    node.NodeId, property, JsonSerializer.SerializeToElement(actor.Id)));
        }

        if (ActiveGraphHost.Scope == GraphScope.StoryFlow && node.Type == "start")
        {
            var graphNode = ActiveGraphHost.Graph.Nodes.Single(candidate =>
                string.Equals(candidate.Id, node.NodeId, StringComparison.Ordinal));
            var trigger = StoryStartSchema.ReadTriggers(graphNode)
                .FirstOrDefault(candidate => candidate.TriggerType == StoryStartSchema.ActorInteraction);
            if (trigger is not null)
            {
                var properties = trigger.TriggerProperties.ValueKind == JsonValueKind.Object
                    ? trigger.TriggerProperties.EnumerateObject().ToDictionary(
                        property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal)
                    : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                properties[StoryStartSchema.ActorIdProperty] = JsonSerializer.SerializeToElement(actor.Id);
                return CommitParameterDrop(() => ActiveGraphHost.SetStoryStartTriggerProperties(
                    node.NodeId, trigger.PortId, properties));
            }
            return FailParameterDrop("Start 节点中没有“角色交互”启动条件，请先在 Inspector 中添加或切换启动条件。");
        }

        return FailParameterDrop("角色资源只能拖到 Start 的角色交互、会话台词说话者或任务角色目标。");
    }

    private bool ApplyItemToNode(GraphEditorNodeViewModel node, CanonicalStoryItemItem item)
    {
        if (ActiveGraphHost.Scope == GraphScope.Task && node.Type == CanonicalTaskObjectiveSchema.NodeType
            && ReadNodeString(node, CanonicalTaskObjectiveSchema.TypeProperty) == CanonicalTaskObjectiveSchema.CollectItem)
            return CommitParameterDrop(() => ActiveGraphHost.SetNodeProperty(
                node.NodeId, CanonicalTaskObjectiveSchema.ItemProperty, JsonSerializer.SerializeToElement(item.Id)));

        if (ActiveGraphHost.Scope == GraphScope.StoryFlow && node.Type == CanonicalStoryActionSchema.NodeType
            && ReadNodeString(node, CanonicalStoryActionSchema.TypeProperty) == CanonicalStoryActionSchema.GiveItem)
        {
            if (item.Item is not IndividualItemResource)
                return FailParameterDrop("“物品给予”只能使用个体物品，不能使用物品组。");
            return CommitParameterDrop(() => ActiveGraphHost.SetNodeProperty(
                node.NodeId, CanonicalStoryActionSchema.ItemProperty, JsonSerializer.SerializeToElement(item.Id)));
        }

        return FailParameterDrop("物品资源只能拖到“收集物品”目标或“物品给予”动作。");
    }

    private bool CommitParameterDrop(Func<bool> mutation)
    {
        if (mutation()) return true;
        var detail = ActiveGraphHost.LastValidationIssues.FirstOrDefault()?.Message;
        return FailParameterDrop(string.IsNullOrWhiteSpace(detail) ? "资源参数没有发生变化。" : $"资源参数未修改：{detail}");
    }

    private bool FailParameterDrop(string message)
    {
        ParameterDropMessage = message;
        return false;
    }

    private static string ReadNodeString(GraphEditorNodeViewModel node, string property)
        => node.Properties.TryGetValue(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;

    /// <summary>Builds a detached synchronization plan for one member editor.</summary>
    public CanonicalAggregateSynchronizationPlan AnalyzeAggregateSynchronization(
        CanonicalGraphResourceEditorViewModel editor)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(editor);
        if (!ContainsGraphEditor(editor))
            throw new ArgumentException("Aggregate synchronization requires a Session or Task member editor.", nameof(editor));
        return StoryEditor.Host.AnalyzeAggregateSynchronization(editor.CreateSnapshot());
    }

    public bool ApplyAggregateSynchronization(
        CanonicalAggregateSynchronizationPlan plan,
        bool confirmReferencedRemoval = false)
    {
        ThrowIfDisposed();
        return StoryEditor.Host.ApplyAggregateSynchronization(plan, confirmReferencedRemoval);
    }

    public bool RollbackLastStoryEdit()
    {
        ThrowIfDisposed();
        return StoryEditor.Host.RollbackLastEdit();
    }

    public void SelectStoryInspector()
    {
        ThrowIfDisposed();
        SelectedTreeItem = null;
        ClearGraphSelection();
        InspectorSelection = StoryEditor;
    }

    public void RefreshResourceCommandStates()
    {
        CreateSelectedResourceCommand.RaiseCanExecuteChanged();
        ReferenceSelectedResourceCommand.RaiseCanExecuteChanged();
        DeleteSelectedResourceCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Applies a loader snapshot to the existing workspace. Unchanged folders,
    /// items, graph editors, active graph, selection, and expansion state keep
    /// their object identity; only the changed membership entries are inserted,
    /// removed, or replaced.
    /// </summary>
    public void ApplyResourceSnapshot(
        CanonicalStoryWorkspaceSnapshot snapshot,
        CanonicalStoryFolderKind selectedFolderKind,
        string? selectedResourceId = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!string.Equals(snapshot.Story.Id, StoryEditor.Id, StringComparison.Ordinal))
            throw new ArgumentException("A resource refresh must target the active canonical Story.", nameof(snapshot));

        _ = StoryEditor.ApplyPersistedSnapshot(snapshot.Story);

        var incoming = new CanonicalStoryWorkspaceViewModel(snapshot, _layoutStore);
        var currentGraphItems = SessionItems.Concat(TaskItems)
            .ToDictionary(item => (item.ResourceKind, item.Id));
        var currentActors = ActorItems.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var currentItems = ItemItems.ToDictionary(item => (item.Type, item.Id));
        var currentMissing = MissingItems.ToDictionary(item => (item.FolderKind, item.Id, item.MembershipKind));
        var transferredEditors = new HashSet<CanonicalGraphResourceEditorViewModel>();

        ICanonicalStoryTreeItem MergeItem(ICanonicalStoryTreeItem item)
        {
            switch (item)
            {
                case CanonicalStoryGraphItem graph:
                    if (currentGraphItems.TryGetValue((graph.ResourceKind, graph.Id), out var existingGraph))
                    {
                        return existingGraph.MembershipKind == graph.MembershipKind
                            && SameProvider(existingGraph.Provider, graph.Provider)
                            ? existingGraph
                            : TransferGraphItem(graph);
                    }
                    if (!graph.IsReadOnly && _detachedEditors.Remove((graph.ResourceKind, graph.Id), out var detached))
                        return new CanonicalStoryGraphItem(detached, graph.MembershipKind);
                    transferredEditors.Add(graph.Editor);
                    return graph;
                case CanonicalStoryActorItem actor
                    when currentActors.TryGetValue(actor.Id, out var existingActor)
                         && existingActor.MembershipKind == actor.MembershipKind
                         && string.Equals(existingActor.DisplayName, actor.DisplayName, StringComparison.Ordinal)
                         && string.Equals(existingActor.Actor.Type, actor.Actor.Type, StringComparison.Ordinal)
                         && SameProvider(existingActor.Provider, actor.Provider):
                    return existingActor;
                case CanonicalStoryItemItem itemResource
                    when currentItems.TryGetValue((itemResource.Type, itemResource.Id), out var existingItem)
                         && existingItem.MembershipKind == itemResource.MembershipKind
                         && string.Equals(existingItem.DisplayName, itemResource.DisplayName, StringComparison.Ordinal)
                         && existingItem.Tags.SequenceEqual(itemResource.Tags, StringComparer.Ordinal)
                         && SameProvider(existingItem.Provider, itemResource.Provider):
                    return existingItem;
                case CanonicalStoryMissingItem missing
                    when currentMissing.TryGetValue((missing.FolderKind, missing.Id, missing.MembershipKind), out var existingMissing):
                    return existingMissing;
                default:
                    return item;
            }
        }

        CanonicalStoryGraphItem TransferGraphItem(CanonicalStoryGraphItem graph)
        {
            transferredEditors.Add(graph.Editor);
            return graph;
        }

        var nextFolders = incoming.Folders.ToDictionary(
            folder => folder.Kind,
            folder => (IReadOnlyList<ICanonicalStoryTreeItem>)folder.Items.Select(MergeItem).ToArray());
        var nextGraphItems = nextFolders.Values.SelectMany(items => items).OfType<CanonicalStoryGraphItem>().ToArray();
        var nextEditors = nextGraphItems.Select(item => item.Editor).ToHashSet();

        if (!ReferenceEquals(ActiveEditor, StoryEditor) && !nextEditors.Contains(ActiveEditor))
        {
            ActiveEditor = StoryEditor;
            ClearGraphSelection();
        }
        if (_nodeInspector is not null) ClearGraphSelection();

        foreach (var editor in SessionEditors.Concat(TaskEditors).Where(editor => !nextEditors.Contains(editor)).ToArray())
        {
            var oldItem = currentGraphItems.GetValueOrDefault((editor.ResourceKind, editor.Id));
            if (oldItem is { IsReadOnly: false })
                _detachedEditors[(editor.ResourceKind, editor.Id)] = editor;
            else
            {
                editor.PropertyChanged -= OnEditorPropertyChanged;
                editor.Dispose();
            }
        }
        foreach (var editor in transferredEditors)
        {
            editor.PropertyChanged -= incoming.OnEditorPropertyChanged;
            editor.PropertyChanged += OnEditorPropertyChanged;
        }

        foreach (var folder in Folders)
            folder.SynchronizeItems(nextFolders[folder.Kind]);

        ActorItems = nextFolders[CanonicalStoryFolderKind.Actors].OfType<CanonicalStoryActorItem>().ToArray();
        ItemItems = nextFolders[CanonicalStoryFolderKind.Items].OfType<CanonicalStoryItemItem>().ToArray();
        SessionItems = nextFolders[CanonicalStoryFolderKind.Sessions].OfType<CanonicalStoryGraphItem>().ToArray();
        TaskItems = nextFolders[CanonicalStoryFolderKind.Tasks].OfType<CanonicalStoryGraphItem>().ToArray();
        SessionEditors = SessionItems.Select(item => item.Editor).ToArray();
        TaskEditors = TaskItems.Select(item => item.Editor).ToArray();
        MissingItems = nextFolders.Values.SelectMany(items => items).OfType<CanonicalStoryMissingItem>().ToArray();
        ValidationIssues = snapshot.ValidationIssues.ToArray();

        OnPropertyChanged(nameof(ActorItems));
        OnPropertyChanged(nameof(ItemItems));
        OnPropertyChanged(nameof(SessionItems));
        OnPropertyChanged(nameof(TaskItems));
        OnPropertyChanged(nameof(SessionEditors));
        OnPropertyChanged(nameof(TaskEditors));
        OnPropertyChanged(nameof(MissingItems));
        OnPropertyChanged(nameof(ValidationIssues));
        OnPropertyChanged(nameof(Editors));
        OnPropertyChanged(nameof(HasDirtyEditors));

        var selected = nextFolders[selectedFolderKind]
            .FirstOrDefault(item => selectedResourceId is not null
                && string.Equals(item.Id, selectedResourceId, StringComparison.Ordinal));
        if (selected is not null) SelectTreeItem(selected);
        else SelectFolder(selectedFolderKind);

        incoming.StoryEditor.PropertyChanged -= incoming.OnEditorPropertyChanged;
        incoming.StoryEditor.Dispose();
        foreach (var editor in incoming.SessionEditors.Concat(incoming.TaskEditors)
                     .Where(editor => !transferredEditors.Contains(editor)).ToArray())
        {
            editor.PropertyChanged -= incoming.OnEditorPropertyChanged;
            editor.Dispose();
        }
        RefreshResourceCommandStates();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeNodeInspector();
        foreach (var editor in AllEditors()) editor.PropertyChanged -= OnEditorPropertyChanged;
        StoryEditor.Dispose();
        foreach (var editor in SessionEditors) editor.Dispose();
        foreach (var editor in TaskEditors) editor.Dispose();
        foreach (var editor in _detachedEditors.Values) editor.Dispose();
        _detachedEditors.Clear();
    }

    private bool Contains(ICanonicalStoryTreeItem item)
        => ActorItems.Any(candidate => ReferenceEquals(candidate, item))
            || ItemItems.Any(candidate => ReferenceEquals(candidate, item))
            || SessionItems.Any(candidate => ReferenceEquals(candidate, item))
            || TaskItems.Any(candidate => ReferenceEquals(candidate, item))
            || MissingItems.Any(candidate => ReferenceEquals(candidate, item));

    private bool ContainsGraphEditor(CanonicalGraphResourceEditorViewModel editor)
        => SessionEditors.Any(candidate => ReferenceEquals(candidate, editor))
            || TaskEditors.Any(candidate => ReferenceEquals(candidate, editor));

    private bool FailAggregate(ValidationIssue issue)
    {
        LastAggregateAuthoringIssues = [issue];
        OnPropertyChanged(nameof(LastAggregateAuthoringIssues));
        return false;
    }

    private bool CanDeleteSelectedResource()
        => DeleteResourceRequested is not null && SelectedTreeItem switch
        {
            CanonicalStoryActorItem => true,
            CanonicalStoryItemItem => true,
            CanonicalStoryGraphItem graph when graph.ResourceKind is GraphResourceKind.Session or GraphResourceKind.Task => true,
            CanonicalStoryMissingItem { FolderKind: CanonicalStoryFolderKind.Actors or CanonicalStoryFolderKind.Items or CanonicalStoryFolderKind.Sessions or CanonicalStoryFolderKind.Tasks } => true,
            _ => false,
        };

    private bool CanCreateSelectedResource()
        => SelectedFolderKind switch
        {
            CanonicalStoryFolderKind.Actors => CreateActorRequested is not null,
            CanonicalStoryFolderKind.Items => CreateItemRequested is not null,
            CanonicalStoryFolderKind.Sessions or CanonicalStoryFolderKind.Tasks =>
                SelectedResourceKind is not null && CreateResourceRequested is not null,
            _ => false,
        };

    private bool CanReferenceSelectedResource()
        => SelectedFolderKind switch
        {
            CanonicalStoryFolderKind.Actors => ReferenceActorRequested is not null,
            CanonicalStoryFolderKind.Items => ReferenceItemRequested is not null,
            CanonicalStoryFolderKind.Sessions or CanonicalStoryFolderKind.Tasks =>
                SelectedResourceKind is not null && ReferenceResourceRequested is not null,
            _ => false,
        };

    private void RequestCreateSelectedResource()
    {
        if (SelectedFolderKind == CanonicalStoryFolderKind.Actors)
        {
            CreateActorRequested?.Invoke();
            return;
        }
        if (SelectedFolderKind == CanonicalStoryFolderKind.Items)
        {
            CreateItemRequested?.Invoke();
            return;
        }
        if (SelectedResourceKind is { } kind)
            CreateResourceRequested?.Invoke(kind);
    }

    private void RequestReferenceSelectedResource()
    {
        if (SelectedFolderKind == CanonicalStoryFolderKind.Actors)
        {
            ReferenceActorRequested?.Invoke();
            return;
        }
        if (SelectedFolderKind == CanonicalStoryFolderKind.Items)
        {
            ReferenceItemRequested?.Invoke();
            return;
        }
        if (SelectedResourceKind is { } kind)
            ReferenceResourceRequested?.Invoke(kind);
    }

    private void RequestDeleteSelectedResource()
    {
        if (SelectedTreeItem is { } item && CanDeleteSelectedResource())
            DeleteResourceRequested?.Invoke(item);
    }

    private static CanonicalStoryFolderKind FolderFor(ICanonicalStoryTreeItem item) => item switch
    {
        CanonicalStoryActorItem => CanonicalStoryFolderKind.Actors,
        CanonicalStoryItemItem => CanonicalStoryFolderKind.Items,
        CanonicalStoryGraphItem { ResourceKind: GraphResourceKind.Session } => CanonicalStoryFolderKind.Sessions,
        CanonicalStoryGraphItem { ResourceKind: GraphResourceKind.Task } => CanonicalStoryFolderKind.Tasks,
        CanonicalStoryMissingItem missing => missing.FolderKind,
        _ => throw new ArgumentOutOfRangeException(nameof(item)),
    };

    private void SynchronizeTypedFolderOrder(
        CanonicalStoryFolderKind folderKind,
        IReadOnlyList<ICanonicalStoryTreeItem> items)
    {
        switch (folderKind)
        {
            case CanonicalStoryFolderKind.Actors:
                ActorItems = items.OfType<CanonicalStoryActorItem>().ToArray();
                OnPropertyChanged(nameof(ActorItems));
                break;
            case CanonicalStoryFolderKind.Items:
                ItemItems = items.OfType<CanonicalStoryItemItem>().ToArray();
                OnPropertyChanged(nameof(ItemItems));
                break;
            case CanonicalStoryFolderKind.Sessions:
                SessionItems = items.OfType<CanonicalStoryGraphItem>().ToArray();
                OnPropertyChanged(nameof(SessionItems));
                break;
            case CanonicalStoryFolderKind.Tasks:
                TaskItems = items.OfType<CanonicalStoryGraphItem>().ToArray();
                OnPropertyChanged(nameof(TaskItems));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(folderKind), folderKind, null);
        }
    }

    private static string OrderHandle(CanonicalStoryFolderKind folderKind, ICanonicalStoryTreeItem item)
        => item switch
        {
            CanonicalStoryItemItem resource => resource.Type == IndividualItemResource.ResourceType
                ? $"item:{resource.Id}"
                : $"item_group:{resource.Id}",
            CanonicalStoryMissingItem missing => missing.OrderHandle,
            _ when folderKind is CanonicalStoryFolderKind.Actors
                or CanonicalStoryFolderKind.Sessions
                or CanonicalStoryFolderKind.Tasks => item.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(item)),
        };

    private static IReadOnlyList<ICanonicalStoryTreeItem> ApplyDisplayOrder(
        IEnumerable<ICanonicalStoryTreeItem> items,
        IReadOnlyList<string> order,
        Func<ICanonicalStoryTreeItem, string> keySelector)
    {
        var source = items.ToArray();
        if (order.Count == 0) return source;
        var remaining = source.ToDictionary(keySelector, StringComparer.Ordinal);
        var result = new List<ICanonicalStoryTreeItem>(source.Length);
        foreach (var handle in order)
        {
            if (!remaining.Remove(handle, out var item)) continue;
            result.Add(item);
        }
        result.AddRange(source.Where(item => remaining.ContainsKey(keySelector(item))));
        return result;
    }

    private static GraphResourceEnvelope SnapshotStory(CanonicalStoryWorkspaceSnapshot? snapshot)
        => (snapshot ?? throw new ArgumentNullException(nameof(snapshot))).Story;

    private static IEnumerable<ActorResourceInfo> SnapshotActors(CanonicalStoryWorkspaceSnapshot? snapshot)
        => (snapshot ?? throw new ArgumentNullException(nameof(snapshot))).Actors
            .Where(entry => entry.Resource is not null)
            .Select(entry => new ActorResourceInfo(
                entry.Resource!.Id,
                entry.Resource.DisplayName,
                entry.Resource.SourcePath ?? string.Empty,
                entry.Resource.Tags.ToArray(),
                entry.Resource.ToResource().Type ?? ActorResource.LegacyResourceType, entry.Resource.DefaultPortraitRef, entry.Resource.PortraitVariants.ToArray()));

    private static IEnumerable<GraphResourceEnvelope> SnapshotGraphs(
        CanonicalStoryWorkspaceSnapshot? snapshot,
        GraphResourceKind kind)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return (kind == GraphResourceKind.Session ? snapshot.Sessions : snapshot.Tasks)
            .Where(entry => entry.Resource is not null)
            .Select(entry => entry.Resource!);
    }

    private static IEnumerable<ItemResource> SnapshotItems(CanonicalStoryWorkspaceSnapshot? snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.Items.Where(entry => entry.Resource is not null).Select(entry => (ItemResource)entry.Resource!)
            .Concat(snapshot.ItemGroups.Where(entry => entry.Resource is not null).Select(entry => (ItemResource)entry.Resource!));
    }

    private static CanonicalGraphResourceEditorViewModel CreateEditor(
        GraphResourceEnvelope resource,
        CanonicalGraphLayoutStore? layoutStore)
    {
        if (layoutStore is null)
            return new CanonicalGraphResourceEditorViewModel(resource);

        var liveNodeIds = (resource.Graph?.Nodes ?? [])
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        var positions = layoutStore.Load(resource.ResourceKind, resource.Id)
            .Where(pair => liveNodeIds.Contains(pair.Key)
                && double.IsFinite(pair.Value.X)
                && double.IsFinite(pair.Value.Y))
            .ToDictionary(
                pair => pair.Key,
                pair => new GraphEditorNodePosition(pair.Value.X, pair.Value.Y),
                StringComparer.Ordinal);
        return new CanonicalGraphResourceEditorViewModel(resource, positions);
    }

    private IReadOnlyList<ICanonicalStoryTreeItem> AdaptEntries<TEntry, TItem>(
        IEnumerable<CanonicalStoryWorkspaceEntry<TEntry>> entries,
        CanonicalStoryFolderKind folderKind,
        IReadOnlyDictionary<string, TItem> resolvedById,
        ICollection<CanonicalStoryMissingItem> missing,
        Func<TItem, CanonicalStoryWorkspaceMembershipKind, OfflineProviderResource?, ICanonicalStoryTreeItem> resolvedFactory,
        Func<string, string> orderHandleFactory)
        where TEntry : class
        where TItem : ICanonicalStoryTreeItem
    {
        var result = new List<ICanonicalStoryTreeItem>();
        foreach (var entry in entries)
        {
            if (entry.IsResolved && resolvedById.TryGetValue(entry.Id, out var resolved))
            {
                result.Add(resolvedFactory(resolved, entry.MembershipKind, entry.Provider));
                continue;
            }

            var issue = ValidationIssues.FirstOrDefault(candidate =>
                    candidate.Code == "story.workspace.member.missing"
                    && string.Equals(candidate.NodeId, entry.Id, StringComparison.Ordinal)
                    && FieldMatchesFolder(candidate.Field, folderKind))
                ?? new ValidationIssue(
                    "story.workspace.member.missing",
                    $"Canonical Story member '{entry.Id}' was not found.",
                    FolderField(folderKind),
                    ValidationSeverity.Error,
                    entry.Id);
            var item = new CanonicalStoryMissingItem(
                entry.Id,
                folderKind,
                entry.MembershipKind,
                issue,
                orderHandleFactory(entry.Id));
            missing.Add(item);
            result.Add(item);
        }
        return result;
    }

    private static bool FieldMatchesFolder(string? field, CanonicalStoryFolderKind folderKind)
        => folderKind == CanonicalStoryFolderKind.Items
            ? field is "items" or "item_groups"
            : string.Equals(field, FolderField(folderKind), StringComparison.Ordinal);

    private static string FolderField(CanonicalStoryFolderKind folderKind) => folderKind switch
    {
        CanonicalStoryFolderKind.Actors => "actors",
        CanonicalStoryFolderKind.Items => "items",
        CanonicalStoryFolderKind.Sessions => "sessions",
        CanonicalStoryFolderKind.Tasks => "tasks",
        _ => throw new ArgumentOutOfRangeException(nameof(folderKind)),
    };

    private static bool IsProviderResource(ICanonicalStoryTreeItem item)
        => item switch
        {
            CanonicalStoryActorItem actor => actor.IsReadOnly,
            CanonicalStoryItemItem resource => resource.IsReadOnly,
            CanonicalStoryGraphItem graph => graph.IsReadOnly,
            _ => false,
        };

    private static bool SameProvider(OfflineProviderResource? left, OfflineProviderResource? right)
        => left is null && right is null
            || left is not null && right is not null
            && string.Equals(left.PackageIdentity.PackageId, right.PackageIdentity.PackageId, StringComparison.Ordinal)
            && string.Equals(left.PackageIdentity.PackageVersion, right.PackageIdentity.PackageVersion, StringComparison.Ordinal)
            && string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.Ordinal);

    private static IReadOnlyList<GraphResourceEnvelope> ValidateResources(
        IEnumerable<GraphResourceEnvelope>? resources,
        GraphResourceKind expectedKind,
        string parameterName)
    {
        var source = (resources ?? []).ToArray();
        if (source.Any(resource => resource is null || resource.ResourceKind != expectedKind))
            throw new ArgumentException($"All resources must have kind '{expectedKind}'.", parameterName);
        var duplicate = source.GroupBy(resource => resource.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Duplicate canonical resource ID '{duplicate.Key}'.", parameterName);
        return source;
    }

    private CanonicalStoryBreadcrumb ProjectBreadcrumb()
        => new(_projectId, _projectDisplayName, CanonicalStoryBreadcrumbKind.Project, null, IsFirst: true, IsCurrent: false);

    private static CanonicalStoryBreadcrumb Breadcrumb(
        CanonicalGraphResourceEditorViewModel editor,
        bool isCurrent)
        => new(
            editor.Id,
            editor.DisplayName,
            editor.ResourceKind switch
            {
                GraphResourceKind.Story => CanonicalStoryBreadcrumbKind.Story,
                GraphResourceKind.Session => CanonicalStoryBreadcrumbKind.Session,
                GraphResourceKind.Task => CanonicalStoryBreadcrumbKind.Task,
                _ => throw new ArgumentOutOfRangeException(nameof(editor)),
            },
            editor.ResourceKind,
            IsFirst: false,
            IsCurrent: isCurrent);

    private IEnumerable<CanonicalGraphResourceEditorViewModel> AllEditors()
        => new[] { StoryEditor }.Concat(SessionEditors).Concat(TaskEditors).Concat(_detachedEditors.Values).Distinct();

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(CanonicalGraphResourceEditorViewModel.IsDirty)
            or nameof(CanonicalGraphResourceEditorViewModel.CanSave))
            OnPropertyChanged(nameof(HasDirtyEditors));
        if (ReferenceEquals(sender, ActiveEditor) && InspectorSelection is CanonicalNodeInspectorViewModel
            && args.PropertyName == nameof(CanonicalGraphResourceEditorViewModel.SaveStateText))
            OnPropertyChanged(nameof(InspectorSaveStateText));
        if (!ReferenceEquals(sender, InspectorSelection)) return;
        if (args.PropertyName == nameof(CanonicalGraphResourceEditorViewModel.Tags)) OnPropertyChanged(nameof(InspectorTagsText));
        if (args.PropertyName == nameof(CanonicalGraphResourceEditorViewModel.DisplayName)) OnPropertyChanged(nameof(InspectorTitle));
        if (args.PropertyName is nameof(CanonicalGraphResourceEditorViewModel.SaveStateText)
            or nameof(CanonicalGraphResourceEditorViewModel.ValidationText))
        {
            OnPropertyChanged(nameof(InspectorSaveStateText));
            OnPropertyChanged(nameof(InspectorValidationText));
        }
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);

    private void DisposeNodeInspector()
    {
        if (_nodeInspector is not null)
            _nodeInspector.PropertyChanged -= OnNodeInspectorPropertyChanged;
        _nodeInspector?.Dispose();
        _nodeInspector = null;
    }

    private void OnNodeInspectorPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(CanonicalNodeInspectorViewModel.ValidationIssues)
            or nameof(CanonicalNodeInspectorViewModel.LineText)
            or nameof(CanonicalNodeInspectorViewModel.ChoicePrompt)
            or nameof(CanonicalNodeInspectorViewModel.EndDisplayName)
            or nameof(CanonicalNodeInspectorViewModel.LogicOutputDisplayName))
        {
            OnPropertyChanged(nameof(InspectorSaveStateText));
            OnPropertyChanged(nameof(InspectorValidationText));
        }
    }
}
