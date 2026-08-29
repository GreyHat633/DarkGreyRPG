using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>A detached replacement and the references which would be removed.</summary>
public sealed class CanonicalAggregateSynchronizationCandidate
{
    private readonly GraphNode _analysisCurrentNode;
    private readonly GraphNode _analysisReplacementNode;
    private readonly IReadOnlyList<GraphConnection> _analysisObsoleteReferences;

    internal CanonicalAggregateSynchronizationCandidate(GraphNode current, GraphNode replacement,
        IReadOnlyList<GraphConnection> obsoleteReferences)
    {
        _analysisCurrentNode = CloneNode(current);
        _analysisReplacementNode = CloneNode(replacement);
        _analysisObsoleteReferences = obsoleteReferences.Select(CloneConnection).ToArray();
        CurrentNode = CloneNode(current);
        ReplacementNode = CloneNode(replacement);
        ObsoleteReferences = obsoleteReferences.Select(CloneConnection).ToArray();
    }

    public string PlacementNodeId => ReplacementNode.Id;
    public string NodeId => PlacementNodeId;
    public GraphNode CurrentNode { get; }
    public GraphNode ExistingNode => CurrentNode;
    public GraphNode ReplacementNode { get; }
    public GraphNode Candidate => ReplacementNode;
    public IReadOnlyList<GraphConnection> ObsoleteReferences { get; }
    public IReadOnlyList<GraphConnection> References => ObsoleteReferences;
    public IReadOnlyList<GraphConnection> ObsoleteConnections => ObsoleteReferences;
    public bool IsChanged => !Equivalent(_analysisCurrentNode, _analysisReplacementNode);

    internal GraphNode AnalysisCurrentNode => _analysisCurrentNode;
    internal GraphNode AnalysisReplacementNode => _analysisReplacementNode;
    internal IReadOnlyList<GraphConnection> AnalysisObsoleteReferences => _analysisObsoleteReferences;

    internal static bool Equivalent(GraphNode left, GraphNode right)
    {
        if (!string.Equals(left.Id, right.Id, StringComparison.Ordinal)
            || !string.Equals(left.Type, right.Type, StringComparison.Ordinal)
            || !string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)) return false;
        var lp = (left.Ports ?? []).ToArray();
        var rp = (right.Ports ?? []).ToArray();
        if (lp.Length != rp.Length) return false;
        for (var i = 0; i < lp.Length; i++)
            if ((lp[i] is null) != (rp[i] is null)
                || lp[i] is not null && rp[i] is not null && (!string.Equals(lp[i].Id, rp[i].Id, StringComparison.Ordinal)
                || !string.Equals(lp[i].DisplayName, rp[i].DisplayName, StringComparison.Ordinal)
                || lp[i].IsInput != rp[i].IsInput || lp[i].InterfaceKind != rp[i].InterfaceKind
                || lp[i].Order != rp[i].Order)) return false;
        var lprops = left.Properties ?? [];
        var rprops = right.Properties ?? [];
        if (lprops.Count != rprops.Count || lprops.Any(item => !rprops.TryGetValue(item.Key, out var value)
            || !JsonElement.DeepEquals(item.Value, value))) return false;
        return true;
    }

    internal static GraphNode CloneNode(GraphNode node)
        => new(node.Id, node.Type, node.DisplayName,
            (node.Ports ?? []).Select(p => p is null ? null! : ClonePort(p)),
            (node.Properties ?? []).ToDictionary(p => p.Key, p => p.Value.Clone(), StringComparer.Ordinal));

    internal static GraphPort ClonePort(GraphPort port)
        => new(port.Id, port.DisplayName, port.IsInput, port.InterfaceKind, port.Order);

    internal static GraphConnection CloneConnection(GraphConnection connection)
        => new(connection.FromNodeId, connection.FromPortId, connection.ToNodeId, connection.ToPortId, connection.InterfaceKind);
}

/// <summary>Read-only, detached synchronization plan for all matching placements.</summary>
public sealed class CanonicalAggregateSynchronizationPlan
{
    private readonly IReadOnlyList<GraphConnection> _analysisObsoleteReferences;

