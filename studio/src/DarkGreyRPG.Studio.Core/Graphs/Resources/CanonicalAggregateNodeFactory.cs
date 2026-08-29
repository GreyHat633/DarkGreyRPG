using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>
/// Creates a detached Story Flow aggregate node from a canonical Session or
/// Task resource.  The source resource and target graph are only inspected;
/// neither is ever changed by this factory.
/// </summary>
public static class CanonicalAggregateNodeFactory
{
    public static GraphNodeAuthoringResult Create(
        GraphResourceEnvelope resource,
        string? placementNodeId)
        => CreateCore(null, resource, placementNodeId);

    public static GraphNodeAuthoringResult Create(
        GraphDocument targetGraph,
        GraphResourceEnvelope resource,
        string? placementNodeId)
    {
        ArgumentNullException.ThrowIfNull(targetGraph);
        return CreateCore(targetGraph, resource, placementNodeId);
    }

    public static GraphNodeAuthoringResult Create(
        GraphResourceEnvelope resource,
        GraphDocument targetGraph,
        string? placementNodeId)
        => Create(targetGraph, resource, placementNodeId);

    public static GraphNodeAuthoringResult CreateNode(
        GraphResourceEnvelope resource,
        string? placementNodeId)
        => Create(resource, placementNodeId);

    public static bool TryCreate(
        GraphResourceEnvelope resource,
        string? placementNodeId,
        out GraphNode? candidate,
        out IReadOnlyList<ValidationIssue> issues)
    {
        var result = Create(resource, placementNodeId);
        candidate = result.Candidate;
        issues = result.Issues;
        return result.IsSuccess;
    }

    public static bool TryCreate(
        GraphDocument targetGraph,
        GraphResourceEnvelope resource,
        string? placementNodeId,
        out GraphNode? candidate,
        out IReadOnlyList<ValidationIssue> issues)
    {
        var result = Create(targetGraph, resource, placementNodeId);
        candidate = result.Candidate;
        issues = result.Issues;
        return result.IsSuccess;
    }

    private static GraphNodeAuthoringResult CreateCore(
        GraphDocument? targetGraph,
        GraphResourceEnvelope resource,
        string? placementNodeId)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (string.IsNullOrWhiteSpace(placementNodeId))
            return FailureIssue(new("graph.aggregate.node.id.required",
                "Aggregate placement node ID is required.", "id"));

        if (resource.SchemaVersion != GraphResourceEnvelope.CurrentSchemaVersion)
            return FailureIssue(new("graph.aggregate.resource.schema_version.unsupported",
                $"Unsupported canonical resource schema_version {resource.SchemaVersion}.", "schema_version"));

        if (resource.ResourceKind is not (GraphResourceKind.Session or GraphResourceKind.Task))
            return FailureIssue(new("graph.aggregate.resource.kind.unsupported",
                "Only Session and Task resources can become Story Flow aggregate nodes.", "resource_kind"));

        if (string.IsNullOrWhiteSpace(resource.Id))
            return FailureIssue(new("graph.aggregate.resource.id.required",
                "Aggregate resource ID is required.", "resource_id"));
        if (string.IsNullOrWhiteSpace(resource.DisplayName))
            return FailureIssue(new("graph.aggregate.resource.display_name.required",
                "Aggregate resource display name is required.", "display_name"));

        GraphDocument? graph;
        try
        {
            graph = resource.Graph;
        }
        catch (GraphResourceEnvelopeException exception)
        {
            return FailureIssue(new(exception.Code, exception.Message, "graph"));
        }

        if (graph is null)
            return FailureIssue(new("graph.aggregate.resource.graph.required",
                "Aggregate resource graph is required.", "graph"));

        var nodeId = placementNodeId!;
        if (targetGraph is not null && (targetGraph.Nodes ?? []).Any(node => node is not null
            && string.Equals(node.Id, nodeId, StringComparison.Ordinal)))
        {
            return FailureIssue(new("graph.aggregate.node.id.duplicate",
                $"Graph node ID '{nodeId}' is already used.", "id", NodeId: nodeId));
        }

        var boundaryIssues = new List<ValidationIssue>();
        var nodes = (graph.Nodes ?? []).Where(node => node is not null).ToArray();
        var flowBoundaries = new List<GraphBoundary>();
        var logicBoundaries = new List<GraphBoundary>();
        var logicInputBoundaries = new List<GraphBoundary>();

        if (resource.ResourceKind == GraphResourceKind.Session)
        {
            foreach (var node in nodes.Where(node => node.Type == "end"))
            {
                ValidateBoundaryNode(node, GraphScope.Session, boundaryIssues);
                if (TryReadPublicBoundary(node, GraphInterfaceKind.Flow, boundaryIssues, out var boundary))
                    flowBoundaries.Add(boundary with { Order = flowBoundaries.Count });
            }
        }
        else
        {
            var settles = nodes.Where(node => node.Type == "settle").ToArray();
            if (settles.Length == 0)
            {
                boundaryIssues.Add(new("graph.aggregate.task.settle.required",
                    "Task resource must contain exactly one settle boundary.", "graph.nodes"));
            }
            else if (settles.Length > 1)
            {
                boundaryIssues.Add(new("graph.aggregate.task.settle.duplicate",
                    "Task resource must contain exactly one settle boundary.", "graph.nodes"));
            }
            else
            {
                var settle = settles[0];
                boundaryIssues.AddRange(GraphNodeShapeValidator.Validate(settle, GraphScope.Task));
                var settlePorts = (settle.Ports ?? []).Where(port => port is not null).ToArray();
                foreach (var port in settlePorts)
                {
                    if (port.IsInput && port.InterfaceKind == GraphInterfaceKind.Logic)
                        flowBoundaries.Add(new GraphBoundary(port.Id, port.DisplayName, port.Order,
                            GraphInterfaceKind.Flow, NodeId: settle.Id));
                }
            }
        }

