using System.Collections.ObjectModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

/// <summary>Host-only position metadata. It is deliberately not part of GraphDocument.</summary>
public readonly record struct GraphEditorNodePosition(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
}

/// <summary>Identifies nodes whose rendered port projection changed.</summary>
public sealed class GraphPortsChangedEventArgs : EventArgs
{
    public GraphPortsChangedEventArgs(IReadOnlyList<string> nodeIds) => NodeIds = nodeIds;

    public IReadOnlyList<string> NodeIds { get; }
}

/// <summary>Identifies graph nodes whose persisted projection changed in one edit.</summary>
public sealed class GraphNodesChangedEventArgs : EventArgs
{
    public GraphNodesChangedEventArgs(IReadOnlyList<string> nodeIds) => NodeIds = nodeIds;

    public IReadOnlyList<string> NodeIds { get; }
}

/// <summary>One validated replacement set for inserting C beside an existing A→B edge.</summary>
public sealed record GraphConnectionSplicePlan(
    GraphConnection Original,
    IReadOnlyList<GraphConnection> Replacements);

/// <summary>Bindable canonical port projection.</summary>
public sealed class GraphEditorPortViewModel : ObservableObject
{
    private string _displayName;
    private int _order;

    internal GraphEditorPortViewModel(GraphPort port)
    {
        PortId = port.Id ?? string.Empty;
        _displayName = port.DisplayName ?? string.Empty;
        _order = port.Order;
        Direction = port.Direction;
        GraphInterfaceKind = port.InterfaceKind;
    }

    public string PortId { get; }
    public string Id => PortId;
    public string DisplayName { get => _displayName; internal set => SetProperty(ref _displayName, value); }
    public string Name => DisplayName;
    public int Order { get => _order; internal set => SetProperty(ref _order, value); }
    public GraphPortDirection Direction { get; internal set; }
    public bool IsInput => Direction == GraphPortDirection.Input;
    public bool IsOutput => !IsInput;
    public GraphInterfaceKind GraphInterfaceKind { get; internal set; }
    public GraphInterfaceKind InterfaceKind => GraphInterfaceKind;
    public GraphInterfaceKind Kind => GraphInterfaceKind;

    internal void Update(GraphPort port)
    {
        DisplayName = port.DisplayName ?? string.Empty;
        Order = port.Order;
        var direction = port.Direction;
        if (Direction != direction)
        {
            Direction = direction;
            OnPropertyChanged(nameof(Direction));
            OnPropertyChanged(nameof(IsInput));
            OnPropertyChanged(nameof(IsOutput));
        }

        if (GraphInterfaceKind != port.InterfaceKind)
        {
            GraphInterfaceKind = port.InterfaceKind;
            OnPropertyChanged(nameof(GraphInterfaceKind));
            OnPropertyChanged(nameof(InterfaceKind));
            OnPropertyChanged(nameof(Kind));
        }
    }
}

/// <summary>Bindable node projection with canonical ID identity and host layout.</summary>
public sealed class GraphEditorNodeViewModel : ObservableObject
{
    private string _type;
    private string _displayName;
    private double _x;
    private double _y;
    private IReadOnlyDictionary<string, JsonElement> _properties =
        new ReadOnlyDictionary<string, JsonElement>(new Dictionary<string, JsonElement>(StringComparer.Ordinal));
    private readonly Action<GraphEditorNodeViewModel, double, double>? _positionChanged;
    private readonly Dictionary<string, GraphEditorPortViewModel> _uniquePorts = new(StringComparer.Ordinal);
    private readonly GraphScope _scope;

    internal GraphEditorNodeViewModel(GraphNode node, double x, double y,
        Action<GraphEditorNodeViewModel, double, double>? positionChanged, GraphScope scope)
    {
        _scope = scope;
        NodeId = node.Id ?? string.Empty;
        _type = node.Type ?? string.Empty;
        _displayName = node.DisplayName ?? string.Empty;
        _x = x;
        _y = y;
        _positionChanged = positionChanged;
        Inputs = new ReadOnlyObservableCollection<GraphEditorPortViewModel>(_inputs);
        Outputs = new ReadOnlyObservableCollection<GraphEditorPortViewModel>(_outputs);
        Update(node);
    }

    private readonly ObservableCollection<GraphEditorPortViewModel> _inputs = [];
    private readonly ObservableCollection<GraphEditorPortViewModel> _outputs = [];

    public string NodeId { get; }
    public string Id => NodeId;
    public string Type { get => _type; private set => SetProperty(ref _type, value); }
    public string DisplayName { get => _displayName; private set => SetProperty(ref _displayName, value); }
    public string Name => DisplayName;
    public IReadOnlyList<GraphEditorPortViewModel> Inputs { get; }
    public IReadOnlyList<GraphEditorPortViewModel> Outputs { get; }
    public IReadOnlyList<GraphEditorPortViewModel> InputPorts => Inputs;
    public IReadOnlyList<GraphEditorPortViewModel> OutputPorts => Outputs;
    /// <summary>Read-only snapshot of the node's untyped JSON properties.</summary>
    public IReadOnlyDictionary<string, JsonElement> Properties => _properties;
    public string ParameterSummary => CanonicalNodeParameterSummary.Format(Type, Properties);
    public bool HasParameterSummary => !string.IsNullOrWhiteSpace(ParameterSummary);
    public double X { get => _x; set => SetPosition(value, _y); }
    public double Y { get => _y; set => SetPosition(_x, value); }
    public GraphEditorNodePosition Position => new(X, Y);

    /// <summary>Moves this host-only node position without changing the graph JSON.</summary>
    public void SetPosition(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        var changed = _x != x || _y != y;
        _x = x;
        _y = y;
        if (changed)
        {
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Y));
            OnPropertyChanged(nameof(Position));
            _positionChanged?.Invoke(this, x, y);
        }
    }

    internal void SetProjectionPosition(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        SetPosition(x, y);
    }

    internal void Update(GraphNode node)
    {
        Type = node.Type ?? string.Empty;
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var pair in node.Properties ?? []) properties[pair.Key] = pair.Value.Clone();
        var extraName = Type == CanonicalStoryActionSchema.NodeType
            && properties.TryGetValue(CanonicalStoryActionSchema.TypeProperty, out var actionType)
            && actionType.ValueKind == JsonValueKind.String
                ? CanonicalStoryActionSchema.AuthoringDisplayNameFor(actionType.GetString())
                : node.DisplayName ?? string.Empty;
        var typeName = GraphNodeDefinitionRegistry.Get(_scope, Type)?.DisplayName ?? Type;
        var hasBoundaryName = false;
        if (Type is "terminate" or "end" or "logic_input" or "logic_output"
            && properties.TryGetValue("display_name", out var boundaryName) && boundaryName.ValueKind == JsonValueKind.String)
        {
            extraName = boundaryName.GetString() ?? "";
            hasBoundaryName = !string.IsNullOrWhiteSpace(extraName);
        }
        DisplayName = string.IsNullOrWhiteSpace(extraName) || extraName == typeName && !hasBoundaryName
            ? typeName : $"{typeName}「{extraName}」";
        _properties = new ReadOnlyDictionary<string, JsonElement>(properties);
        OnPropertyChanged(nameof(Properties));
        OnPropertyChanged(nameof(ParameterSummary));
        OnPropertyChanged(nameof(HasParameterSummary));

        var ports = (node.Ports ?? []).Where(port => port is not null).ToArray();
        var counts = ports.GroupBy(port => port.Id ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var nextInputs = new List<GraphEditorPortViewModel>();
        var nextOutputs = new List<GraphEditorPortViewModel>();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var port in ports.OrderBy(port => port.Order).ThenBy(port => port.Id ?? string.Empty, StringComparer.Ordinal))
        {
            var id = port.Id ?? string.Empty;
            var canReuse = !string.IsNullOrWhiteSpace(id) && counts[id] == 1 && used.Add(id);
            GraphEditorPortViewModel item;
            if (canReuse && _uniquePorts.TryGetValue(id, out var existing))
            {
                item = existing;
                item.Update(port);
            }
            else
            {
                item = new GraphEditorPortViewModel(port);
                if (canReuse) _uniquePorts[id] = item;
            }

            (item.IsInput ? nextInputs : nextOutputs).Add(item);
        }

        _inputs.Clear();
        foreach (var item in nextInputs) _inputs.Add(item);
        _outputs.Clear();
        foreach (var item in nextOutputs) _outputs.Add(item);
    }
}

