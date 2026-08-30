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

    internal GraphEditorNodeViewModel(GraphNode node, double x, double y,
        Action<GraphEditorNodeViewModel, double, double>? positionChanged)
    {
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
        DisplayName = node.DisplayName ?? string.Empty;
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var pair in node.Properties ?? []) properties[pair.Key] = pair.Value.Clone();
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
public sealed class GraphEditorHostViewModel : ObservableObject
{
    private readonly GraphEditSession _session;
    private readonly GraphEditorCommandBridge _commandBridge;
    private readonly Dictionary<string, GraphEditorNodeViewModel> _uniqueNodeItems = new(StringComparer.Ordinal);
    private readonly Dictionary<string, GraphEditorNodePosition> _layout = new(StringComparer.Ordinal);
    private IReadOnlyList<ValidationIssue> _lastValidationIssues = [];

    /// <summary>
    /// Raised only after a successful canonical graph mutation. Preview,
    /// validation failure, projection refresh, and layout-only movement do not
    /// advance the graph revision.
    /// </summary>
    public event EventHandler? GraphChanged;

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
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;
    public IReadOnlyList<ValidationIssue> LastValidationIssues => _lastValidationIssues;
    public IReadOnlyDictionary<string, GraphEditorNodePosition> Layout => _layout;
    public long GraphRevision { get; private set; }

