using System.Collections.ObjectModel;
using System.ComponentModel;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
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
}

public sealed record CanonicalStoryActorItem(
    ActorResourceInfo Actor,
    CanonicalStoryWorkspaceMembershipKind MembershipKind = CanonicalStoryWorkspaceMembershipKind.Owned)
    : ICanonicalStoryTreeItem
{
    public string Id => Actor.Id;
    public string DisplayName => Actor.DisplayName;
    public bool IsOwned => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Owned;
    public bool IsReferenced => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Referenced;
}

public sealed record CanonicalStoryItemItem(
    ItemResource Item,
    CanonicalStoryWorkspaceMembershipKind MembershipKind = CanonicalStoryWorkspaceMembershipKind.Owned)
    : ICanonicalStoryTreeItem
{
    public string Id => Item.Id;
    public string DisplayName => Item.DisplayName;
    public string Type => Item.Type;
    public IReadOnlyList<string> Tags => Item.Tags;
    public bool IsOwned => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Owned;
    public bool IsReferenced => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Referenced;
}

public sealed record CanonicalStoryGraphItem(
    CanonicalGraphResourceEditorViewModel Editor,
    CanonicalStoryWorkspaceMembershipKind MembershipKind = CanonicalStoryWorkspaceMembershipKind.Owned)
    : ICanonicalStoryTreeItem
{
    public string Id => Editor.Id;
    public string DisplayName => Editor.DisplayName;
    public GraphResourceKind ResourceKind => Editor.ResourceKind;
    public bool IsOwned => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Owned;
    public bool IsReferenced => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Referenced;
}

public sealed record CanonicalStoryMissingItem(
    string Id,
    CanonicalStoryFolderKind FolderKind,
    CanonicalStoryWorkspaceMembershipKind MembershipKind,
    ValidationIssue Issue) : ICanonicalStoryTreeItem
{
    public string DisplayName => $"{Id}（缺失）";
    public bool IsOwned => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Owned;
    public bool IsReferenced => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Referenced;
}

public sealed record CanonicalStoryBreadcrumb(
    string Id,
    string DisplayName,
    GraphResourceKind ResourceKind);

public sealed record CanonicalStoryNodeFocusRequest(
    string NodeId,
    string? Field,
    long Sequence);

/// <summary>One expandable logical resource folder; toggling never changes the graph workspace.</summary>
public sealed class CanonicalStoryFolderViewModel : ObservableObject
{
    private bool _isExpanded = true;