    internal CanonicalAggregateSynchronizationPlan(GraphResourceEnvelope resource,
        IEnumerable<CanonicalAggregateSynchronizationCandidate> placements,
        IEnumerable<ValidationIssue> issues)
    {
        ResourceKind = resource.ResourceKind;
        ResourceId = resource.Id;
        Placements = placements.ToArray();
        Issues = issues.ToArray();
        _analysisObsoleteReferences = Placements.SelectMany(p => p.AnalysisObsoleteReferences).Select(
            CanonicalAggregateSynchronizationCandidate.CloneConnection).ToArray();
        ObsoleteReferences = _analysisObsoleteReferences.Select(
            CanonicalAggregateSynchronizationCandidate.CloneConnection).ToArray();
    }

    public GraphResourceKind ResourceKind { get; }
    public string ResourceId { get; }
    public IReadOnlyList<CanonicalAggregateSynchronizationCandidate> Placements { get; }
    public IReadOnlyList<CanonicalAggregateSynchronizationCandidate> Candidates => Placements;
    public IReadOnlyList<CanonicalAggregateSynchronizationCandidate> ReplacementCandidates => Placements;
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public IReadOnlyList<ValidationIssue> ValidationIssues => Issues;
    public IReadOnlyList<GraphConnection> ObsoleteReferences { get; }
    public IReadOnlyList<GraphConnection> References => ObsoleteReferences;
    public bool IsSuccess => Issues.Count == 0;
    public bool Succeeded => IsSuccess;
    public bool RequiresConfirmation => _analysisObsoleteReferences.Count != 0;
    public bool HasChanges => Placements.Any(p => p.IsChanged);
    public bool IsNoOp => IsSuccess && !HasChanges;

    internal IReadOnlyList<GraphConnection> AnalysisObsoleteReferences => _analysisObsoleteReferences;
}

