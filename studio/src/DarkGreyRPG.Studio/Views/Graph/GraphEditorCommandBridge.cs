using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>
/// The identity carried by a graph-port gesture.  Display labels deliberately
/// do not form part of an endpoint: labels are presentation and may change
/// without changing an existing connection.
/// </summary>
public readonly record struct GraphEditorEndpoint(
    string NodeId,
    string PortId,
    GraphPortDirection Direction,
    GraphInterfaceKind InterfaceKind)
{
    public GraphEditorEndpoint(string nodeId, string portId, bool isInput, GraphInterfaceKind interfaceKind)
        : this(nodeId, portId, isInput ? GraphPortDirection.Input : GraphPortDirection.Output, interfaceKind) { }

    public bool IsInput => Direction == GraphPortDirection.Input;
    public bool IsOutput => Direction == GraphPortDirection.Output;

    public static GraphEditorEndpoint Input(string nodeId, string portId, GraphInterfaceKind interfaceKind)
        => new(nodeId, portId, GraphPortDirection.Input, interfaceKind);

    public static GraphEditorEndpoint Output(string nodeId, string portId, GraphInterfaceKind interfaceKind)
        => new(nodeId, portId, GraphPortDirection.Output, interfaceKind);
}

/// <summary>
/// Shared WPF-host-ready command surface for Story, Session, and Task graphs.
/// It contains no Window, Dispatcher, or visual-tree dependency, which keeps
/// gesture completion deterministic in both WPF hosts and tests.
/// </summary>
public sealed class GraphEditorCommandBridge
{
    private readonly GraphEditSession _session;
    private IReadOnlyList<ValidationIssue> _lastValidationIssues = [];

    public GraphEditorCommandBridge(GraphEditSession session)
        => _session = session ?? throw new ArgumentNullException(nameof(session));

    public GraphDocument Graph => _session.Graph;
    public GraphScope? Scope => _session.Scope;
    public bool CanUndo => _session.CanUndo;
    public bool CanRedo => _session.CanRedo;
    public IReadOnlyList<ValidationIssue> LastValidationIssues => _lastValidationIssues;

    /// <summary>Completes a new-wire or existing-wire drag.</summary>
    /// <param name="first">The endpoint at either end of the gesture.</param>
    /// <param name="second">The other endpoint, or null for a blank drop.</param>
    /// <param name="original">The existing edge for a reconnect gesture.</param>
    public bool CompleteConnectionDrag(GraphEditorEndpoint? first, GraphEditorEndpoint? second,
        GraphConnection? original = null)
    {
        if (!first.HasValue || !second.HasValue)
            return original is null ? ClearForNoOp() : Disconnect(original);

        return original is null
            ? Connect(first.Value, second.Value)
            : Reconnect(original, first.Value, second.Value);
    }

    // Alias reads naturally at call sites that model the gesture as a wire.
    public bool CompleteWireDrag(GraphEditorEndpoint? first, GraphEditorEndpoint? second,
        GraphConnection? original = null)
        => CompleteConnectionDrag(first, second, original);

    public bool Connect(GraphEditorEndpoint first, GraphEditorEndpoint second)
    {
        if (!TryNormalize(first, second, out var candidate)) return false;
        return Execute(() => _session.Connect(candidate));
    }

    /// <summary>Checks a prospective connection without changing the graph or history.</summary>
    public bool CanConnect(GraphEditorEndpoint first, GraphEditorEndpoint second)
    {
        if (!TryNormalize(first, second, out var candidate)) return false;
        return ValidateCandidate(candidate);
    }

