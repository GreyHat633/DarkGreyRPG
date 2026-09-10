using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Core.Identity;

/// <summary>Detached authoring inventory; the planner never writes repositories or settings.</summary>
public sealed record NamespaceProjectSnapshot(
    IReadOnlyList<GraphResourceEnvelope> Graphs,
    IReadOnlyList<CanonicalStoryMembershipManifest> Memberships,
    IReadOnlyList<ActorResource> Actors,
    IReadOnlyList<ItemResource> Items,
    CanonicalStoryLogicGraph Logic,
    NamespacePolicy? Policy);

public sealed record NamespaceMigrationPlan(ResourceRenameMap Renames, NamespaceProjectSnapshot Result);

public static class NamespaceMigrationPlanner
{
    public static NamespaceMigrationPlan ChangeGlobal(NamespaceProjectSnapshot project, string newNamespace)
        => Plan(project, newNamespace, null, null);

    public static NamespaceMigrationPlan ChangeStory(NamespaceProjectSnapshot project, string storyId, string customNamespace)
        => Plan(project, project.Policy?.GlobalNamespace
            ?? throw new InvalidOperationException("Set the global Namespace before customizing a Story."), storyId, customNamespace);

    public static NamespaceMigrationPlan ReturnToGlobal(NamespaceProjectSnapshot project, string storyId)
        => Plan(project, project.Policy?.GlobalNamespace
            ?? throw new InvalidOperationException("Set the global Namespace first."), storyId, null);

    private static NamespaceMigrationPlan Plan(NamespaceProjectSnapshot project, string global, string? selectedStory, string? custom)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (!DgrResourceId.IsValidNamespace(global) || custom is not null && !DgrResourceId.IsValidNamespace(custom))
            throw new ArgumentException("Namespace is invalid.");
        var stories = project.Graphs.Where(resource => resource.ResourceKind == GraphResourceKind.Story)
            .ToDictionary(resource => resource.Id, StringComparer.Ordinal);
        var memberships = project.Memberships.ToDictionary(member => member.StoryId, StringComparer.Ordinal);
        if (selectedStory is not null && !stories.ContainsKey(selectedStory))
            throw new InvalidOperationException($"Story '{selectedStory}' does not exist.");
        if (!stories.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(memberships.Keys))
            throw new InvalidOperationException("Every Story must have exactly one canonical Membership.");

        var inventory = Inventory(project).ToArray();
        if (inventory.Distinct().Count() != inventory.Length)
            throw new InvalidOperationException("The project contains duplicate typed resource identities.");
        var resources = inventory.ToHashSet();
        foreach (var membership in memberships.Values)
            foreach (var owned in Members(membership.OwnedResources))
            {
                if (!resources.Contains(owned))
                    throw new InvalidOperationException($"Story '{membership.StoryId}' owns missing {owned.Kind} '{owned.Id}'.");
            }

        var map = new ResourceRenameMap();
        var sharedDestinations = new Dictionary<DgrResourceKey, string>();
        foreach (var story in stories.Keys)
        {
            var isCustom = project.Policy?.StoryOverrides.ContainsKey(story) == true;
            var affected = selectedStory is null ? !isCustom : selectedStory == story;
            var destinationNamespace = selectedStory is null ? global : custom ?? global;
            if (affected) map.Add(DgrResourceKind.Story, story, DgrResourceId.Qualify(destinationNamespace, DgrResourceId.LocalId(story)));
            foreach (var owned in Members(memberships[story].OwnedResources))
            {
                var destination = affected ? DgrResourceId.Qualify(destinationNamespace, DgrResourceId.LocalId(owned.Id)) : owned.Id;
                if (sharedDestinations.TryGetValue(owned, out var previous) && previous != destination)
                    throw new InvalidOperationException($"Shared resource {owned.Kind} '{owned.Id}' has incompatible owner Namespace changes.");
                sharedDestinations[owned] = destination;
                if (affected) map.Add(owned.Kind, owned.Id, destination);
            }
        }
        map.ValidateCollisions(inventory);

        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in project.Policy?.StoryOverrides ?? new Dictionary<string, string>())
        {
            if (!stories.ContainsKey(pair.Key)) throw new InvalidOperationException($"Namespace policy references missing Story '{pair.Key}'.");
            if (pair.Key != selectedStory) overrides.Add(map.Resolve(DgrResourceKind.Story, pair.Key), pair.Value);
        }
        if (selectedStory is not null && custom is not null)
            overrides.Add(map.Resolve(DgrResourceKind.Story, selectedStory), custom);
        var result = new NamespaceProjectSnapshot(
            project.Graphs.Select(map.Rewrite).ToArray(), project.Memberships.Select(map.Rewrite).ToArray(),
            project.Actors.Select(map.Rewrite).ToArray(), project.Items.Select(map.Rewrite).ToArray(),
            map.Rewrite(project.Logic), new NamespacePolicy(global, overrides));
        foreach (var resource in Inventory(result))
            if (!DgrResourceId.IsFullId(resource.Id))
                throw new InvalidOperationException($"Resource {resource.Kind} '{resource.Id}' remains unnamespaced; assign its owning Story before migration.");
        return new(map, result);
    }

    public static IEnumerable<DgrResourceKey> Inventory(NamespaceProjectSnapshot project)
        => project.Graphs.Select(resource => new DgrResourceKey(ResourceRenameMap.Kind(resource.ResourceKind), resource.Id))
            .Concat(project.Actors.Select(actor => new DgrResourceKey(DgrResourceKind.Actor, actor.Id)))
            .Concat(project.Items.Select(item => new DgrResourceKey(item is IndividualItemResource ? DgrResourceKind.Item : DgrResourceKind.ItemGroup, item.Id)));

    public static IEnumerable<DgrResourceKey> Members(CanonicalStoryMembershipSet set)
        => set.Actors.Select(id => new DgrResourceKey(DgrResourceKind.Actor, id))
            .Concat(set.Items.Select(id => new DgrResourceKey(DgrResourceKind.Item, id)))
            .Concat(set.ItemGroups.Select(id => new DgrResourceKey(DgrResourceKind.ItemGroup, id)))
            .Concat(set.Sessions.Select(id => new DgrResourceKey(DgrResourceKind.Session, id)))
            .Concat(set.Tasks.Select(id => new DgrResourceKey(DgrResourceKind.Task, id)));
}