/// <summary>
/// Computes detached aggregate replacements from the canonical factory. It
/// matches only the expected aggregate type and an ordinal-exact resource_id.
/// </summary>
public sealed class CanonicalAggregateSynchronizationService
{
    public CanonicalAggregateSynchronizationPlan Analyze(GraphDocument storyGraph, GraphResourceEnvelope resource)
    {
        ArgumentNullException.ThrowIfNull(storyGraph);
        ArgumentNullException.ThrowIfNull(resource);

        var issues = new List<ValidationIssue>();
        // Let the authoritative factory own all boundary parsing and projection validation.
        var probe = CanonicalAggregateNodeFactory.Create(resource, "__aggregate_sync_probe__");
        if (!probe.IsSuccess)
            return new(resource, [], probe.Issues);

        var expectedType = resource.ResourceKind == GraphResourceKind.Session ? "session" : "task";
        var placements = new List<CanonicalAggregateSynchronizationCandidate>();
        // A placement bound to the requested resource but authored with the
        // other aggregate kind is unsafe to silently ignore: fail closed while
        // still never treating it as a replacement candidate.
        foreach (var node in (storyGraph.Nodes ?? []).Where(node => node is not null
                     && node.Type is "session" or "task"
                     && !string.Equals(node.Type, expectedType, StringComparison.Ordinal)))
        {
            if (TryReadResourceId(node, out var wrongResourceId, out _)
                && string.Equals(wrongResourceId, resource.Id, StringComparison.Ordinal))
                issues.Add(new("graph.aggregate.sync.placement.kind.invalid",
                    $"Placement '{node.Id}' has aggregate kind '{node.Type}', expected '{expectedType}'.",
                    "type", NodeId: node.Id));
        }
        foreach (var node in (storyGraph.Nodes ?? []).Where(node => node is not null
                     && string.Equals(node.Type, expectedType, StringComparison.Ordinal)))
        {
            if (!TryReadResourceId(node, out var resourceId, out var issue))
            {
                issues.Add(issue! with { NodeId = node.Id });
                continue;
            }
            if (!string.Equals(resourceId, resource.Id, StringComparison.Ordinal)) continue;

            var sameId = (storyGraph.Nodes ?? []).Count(candidate => candidate is not null
                && string.Equals(candidate.Id, node.Id, StringComparison.Ordinal));
            if (sameId != 1)
            {
                if (!issues.Any(item => item.Code == "graph.aggregate.sync.placement.ambiguous"
                    && string.Equals(item.NodeId, node.Id, StringComparison.Ordinal)))
                    issues.Add(new("graph.aggregate.sync.placement.ambiguous",
                        $"Aggregate placement node ID '{node.Id}' is ambiguous.", "id", NodeId: node.Id));
                continue;
            }

            var duplicatePorts = (node.Ports ?? []).Where(port => port is not null)
                .GroupBy(port => port.Id, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();
            if (duplicatePorts.Length != 0)
            {
                issues.Add(new("graph.aggregate.sync.placement.port.ambiguous",
                    $"Aggregate placement '{node.Id}' contains ambiguous port ID(s): {string.Join(", ", duplicatePorts)}.",
                    "ports", NodeId: node.Id));
                continue;
            }

            var result = CanonicalAggregateNodeFactory.Create(resource, node.Id);
            if (!result.IsSuccess)
            {
                issues.AddRange(result.Issues.Select(item => item with { NodeId = item.NodeId ?? node.Id }));
                continue;
            }
            var replacement = result.Candidate!;
            var obsoleteIds = (node.Ports ?? []).Where(port => port is not null
                && !(replacement.Ports ?? []).Any(candidate => candidate is not null
                    && string.Equals(candidate.Id, port.Id, StringComparison.Ordinal)
                    && candidate.IsInput == port.IsInput
                    && candidate.InterfaceKind == port.InterfaceKind))
                .Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
            var references = (storyGraph.Connections ?? []).Where(connection => connection is not null
                && ((string.Equals(connection.FromNodeId, node.Id, StringComparison.Ordinal)
                     && obsoleteIds.Contains(connection.FromPortId))
                    || (string.Equals(connection.ToNodeId, node.Id, StringComparison.Ordinal)
                     && obsoleteIds.Contains(connection.ToPortId))))
                .Select(CanonicalAggregateSynchronizationCandidate.CloneConnection).ToArray();
            placements.Add(new(node, replacement, references));
        }

        return new(resource, placements, issues);
    }

    public CanonicalAggregateSynchronizationPlan Analyze(GraphResourceEnvelope resource, GraphDocument storyGraph)
        => Analyze(storyGraph, resource);

    public CanonicalAggregateSynchronizationPlan Plan(GraphDocument storyGraph, GraphResourceEnvelope resource)
        => Analyze(storyGraph, resource);

    public CanonicalAggregateSynchronizationPlan CreatePlan(GraphDocument storyGraph, GraphResourceEnvelope resource)
        => Analyze(storyGraph, resource);

    public CanonicalAggregateSynchronizationPlan AnalyzeAggregate(GraphDocument storyGraph, GraphResourceEnvelope resource)
        => Analyze(storyGraph, resource);

    public CanonicalAggregateSynchronizationPlan BuildPlan(GraphDocument storyGraph, GraphResourceEnvelope resource)
        => Analyze(storyGraph, resource);

    private static bool TryReadResourceId(GraphNode node, out string? value, out ValidationIssue? issue)
    {
        value = null;
        issue = null;
        if (!(node.Properties ?? []).TryGetValue("resource_id", out var json))
        {
            issue = new("graph.aggregate.sync.binding.resource_id.required",
                "Matching aggregate placement requires a resource_id string.", "properties.resource_id");
            return false;
        }
        if (json.ValueKind != JsonValueKind.String)
        {
            issue = new("graph.aggregate.sync.binding.resource_id.kind",
                "Matching aggregate placement resource_id must be a string.", "properties.resource_id");
            return false;
        }
        value = json.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            issue = new("graph.aggregate.sync.binding.resource_id.required",
                "Matching aggregate placement requires a non-blank resource_id.", "properties.resource_id");
            return false;
        }
        return true;
    }
}
