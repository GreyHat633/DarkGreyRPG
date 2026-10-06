using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class GraphEditorHostViewModel
{
    private List<GraphCommentFrame> _frames = [];
    public IReadOnlyList<GraphCommentFrame> Frames => _frames;
    public IReadOnlyList<GraphCommentFrame> FrameSnapshot()
    {
        var nodeIds = Nodes.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        return _frames.Select(frame => frame with
        {
            Members = frame.Members.Where(nodeIds.Contains).ToArray(),
            Groups = frame.Groups.ToArray()
        }).ToArray();
    }

    public void RestoreFrames(IEnumerable<GraphCommentFrame> frames)
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        _frames = frames.Where(frame => frame.IsValid).DistinctBy(frame => frame.Id).Select(frame => frame with
        {
            Members = frame.Members.Where(id => Nodes.Any(node => node.NodeId == id) && claimed.Add(id)).ToArray()
        }).ToList();
        try { GraphGroupOperations.Validate(_frames, Nodes.Select(n => n.NodeId)); }
        catch (ArgumentException error) {
            SetAuthoringIssue("graph.groups.invalid", new("graph.groups.invalid", error.Message));
            // A corrupt sidecar never deletes or changes business nodes. Retain only its flat valid membership.
            _frames = _frames.Select(f => f with { Groups = [] }).ToList();
        }
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public string AddFrame(IEnumerable<string> members, double x, double y, double width, double height)
    {
        var ids = members.Distinct(StringComparer.Ordinal).Where(id => Nodes.Any(node => node.NodeId == id)).ToArray();
        var frame = new GraphCommentFrame(Guid.NewGuid().ToString("N"), "分组注释", x, y, Math.Max(80, width), Math.Max(50, height), ids) { Color = GraphCommentFrame.RandomColor() };
        if (!frame.IsValid) throw new ArgumentException("Invalid frame rectangle");
        var before = _frames.ToArray();
        var after = before.Select(old => old with { Members = old.Members.Except(ids).ToArray() }).Append(frame).ToArray();
        EditMetadata(() => ApplyFrames(before), () => ApplyFrames(after));
        return frame.Id;
    }

    public void RemoveFrame(string id)
    {
        var before = _frames.ToArray();
        if (!before.Any(frame => frame.Id == id)) return;
        var after = GraphGroupOperations.Ungroup(before, id);
        EditMetadata(() => ApplyFrames(before), () => ApplyFrames(after));
    }

    public void UpdateFrame(GraphCommentFrame next, bool moveMembers = false)
    {
        var previous = _frames.FirstOrDefault(frame => frame.Id == next.Id);
        if (previous is null || !next.IsValid || previous == next) return;
        var before = _frames.ToArray();
        var after = before.Select(frame => frame.Id == next.Id ? next : frame with { Members = frame.Members.Except(next.Members).ToArray() }).ToArray();
        var descendants = GraphGroupOperations.DescendantNodes(before, [previous.Id]);
        var positions = Nodes.Where(node => descendants.Contains(node.NodeId)).ToDictionary(node => node.NodeId, node => node.Position);
        var moved = positions.ToDictionary(pair => pair.Key, pair => new GraphEditorNodePosition(pair.Value.X + next.X - previous.X, pair.Value.Y + next.Y - previous.Y));
        void Apply(GraphCommentFrame[] frames, IReadOnlyDictionary<string, GraphEditorNodePosition> layout)
        {
            if (moveMembers) ApplyLayoutSnapshot(layout, publishChange: false);
            ApplyFrames(frames);
        }
        EditMetadata(() => Apply(before, positions), () => Apply(after, moved));
    }

    private void ApplyFrames(IEnumerable<GraphCommentFrame> frames)
    {
        _frames = frames.ToList();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CombineSelection(IEnumerable<string> nodes, IEnumerable<string> groups)
    {
        var before = FrameSnapshot().ToArray();
        var selected = nodes.Where(id => Nodes.Any(n => n.NodeId == id)).ToArray();
        var after = GraphGroupOperations.Combine(before, selected, groups, Guid.NewGuid().ToString("N"));
        GraphGroupOperations.Validate(after, Nodes.Select(n => n.NodeId));
        if (System.Text.Json.JsonSerializer.Serialize(before) == System.Text.Json.JsonSerializer.Serialize(after)) return false;
        EditMetadata(() => ApplyFrames(before), () => ApplyFrames(after));
        return true;
    }

    public void CommitGroupMove(IReadOnlyDictionary<string, GraphEditorNodePosition> beforePositions,
        IReadOnlyDictionary<string, GraphEditorNodePosition> afterPositions, IEnumerable<string>? reparentNodes = null, string? target = null)
    {
        var before = FrameSnapshot().ToArray();
        var after = reparentNodes is null ? before : GraphGroupOperations.MoveNodes(before, reparentNodes, target);
        GraphGroupOperations.Validate(after, Nodes.Select(n => n.NodeId));
        void Apply(GraphCommentFrame[] frames, IReadOnlyDictionary<string, GraphEditorNodePosition> positions)
        { ApplyLayoutSnapshot(positions, publishChange: false); ApplyFrames(frames); }
        EditMetadata(() => Apply(before, beforePositions), () => Apply(after, afterPositions));
    }

    public bool DeleteGroups(IEnumerable<string> groups, IEnumerable<string> selectedNodes, bool confirmed)
    {
        if (Scope == DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphScope.Project) return false;
        var before = FrameSnapshot().ToArray();
        var removeGroups = GraphGroupOperations.DescendantGroups(before, groups);
        var ids = GraphGroupOperations.DescendantNodes(before, removeGroups).Concat(selectedNodes).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Any(id => IsReadOnlySource(id) || Nodes.Any(node => node.NodeId == id
            && DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphNodeDefinitionRegistry.TryGet(Scope, node.Type, out var definition)
            && !DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphScopePolicy.CanDeleteNode(Scope, node.Type, Graph)))) return false;
        if (Scope == DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphScope.StoryFlow
            && Graph.Nodes.Where(node => node.Type == "start").All(node => ids.Contains(node.Id))) return false;
        var graph = DarkGreyRPG.Studio.Core.Graphs.GraphDocument.FromJson(Graph.ToJson());
        var session = new DarkGreyRPG.Studio.Core.Graphs.Editing.GraphEditSession(graph, Scope);
        if (ids.Length > 0 && !session.RemoveNodes(ids, confirmed)) return false;
        var after = GraphGroupOperations.Clean(before.Where(f => !removeGroups.Contains(f.Id)).Select(f => f with
        { Members = f.Members.Except(ids).ToArray(), Groups = f.Groups.Where(id => !removeGroups.Contains(id)).ToArray() }));
        CommitClipboardSnapshot(graph, Layout.Where(p => !ids.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value), frames: after);
        return true;
    }
}
