using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class GraphEditorHostViewModel
{
    private List<GraphCommentFrame> _frames = [];
    public IReadOnlyList<GraphCommentFrame> Frames => _frames;
    public IReadOnlyList<GraphCommentFrame> FrameSnapshot() => _frames.Select(frame => frame with
    {
        Members = frame.Members.Where(id => Nodes.Any(node => node.NodeId == id)).ToArray()
    }).ToArray();

    public void RestoreFrames(IEnumerable<GraphCommentFrame> frames)
    {
        var claimed = new HashSet<string>(StringComparer.Ordinal);
        _frames = frames.Where(frame => frame.IsValid).DistinctBy(frame => frame.Id).Select(frame => frame with
        {
            Members = frame.Members.Where(id => Nodes.Any(node => node.NodeId == id) && claimed.Add(id)).ToArray()
        }).ToList();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    public string AddFrame(IEnumerable<string> members, double x, double y, double width, double height)
    {
        var ids = members.Distinct(StringComparer.Ordinal).Where(id => Nodes.Any(node => node.NodeId == id)).ToArray();
        var frame = new GraphCommentFrame(Guid.NewGuid().ToString("N"), "分组注释", x, y, Math.Max(80, width), Math.Max(50, height), ids);
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
        var after = before.Where(frame => frame.Id != id).ToArray();
        EditMetadata(() => ApplyFrames(before), () => ApplyFrames(after));
    }

    public void UpdateFrame(GraphCommentFrame next, bool moveMembers = false)
    {
        var previous = _frames.FirstOrDefault(frame => frame.Id == next.Id);
        if (previous is null || !next.IsValid || previous == next) return;
        var before = _frames.ToArray();
        var after = before.Select(frame => frame.Id == next.Id ? next : frame with { Members = frame.Members.Except(next.Members).ToArray() }).ToArray();
        var positions = Nodes.Where(node => previous.Members.Contains(node.NodeId)).ToDictionary(node => node.NodeId, node => node.Position);
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
}
