using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Core.Identity;

/// <summary>Detached current-format content; no Namespace policy or migration side effects.</summary>
public sealed record CurrentProjectSnapshot(
    IReadOnlyList<GraphResourceEnvelope> Graphs,
    IReadOnlyList<CanonicalStoryMembershipManifest> Memberships,
    IReadOnlyList<ActorResource> Actors,
    IReadOnlyList<ItemResource> Items,
    CanonicalStoryLogicGraph Logic);

public static class CurrentProjectInventory
{
    public static CurrentProjectSnapshot Read(string projectDirectory)
    {
        var root = Path.GetFullPath(projectDirectory);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var store = new CanonicalProjectGraphStore(root);
        var graphs = new[] { store.Stories, store.Sessions, store.Tasks }
            .SelectMany(repository => repository.List().Select(info => repository.Load(info.Id))).ToArray();
        var memberships = store.Memberships.List().Select(info => store.Memberships.Load(info.StoryId)).ToArray();
        var actors = Directory.Exists(Path.Combine(root, "actors"))
            ? new ActorRepository(root).ListActors().Select(info => ActorSerializer.Deserialize(File.ReadAllText(info.SourcePath))).ToArray()
            : Array.Empty<ActorResource>();
        var repository = new ItemRepository(root);
        var items = repository.ListItems().Concat(repository.ListGroups())
            .Select(info => ItemSerializer.Deserialize(File.ReadAllText(info.Path))).ToArray();
        return new(graphs, memberships, actors, items, store.StoryLogicGraph.Load());
    }

    public static DgrResourceKind Kind(GraphResourceKind kind) => kind switch
    {
        GraphResourceKind.Story => DgrResourceKind.Story,
        GraphResourceKind.Session => DgrResourceKind.Session,
        GraphResourceKind.Task => DgrResourceKind.Task,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static bool IsValid(DgrResourceKey key)
    {
        if (key.Kind == DgrResourceKind.Story) return StoryUid.IsValid(key.Id);
        if (!ResourceAddress.IsKey(key.Id)) return false;
        return ResourceAddress.FromKey(key.Id).Kind.ToString() == key.Kind.ToString();
    }

    public static IEnumerable<DgrResourceKey> Resources(CurrentProjectSnapshot project)
        => project.Graphs.Select(resource => new DgrResourceKey(Kind(resource.ResourceKind), resource.Id))
            .Concat(project.Actors.Select(actor => new DgrResourceKey(DgrResourceKind.Actor, actor.Id)))
            .Concat(project.Items.Select(item => new DgrResourceKey(item is IndividualItemResource ? DgrResourceKind.Item : DgrResourceKind.ItemGroup, item.Id)));

    public static IEnumerable<DgrResourceKey> Members(CanonicalStoryMembershipSet set)
        => set.Actors.Select(id => new DgrResourceKey(DgrResourceKind.Actor, id))
            .Concat(set.Items.Select(id => new DgrResourceKey(DgrResourceKind.Item, id)))
            .Concat(set.ItemGroups.Select(id => new DgrResourceKey(DgrResourceKind.ItemGroup, id)))
            .Concat(set.Sessions.Select(id => new DgrResourceKey(DgrResourceKind.Session, id)))
            .Concat(set.Tasks.Select(id => new DgrResourceKey(DgrResourceKind.Task, id)));
}