    /// <summary>Reprojects the current document while retaining unique node items and layout.</summary>
    public void Refresh()
        => Refresh(_commandBridge.LastValidationIssues);

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
                item = new GraphEditorNodeViewModel(node, position.X, position.Y, OnNodePositionChanged);
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
        else if (!string.IsNullOrWhiteSpace(nodeId)) _layout[nodeId] = new GraphEditorNodePosition(x, y);
    }

    public bool CanConnect(GraphEditorEndpoint first, GraphEditorEndpoint second)
    {
        var result = _commandBridge.CanConnect(first, second);
        PublishState(_commandBridge.LastValidationIssues);
        return result;
    }

    public bool CanReconnect(GraphConnection original, GraphEditorEndpoint first, GraphEditorEndpoint second)
    {
        var result = _commandBridge.CanReconnect(original, first, second);
        PublishState(_commandBridge.LastValidationIssues);
        return result;
    }

    public bool Connect(GraphEditorEndpoint first, GraphEditorEndpoint second)
        => ExecuteBridge(() => _commandBridge.Connect(first, second));

    public bool Reconnect(GraphConnection original, GraphEditorEndpoint first, GraphEditorEndpoint second)
        => ExecuteBridge(() => _commandBridge.Reconnect(original, first, second));

    public bool Disconnect(GraphConnection connection)
        => ExecuteBridge(() => _commandBridge.Disconnect(connection));

    public bool Disconnect(GraphEditorConnectionViewModel connection)
        => connection is not null && Disconnect(connection.Connection);

    public bool Disconnect(GraphEditorEndpoint first, GraphEditorEndpoint second)
        => ExecuteBridge(() => _commandBridge.Disconnect(first, second));

    public bool AddNode(GraphNode node)
        => ExecuteSession(() => _session.AddNode(node));

    public IReadOnlyList<GraphConnection> GetNodeReferences(string nodeId)
        => _session.GetNodeReferences(nodeId);

    public bool RemoveNode(string nodeId, bool confirmReferencedRemoval = false)
        => ExecuteSession(() => _session.RemoveNode(nodeId, confirmReferencedRemoval));

    public bool CompleteConnectionDrag(GraphEditorEndpoint? first, GraphEditorEndpoint? second,
        GraphConnection? original = null)
        => ExecuteBridge(() => _commandBridge.CompleteConnectionDrag(first, second, original));

    public bool CompleteWireDrag(GraphEditorEndpoint? first, GraphEditorEndpoint? second,
        GraphConnection? original = null)
        => ExecuteBridge(() => _commandBridge.CompleteWireDrag(first, second, original));

    /// <summary>
    /// Reconnects every edge incident to a multi-wire endpoint as one validated
    /// gesture.  Validation runs against a detached document with all of the
    /// originals removed, so a failed target cannot partially move the bundle.
    /// </summary>
    public bool CompleteIncidentWireDrag(IReadOnlyList<GraphConnection> originals,
        GraphEditorEndpoint movingEndpoint, GraphEditorEndpoint? target)
    {
        if (originals is null || originals.Count == 0) return false;
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
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
            if (disconnected) PublishGraphChanged();
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
        if (reconnected) PublishGraphChanged();
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return reconnected;
    }

    public bool CanReconnectIncidentConnections(IReadOnlyList<GraphConnection> originals,
        GraphEditorEndpoint movingEndpoint, GraphEditorEndpoint target)
    {
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

    public bool SetNodeProperty<T>(string nodeId, string property, T value)
        => ExecuteSession(() => _session.SetNodeProperty(nodeId, property, value));

    public bool SetStoryStartTriggerProperties(string nodeId, string portId,
        IReadOnlyDictionary<string, JsonElement> properties)
        => ExecuteSession(() => _session.SetStoryStartTriggerProperties(nodeId, portId, properties));

    public bool ChangeObjectiveType(string nodeId, string? type, string? actorId = null)
        => ExecuteSession(() => _session.ChangeObjectiveType(nodeId, type, actorId));

    public bool SetObjectiveType(string nodeId, string? type, string? actorId = null)
        => ChangeObjectiveType(nodeId, type, actorId);

    public bool SetObjectiveDescription(string nodeId, string? description)
        => ExecuteSession(() => _session.SetObjectiveDescription(nodeId, description));

    public bool SetObjectiveRequired(string nodeId, int required)
        => ExecuteSession(() => _session.SetObjectiveRequired(nodeId, required));

    public bool ChangeStoryActionType(string nodeId, string? type)
        => ExecuteSession(() => _session.ChangeStoryActionType(nodeId, type));

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
        var result = _session.ApplyAggregateSynchronization(plan, confirmReferencedRemoval);
        var changed = result && _session.UndoCount != oldUndoCount;
        if (changed)
        {
            Refresh(_session.LastValidationIssues);
            PublishGraphChanged();
        }
        else PublishState(_session.LastValidationIssues);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return result;
    }

    /// <summary>Compensates the last edit after a failed outer persistence transaction.</summary>
    public bool RollbackLastEdit()
        => ExecuteSession(_session.RollbackLastEdit);

    public bool Undo() => ExecuteBridge(_commandBridge.Undo);
    public bool Redo() => ExecuteBridge(_commandBridge.Redo);

    private bool ExecuteBridge(Func<bool> command)
    {
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var result = command();
        var issues = _commandBridge.LastValidationIssues;
        if (result)
        {
            Refresh(issues);
            PublishGraphChanged();
        }
        else PublishState(issues);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return result;
    }

    private bool ExecuteSession(Func<bool> command)
    {
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        var result = command();
        var issues = _session.LastValidationIssues;
        if (result)
        {
            Refresh(issues);
            PublishGraphChanged();
        }
        else PublishState(issues);
        NotifyHistoryStateChanged(oldUndo, oldRedo);
        return result;
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

    private void PublishState(IReadOnlyList<ValidationIssue> issues)
    {
        var oldUndo = CanUndo;
        var oldRedo = CanRedo;
        _lastValidationIssues = issues.ToArray();
        OnPropertyChanged(nameof(LastValidationIssues));
        if (oldUndo != CanUndo) OnPropertyChanged(nameof(CanUndo));
        if (oldRedo != CanRedo) OnPropertyChanged(nameof(CanRedo));
    }

    private void OnNodePositionChanged(GraphEditorNodeViewModel node, double x, double y)
    {
        if (!string.IsNullOrWhiteSpace(node.NodeId) && double.IsFinite(x) && double.IsFinite(y))
            _layout[node.NodeId] = new GraphEditorNodePosition(x, y);
    }

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