        foreach (var node in nodes.Where(node => node.Type == "logic_output"))
        {
            var scope = resource.ResourceKind == GraphResourceKind.Session
                ? GraphScope.Session : GraphScope.Task;
            ValidateBoundaryNode(node, scope, boundaryIssues);
            if (TryReadPublicBoundary(node, GraphInterfaceKind.Logic, boundaryIssues, out var boundary))
                logicBoundaries.Add(boundary with { Order = logicBoundaries.Count });
        }

        foreach (var node in nodes.Where(node => node.Type == "logic_input"))
        {
            var scope = resource.ResourceKind == GraphResourceKind.Session
                ? GraphScope.Session : GraphScope.Task;
            ValidateBoundaryNode(node, scope, boundaryIssues);
            if (TryReadPublicBoundary(node, GraphInterfaceKind.Logic, boundaryIssues, out var boundary))
                logicInputBoundaries.Add(boundary with { IsInput = true, Order = logicInputBoundaries.Count });
        }

        if (boundaryIssues.Count != 0)
            return FailureIssues(boundaryIssues);

        IReadOnlyList<GraphPort> projectedPorts;
        try
        {
            projectedPorts = resource.ResourceKind == GraphResourceKind.Session
                ? GraphAggregatePortProjection.ProjectSession(flowBoundaries, logicBoundaries, logicInputBoundaries)
                : GraphAggregatePortProjection.ProjectTask(flowBoundaries, logicBoundaries, logicInputBoundaries);
        }
        catch (AggregatePortProjectionException exception)
        {
            return FailureIssues(exception.Issues);
        }

        var type = resource.ResourceKind == GraphResourceKind.Session ? "session" : "task";
        var candidate = GraphNodeFactory.Create(GraphScope.StoryFlow, type, nodeId, resource.DisplayName);
        candidate.Properties["resource_id"] = JsonSerializer.SerializeToElement(resource.Id);
        // The registry owns the aggregate's fixed Flow input. Projection repeats
        // that input before the child-owned named Logic inputs and public outputs.
        candidate.Ports.AddRange(projectedPorts.Skip(candidate.Ports.Count).Select(ClonePort));

        var shapeIssues = GraphNodeShapeValidator.Validate(candidate, GraphScope.StoryFlow);
        return shapeIssues.Count == 0 ? GraphNodeAuthoringResult.FromCandidate(candidate) : FailureIssues(shapeIssues);
    }

    private static void ValidateBoundaryNode(
        GraphNode node,
        GraphScope scope,
        ICollection<ValidationIssue> issues)
    {
        foreach (var issue in GraphNodeShapeValidator.Validate(node, scope))
            issues.Add(issue);
    }

    private static bool TryReadPublicBoundary(
        GraphNode node,
        GraphInterfaceKind kind,
        ICollection<ValidationIssue> issues,
        out GraphBoundary boundary)
    {
        boundary = default!;
        var hasId = TryReadRequiredString(node, "port_id", issues, out var portId);
        var hasName = TryReadRequiredString(node, "display_name", issues, out var displayName);
        if (!hasId || !hasName)
            return false;

        boundary = new GraphBoundary(portId!, displayName!, 0, kind, NodeId: node.Id);
        return true;
    }

    private static bool TryReadRequiredString(
        GraphNode node,
        string property,
        ICollection<ValidationIssue> issues,
        out string? value)
    {
        value = null;
        if (!(node.Properties ?? []).TryGetValue(property, out var json))
        {
            issues.Add(new("graph.aggregate.port.property.required",
                $"Public boundary property '{property}' is required.", $"properties.{property}", NodeId: node.Id));
            return false;
        }
        if (json.ValueKind != JsonValueKind.String)
        {
            issues.Add(new("graph.aggregate.port.property.kind",
                $"Public boundary property '{property}' must be a string.", $"properties.{property}", NodeId: node.Id));
            return false;
        }

        value = json.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new(property == "port_id"
                    ? "graph.aggregate.port.id.required"
                    : "graph.aggregate.port.display_name.required",
                $"Aggregate boundary {property} is required.", $"properties.{property}", NodeId: node.Id));
            return false;
        }
        return true;
    }

    private static GraphPort ClonePort(GraphPort port)
        => new(port.Id, port.DisplayName, port.IsInput, port.InterfaceKind, port.Order);

    private static GraphNodeAuthoringResult FailureIssue(ValidationIssue issue)
        => GraphNodeAuthoringResult.FromIssues([issue]);

    private static GraphNodeAuthoringResult FailureIssues(IEnumerable<ValidationIssue> issues)
        => GraphNodeAuthoringResult.FromIssues(issues);
}