/// <summary>Bindable connection projection retaining only canonical endpoint IDs.</summary>
public sealed class GraphEditorConnectionViewModel
{
    public GraphEditorConnectionViewModel(GraphConnection connection)
    {
        Connection = connection;
        FromNodeId = connection.FromNodeId ?? string.Empty;
        FromPortId = connection.FromPortId ?? string.Empty;
        ToNodeId = connection.ToNodeId ?? string.Empty;
        ToPortId = connection.ToPortId ?? string.Empty;
        InterfaceKind = connection.InterfaceKind;
    }

    public GraphConnection Connection { get; }
    public string FromNodeId { get; }
    public string FromPortId { get; }
    public string ToNodeId { get; }
    public string ToPortId { get; }
    public GraphInterfaceKind InterfaceKind { get; }
    public GraphInterfaceKind GraphInterfaceKind => InterfaceKind;
    public string SourceNodeId => FromNodeId;
    public string SourcePortId => FromPortId;
    public string TargetNodeId => ToNodeId;
    public string TargetPortId => ToPortId;
}

/// <summary>
/// The one WPF-facing host for Story Flow, Session, and Task graph scopes.
/// It projects the canonical document and routes all edits through the shared
/// session/bridge pair.
/// </summary>
public sealed partial class GraphEditorHostViewModel : ObservableObject
{
    private readonly GraphEditSession _session;
    private readonly GraphEditorCommandBridge _commandBridge;
    private readonly Dictionary<string, GraphEditorNodeViewModel> _uniqueNodeItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphEditorNodePosition> _layout = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ValidationIssue> _authoringIssues = new(StringComparer.Ordinal);
    private readonly Stack<HostHistoryEntry> _undoHistory = [];
    private readonly Stack<HostHistoryEntry> _redoHistory = [];
    private IReadOnlyList<ValidationIssue> _lastValidationIssues = [];
    private IReadOnlyList<ValidationIssue> _coreValidationIssues = [];
    private Func<string, bool>? _readOnlySourcePolicy;
    private Dictionary<string, GraphEditorNodePosition>? _activeLayoutMoveBefore;
    private bool _suppressLayoutChanged;

    private abstract record HostHistoryEntry(IReadOnlyList<HostHistoryEntry> DisplacedRedo)
    {
        public long Sequence { get; } = EditHistoryClock.Next();
    }

    private sealed record GraphHistoryEntry(IReadOnlyList<HostHistoryEntry> DisplacedRedo)
        : HostHistoryEntry(DisplacedRedo);

    private sealed record EditorHistoryEntry(Action Undo, Action Redo, IReadOnlyList<HostHistoryEntry> DisplacedRedo)
        : HostHistoryEntry(DisplacedRedo);

    public string AuthoringResourceKey { get; internal set; } = Guid.NewGuid().ToString("N");

    /// <summary>History for persisted editor-only metadata, never sent to Runtime.</summary>
    public void EditMetadata(Action undo, Action redo)
    {
        var oldUndo = CanUndo; var oldRedo = CanRedo;
        redo();
        RecordHistory(new EditorHistoryEntry(undo, redo, _redoHistory.ToArray()));
        NotifyHistoryStateChanged(oldUndo, oldRedo);
    }

    private static bool ApplyEditorHistory(Action action) { action(); return true; }

    private sealed record LayoutHistoryEntry(
        IReadOnlyDictionary<string, GraphEditorNodePosition> Before,
        IReadOnlyDictionary<string, GraphEditorNodePosition> After,
        IReadOnlyList<HostHistoryEntry> DisplacedRedo)
        : HostHistoryEntry(DisplacedRedo);

    /// <summary>
    /// Raised only after a successful canonical graph mutation. Preview,
    /// validation failure, projection refresh, and layout-only movement do not
    /// advance the graph revision.
    /// </summary>
    public event EventHandler? GraphChanged;
    public event EventHandler? LayoutChanged;
    public event EventHandler<GraphPortsChangedEventArgs>? PortsChanged;
    public event EventHandler<GraphNodesChangedEventArgs>? NodesChanged;

    public GraphEditorHostViewModel(GraphDocument graph, GraphScope scope)
        : this(graph, scope, compatibilityMode: false, layout: null) { }

    public GraphEditorHostViewModel(GraphDocument graph, GraphScope scope, bool compatibilityMode)
        : this(graph, scope, compatibilityMode, layout: null) { }

    public GraphEditorHostViewModel(GraphDocument graph, GraphScope scope,
        IReadOnlyDictionary<string, GraphEditorNodePosition>? layout)
        : this(graph, scope, compatibilityMode: false, layout) { }

    // Object keeps the optional layout call site friendly to tuple dictionaries
    // while the strongly typed position overload remains the canonical API.
    // It also leaves a literal null unambiguous (the typed overload wins).
    public GraphEditorHostViewModel(GraphDocument graph, GraphScope scope, object? layout)
        : this(graph, scope, compatibilityMode: false, CoerceLayout(layout)) { }

    public GraphEditorHostViewModel(GraphDocument graph, GraphScope scope, bool compatibilityMode,
        IReadOnlyDictionary<string, GraphEditorNodePosition>? layout)
    {
        Graph = graph ?? throw new ArgumentNullException(nameof(graph));
        Scope = scope;
        if (layout is not null)
        {
            foreach (var item in layout)
                if (!string.IsNullOrWhiteSpace(item.Key) && item.Value.IsFinite)
                    _layout[item.Key] = item.Value;
        }

        _session = new GraphEditSession(Graph, Scope, compatibilityMode);
        _commandBridge = new GraphEditorCommandBridge(_session);
        Nodes = new ObservableCollection<GraphEditorNodeViewModel>();
        Connections = new ObservableCollection<GraphEditorConnectionViewModel>();
        Refresh();
    }

