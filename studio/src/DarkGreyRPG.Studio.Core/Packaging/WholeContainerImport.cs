using System.Text;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Core.Packaging;

/// <summary>Allocate every member first, then commit the entire detached container copy.</summary>
internal static class WholeContainerImport
{
    internal static OfflineImportPlan Build(string projectDirectory, string packagePath)
    {
        var root = Path.GetFullPath(projectDirectory);
        var sourceStamp = OfflineProjectStateStamp.Capture(root);
        var packages = OfflineDgrsPackageReader.ReadContainer(packagePath);
        var current = CurrentProjectInventory.Read(root);
        var catalog = OfflineProviderCatalog.Load(root);
        if (catalog.Diagnostics.Count != 0) throw new StoryPackageException(string.Join("; ", catalog.Diagnostics.Select(issue => issue.Message)));
        var members = packages.Select(package => CanonicalStoryMembershipSerializer.Deserialize(
            Encoding.UTF8.GetString(package.GetEntry(package.Manifest.RequiredResources.CanonicalMemberships.Single())))).ToArray();
        var visible = current.Graphs.Where(graph => graph.ResourceKind == GraphResourceKind.Story).Select(graph => graph.Id)
            .Concat(catalog.Providers.Select(package => package.Manifest.StoryId)).Concat(packages.Select(package => package.Manifest.StoryId))
            .Select(StoryUid.Parse).ToHashSet();
        var mapping = new ResourceCopyRemapper();
        var storyIds = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var package in packages)
        {
            var uid = StoryUid.Create(visible); visible.Add(uid);
            storyIds.Add(package.Manifest.StoryId, uid.Value);
            mapping.Add(DgrResourceKind.Story, package.Manifest.StoryId, uid.Value);
        }
        var owned = members.SelectMany(member => CurrentProjectInventory.Members(member.OwnedResources)).ToHashSet();
        foreach (var key in owned)
        {
            var source = ResourceAddress.FromKey(key.Id);
            mapping.Add(key.Kind, key.Id, new ResourceAddress(StoryUid.Parse(storyIds[source.StoryUid.Value]), source.Kind, source.LocalId).ToKey());
        }
        var changes = new Dictionary<string, ProjectFileChange>(StringComparer.OrdinalIgnoreCase);
        var graphs = current.Graphs.ToList(); var actors = current.Actors.ToList(); var items = current.Items.ToList();
        var memberships = current.Memberships.ToList();
        var imported = new List<OfflineProviderResource>();
        var store = new CanonicalProjectGraphStore(root);
        foreach (var resource in packages.SelectMany(package => package.Resources).DistinctBy(resource => (resource.Kind, resource.Id)))
        {
            if (resource.Kind == DgrResourceKind.Story ? !storyIds.ContainsKey(resource.Id) : !owned.Contains(new(resource.Kind, resource.Id))) continue;
            var id = mapping.Resolve(resource.Kind, resource.Id);
            string json, destination;
            if (resource.ReadGraphDefinition() is { } graph)
            {
                var copy = mapping.Rewrite(graph); graphs.Add(copy); json = copy.ToJson();
                destination = (copy.ResourceKind switch { GraphResourceKind.Story => store.Stories, GraphResourceKind.Session => store.Sessions, _ => store.Tasks }).GetPath(id);
            }
            else if (resource.ReadActorDefinition() is { } actor)
            {
                var copy = mapping.Rewrite(actor); actors.Add(copy); json = ActorSerializer.Serialize(copy, ActorIdPolicy.ExistingResource);
                destination = new ActorRepository(root).GetActorPath(id);
            }
            else
            {
                var copy = mapping.Rewrite(resource.ReadItemDefinition()!); items.Add(copy); json = ItemSerializer.Serialize(copy);
                var repository = new ItemRepository(root);
                destination = resource.Kind == DgrResourceKind.Item ? repository.GetItemPath(id) : repository.GetGroupPath(id);
            }
            Add(destination, Encoding.UTF8.GetBytes(json), fresh: true);
            imported.Add(resource with { Id = id, StoryId = storyIds[ResourceOwner(resource)], DefinitionJson = json, IsReadOnly = false });
        }
        foreach (var member in members)
        {
            var copy = mapping.Rewrite(member); memberships.Add(copy);
            Add(Path.Combine(root, "resources", "canonical", "memberships", copy.StoryId + ".json"),
                Encoding.UTF8.GetBytes(CanonicalStoryMembershipSerializer.Serialize(copy)), fresh: true);
        }
        foreach (var media in packages.SelectMany(package => package.Manifest.RequiredResources.Media).Distinct(StringComparer.Ordinal))
        {
            var bytes = packages[0].GetEntry(media);
            Add(Path.Combine(root, "resources", media), bytes, shared: true);
        }
        var logic = new CanonicalStoryLogicGraph(2, current.Logic.Connections.Concat(mapping.Rewrite(packages[0].ContainerConnections).Connections).ToArray());
        if (packages.Count > 1) Add(store.StoryLogicGraph.Path, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(logic)));

        // Referenced definitions retain their original owner. Keep a complete read-only source
        // only when those dependencies do not already have a native/provider home.
        var available = CurrentProjectInventory.Resources(current).Concat(catalog.Resources.Select(resource => new DgrResourceKey(resource.Kind, resource.Id))).ToHashSet();
        var external = members.SelectMany(member => CurrentProjectInventory.Members(member.ReferencedResources)).Where(key => !owned.Contains(key)).Distinct().ToArray();
        string? referencePath = null;
        if (external.Any(key => !available.Contains(key)))
        {
            var allSource = packages.SelectMany(package => package.Resources).Select(resource => new DgrResourceKey(resource.Kind, resource.Id)).Distinct().ToArray();
            if (allSource.Any(available.Contains)) throw new StoryPackageException("Import dependencies need a complete source Reference, but its identities overlap existing content. Resolve the provider overlap first.");
            referencePath = Path.Combine(root, "references", storyIds.Values.First() + (packages.Count > 1 ? ".dgrs.g" : ".dgrs"));
            Add(referencePath, packages[0].ArchiveBytes, fresh: true);
        }
        // Existing read-only Story endpoints remain legal in the detached graph validation.
        foreach (var provider in catalog.Providers)
        {
            foreach (var resource in provider.Resources)
                if (resource.ReadGraphDefinition() is { ResourceKind: GraphResourceKind.Story } graph && graphs.All(existing => existing.Id != graph.Id)) graphs.Add(graph);
            var member = CanonicalStoryMembershipSerializer.Deserialize(Encoding.UTF8.GetString(provider.GetEntry(provider.Manifest.RequiredResources.CanonicalMemberships.Single())));
            if (memberships.All(existing => existing.StoryId != member.StoryId))
                memberships.Add(new(member.StoryId, new(), new CanonicalStoryMembershipSet
                { Actors = member.OwnedResources.Actors.Concat(member.ReferencedResources.Actors).ToList(), Items = member.OwnedResources.Items.Concat(member.ReferencedResources.Items).ToList(),
                  ItemGroups = member.OwnedResources.ItemGroups.Concat(member.ReferencedResources.ItemGroups).ToList(), Sessions = member.OwnedResources.Sessions.Concat(member.ReferencedResources.Sessions).ToList(), Tasks = member.OwnedResources.Tasks.Concat(member.ReferencedResources.Tasks).ToList() }));
        }
        var final = new CurrentProjectSnapshot(graphs, memberships, actors, items, logic);
        CurrentProjectValidator.Validate(final);
        if (packages.Count > 1)
        {
            var names = new StoryGroupNameStore(root);
            var groups = StoryGroupCatalog.Derive(graphs.Where(graph => graph.ResourceKind == GraphResourceKind.Story).Select(graph => graph.Id), logic, names.Load());
            var group = groups.ByStory[storyIds.Values.First()];
            Add(names.Path, Encoding.UTF8.GetBytes(StoryGroupNameStore.Serialize(groups.Rename(group.Key, packages[0].GroupDisplayName!))));
        }
        if (sourceStamp != OfflineProjectStateStamp.Capture(root)) throw new StoryPackageException("Project changed while preparing Import.");
        return new(root, packages[0], changes.Values.ToArray(), [], referencePath, imported)
        { StoryUidMap = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(storyIds), FinalSnapshot = final };

        void Add(string path, byte[] bytes, bool fresh = false, bool shared = false)
        {
            var relative = Path.GetRelativePath(root, path);
            var previous = File.Exists(path) ? File.ReadAllBytes(path) : null;
            if (fresh && previous is not null || shared && previous is not null && !previous.AsSpan().SequenceEqual(bytes))
                throw new StoryPackageException("Import would overwrite existing content: " + relative);
            if (changes.TryGetValue(relative, out var change) && !change.DesiredBytes!.AsSpan().SequenceEqual(bytes)) throw new StoryPackageException("Import destinations disagree");
            changes[relative] = new(relative, previous, bytes);
        }
        string ResourceOwner(OfflineProviderResource resource) => resource.Kind == DgrResourceKind.Story ? resource.Id : ResourceAddress.FromKey(resource.Id).StoryUid.Value;
    }
}