    public bool Reconnect(GraphConnection original, GraphEditorEndpoint first, GraphEditorEndpoint second)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!TryNormalize(first, second, out var replacement)) return false;
        return Execute(() => _session.Reconnect(original, replacement));
    }

    // Keep both argument orders available for hosts whose drag state stores the
    // original edge before the two visual endpoints.
    public bool Reconnect(GraphEditorEndpoint first, GraphEditorEndpoint second, GraphConnection original)
        => Reconnect(original, first, second);

    /// <summary>Checks a prospective reconnect without changing the graph or history.</summary>
    public bool CanReconnect(GraphConnection original, GraphEditorEndpoint first, GraphEditorEndpoint second)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (!TryNormalize(first, second, out var replacement)) return false;
        return ValidateCandidate(replacement, original);
    }

    public bool CanReconnect(GraphEditorEndpoint first, GraphEditorEndpoint second, GraphConnection original)
        => CanReconnect(original, first, second);

    public bool Disconnect(GraphConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return Execute(() => _session.Disconnect(connection));
    }

    public bool Disconnect(GraphEditorEndpoint first, GraphEditorEndpoint second)
    {
        if (!TryNormalize(first, second, out var connection)) return false;
        return Disconnect(connection);
    }

    public bool Undo()
    {
        _lastValidationIssues = [];
        return _session.Undo();
    }

    public bool Redo()
    {
        _lastValidationIssues = [];
        return _session.Redo();
    }

    private bool TryNormalize(GraphEditorEndpoint first, GraphEditorEndpoint second,
        out GraphConnection connection)
    {
        connection = null!;
        var issues = new List<ValidationIssue>();
        ValidateEndpoint(first, "from", issues);
        ValidateEndpoint(second, "to", issues);

        if (issues.Count == 0)
        {
            if (string.Equals(first.NodeId, second.NodeId, StringComparison.Ordinal))
            {
                issues.Add(new("graph.editor.connection.nodes.same",
                    "A graph connection must join two different nodes.", "node_id", NodeId: first.NodeId));
            }
            if (first.Direction == second.Direction)
            {
                issues.Add(new("graph.editor.connection.direction.same",
                    "A graph connection requires one input and one output endpoint.", "direction"));
            }
            if (first.InterfaceKind != second.InterfaceKind)
            {
                issues.Add(new("graph.editor.connection.interface_kind.mixed",
                    "Flow and logic endpoints cannot be mixed.", "interface_kind"));
            }
        }

        if (issues.Count != 0)
        {
            _lastValidationIssues = issues.ToArray();
            return false;
        }

        var output = first.IsOutput ? first : second;
        var input = first.IsInput ? first : second;
        connection = new GraphConnection(output.NodeId, output.PortId, input.NodeId, input.PortId,
            output.InterfaceKind);
        _lastValidationIssues = [];
        return true;
    }

    private static void ValidateEndpoint(GraphEditorEndpoint endpoint, string side,
        ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(endpoint.NodeId))
            issues.Add(new($"graph.editor.endpoint.{side}.node_id.required",
                $"The {side} endpoint requires a node ID.", $"{side}_node_id"));
        if (string.IsNullOrWhiteSpace(endpoint.PortId))
            issues.Add(new($"graph.editor.endpoint.{side}.port_id.required",
                $"The {side} endpoint requires a port ID.", $"{side}_port_id"));
        if (endpoint.Direction is not (GraphPortDirection.Input or GraphPortDirection.Output))
            issues.Add(new($"graph.editor.endpoint.{side}.direction.invalid",
                $"The {side} endpoint has an invalid direction.", "direction"));
        if (endpoint.InterfaceKind is not (GraphInterfaceKind.Flow or GraphInterfaceKind.Logic))
            issues.Add(new($"graph.editor.endpoint.{side}.interface_kind.invalid",
                $"The {side} endpoint has an invalid interface kind.", "interface_kind"));
    }

    private bool Execute(Func<bool> command)
    {
        var result = command();
        _lastValidationIssues = _session.LastValidationIssues.ToArray();
        return result;
    }

    private bool ValidateCandidate(GraphConnection candidate, GraphConnection? excludedConnection = null)
    {
        _lastValidationIssues = _session.ValidateCandidate(candidate, excludedConnection).ToArray();
        return _lastValidationIssues.Count == 0;
    }

    private bool ClearForNoOp()
    {
        _lastValidationIssues = [];
        return false;
    }
}
