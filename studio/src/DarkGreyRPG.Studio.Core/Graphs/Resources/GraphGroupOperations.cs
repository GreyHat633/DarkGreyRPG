namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Pure sidecar operations. Selection retains node/group object types.</summary>
public static class GraphGroupOperations
{
    public static void Validate(IReadOnlyList<GraphCommentFrame> frames, IEnumerable<string> nodeIds)
    {
        var nodes = nodeIds.ToHashSet(StringComparer.Ordinal);
        var groups = frames.Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        if (groups.Count != frames.Count) throw new ArgumentException("重复组合标识");
        var members = new HashSet<string>(StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var frame in frames)
        {
            if (!frame.IsValid || frame.Groups is null) throw new ArgumentException("组合数据无效");
            foreach (var id in frame.Members)
                if (!nodes.Contains(id) || !members.Add(id)) throw new ArgumentException("节点缺失或有多个直接父组");
            foreach (var id in frame.Groups)
                if (!groups.Contains(id) || !parents.TryAdd(id, frame.Id)) throw new ArgumentException("子组缺失或有多个直接父组");
        }
        foreach (var id in groups)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { id };
            var current = id;
            while (parents.TryGetValue(current, out var parent))
            {
                if (!seen.Add(parent)) throw new ArgumentException("组合不能循环包含");
                current = parent;
            }
        }
    }

    public static HashSet<string> DescendantGroups(IReadOnlyList<GraphCommentFrame> frames, IEnumerable<string> roots)
    {
        var index = frames.ToDictionary(f => f.Id, StringComparer.Ordinal);
        var result = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(roots);
        while (pending.TryPop(out var id))
            if (index.TryGetValue(id, out var group) && result.Add(id)) foreach (var child in group.Groups) pending.Push(child);
        return result;
    }
    public static HashSet<string> DescendantNodes(IReadOnlyList<GraphCommentFrame> frames, IEnumerable<string> roots)
    {
        var groups = DescendantGroups(frames, roots);
        return frames.Where(f => groups.Contains(f.Id)).SelectMany(f => f.Members).ToHashSet(StringComparer.Ordinal);
    }

    public static GraphCommentFrame[] Clean(IEnumerable<GraphCommentFrame> source)
    {
        var frames = source.ToArray();
        while (true)
        {
            var empty = frames.Where(f => f.Members.Length == 0 && f.Groups.Length == 0).Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
            if (empty.Count == 0) return frames;
            frames = frames.Where(f => !empty.Contains(f.Id)).Select(f => f with { Groups = f.Groups.Where(id => !empty.Contains(id)).ToArray() }).ToArray();
        }
    }

    public static GraphCommentFrame[] MoveNodes(IReadOnlyList<GraphCommentFrame> frames, IEnumerable<string> nodeIds, string? target)
    {
        var moved = nodeIds.ToHashSet(StringComparer.Ordinal);
        if (target is not null && !frames.Any(f => f.Id == target)) throw new ArgumentException("目标组不存在");
        return Clean(frames.Select(f => f with { Members = f.Id == target ? f.Members.Concat(moved).Distinct(StringComparer.Ordinal).ToArray() : f.Members.Where(id => !moved.Contains(id)).ToArray() }));
    }

    public static GraphCommentFrame[] Ungroup(IReadOnlyList<GraphCommentFrame> frames, string id)
    {
        var group = frames.SingleOrDefault(f => f.Id == id);
        if (group is null) return frames.ToArray();
        return Clean(frames.Where(f => f.Id != id).Select(f => !f.Groups.Contains(id) ? f : f with
        {
            Members = f.Members.Concat(group.Members).ToArray(),
            Groups = f.Groups.SelectMany(child => child == id ? group.Groups : [child]).ToArray()
        }));
    }

    public static GraphCommentFrame[] Combine(IReadOnlyList<GraphCommentFrame> frames, IEnumerable<string> nodeIds, IEnumerable<string> selectedGroups, string newId)
    {
        var parents = frames.SelectMany(f => f.Groups.Select(id => (id, parent: f.Id))).ToDictionary(p => p.id, p => p.parent, StringComparer.Ordinal);
        var groups = selectedGroups.Where(id => frames.Any(f => f.Id == id)).ToHashSet(StringComparer.Ordinal);
        var allSelected = groups.ToHashSet(StringComparer.Ordinal);
        groups.RemoveWhere(id => HasSelectedAncestor(id, allSelected, parents));
        var covered = DescendantNodes(frames, groups);
        var nodes = nodeIds.Distinct(StringComparer.Ordinal).Where(id => !covered.Contains(id)).ToArray();
        if (groups.Count == 1) return MoveNodes(frames, nodes, groups.Single());
        if (groups.Count == 0)
        {
            if (nodes.Length < 2) return frames.ToArray();
            var owners = frames.Where(f => f.Members.Intersect(nodes).Any()).Select(f => f.Id).ToArray();
            if (owners.Length == 1) return MoveNodes(frames, nodes, owners[0]);
        }
        if (groups.Count == 0 && nodes.Length < 2) return frames.ToArray();
        string? ParentOfNode(string id) => frames.FirstOrDefault(f => f.Members.Contains(id))?.Id;
        var originalParents = groups.Select(id => parents.GetValueOrDefault(id)).Concat(nodes.Select(ParentOfNode)).ToArray();
        string? common = originalParents.FirstOrDefault();
        while (common is not null && !originalParents.All(parent => IsWithin(parent, common, parents))) common = parents.GetValueOrDefault(common);
        var group = new GraphCommentFrame(newId, "组合", 0, 0, 80, 50, nodes) { Groups = groups.ToArray(), Color = GraphCommentFrame.RandomColor() };
        return Clean(frames.Select(f => f with
        {
            Members = f.Members.Except(nodes, StringComparer.Ordinal).ToArray(),
            Groups = f.Groups.Where(id => !groups.Contains(id)).Concat(f.Id == common ? [newId] : []).ToArray()
        }).Append(group));
    }
    private static bool HasSelectedAncestor(string id, HashSet<string> selected, Dictionary<string, string> parents)
    {
        while (parents.TryGetValue(id, out var parent)) { if (selected.Contains(parent)) return true; id = parent; }
        return false;
    }
    private static bool IsWithin(string? id, string ancestor, Dictionary<string, string> parents)
    {
        while (id is not null) { if (id == ancestor) return true; id = parents.GetValueOrDefault(id); }
        return false;
    }
}
