using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Presentation metadata for a derived connected component, never a runtime identity.</summary>
public sealed class StoryGroup
{
    internal StoryGroup(IEnumerable<string> members, string displayName)
    {
        Members = Array.AsReadOnly(members.Order(StringComparer.Ordinal).ToArray());
        DisplayName = displayName;
        Key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', Members))));
    }
    public string Key { get; }
    public bool IsGroup => true;
    public string DisplayName { get; }
    public IReadOnlyList<string> Members { get; }
}

/// <summary>Immutable current-component/name snapshot; restore this same snapshot during Undo/Redo.</summary>
public sealed class StoryGroupCatalog
{
    private StoryGroupCatalog(IEnumerable<StoryGroup> groups, IEnumerable<string> singles, int nextOrdinal)
    {
        Groups = Array.AsReadOnly(groups.ToArray());
        Singles = Array.AsReadOnly(singles.ToArray());
        NextOrdinal = nextOrdinal;
        ByStory = new ReadOnlyDictionary<string, StoryGroup>(Groups.SelectMany(group => group.Members.Select(uid => (uid, group)))
            .ToDictionary(pair => pair.uid, pair => pair.group, StringComparer.Ordinal));
    }

    public static StoryGroupCatalog Empty { get; } = new([], [], 1);
    public IReadOnlyList<StoryGroup> Groups { get; }
    public IReadOnlyList<string> Singles { get; }
    public IReadOnlyDictionary<string, StoryGroup> ByStory { get; }
    public int NextOrdinal { get; }

    // Stored members describe the previous naming snapshot only. Derive always
    // rebuilds current membership from the canonical edges.
    internal static StoryGroupCatalog Restore(IEnumerable<StoryGroup> groups, int nextOrdinal)
    {
        var snapshot = groups.ToArray();
        if (nextOrdinal < 1 || snapshot.Length > 2048 || snapshot.Sum(group => group.Members.Count) > 4096
            || snapshot.Any(group => group.Members.Count < 2 || string.IsNullOrWhiteSpace(group.DisplayName) || group.DisplayName.Length > 128)
            || snapshot.SelectMany(group => group.Members).Any(string.IsNullOrWhiteSpace)
            || snapshot.SelectMany(group => group.Members).Distinct(StringComparer.Ordinal).Count() != snapshot.Sum(group => group.Members.Count))
            throw new InvalidDataException("故事组名称快照无效。");
        return new(snapshot, [], nextOrdinal);
    }

    public StoryGroupCatalog Rename(string groupKey, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("故事组名称需为 1—128 个字符。", nameof(name));
        if (!Groups.Any(group => group.Key == groupKey)) throw new ArgumentException("故事组已经变化，请刷新后重试。", nameof(groupKey));
        return new(Groups.Select(group => group.Key == groupKey ? new StoryGroup(group.Members, name.Trim()) : group), Singles, NextOrdinal);
    }

    public static StoryGroupCatalog Derive(IEnumerable<string> storyIds, CanonicalStoryLogicGraph graph, StoryGroupCatalog? previous = null)
    {
        ArgumentNullException.ThrowIfNull(storyIds);
        ArgumentNullException.ThrowIfNull(graph);
        previous ??= Empty;
        var ids = storyIds.ToArray();
        if (ids.Length > 4096 || ids.Any(id => string.IsNullOrWhiteSpace(id) || id.Contains('\n') || id.Contains('\r'))
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new ArgumentException("Story component input has invalid, duplicate or excessive identities.", nameof(storyIds));
        var neighbors = ids.ToDictionary(id => id, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var edge in graph.Connections)
        {
            if (edge.InterfaceKind is not ("Flow" or "Logic")) throw new ArgumentException("Unknown Story interface kind.", nameof(graph));
            if (!neighbors.ContainsKey(edge.SourceStoryId) || !neighbors.ContainsKey(edge.TargetStoryId))
                throw new ArgumentException("Story graph contains an undeclared endpoint.", nameof(graph));
            neighbors[edge.SourceStoryId].Add(edge.TargetStoryId);
            neighbors[edge.TargetStoryId].Add(edge.SourceStoryId);
        }
        var groups = new List<StoryGroup>();
        var singles = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var ordinal = previous.NextOrdinal;
        var usedNames = previous.Groups.Select(group => group.DisplayName).ToHashSet(StringComparer.Ordinal);
        foreach (var id in ids.Order(StringComparer.Ordinal))
        {
            if (!visited.Add(id)) continue;
            var members = new HashSet<string>(StringComparer.Ordinal) { id };
            var queue = new Queue<string>(); queue.Enqueue(id);
            while (queue.TryDequeue(out var current))
                foreach (var next in neighbors[current])
                    if (visited.Add(next)) { members.Add(next); queue.Enqueue(next); }
            if (members.Count == 1) { singles.Add(id); continue; }
            var ancestors = members.Where(previous.ByStory.ContainsKey).Select(member => previous.ByStory[member]).Distinct().ToArray();
            // Only an intact, single previous Group may pass its name to an expansion.
            // Partial overlap means split; multiple ancestors mean merge.
            string name;
            if (ancestors.Length == 1 && ancestors[0].Members.All(members.Contains)) name = ancestors[0].DisplayName;
            else
            {
                do { name = $"故事组（{ordinal}）"; ordinal = checked(ordinal + 1); } while (!usedNames.Add(name));
            }
            groups.Add(new StoryGroup(members, name));
        }
        return new(groups, singles, ordinal);
    }
}
