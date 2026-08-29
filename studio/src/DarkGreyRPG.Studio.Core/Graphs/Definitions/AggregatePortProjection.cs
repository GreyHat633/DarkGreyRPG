using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>A stable public boundary declaration used to build an aggregate node.</summary>
public sealed record GraphBoundary(
    string PortId,
    string DisplayName,
    int Order,
    GraphInterfaceKind Kind,
    bool IsInput = false,
    string? NodeId = null)
{
    public GraphBoundary(GraphPort port, string? nodeId = null)
        : this(port.Id, port.DisplayName, port.Order, port.InterfaceKind, port.IsInput, nodeId) { }

    public string Id => PortId;
    public GraphInterfaceKind InterfaceKind => Kind;
    public bool IsOutput => !IsInput;
}

/// <summary>Result-independent deterministic projection of Session and Task boundaries.</summary>
public static class GraphAggregatePortProjection
{
    public const string FlowInputId = "flow_in";
    public const string LogicInputId = "logic_in";

    public static IReadOnlyList<GraphPort> ProjectSession(
        IEnumerable<GraphBoundary>? endBoundaries,
        IEnumerable<GraphBoundary>? logicOutputBoundaries,
        bool includeLogicInput = false)
    {
        var flow = Materialize(endBoundaries);
        var logic = Materialize(logicOutputBoundaries);
        var issues = ValidateBoundaries(flow, logic, GraphScope.Session);
        ThrowIfInvalid(issues);

        var ports = new List<GraphPort> { new(FlowInputId, "Flow In", true, GraphInterfaceKind.Flow, 0) };
        if (includeLogicInput)
            ports.Add(new GraphPort(LogicInputId, "Logic In", true, GraphInterfaceKind.Logic, 1));
        ports.AddRange(ToOutputPorts(flow));
        ports.AddRange(ToOutputPorts(logic));
        return ports;
    }

    public static IReadOnlyList<GraphPort> ProjectTask(
        IEnumerable<GraphBoundary>? settlementBoundaries,
        IEnumerable<GraphBoundary>? logicOutputBoundaries)
    {
        var flow = Materialize(settlementBoundaries);
        var logic = Materialize(logicOutputBoundaries);
        var issues = ValidateBoundaries(flow, logic, GraphScope.Task);
        ThrowIfInvalid(issues);

        var ports = new List<GraphPort> { new(FlowInputId, "Flow In", true, GraphInterfaceKind.Flow, 0) };
        ports.AddRange(ToOutputPorts(flow));
        ports.AddRange(ToOutputPorts(logic));
        return ports;
    }

    public static IReadOnlyList<GraphPort> ProjectSessionPorts(
        IEnumerable<GraphBoundary>? endBoundaries,
        IEnumerable<GraphBoundary>? logicOutputBoundaries,
        bool includeLogicInput = false)
        => ProjectSession(endBoundaries, logicOutputBoundaries, includeLogicInput);

    public static IReadOnlyList<GraphPort> ProjectTaskPorts(
        IEnumerable<GraphBoundary>? settlementBoundaries,
        IEnumerable<GraphBoundary>? logicOutputBoundaries)
        => ProjectTask(settlementBoundaries, logicOutputBoundaries);

    public static IReadOnlyList<ValidationIssue> ValidateSession(
        IEnumerable<GraphBoundary>? endBoundaries,
        IEnumerable<GraphBoundary>? logicOutputBoundaries,
        bool includeLogicInput = false)
    {
        var flow = Materialize(endBoundaries);
        var logic = Materialize(logicOutputBoundaries);
        return ValidateBoundaries(flow, logic, GraphScope.Session);
    }

    public static IReadOnlyList<ValidationIssue> ValidateTask(
        IEnumerable<GraphBoundary>? settlementBoundaries,
        IEnumerable<GraphBoundary>? logicOutputBoundaries)
        => ValidateBoundaries(Materialize(settlementBoundaries), Materialize(logicOutputBoundaries), GraphScope.Task);

    private static List<GraphBoundary> Materialize(IEnumerable<GraphBoundary>? boundaries)
        => boundaries?.Where(boundary => boundary is not null).ToList() ?? [];

