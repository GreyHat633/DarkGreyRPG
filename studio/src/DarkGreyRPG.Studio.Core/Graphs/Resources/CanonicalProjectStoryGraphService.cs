using System.Collections.ObjectModel;
using System.Text.Json;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>A stable diagnostic in the UI-independent canonical Project Story Graph.</summary>
public sealed record CanonicalProjectStoryGraphDiagnostic(
    string Code,
    string Message,
    string? StoryId = null,
    string? NodeId = null)
{
    public string? SourceStoryId => StoryId;
    public string? StoryNodeId => NodeId;
}

/// <summary>
/// One detached cross-Story relation. Incoming connection identities are
/// retained so a consumer can explain where a transition came from without
/// reopening the source document.
/// </summary>
public sealed record CanonicalProjectStoryGraphTransition
{
    public CanonicalProjectStoryGraphTransition(
        string sourceStoryId,
        string targetStoryId,
        string nodeId,
        IEnumerable<string>? incomingSourceNodeIds = null,
        IEnumerable<string>? incomingBranchOutputs = null)
    {
        SourceStoryId = sourceStoryId;
        TargetStoryId = targetStoryId;
        NodeId = nodeId;
        IncomingSourceNodeIds = ReadOnlySortedDistinct(incomingSourceNodeIds);
        IncomingBranchOutputs = ReadOnlySortedDistinct(incomingBranchOutputs);
    }

    public string SourceStoryId { get; }
    public string TargetStoryId { get; }
    public string NodeId { get; }
    public string EnterStoryNodeId => NodeId;
    public IReadOnlyList<string> IncomingSourceNodeIds { get; }
    public IReadOnlyList<string> SourceNodeIds => IncomingSourceNodeIds;
    public IReadOnlyList<string> IncomingOutputs => IncomingBranchOutputs;
    public IReadOnlyList<string> BranchReasons => IncomingBranchOutputs;
    public IReadOnlyList<string> IncomingBranchOutputs { get; }