    public CanonicalStoryFolderViewModel(
        CanonicalStoryFolderKind kind,
        string displayName,
        IEnumerable<ICanonicalStoryTreeItem> items)
    {
        Kind = kind;
        DisplayName = displayName;
        Items = new ReadOnlyCollection<ICanonicalStoryTreeItem>((items ?? []).ToList());
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
    private bool _disposed;

    public CanonicalStoryWorkspaceViewModel(
        GraphResourceEnvelope story,
        IEnumerable<ActorResourceInfo>? actors = null,
        IEnumerable<GraphResourceEnvelope>? sessions = null,
        IEnumerable<GraphResourceEnvelope>? tasks = null,
        IEnumerable<ItemResource>? items = null)
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
        try
        {
            StoryEditor = new CanonicalGraphResourceEditorViewModel(story);
            createdEditors.Add(StoryEditor);
            SessionEditors = sessionResources.Select(resource =>
            {
                var editor = new CanonicalGraphResourceEditorViewModel(resource);
                createdEditors.Add(editor);
                return editor;
            }).ToArray();
            TaskEditors = taskResources.Select(resource =>
            {
                var editor = new CanonicalGraphResourceEditorViewModel(resource);
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
            .OrderBy(actor => actor.DisplayName, StringComparer.Ordinal)
            .ThenBy(actor => actor.Id, StringComparer.Ordinal)
            .Select(actor => new CanonicalStoryActorItem(actor))
            .ToArray();
        ItemItems = (items ?? [])
            .OrderBy(item => item.DisplayName, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
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
    public CanonicalStoryWorkspaceViewModel(CanonicalStoryWorkspaceSnapshot snapshot)
        : this(
            SnapshotStory(snapshot),
            SnapshotActors(snapshot),
            SnapshotGraphs(snapshot, GraphResourceKind.Session),
            SnapshotGraphs(snapshot, GraphResourceKind.Task),
            SnapshotItems(snapshot))
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
            (item, membershipKind) => new CanonicalStoryActorItem(item.Actor, membershipKind));
        var sessionFolderItems = AdaptEntries(
            snapshot.Sessions,
            CanonicalStoryFolderKind.Sessions,
            sessionsById,
            missing,
            (item, membershipKind) => new CanonicalStoryGraphItem(item.Editor, membershipKind));
        var itemFolderItems = AdaptEntries(
                snapshot.Items,
                CanonicalStoryFolderKind.Items,
                individualItemsById,
                missing,
                (item, membershipKind) => new CanonicalStoryItemItem(item.Item, membershipKind))
            .Concat(AdaptEntries(
                snapshot.ItemGroups,
                CanonicalStoryFolderKind.Items,
                collectiveItemsById,
                missing,
                (item, membershipKind) => new CanonicalStoryItemItem(item.Item, membershipKind)))
            .ToArray();
        var taskFolderItems = AdaptEntries(
            snapshot.Tasks,
            CanonicalStoryFolderKind.Tasks,
            tasksById,
            missing,
            (item, membershipKind) => new CanonicalStoryGraphItem(item.Editor, membershipKind));

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
    public IReadOnlyList<CanonicalGraphResourceEditorViewModel> SessionEditors { get; }
    public IReadOnlyList<CanonicalGraphResourceEditorViewModel> TaskEditors { get; }
    public IReadOnlyList<CanonicalStoryActorItem> ActorItems { get; }
    public IReadOnlyList<CanonicalStoryItemItem> ItemItems { get; }
    public IReadOnlyList<CanonicalStoryGraphItem> SessionItems { get; }
    public IReadOnlyList<CanonicalStoryGraphItem> TaskItems { get; }
    public IReadOnlyList<CanonicalStoryMissingItem> MissingItems { get; private set; } = [];
    public IReadOnlyList<CanonicalStoryFolderViewModel> Folders { get; }
    public IReadOnlyList<ValidationIssue> ValidationIssues { get; private set; } = [];
    public IReadOnlyList<ValidationIssue> LastAggregateAuthoringIssues { get; private set; } = [];
    public IReadOnlyList<CanonicalGraphResourceEditorViewModel> Editors => AllEditors().ToArray();
    public bool HasDirtyEditors => AllEditors().Any(editor => editor.IsDirty);
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

    /// <summary>Optional shell-owned unsaved-changes gate for graph switching.</summary>
    public Func<CanonicalGraphResourceEditorViewModel, bool>? CanLeaveGraph { get; set; }

    public CanonicalGraphResourceEditorViewModel ActiveEditor
    {
        get => _activeEditor;
        private set
        {
            if (!SetProperty(ref _activeEditor, value)) return;
            OnPropertyChanged(nameof(ActiveGraphHost));
            OnPropertyChanged(nameof(IsStoryFlowActive));
            OnPropertyChanged(nameof(IsLocalGraphOpen));
            OnPropertyChanged(nameof(Breadcrumbs));
            ReturnToStoryCommand.RaiseCanExecuteChanged();
        }
    }

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
        }
    }

    /// <summary>Node Inspector projection for the currently selected graph node.</summary>
    public CanonicalNodeInspectorViewModel? NodeInspector => _nodeInspector;
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
        CanonicalNodeInspectorViewModel node => node.NodeType,
        CanonicalStoryActorItem => "角色",
        CanonicalStoryItemItem { Item: IndividualItemResource } => "个体物品",
        CanonicalStoryItemItem { Item: CollectiveItemResource } => "集体物品",
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
        CanonicalGraphResourceEditorViewModel editor => editor.Id,
        _ => StoryEditor.Id,
    };

    public string InspectorSaveStateText => InspectorSelection switch
    {
        CanonicalNodeInspectorViewModel node => ActiveEditor.IsDirty ? "未保存" : "已保存",
        CanonicalGraphResourceEditorViewModel editor => editor.SaveStateText,
        CanonicalStoryMissingItem => "资源缺失",
        _ => string.Empty,
    };

    public string InspectorValidationText => InspectorSelection switch
    {
        CanonicalNodeInspectorViewModel node => string.Join(Environment.NewLine, node.ValidationIssues.Select(issue => issue.Message)),
        CanonicalGraphResourceEditorViewModel editor => editor.ValidationText,
        CanonicalStoryMissingItem missing => missing.Issue.Message,
        _ => string.Empty,
    };

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
        ? [Breadcrumb(StoryEditor)]
        : [Breadcrumb(StoryEditor), Breadcrumb(ActiveEditor)];

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
        _nodeInspector = new CanonicalNodeInspectorViewModel(ActiveGraphHost, node, ActorItems);
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

    /// <summary>Double-click/Enter behavior for a selected Session or Task.</summary>
    public bool OpenSelectedResource()
        => SelectedGraphResource is { } item && OpenGraphResource(item);

    public bool OpenGraphResource(CanonicalStoryGraphItem item)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(item);
        if (!Contains(item)) return false;
        if (ReferenceEquals(ActiveEditor, item.Editor)) return true;
        if (CanLeaveGraph is not null && !CanLeaveGraph(ActiveEditor)) return false;
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
        if (CanLeaveGraph is not null && !CanLeaveGraph(ActiveEditor)) return false;
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        DisposeNodeInspector();
        foreach (var editor in AllEditors()) editor.PropertyChanged -= OnEditorPropertyChanged;
        StoryEditor.Dispose();
        foreach (var editor in SessionEditors) editor.Dispose();
        foreach (var editor in TaskEditors) editor.Dispose();
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
            CanonicalStoryGraphItem { ResourceKind: GraphResourceKind.Session or GraphResourceKind.Task } => true,
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

    private static GraphResourceEnvelope SnapshotStory(CanonicalStoryWorkspaceSnapshot? snapshot)
        => (snapshot ?? throw new ArgumentNullException(nameof(snapshot))).Story;

    private static IEnumerable<ActorResourceInfo> SnapshotActors(CanonicalStoryWorkspaceSnapshot? snapshot)
        => (snapshot ?? throw new ArgumentNullException(nameof(snapshot))).Actors
            .Where(entry => entry.Resource is not null)
            .Select(entry => new ActorResourceInfo(
                entry.Resource!.Id,
                entry.Resource.DisplayName,
                entry.Resource.SourcePath ?? string.Empty,
                entry.Resource.Tags.ToArray()));

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

    private IReadOnlyList<ICanonicalStoryTreeItem> AdaptEntries<TEntry, TItem>(
        IEnumerable<CanonicalStoryWorkspaceEntry<TEntry>> entries,
        CanonicalStoryFolderKind folderKind,
        IReadOnlyDictionary<string, TItem> resolvedById,
        ICollection<CanonicalStoryMissingItem> missing,
        Func<TItem, CanonicalStoryWorkspaceMembershipKind, ICanonicalStoryTreeItem> resolvedFactory)
        where TEntry : class
        where TItem : ICanonicalStoryTreeItem
    {
        var result = new List<ICanonicalStoryTreeItem>();
        foreach (var entry in entries)
        {
            if (entry.IsResolved && resolvedById.TryGetValue(entry.Id, out var resolved))
            {
                result.Add(resolvedFactory(resolved, entry.MembershipKind));
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
            var item = new CanonicalStoryMissingItem(entry.Id, folderKind, entry.MembershipKind, issue);
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

    private static CanonicalStoryBreadcrumb Breadcrumb(CanonicalGraphResourceEditorViewModel editor)
        => new(editor.Id, editor.DisplayName, editor.ResourceKind);

    private IEnumerable<CanonicalGraphResourceEditorViewModel> AllEditors()
        => new[] { StoryEditor }.Concat(SessionEditors).Concat(TaskEditors);

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(CanonicalGraphResourceEditorViewModel.IsDirty)
            or nameof(CanonicalGraphResourceEditorViewModel.CanSave))
            OnPropertyChanged(nameof(HasDirtyEditors));
        if (!ReferenceEquals(sender, InspectorSelection)) return;
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