    public GraphDocument Graph { get; }
    public GraphScope Scope { get; }
    public GraphEditSession Session => _session;
    public GraphEditorCommandBridge CommandBridge => _commandBridge;
    public ObservableCollection<GraphEditorNodeViewModel> Nodes { get; }
    public ObservableCollection<GraphEditorConnectionViewModel> Connections { get; }
    public long UndoSequence => _undoHistory.TryPeek(out var entry) ? entry.Sequence : 0;
    public long RedoSequence => _redoHistory.TryPeek(out var entry) ? entry.Sequence : long.MaxValue;
    public bool CanUndo => _undoHistory.Count != 0;
    public bool CanRedo => _redoHistory.Count != 0;
    public IReadOnlyList<ValidationIssue> LastValidationIssues => _lastValidationIssues;
    public IReadOnlyDictionary<string, GraphEditorNodePosition> Layout => _layout;
    public long GraphRevision { get; private set; }

    /// <summary>
    /// Optional host policy for graph edges whose output/source is owned by a
    /// read-only projection.  The policy is deliberately opt-in so the shared
    /// editor remains usable for ordinary story/session/task documents.
    /// </summary>
    public Func<string, bool>? ReadOnlySourcePolicy
    {
        get => _readOnlySourcePolicy;
        set => _readOnlySourcePolicy = value;
    }

    /// <summary>Publishes or clears a staged editor error without mutating the graph.</summary>
    public void SetAuthoringIssue(string key, ValidationIssue? issue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (issue is null) _authoringIssues.Remove(key);
        else _authoringIssues[key] = issue;
        PublishState(_coreValidationIssues);
    }

    /// <summary>Reprojects the current document while retaining unique node items and layout.</summary>
    public void Refresh()
        => Refresh(_commandBridge.LastValidationIssues);

    public void RefreshProjectedStoryBoundary(string storyId)
    {
        if (Scope != GraphScope.Project) throw new InvalidOperationException("Story boundary projection requires project scope.");
        Refresh();
        // Canvas ports are retained visuals; refreshing only view models leaves old sockets on screen.
        PortsChanged?.Invoke(this, new GraphPortsChangedEventArgs([storyId]));
    }

    /// <summary>
    /// Reconciles the retained WPF host with a repository-committed graph and
    /// clears history that belongs to the superseded persistence baseline.
    /// </summary>
    public void RestoreClipboardSnapshot(GraphDocument graph, IReadOnlyDictionary<string, GraphEditorNodePosition> positions)
    {
        var ports = CapturePortSignatures(); var nodes = CaptureNodeSignatures();
        var detached = GraphDocument.FromJson(graph.ToJson());
        Graph.Nodes = detached.Nodes; Graph.Connections = detached.Connections;
        _layout.Clear(); foreach (var pair in positions) _layout[pair.Key] = pair.Value;
        Refresh(); PublishGraphChanged(); PublishPortsChanged(ports); PublishNodesChanged(nodes);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CommitClipboardSnapshot(GraphDocument graph, IReadOnlyDictionary<string, GraphEditorNodePosition> layout,
        Action? undoResources = null, Action? redoResources = null, IReadOnlyList<GraphCommentFrame>? frames = null)
    {
        var before = Graph.ToJson(); var after = graph.ToJson();
        var beforeLayout = _layout.ToDictionary(p => p.Key, p => p.Value);
        var afterLayout = layout.ToDictionary(p => p.Key, p => p.Value);
        var beforeFrames = FrameSnapshot();
        var afterFrames = frames?.ToArray() ?? beforeFrames;
        void Apply(string desired, IReadOnlyDictionary<string, GraphEditorNodePosition> positions, Action? applyResources,
            Action? revertResources, string previous, IReadOnlyDictionary<string, GraphEditorNodePosition> previousPositions)
        {
            applyResources?.Invoke();
            try { RestoreClipboardSnapshot(GraphDocument.FromJson(desired), positions); }
            catch
            {
                revertResources?.Invoke();
                RestoreClipboardSnapshot(GraphDocument.FromJson(previous), previousPositions);
                throw;
            }
        }
        EditMetadata(() => { Apply(before, beforeLayout, undoResources, redoResources, after, afterLayout); RestoreFrames(beforeFrames); },
            () => { Apply(after, afterLayout, redoResources, undoResources, before, beforeLayout); RestoreFrames(afterFrames); });
    }

    public void ApplyPersistedSnapshot(GraphDocument graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var beforePorts = CapturePortSignatures();
        var beforeNodes = CaptureNodeSignatures();
        _session.ResetToPersistedSnapshot(graph);
        _undoHistory.Clear();
        _redoHistory.Clear();
        _activeLayoutMoveBefore = null;
        _authoringIssues.Clear();
        var liveNodeIds = (Graph.Nodes ?? [])
            .Where(node => node is not null && !string.IsNullOrWhiteSpace(node.Id))
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var staleId in _layout.Keys.Where(id => !liveNodeIds.Contains(id)).ToArray())
            _layout.Remove(staleId);
        Refresh(_session.LastValidationIssues);
        PublishGraphChanged();
        PublishPortsChanged(beforePorts);
        PublishNodesChanged(beforeNodes);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
    }

    private void Refresh(IReadOnlyList<ValidationIssue> issues)
    {
        var sourceNodes = (Graph.Nodes ?? []).Where(node => node is not null).ToArray();
        var counts = sourceNodes.GroupBy(node => node.Id ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var fallbackIndex = BuildFallbackIndices(sourceNodes, counts);
        var next = new List<GraphEditorNodeViewModel>(sourceNodes.Length);
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < sourceNodes.Length; index++)
        {
            var node = sourceNodes[index];
            var id = node.Id ?? string.Empty;
            var unique = !string.IsNullOrWhiteSpace(id) && counts[id] == 1 && usedIds.Add(id);
            GraphEditorNodeViewModel item;
            if (unique && _uniqueNodeItems.TryGetValue(id, out var existing))
            {
                item = existing;
                item.Update(node);
            }
            else
            {
                var position = PositionFor(id, index, fallbackIndex);
                item = new GraphEditorNodeViewModel(node, position.X, position.Y, OnNodePositionChanged, Scope);
                if (unique) _uniqueNodeItems[id] = item;
            }

            if (unique)
            {
                var position = PositionFor(id, index, fallbackIndex);
                item.SetProjectionPosition(position.X, position.Y);
            }
            next.Add(item);
        }

        SynchronizeCollection(Nodes, next);

        var availableConnections = Connections.ToList();
        var nextConnections = new List<GraphEditorConnectionViewModel>();
        foreach (var connection in (Graph.Connections ?? []).Where(connection => connection is not null))
        {
            var existing = availableConnections.FirstOrDefault(candidate => candidate.Connection.Equals(connection));
            if (existing is not null)
            {
                availableConnections.Remove(existing);
                nextConnections.Add(existing);
            }
            else nextConnections.Add(new GraphEditorConnectionViewModel(connection));
        }
        SynchronizeCollection(Connections, nextConnections);

        PublishState(issues);
    }