    private static IReadOnlyList<string> ReadOnlySortedDistinct(IEnumerable<string>? values)
        => new ReadOnlyCollection<string>((values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
}

/// <summary>One aggregate edge for a source/target Story ID pair.</summary>
public sealed record CanonicalProjectStoryGraphEdge
{
    public CanonicalProjectStoryGraphEdge(
        string sourceStoryId,
        string targetStoryId,
        IEnumerable<CanonicalProjectStoryGraphTransition>? transitions)
    {
        SourceStoryId = sourceStoryId;
        TargetStoryId = targetStoryId;
        Transitions = new ReadOnlyCollection<CanonicalProjectStoryGraphTransition>(
            (transitions ?? [])
                .Where(transition => transition is not null)
                .OrderBy(transition => transition.NodeId, StringComparer.Ordinal)
                .ThenBy(transition => string.Join("\u001f", transition.IncomingSourceNodeIds), StringComparer.Ordinal)
                .ThenBy(transition => string.Join("\u001f", transition.IncomingBranchOutputs), StringComparer.Ordinal)
                .ToArray());
    }

    public string SourceStoryId { get; }
    public string TargetStoryId { get; }
    public IReadOnlyList<CanonicalProjectStoryGraphTransition> Transitions { get; }
    public IReadOnlyList<CanonicalProjectStoryGraphTransition> TransitionDetails => Transitions;
    public int Count => Transitions.Count;
    public int TransitionCount => Count;
    public bool IsSelfLoop => string.Equals(SourceStoryId, TargetStoryId, StringComparison.Ordinal);
    public bool SelfLoop => IsSelfLoop;
    public IReadOnlyList<string> EnterStoryNodeIds =>
        new ReadOnlyCollection<string>(Transitions.Select(transition => transition.NodeId).ToArray());
    public IReadOnlyList<string> NodeIds => EnterStoryNodeIds;
    public IReadOnlyList<string> IncomingSourceNodeIds =>
        new ReadOnlyCollection<string>(Transitions.SelectMany(transition => transition.IncomingSourceNodeIds)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    public IReadOnlyList<string> IncomingBranchOutputs =>
        new ReadOnlyCollection<string>(Transitions.SelectMany(transition => transition.IncomingBranchOutputs)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
}

/// <summary>One Story identity in a detached canonical Project Story Graph snapshot.</summary>
public sealed record CanonicalProjectStoryGraphNode(
    string Id,
    string DisplayName,
    bool HasStoryRoot,
    bool HasMembershipRoot,
    bool IsComplete,
    bool IsValid,
    bool IsIsolated,
    IReadOnlyList<CanonicalProjectStoryGraphDiagnostic> Issues)
{
    public string StoryId => Id;
    public bool HasWarning => Issues.Count > 0;
    public bool IsIncomplete => !IsComplete;
}

/// <summary>Detached, deterministic, read-only Project Story Graph derivation.</summary>
public sealed class CanonicalProjectStoryGraphSnapshot
{
    public CanonicalProjectStoryGraphSnapshot(
        IEnumerable<CanonicalProjectStoryGraphNode>? nodes,
        IEnumerable<CanonicalProjectStoryGraphEdge>? edges,
        IEnumerable<CanonicalProjectStoryGraphDiagnostic>? diagnostics)
    {
        Nodes = new ReadOnlyCollection<CanonicalProjectStoryGraphNode>((nodes ?? [])
            .Where(node => node is not null)
            .OrderBy(node => node.Id, StringComparer.Ordinal).ToArray());
        Edges = new ReadOnlyCollection<CanonicalProjectStoryGraphEdge>((edges ?? [])
            .Where(edge => edge is not null)
            .OrderBy(edge => edge.SourceStoryId, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetStoryId, StringComparer.Ordinal).ToArray());
        Diagnostics = new ReadOnlyCollection<CanonicalProjectStoryGraphDiagnostic>((diagnostics ?? [])
            .Where(issue => issue is not null)
            .OrderBy(issue => issue.StoryId, StringComparer.Ordinal)
            .ThenBy(issue => issue.NodeId, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ThenBy(issue => issue.Message, StringComparer.Ordinal).ToArray());
    }

    public IReadOnlyList<CanonicalProjectStoryGraphNode> Nodes { get; }
    public IReadOnlyList<CanonicalProjectStoryGraphNode> StoryNodes => Nodes;
    public IReadOnlyList<CanonicalProjectStoryGraphEdge> Edges { get; }
    public IReadOnlyList<CanonicalProjectStoryGraphEdge> Relationships => Edges;
    public IReadOnlyList<CanonicalProjectStoryGraphDiagnostic> Diagnostics { get; }
    public bool IsEmpty => Nodes.Count == 0;
}

/// <summary>
/// Derives the read-only canonical Project Story Graph from the union exposed
/// by <see cref="CanonicalStoryDiscoveryService"/>. This service never calls
/// store initialization or any repository write operation.
/// </summary>
public sealed class CanonicalProjectStoryGraphService
{
    private const string IsolatedStoryCode = "project_graph.story.isolated";
    private const string CycleStoryCode = "project_graph.story.cycle";

    private readonly CanonicalProjectGraphStore _store;
    private readonly CanonicalStoryDiscoveryService _discovery;

    public CanonicalProjectStoryGraphService(CanonicalProjectGraphStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _discovery = new CanonicalStoryDiscoveryService(store);
    }

    public CanonicalProjectGraphStore Store => _store;

    public CanonicalProjectStoryGraphSnapshot Derive()
    {
        var discovery = _discovery.Discover();
        var diagnostics = discovery.Issues
            .Select(issue => new CanonicalProjectStoryGraphDiagnostic(issue.Code, issue.Message, issue.StoryId))
            .ToList();
        var items = discovery.Items;
        var storyIds = items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var transitions = new List<CanonicalProjectStoryGraphTransition>();
        var invalidStoryIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (item.Story is null)
            {
                invalidStoryIds.Add(item.Id);
                continue;
            }

        }

        var edges = transitions
            .GroupBy(transition => (transition.SourceStoryId, transition.TargetStoryId))
            .OrderBy(group => group.Key.SourceStoryId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.TargetStoryId, StringComparer.Ordinal)
            .Select(group => new CanonicalProjectStoryGraphEdge(
                group.Key.SourceStoryId, group.Key.TargetStoryId, group))
            .ToArray();

        var connected = edges.SelectMany(edge => new[] { edge.SourceStoryId, edge.TargetStoryId })
            .ToHashSet(StringComparer.Ordinal);
        foreach (var item in items.Where(item => !connected.Contains(item.Id)))
        {
            diagnostics.Add(new(IsolatedStoryCode,
                $"Story '{item.Id}' is not connected to any EnterStory transition.", item.Id));
        }

        var adjacency = storyIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in edges) adjacency[edge.SourceStoryId].Add(edge.TargetStoryId);
        foreach (var targets in adjacency.Values)
        {
            targets.Sort(StringComparer.Ordinal);
            for (var index = targets.Count - 1; index > 0; index--)
                if (string.Equals(targets[index], targets[index - 1], StringComparison.Ordinal)) targets.RemoveAt(index);
        }
        foreach (var component in FindStronglyConnectedComponents(storyIds, adjacency)
                     .Where(component => component.Count > 1 || adjacency[component[0]].Contains(component[0], StringComparer.Ordinal)))
        {
            var members = string.Join("、", component);
            foreach (var storyId in component)
                diagnostics.Add(new(CycleStoryCode,
                    $"Story '{storyId}' is in a cycle ({members}).", storyId));
        }

        var issueByStory = diagnostics.GroupBy(issue => issue.StoryId ?? string.Empty, StringComparer.Ordinal);
        var issueLookup = issueByStory.ToDictionary(group => group.Key, group =>
            (IReadOnlyList<CanonicalProjectStoryGraphDiagnostic>)new ReadOnlyCollection<CanonicalProjectStoryGraphDiagnostic>(group
                .OrderBy(issue => issue.NodeId, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToArray()),
            StringComparer.Ordinal);
        var nodesSnapshot = items.Select(item => new CanonicalProjectStoryGraphNode(
            item.Id,
            item.DisplayName,
            item.HasStoryRoot,
            item.HasMembershipRoot,
            item.IsComplete,
            item.IsValid && !invalidStoryIds.Contains(item.Id),
            !connected.Contains(item.Id),
            issueLookup.TryGetValue(item.Id, out var issues) ? issues : Array.AsReadOnly(Array.Empty<CanonicalProjectStoryGraphDiagnostic>())))
            .ToArray();

        return new CanonicalProjectStoryGraphSnapshot(nodesSnapshot, edges, diagnostics);
    }

    public CanonicalProjectStoryGraphSnapshot Build() => Derive();
    public CanonicalProjectStoryGraphSnapshot Inspect() => Derive();
    public CanonicalProjectStoryGraphSnapshot Discover() => Derive();
    public CanonicalProjectStoryGraphSnapshot GetSnapshot() => Derive();
    public CanonicalProjectStoryGraphSnapshot DeriveSnapshot() => Derive();
    public CanonicalProjectStoryGraphSnapshot CreateSnapshot() => Derive();
    public CanonicalProjectStoryGraphSnapshot Snapshot() => Derive();

    private static IReadOnlyList<IReadOnlyList<string>> FindStronglyConnectedComponents(
        IEnumerable<string> storyIds,
        IReadOnlyDictionary<string, List<string>> adjacency)
    {
        var index = 0;
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinks = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<IReadOnlyList<string>>();

        foreach (var id in storyIds.Order(StringComparer.Ordinal))
            if (!indices.ContainsKey(id)) StrongConnect(id);
        return components;

        void StrongConnect(string id)
        {
            indices[id] = index;
            lowLinks[id] = index++;
            stack.Push(id);
            onStack.Add(id);
            foreach (var target in adjacency[id].Order(StringComparer.Ordinal))
            {
                if (!indices.ContainsKey(target))
                {
                    StrongConnect(target);
                    lowLinks[id] = Math.Min(lowLinks[id], lowLinks[target]);
                }
                else if (onStack.Contains(target))
                {
                    lowLinks[id] = Math.Min(lowLinks[id], indices[target]);
                }
            }
            if (lowLinks[id] != indices[id]) return;
            var component = new List<string>();
            string member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            } while (!string.Equals(member, id, StringComparison.Ordinal));
            component.Sort(StringComparer.Ordinal);
            components.Add(component);
        }
    }
}