    private static List<ValidationIssue> ValidateBoundaries(
        IReadOnlyList<GraphBoundary> flow,
        IReadOnlyList<GraphBoundary> logic,
        GraphScope scope)
    {
        var issues = new List<ValidationIssue>();
        foreach (var boundary in flow)
        {
            if (string.IsNullOrWhiteSpace(boundary.PortId))
                issues.Add(Issue("graph.aggregate.port.id.required", "Aggregate boundary port ID is required."));
            if (string.IsNullOrWhiteSpace(boundary.DisplayName))
                issues.Add(Issue("graph.aggregate.port.display_name.required", "Aggregate boundary display name is required."));
            if (boundary.Kind != GraphInterfaceKind.Flow)
                issues.Add(Issue("graph.aggregate.boundary.kind.invalid", $"{scope} flow boundary '{boundary.PortId}' must have kind flow."));
            if (boundary.IsInput)
                issues.Add(Issue("graph.aggregate.boundary.direction.invalid", $"Boundary '{boundary.PortId}' must be an output."));
        }
        foreach (var boundary in logic)
        {
            if (string.IsNullOrWhiteSpace(boundary.PortId))
                issues.Add(Issue("graph.aggregate.port.id.required", "Aggregate boundary port ID is required."));
            if (string.IsNullOrWhiteSpace(boundary.DisplayName))
                issues.Add(Issue("graph.aggregate.port.display_name.required", "Aggregate boundary display name is required."));
            if (boundary.Kind != GraphInterfaceKind.Logic)
                issues.Add(Issue("graph.aggregate.boundary.kind.invalid", $"{scope} logic boundary '{boundary.PortId}' must have kind logic."));
            if (boundary.IsInput)
                issues.Add(Issue("graph.aggregate.boundary.direction.invalid", $"Boundary '{boundary.PortId}' must be an output."));
        }

        var all = flow.Concat(logic).ToArray();
        foreach (var group in all.Where(item => !string.IsNullOrWhiteSpace(item.PortId)).GroupBy(item => item.PortId, StringComparer.Ordinal))
            if (group.Count() > 1)
                issues.Add(Issue("graph.aggregate.port.id.duplicate", $"Aggregate port ID '{group.Key}' is duplicated."));
        foreach (var group in all.Where(item => !string.IsNullOrWhiteSpace(item.DisplayName)).GroupBy(item => item.DisplayName, StringComparer.Ordinal))
            if (group.Count() > 1)
                issues.Add(Issue("graph.aggregate.port.display_name.duplicate", $"Aggregate display name '{group.Key}' is duplicated across flow and logic outputs."));
        foreach (var boundary in all.Where(item => string.Equals(item.PortId, FlowInputId, StringComparison.Ordinal)
                                                   || string.Equals(item.PortId, LogicInputId, StringComparison.Ordinal)))
            issues.Add(Issue("graph.aggregate.port.reserved_id", $"Boundary port ID '{boundary.PortId}' is reserved."));
        return issues;
    }

    private static IEnumerable<GraphPort> ToOutputPorts(IEnumerable<GraphBoundary> boundaries)
        => boundaries.OrderBy(boundary => boundary.Order).ThenBy(boundary => boundary.PortId, StringComparer.Ordinal)
            .Select(boundary => new GraphPort(boundary.PortId, boundary.DisplayName, false, boundary.Kind, boundary.Order));

    private static ValidationIssue Issue(string code, string message)
        => new(code, message, "ports");

    private static void ThrowIfInvalid(IReadOnlyList<ValidationIssue> issues)
    {
        if (issues.Count != 0)
            throw new AggregatePortProjectionException(issues);
    }
}

/// <summary>Validation exception retaining all projection diagnostics.</summary>
public sealed class AggregatePortProjectionException : ArgumentException
{
    public AggregatePortProjectionException(IReadOnlyList<ValidationIssue> issues)
        : base(string.Join(" ", issues.Select(issue => issue.Message))) => Issues = issues;

    public IReadOnlyList<ValidationIssue> Issues { get; }
}

/// <summary>Compatibility facade for the shorter projector name.</summary>
public static class AggregatePortProjector
{
    public static IReadOnlyList<GraphPort> ProjectSession(IEnumerable<GraphBoundary>? end, IEnumerable<GraphBoundary>? logic, bool includeLogicInput = false)
        => GraphAggregatePortProjection.ProjectSession(end, logic, includeLogicInput);
    public static IReadOnlyList<GraphPort> ProjectTask(IEnumerable<GraphBoundary>? settle, IEnumerable<GraphBoundary>? logic)
        => GraphAggregatePortProjection.ProjectTask(settle, logic);
}