    /// <summary>
    /// Reconciles a bindable projection without Clear/Reset. Existing instances
    /// remain alive, so WPF keeps node visuals, selection, focus, and viewport
    /// state while a single graph mutation is projected.
    /// </summary>
    private static void SynchronizeCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> next)
        where T : class
    {
        for (var index = 0; index < next.Count; index++)
        {
            var item = next[index];
            if (index < target.Count && ReferenceEquals(target[index], item)) continue;

            var existingIndex = -1;
            for (var candidate = index + 1; candidate < target.Count; candidate++)
            {
                if (!ReferenceEquals(target[candidate], item)) continue;
                existingIndex = candidate;
                break;
            }

            if (existingIndex >= 0) target.Move(existingIndex, index);
            else target.Insert(index, item);
        }

        while (target.Count > next.Count) target.RemoveAt(target.Count - 1);
    }

    public void SetNodePosition(string nodeId, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) return;
        var node = Nodes.FirstOrDefault(item => string.Equals(item.NodeId, nodeId, StringComparison.Ordinal));
        if (node is not null) node.SetPosition(x, y);
        else if (!string.IsNullOrWhiteSpace(nodeId))
        {
            var position = new GraphEditorNodePosition(x, y);
            if (_layout.TryGetValue(nodeId, out var current) && current == position) return;
            _layout[nodeId] = position;
            if (!_suppressLayoutChanged) LayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Starts one transient host-only layout transaction. Realtime node motion
    /// updates the retained projection, but persistence is dirtied only once
    /// when the transaction commits.
    /// </summary>
    public bool BeginLayoutMove(IEnumerable<string> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        if (_activeLayoutMoveBefore is not null) return false;
        var requested = nodeIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
        var before = new Dictionary<string, GraphEditorNodePosition>(StringComparer.Ordinal);
        foreach (var id in requested)
        {
            var matches = Nodes.Where(node => string.Equals(node.NodeId, id, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1) return false;
            before[id] = matches[0].Position;
        }
        if (before.Count == 0) return false;
        _activeLayoutMoveBefore = before;
        _suppressLayoutChanged = true;
        return true;
    }

    /// <summary>Commits the active multi-node move as one host Undo entry.</summary>
    public bool CommitLayoutMove()
    {
        if (_activeLayoutMoveBefore is null) return false;
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var before = _activeLayoutMoveBefore;
        var after = before.Keys.ToDictionary(id => id, id =>
        {
            var node = Nodes.Single(item => string.Equals(item.NodeId, id, StringComparison.Ordinal));
            return node.Position;
        }, StringComparer.Ordinal);
        _activeLayoutMoveBefore = null;
        _suppressLayoutChanged = false;
        if (SameLayout(before, after)) return false;

        RecordHistory(new LayoutHistoryEntry(before, after, _redoHistory.ToArray()));
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return true;
    }

    /// <summary>Restores an uncommitted layout preview without creating history.</summary>
    public bool CancelLayoutMove()
    {
        if (_activeLayoutMoveBefore is null) return false;
        var before = _activeLayoutMoveBefore;
        _activeLayoutMoveBefore = null;
        ApplyLayoutSnapshot(before, publishChange: false);
        _suppressLayoutChanged = false;
        return true;
    }

    public bool CanConnect(GraphEditorEndpoint first, GraphEditorEndpoint second)
    {
        if (RejectReadOnlySource(first, second)) return false;
        var result = _commandBridge.CanConnect(first, second);
        PublishState(_commandBridge.LastValidationIssues);
        return result;
    }

    public bool CanReconnect(GraphConnection original, GraphEditorEndpoint first, GraphEditorEndpoint second)
    {
        if (RejectReadOnlySource(original.FromNodeId)
            || RejectReadOnlySource(first, second)) return false;
        var result = _commandBridge.CanReconnect(original, first, second);
        PublishState(_commandBridge.LastValidationIssues);
        return result;
    }

    public bool Connect(GraphEditorEndpoint first, GraphEditorEndpoint second)
        => RejectReadOnlySource(first, second)
            ? false
            : ExecuteBridge(() => _commandBridge.Connect(first, second));

    public bool Reconnect(GraphConnection original, GraphEditorEndpoint first, GraphEditorEndpoint second)
        => RejectReadOnlySource(original.FromNodeId)
            || RejectReadOnlySource(first, second)
            ? false
            : ExecuteBridge(() => _commandBridge.Reconnect(original, first, second));

    public bool Disconnect(GraphConnection connection)
        => RejectReadOnlySource(connection.FromNodeId)
            ? false
            : ExecuteBridge(() => _commandBridge.Disconnect(connection));

    public bool Disconnect(GraphEditorConnectionViewModel connection)
        => connection is not null && Disconnect(connection.Connection);

    public bool Disconnect(GraphEditorEndpoint first, GraphEditorEndpoint second)
        => RejectReadOnlySource(first, second)
            ? false
            : ExecuteBridge(() => _commandBridge.Disconnect(first, second));

    public bool AddNode(GraphNode node)
        => ExecuteSession(() => _session.AddNode(node));

    public IReadOnlyList<GraphConnection> GetNodeReferences(string nodeId)
        => _session.GetNodeReferences(nodeId);

    public bool RemoveNode(string nodeId, bool confirmReferencedRemoval = false)
        => ExecuteSession(() => _session.RemoveNode(nodeId, confirmReferencedRemoval));

    public bool RemoveNodes(IReadOnlyList<string> nodeIds, bool confirmReferencedRemoval = false)
        => ExecuteSession(() => _session.RemoveNodes(nodeIds, confirmReferencedRemoval));

    /// <summary>
    /// Resolves the dragged node's unique same-kind input and/or output and
    /// preflights the complete replacement graph state on a detached document.
    /// Invalid and ambiguous candidates are intentionally silent because a
    /// Shift-drag miss remains an ordinary layout gesture.
    /// </summary>
    public bool TryCreateSplicePlan(GraphConnection original, string draggedNodeId,
        out GraphConnectionSplicePlan plan)
    {
        plan = null!;
        if (original is null || string.IsNullOrWhiteSpace(draggedNodeId)) return false;
        if (IsReadOnlySource(original.FromNodeId))
        {
            RejectReadOnlySource(original.FromNodeId);
            return false;
        }
        var liveOriginals = (Graph.Connections ?? []).Where(connection => connection is not null
            && connection.Equals(original)).ToArray();
        var nodes = (Graph.Nodes ?? []).Where(node => node is not null
            && string.Equals(node.Id, draggedNodeId, StringComparison.Ordinal)).ToArray();
        if (liveOriginals.Length != 1 || nodes.Length != 1) return false;

        var ports = (nodes[0].Ports ?? []).Where(port => port is not null
            && port.InterfaceKind == original.InterfaceKind
            && !string.IsNullOrWhiteSpace(port.Id)).ToArray();
        var inputs = ports.Where(port => port.Direction == GraphPortDirection.Input).ToArray();
        var outputs = ports.Where(port => port.Direction == GraphPortDirection.Output).ToArray();
        if (inputs.Length > 1 || outputs.Length > 1 || inputs.Length + outputs.Length == 0)
            return false;

        var replacements = new List<GraphConnection>(2);
        if (inputs.Length == 1)
            replacements.Add(new GraphConnection(original.FromNodeId, original.FromPortId,
                draggedNodeId, inputs[0].Id, original.InterfaceKind));
        if (outputs.Length == 1)
            replacements.Add(new GraphConnection(draggedNodeId, outputs[0].Id,
                original.ToNodeId, original.ToPortId, original.InterfaceKind));
        if (replacements.Any(replacement => IsReadOnlySource(replacement.FromNodeId)))
        {
            RejectReadOnlySource(replacements.First(replacement => IsReadOnlySource(replacement.FromNodeId)).FromNodeId);
            return false;
        }
        var detached = GraphDocument.FromJson(Graph.ToJson());
        var preview = new GraphEditSession(detached, Scope, _session.CompatibilityMode);
        if (!preview.ReplaceConnections([original], replacements)) return false;
        plan = new GraphConnectionSplicePlan(original, replacements.ToArray());
        return true;
    }

    public bool CanSpliceConnection(GraphConnection original, string draggedNodeId)
        => TryCreateSplicePlan(original, draggedNodeId, out _);

    public bool SpliceConnection(GraphConnectionSplicePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (RejectReadOnlySource(plan.Original.FromNodeId)
            || plan.Replacements.Any(replacement => IsReadOnlySource(replacement.FromNodeId)))
            return false;
        return ExecuteSession(() => _session.ReplaceConnections(
                [plan.Original], plan.Replacements));
    }

    public bool CompleteConnectionDrag(GraphEditorEndpoint? first, GraphEditorEndpoint? second,
        GraphConnection? original = null)
        => RejectReadOnlySource(original?.FromNodeId)
            || RejectReadOnlySource(first, second)
            ? false
            : ExecuteBridge(() => _commandBridge.CompleteConnectionDrag(first, second, original));

    public bool CompleteWireDrag(GraphEditorEndpoint? first, GraphEditorEndpoint? second,
        GraphConnection? original = null)
        => RejectReadOnlySource(original?.FromNodeId)
            || RejectReadOnlySource(first, second)
            ? false
            : ExecuteBridge(() => _commandBridge.CompleteWireDrag(first, second, original));

    /// <summary>
    /// Reconnects every edge incident to a multi-wire endpoint as one validated
    /// gesture.  Validation runs against a detached document with all of the
    /// originals removed, so a failed target cannot partially move the bundle.
    /// </summary>
    public bool CompleteIncidentWireDrag(IReadOnlyList<GraphConnection> originals,
        GraphEditorEndpoint movingEndpoint, GraphEditorEndpoint? target)
    {
        if (originals is null || originals.Count == 0) return false;
        if (RejectReadOnlySources(originals.Select(connection => connection?.FromNodeId))
            || RejectReadOnlySource(movingEndpoint, target)) return false;
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var beforePorts = CapturePortSignatures();
        var beforeNodes = CaptureNodeSignatures();
        var current = originals.Where(connection => connection is not null).ToArray();
        if (current.Length != originals.Count || current.Select(connection => connection).Distinct().Count() != current.Length)
            return false;

        var documentConnections = (Graph.Connections ?? []).Where(connection => connection is not null).ToArray();
        if (current.Any(original => documentConnections.Count(connection => connection.Equals(original)) != 1))
            return false;

        if (!target.HasValue)
        {
            var disconnected = _session.ReplaceConnections(current, []);
            Refresh(_session.LastValidationIssues);
            if (disconnected)
            {
                PublishGraphChanged();
                PublishPortsChanged(beforePorts);
                PublishNodesChanged(beforeNodes);
                RecordHistory(new GraphHistoryEntry(_redoHistory.ToArray()));
            }
            NotifyHistoryStateChanged(oldUndo, oldRedo);
            return disconnected;
        }

        var replacement = target.Value;
        if (movingEndpoint.Direction != replacement.Direction
            || movingEndpoint.InterfaceKind != replacement.InterfaceKind
            || movingEndpoint.Direction is not (GraphPortDirection.Input or GraphPortDirection.Output))
            return false;

        var detached = GraphDocument.FromJson(Graph.ToJson());
        detached.Connections = (detached.Connections ?? [])
            .Where(connection => connection is not null && !current.Any(original => connection.Equals(original)))
            .ToList();
        var candidates = new List<GraphConnection>(current.Length);
        foreach (var original in current)
        {
            var candidate = movingEndpoint.IsInput
                ? new GraphConnection(original.FromNodeId, original.FromPortId,
                    replacement.NodeId, replacement.PortId, movingEndpoint.InterfaceKind)
                : new GraphConnection(replacement.NodeId, replacement.PortId,
                    original.ToNodeId, original.ToPortId, movingEndpoint.InterfaceKind);
            var issues = CandidateEdgeValidator.Validate(detached, candidate, Scope,
                excludedConnection: null, compatibilityMode: _session.CompatibilityMode);
            if (issues.Count != 0)
            {
                PublishState(issues);
                return false;
            }
            detached.Connections.Add(GraphConnection.FromJson(candidate.ToJson()));
            candidates.Add(candidate);
        }

        var reconnected = _session.ReplaceConnections(current, candidates);
        Refresh(_session.LastValidationIssues);
        if (reconnected)
        {
            PublishGraphChanged();
            PublishPortsChanged(beforePorts);
            PublishNodesChanged(beforeNodes);
            RecordHistory(new GraphHistoryEntry(_redoHistory.ToArray()));
        }
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return reconnected;
    }

    public bool CanReconnectIncidentConnections(IReadOnlyList<GraphConnection> originals,
        GraphEditorEndpoint movingEndpoint, GraphEditorEndpoint target)
    {
        if (RejectReadOnlySources(originals.Select(connection => connection?.FromNodeId))
            || RejectReadOnlySource(movingEndpoint, target)) return false;
        if (!TryBuildIncidentCandidates(originals, movingEndpoint, target, out _, out var issues))
        {
            PublishState(issues);
            return false;
        }
        PublishState([]);
        return true;
    }

    public bool ReconnectIncidentConnections(IReadOnlyList<GraphConnection> originals,
        GraphEditorEndpoint movingEndpoint, GraphEditorEndpoint? target)
        => CompleteIncidentWireDrag(originals, movingEndpoint, target);

    private bool TryBuildIncidentCandidates(IReadOnlyList<GraphConnection> originals,
        GraphEditorEndpoint movingEndpoint, GraphEditorEndpoint target,
        out IReadOnlyList<GraphConnection> candidates, out IReadOnlyList<ValidationIssue> issues)
    {
        candidates = [];
        issues = [];
        if (originals is null || originals.Count == 0
            || movingEndpoint.Direction is not (GraphPortDirection.Input or GraphPortDirection.Output)
            || movingEndpoint.InterfaceKind != target.InterfaceKind
            || movingEndpoint.Direction != target.Direction)
            return false;
        if (IsReadOnlySource(movingEndpoint.IsOutput ? movingEndpoint.NodeId : target.NodeId)
            || originals.Any(connection => connection is not null
                && IsReadOnlySource(connection.FromNodeId)))
        {
            var sourceId = movingEndpoint.IsOutput ? movingEndpoint.NodeId
                : originals.FirstOrDefault(connection => connection is not null
                    && IsReadOnlySource(connection.FromNodeId))?.FromNodeId;
            if (sourceId is not null) RejectReadOnlySource(sourceId);
            return false;
        }
        var current = originals.Where(connection => connection is not null).ToArray();
        var documentConnections = (Graph.Connections ?? []).Where(connection => connection is not null).ToArray();
        if (current.Length != originals.Count || current.Distinct().Count() != current.Length
            || current.Any(original => documentConnections.Count(connection => connection.Equals(original)) != 1))
            return false;
        var detached = GraphDocument.FromJson(Graph.ToJson());
        detached.Connections = (detached.Connections ?? [])
            .Where(connection => connection is not null && !current.Any(original => connection.Equals(original)))
            .ToList();
        var built = new List<GraphConnection>(current.Length);
        foreach (var original in current)
        {
            var candidate = movingEndpoint.IsInput
                ? new GraphConnection(original.FromNodeId, original.FromPortId, target.NodeId, target.PortId, movingEndpoint.InterfaceKind)
                : new GraphConnection(target.NodeId, target.PortId, original.ToNodeId, original.ToPortId, movingEndpoint.InterfaceKind);
            var validation = CandidateEdgeValidator.Validate(detached, candidate, Scope,
                compatibilityMode: _session.CompatibilityMode);
            if (validation.Count != 0)
            {
                issues = validation;
                return false;
            }
            detached.Connections.Add(GraphConnection.FromJson(candidate.ToJson()));
            built.Add(candidate);
        }
        candidates = built;
        return true;
    }

    public bool CanReconnect(GraphEditorEndpoint first, GraphEditorEndpoint second, GraphConnection original)
        => CanReconnect(original, first, second);

    public bool Reconnect(GraphEditorEndpoint first, GraphEditorEndpoint second, GraphConnection original)
        => Reconnect(original, first, second);

    public bool AddDynamicPort(string nodeId, string displayName, GraphPortDirection direction,
        GraphInterfaceKind interfaceKind)
        => ExecuteSession(() => _session.AddDynamicPort(nodeId, displayName, direction, interfaceKind));

    public bool AddDynamicPort(string nodeId, string displayName, bool isInput, GraphInterfaceKind interfaceKind)
        => ExecuteSession(() => _session.AddDynamicPort(nodeId, displayName, isInput, interfaceKind));

    public bool AddDynamicPort(string nodeId, string displayName, GraphInterfaceKind interfaceKind)
        => ExecuteSession(() => _session.AddDynamicPort(nodeId, displayName, interfaceKind));

    public bool AddDynamicPort(string nodeId, GraphInterfaceKind interfaceKind, bool isInput,
        string displayName)
        => ExecuteSession(() => _session.AddDynamicPort(nodeId, interfaceKind, isInput, displayName));

    public bool AddDynamicPort(string nodeId, bool isInput, GraphInterfaceKind interfaceKind,
        string displayName)
        => ExecuteSession(() => _session.AddDynamicPort(nodeId, isInput, interfaceKind, displayName));

    public bool AddDynamicPort(string nodeId, GraphPortDirection direction,
        GraphInterfaceKind interfaceKind, string displayName)
        => ExecuteSession(() => _session.AddDynamicPort(nodeId, direction, interfaceKind, displayName));

    public bool AddDynamicPort(string nodeId, GraphInterfaceKind interfaceKind,
        GraphPortDirection direction, string displayName)
        => ExecuteSession(() => _session.AddDynamicPort(nodeId, direction, interfaceKind, displayName));

    public bool AddDynamicPort(string nodeId, string displayName)
        => ExecuteSession(() => _session.AddDynamicPort(nodeId, displayName));

    public bool RenamePortDisplayName(string nodeId, string portId, string displayName)
        => ExecuteSession(() => _session.RenamePortDisplayName(nodeId, portId, displayName));

    public bool ReorderPort(string nodeId, string portId, int order)
        => ExecuteSession(() => _session.ReorderPort(nodeId, portId, order));

    public bool MoveDynamicPort(string nodeId, string portId, int order)
        => ExecuteSession(() => _session.MoveDynamicPort(nodeId, portId, order));

    public bool ReorderDynamicPort(string nodeId, string portId, int order)
        => ExecuteSession(() => _session.ReorderDynamicPort(nodeId, portId, order));

    public bool SetNodeProperty(string nodeId, string property, JsonElement value)
        => ExecuteSession(() => _session.SetNodeProperty(nodeId, property, value));

    public bool SetAdvancedActionMode(string nodeId, bool enabled)
    {
        var node = Nodes.FirstOrDefault(n => n.NodeId == nodeId);
        if (node is null || !node.Properties.TryGetValue("action_type", out var value)) return false;
        var target = enabled ? CanonicalStoryActionSchema.ExecuteCommand
            : CanonicalStoryActionSchema.ActionTypes.First(type => type != CanonicalStoryActionSchema.ExecuteCommand);
        return ChangeStoryActionType(nodeId, target);
    }

    public bool SetSessionMusic(string nodeId, string? mediaRef)
        => ExecuteSession(() => _session.SetSessionMusic(nodeId, mediaRef));

    public bool ChangeSessionSpeaker(string nodeId, string? actorId)
        => ExecuteSession(() => _session.ChangeSessionSpeaker(nodeId, actorId));

    public bool ChangeStoryBuffMode(string nodeId, bool modExtension)
        => ExecuteSession(() => _session.ChangeStoryBuffMode(nodeId, modExtension));

    public bool SetNodeProperty<T>(string nodeId, string property, T value)
        => ExecuteSession(() => _session.SetNodeProperty(nodeId, property, value));

    public bool SetStoryStartTriggerProperties(string nodeId, string portId,
        IReadOnlyDictionary<string, JsonElement> properties)
        => ExecuteSession(() => _session.SetStoryStartTriggerProperties(nodeId, portId, properties));

    public bool AddStoryStartTrigger(string nodeId, string displayName, string triggerType,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
        => ExecuteSession(() => _session.AddStoryStartTrigger(nodeId, displayName, triggerType, triggerProperties));

    public bool SetStoryStartTriggerType(string nodeId, string portId, string triggerType,
        string? actorId = null)
        => ExecuteSession(() => _session.SetStoryStartTriggerType(nodeId, portId, triggerType, actorId));

    public bool RenameStoryStartTrigger(string nodeId, string portId, string displayName)
        => ExecuteSession(() => _session.RenameStoryStartTrigger(nodeId, portId, displayName));

    public bool ReorderStoryStartTrigger(string nodeId, string portId, int order)
        => ExecuteSession(() => _session.ReorderStoryStartTrigger(nodeId, portId, order));

    public bool RemoveStoryStartTrigger(string nodeId, string portId, bool confirmReferencedRemoval = false)
        => ExecuteSession(() => _session.RemoveStoryStartTrigger(nodeId, portId, confirmReferencedRemoval));

    public bool SetStoryStartRepeatPolicy(string nodeId, string repeatPolicy)
        => ExecuteSession(() => _session.SetStoryStartRepeatPolicy(nodeId, repeatPolicy));

    public bool ChangeObjectiveType(string nodeId, string? type, string? actorId = null)
        => ExecuteSession(() => _session.ChangeObjectiveType(nodeId, type, actorId));

    public bool ChangeObjectiveTarget(string nodeId, string? targetId)
        => ExecuteSession(() => _session.ChangeObjectiveTarget(nodeId, targetId));

    public bool SetObjectiveType(string nodeId, string? type, string? actorId = null)
        => ChangeObjectiveType(nodeId, type, actorId);

    public bool SetObjectiveDescription(string nodeId, string? description)
        => ExecuteSession(() => _session.SetObjectiveDescription(nodeId, description));

    public bool SetObjectiveRequired(string nodeId, int required)
        => ExecuteSession(() => _session.SetObjectiveRequired(nodeId, required));

    public bool SetObjectivePrerequisiteEnabled(string nodeId, bool enabled)
        => ExecuteSession(() => _session.SetObjectivePrerequisiteEnabled(nodeId, enabled));

    public bool ChangeStoryActionType(string nodeId, string? type, string? itemId = null)
        => ExecuteSession(() => _session.ChangeStoryActionType(nodeId, type, itemId));

    public bool AddSessionChoiceOption(string nodeId, string displayText)
        => ExecuteSession(() => _session.AddSessionChoiceOption(nodeId, displayText));

    public bool RenameSessionChoiceOption(string nodeId, string optionId, string displayText)
        => ExecuteSession(() => _session.RenameSessionChoiceOption(nodeId, optionId, displayText));

    public bool ReorderSessionChoiceOption(string nodeId, string optionId, int order)
        => ExecuteSession(() => _session.ReorderSessionChoiceOption(nodeId, optionId, order));

    public bool RemoveSessionChoiceOption(string nodeId, string optionId, bool confirmReferencedRemoval = false)
        => ExecuteSession(() => _session.RemoveSessionChoiceOption(nodeId, optionId, confirmReferencedRemoval));

    public bool RemoveNodeProperty(string nodeId, string property)
        => ExecuteSession(() => _session.RemoveNodeProperty(nodeId, property));

    public IReadOnlyList<GraphConnection> GetPortReferences(string nodeId, string portId)
        => _session.GetPortReferences(nodeId, portId);

    public IReadOnlyList<GraphConnection> GetDynamicPortReferences(string nodeId, string portId)
        => GetPortReferences(nodeId, portId);

    public bool RemoveDynamicPort(string nodeId, string portId, bool confirmReferencedRemoval = false)
        => ExecuteSession(() => _session.RemoveDynamicPort(nodeId, portId, confirmReferencedRemoval));

    public CanonicalAggregateSynchronizationPlan AnalyzeAggregateSynchronization(
        GraphResourceEnvelope resource)
    {
        var plan = _session.AnalyzeAggregateSynchronization(resource);
        PublishState(_session.LastValidationIssues);
        return plan;
    }

    public bool ApplyAggregateSynchronization(
        CanonicalAggregateSynchronizationPlan plan,
        bool confirmReferencedRemoval = false)
    {
        var oldUndoCount = _session.UndoCount;
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var beforePorts = CapturePortSignatures();
        var beforeNodes = CaptureNodeSignatures();
        var result = _session.ApplyAggregateSynchronization(plan, confirmReferencedRemoval);
        var changed = result && _session.UndoCount != oldUndoCount;
        if (changed)
        {
            Refresh(_session.LastValidationIssues);
            PublishGraphChanged();
            PublishPortsChanged(beforePorts);
            PublishNodesChanged(beforeNodes);
            RecordHistory(new GraphHistoryEntry(_redoHistory.ToArray()));
        }
        else PublishState(_session.LastValidationIssues);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return result;
    }

    /// <summary>Compensates the last edit after a failed outer persistence transaction.</summary>
    public bool RollbackLastEdit()
    {
        if (_undoHistory.TryPeek(out var entry) is false || entry is not GraphHistoryEntry)
            return false;
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var result = ApplyGraphHistory(_session.RollbackLastEdit, () => _session.LastValidationIssues);
        if (!result) return false;
        _ = _undoHistory.Pop();
        _redoHistory.Clear();
        foreach (var displaced in entry.DisplacedRedo.Reverse()) _redoHistory.Push(displaced);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return true;
    }

    public bool Undo()
    {
        if (!_undoHistory.TryPeek(out var entry)) return false;
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var result = entry switch
        {
            GraphHistoryEntry => ApplyGraphHistory(_commandBridge.Undo, () => _commandBridge.LastValidationIssues),
            LayoutHistoryEntry layout => ApplyLayoutSnapshot(layout.Before, publishChange: true),
            EditorHistoryEntry metadata => ApplyEditorHistory(metadata.Undo),
            _ => false,
        };
        if (!result) return false;
        _ = _undoHistory.Pop();
        _redoHistory.Push(entry);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return true;
    }

    public bool Redo()
    {
        if (!_redoHistory.TryPeek(out var entry)) return false;
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var result = entry switch
        {
            GraphHistoryEntry => ApplyGraphHistory(_commandBridge.Redo, () => _commandBridge.LastValidationIssues),
            LayoutHistoryEntry layout => ApplyLayoutSnapshot(layout.After, publishChange: true),
            EditorHistoryEntry metadata => ApplyEditorHistory(metadata.Redo),
            _ => false,
        };
        if (!result) return false;
        _ = _redoHistory.Pop();
        _undoHistory.Push(entry);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return true;
    }

    private bool IsReadOnlySource(string? nodeId)
        => !string.IsNullOrWhiteSpace(nodeId)
            && _readOnlySourcePolicy?.Invoke(nodeId) == true;

    private bool RejectReadOnlySource(string? nodeId)
    {
        if (!IsReadOnlySource(nodeId)) return false;
        PublishState([
            new ValidationIssue(
                "story.graph.reference.readonly",
                "引用故事的输出连线由源故事包定义；请导入后编辑。可从本地故事接入引用故事输入。",
                "source_story_id",
                NodeId: nodeId)]);
        return true;
    }

    private bool RejectReadOnlySources(IEnumerable<string?> nodeIds)
    {
        var nodeId = nodeIds.FirstOrDefault(IsReadOnlySource);
        return nodeId is not null && RejectReadOnlySource(nodeId);
    }

    private bool RejectReadOnlySource(GraphEditorEndpoint first, GraphEditorEndpoint second)
    {
        if (first.IsOutput) return RejectReadOnlySource(first.NodeId);
        if (second.IsOutput) return RejectReadOnlySource(second.NodeId);
        return false;
    }

    private bool RejectReadOnlySource(GraphEditorEndpoint? first, GraphEditorEndpoint? second)
    {
        if (first.HasValue && first.Value.IsOutput
            && RejectReadOnlySource(first.Value.NodeId)) return true;
        return second.HasValue && second.Value.IsOutput
            && RejectReadOnlySource(second.Value.NodeId);
    }

    private bool RejectReadOnlySource(GraphEditorEndpoint movingEndpoint,
        GraphEditorEndpoint? target)
    {
        if (movingEndpoint.IsOutput && RejectReadOnlySource(movingEndpoint.NodeId)) return true;
        return target.HasValue && target.Value.IsOutput
            && RejectReadOnlySource(target.Value.NodeId);
    }

    private bool ExecuteBridge(Func<bool> command)
    {
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var oldUndoCount = _session.UndoCount;
        var beforePorts = CapturePortSignatures();
        var beforeNodes = CaptureNodeSignatures();
        var result = command();
        var issues = _commandBridge.LastValidationIssues;
        if (result)
        {
            Refresh(issues);
            PublishGraphChanged();
            PublishPortsChanged(beforePorts);
            PublishNodesChanged(beforeNodes);
            if (_session.UndoCount > oldUndoCount)
                RecordHistory(new GraphHistoryEntry(_redoHistory.ToArray()));
        }
        else PublishState(issues);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return result;
    }

    private bool ExecuteSession(Func<bool> command)
    {
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var oldUndoCount = _session.UndoCount;
        var beforePorts = CapturePortSignatures();
        var beforeNodes = CaptureNodeSignatures();
        var result = command();
        var issues = _session.LastValidationIssues;
        if (result)
        {
            Refresh(issues);
            PublishGraphChanged();
            PublishPortsChanged(beforePorts);
            PublishNodesChanged(beforeNodes);
            if (_session.UndoCount > oldUndoCount)
                RecordHistory(new GraphHistoryEntry(_redoHistory.ToArray()));
        }
        else PublishState(issues);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return result;
    }

    private bool ApplyGraphHistory(
        Func<bool> command,
        Func<IReadOnlyList<ValidationIssue>> issues)
    {
        var beforePorts = CapturePortSignatures();
        var beforeNodes = CaptureNodeSignatures();
        var result = command();
        if (!result)
        {
            PublishState(issues());
            return false;
        }
        Refresh(issues());
        PublishGraphChanged();
        PublishPortsChanged(beforePorts);
        PublishNodesChanged(beforeNodes);
        return true;
    }

    private void RecordHistory(HostHistoryEntry entry)
    {
        _undoHistory.Push(entry);
        _redoHistory.Clear();
    }

    private void NotifyHistoryStateChanged(bool oldUndo, bool oldRedo)
    {
        if (oldUndo != CanUndo) OnPropertyChanged(nameof(CanUndo));
        if (oldRedo != CanRedo) OnPropertyChanged(nameof(CanRedo));
    }

    private void PublishGraphChanged()
    {
        GraphRevision++;
        OnPropertyChanged(nameof(GraphRevision));
        GraphChanged?.Invoke(this, EventArgs.Empty);
    }

    private Dictionary<string, string> CapturePortSignatures()
    {
        var signatures = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in (Graph.Nodes ?? []).Where(node => node is not null))
        {
            var id = node.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var signature = string.Join("\u001e", (node.Ports ?? [])
                .Where(port => port is not null)
                .OrderBy(port => port.Order)
                .ThenBy(port => port.Id ?? string.Empty, StringComparer.Ordinal)
                .Select(port => string.Join("\u001f", port.Id ?? string.Empty, port.DisplayName ?? string.Empty,
                    port.Direction, port.InterfaceKind, port.Order)));
            if (!signatures.TryAdd(id, signature)) signatures.Remove(id);
        }
        return signatures;
    }

    private void PublishPortsChanged(IReadOnlyDictionary<string, string> before)
    {
        var after = CapturePortSignatures();
        var changed = before.Keys.Union(after.Keys, StringComparer.Ordinal)
            .Where(id => !before.TryGetValue(id, out var oldSignature)
                || !after.TryGetValue(id, out var newSignature)
                || !string.Equals(oldSignature, newSignature, StringComparison.Ordinal))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (changed.Length != 0) PortsChanged?.Invoke(this, new GraphPortsChangedEventArgs(changed));
    }

    private Dictionary<string, string> CaptureNodeSignatures()
    {
        var signatures = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var node in (Graph.Nodes ?? []).Where(node => node is not null))
        {
            var id = node.Id ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var signature = JsonSerializer.Serialize(node, GraphSerializer.Options);
            if (!signatures.TryAdd(id, signature)) signatures.Remove(id);
        }
        return signatures;
    }

    private void PublishNodesChanged(IReadOnlyDictionary<string, string> before)
    {
        var after = CaptureNodeSignatures();
        var changed = before.Keys.Union(after.Keys, StringComparer.Ordinal)
            .Where(id => !before.TryGetValue(id, out var oldSignature)
                || !after.TryGetValue(id, out var newSignature)
                || !string.Equals(oldSignature, newSignature, StringComparison.Ordinal))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (changed.Length != 0) NodesChanged?.Invoke(this, new GraphNodesChangedEventArgs(changed));
    }

    private void PublishState(IReadOnlyList<ValidationIssue> issues)
    {
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        _coreValidationIssues = issues.ToArray();
        _lastValidationIssues = _coreValidationIssues.Concat(_authoringIssues.Values).ToArray();
        OnPropertyChanged(nameof(LastValidationIssues));
        if (oldUndo != CanUndo) OnPropertyChanged(nameof(CanUndo));
        if (oldRedo != CanRedo) OnPropertyChanged(nameof(CanRedo));
    }

    private void OnNodePositionChanged(GraphEditorNodeViewModel node, double x, double y)
    {
        if (!string.IsNullOrWhiteSpace(node.NodeId) && double.IsFinite(x) && double.IsFinite(y))
        {
            var position = new GraphEditorNodePosition(x, y);
            if (_layout.TryGetValue(node.NodeId, out var current) && current == position) return;
            _layout[node.NodeId] = position;
            if (!_suppressLayoutChanged) LayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool ApplyLayoutSnapshot(
        IReadOnlyDictionary<string, GraphEditorNodePosition> snapshot,
        bool publishChange)
    {
        var resolved = new List<(GraphEditorNodeViewModel Node, GraphEditorNodePosition Position)>(snapshot.Count);
        foreach (var pair in snapshot)
        {
            if (!pair.Value.IsFinite) return false;
            var matches = Nodes.Where(node => string.Equals(node.NodeId, pair.Key, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1) return false;
            resolved.Add((matches[0], pair.Value));
        }

        var priorSuppression = _suppressLayoutChanged;
        _suppressLayoutChanged = true;
        try
        {
            foreach (var item in resolved) item.Node.SetPosition(item.Position.X, item.Position.Y);
        }
        finally { _suppressLayoutChanged = priorSuppression; }
        if (publishChange) LayoutChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static bool SameLayout(
        IReadOnlyDictionary<string, GraphEditorNodePosition> first,
        IReadOnlyDictionary<string, GraphEditorNodePosition> second)
        => first.Count == second.Count && first.All(pair =>
            second.TryGetValue(pair.Key, out var position) && position == pair.Value);

    private static Dictionary<string, int> BuildFallbackIndices(GraphNode[] nodes,
        IReadOnlyDictionary<string, int> counts)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        var uniqueIds = nodes.Select(node => node.Id ?? string.Empty)
            .Where(id => !string.IsNullOrWhiteSpace(id) && counts.TryGetValue(id, out var count) && count == 1)
            .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        for (var index = 0; index < uniqueIds.Length; index++) result[uniqueIds[index]] = index;
        return result;
    }

    private static IReadOnlyDictionary<string, GraphEditorNodePosition>? CoerceLayout(object? layout)
    {
        if (layout is null) return null;
        if (layout is IReadOnlyDictionary<string, GraphEditorNodePosition> positions)
            return positions;
        if (layout is IReadOnlyDictionary<string, (double X, double Y)> tuples)
            return tuples.ToDictionary(item => item.Key,
                item => new GraphEditorNodePosition(item.Value.X, item.Value.Y), StringComparer.Ordinal);
        throw new ArgumentException(
            "Layout must be a dictionary of GraphEditorNodePosition or (double X, double Y) values.",
            nameof(layout));
    }

    private GraphEditorNodePosition PositionFor(string id, int index,
        IReadOnlyDictionary<string, int> fallbackIndex)
    {
        if (!string.IsNullOrWhiteSpace(id) && _layout.TryGetValue(id, out var supplied) && supplied.IsFinite)
            return supplied;
        var ordinal = fallbackIndex.TryGetValue(id, out var sortedIndex) ? sortedIndex : index;
        var position = new GraphEditorNodePosition(80 + (ordinal % 5) * 260, 80 + (ordinal / 5) * 170);
        if (!string.IsNullOrWhiteSpace(id)) _layout.TryAdd(id, position);
        return position;
    }
}
