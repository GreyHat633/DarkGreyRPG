using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Provenance retained for one canonical Story membership entry.</summary>
public enum CanonicalStoryWorkspaceMembershipKind
{
    Owned,
    Referenced,
}

/// <summary>A resolved (or missing) canonical Story member.</summary>
public sealed record CanonicalStoryWorkspaceEntry<T>(
    string Id,
    CanonicalStoryWorkspaceMembershipKind MembershipKind,
    T? Resource)
{
    public CanonicalStoryWorkspaceMembershipKind Kind => MembershipKind;
    public bool IsOwned => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Owned;
    public bool IsReferenced => MembershipKind == CanonicalStoryWorkspaceMembershipKind.Referenced;
    public bool IsResolved => Resource is not null;
    public bool IsMissing => Resource is null;
}

/// <summary>
/// Detached canonical Story envelope, membership manifest, and only the
/// resources named by that manifest.
/// </summary>
public sealed class CanonicalStoryWorkspaceSnapshot
{
    private readonly GraphResourceEnvelope _story;
    private readonly CanonicalStoryMembershipManifest _membership;

    internal CanonicalStoryWorkspaceSnapshot(
        GraphResourceEnvelope story,
        CanonicalStoryMembershipManifest membership,
        IReadOnlyList<CanonicalStoryWorkspaceEntry<ActorDocument>> actors,
        IReadOnlyList<CanonicalStoryWorkspaceEntry<IndividualItemResource>> items,
        IReadOnlyList<CanonicalStoryWorkspaceEntry<CollectiveItemResource>> itemGroups,
        IReadOnlyList<CanonicalStoryWorkspaceEntry<GraphResourceEnvelope>> sessions,
        IReadOnlyList<CanonicalStoryWorkspaceEntry<GraphResourceEnvelope>> tasks,
        IReadOnlyList<ValidationIssue> validationIssues)
    {
        _story = CloneEnvelope(story);
        _membership = CloneMembership(membership);
        Actors = actors.ToArray();
        Items = items.ToArray();
        ItemGroups = itemGroups.ToArray();
        Sessions = sessions.ToArray();
        Tasks = tasks.ToArray();
        ValidationIssues = validationIssues.ToArray();
    }

    /// <summary>Gets a fresh detached Story envelope snapshot.</summary>
    public GraphResourceEnvelope Story => CloneEnvelope(_story);

    /// <summary>Gets a fresh detached membership manifest snapshot.</summary>
    public CanonicalStoryMembershipManifest Membership => CloneMembership(_membership);

    public IReadOnlyList<CanonicalStoryWorkspaceEntry<ActorDocument>> Actors { get; }
    public IReadOnlyList<CanonicalStoryWorkspaceEntry<IndividualItemResource>> Items { get; }
    public IReadOnlyList<CanonicalStoryWorkspaceEntry<CollectiveItemResource>> ItemGroups { get; }
    public IReadOnlyList<CanonicalStoryWorkspaceEntry<GraphResourceEnvelope>> Sessions { get; }
    public IReadOnlyList<CanonicalStoryWorkspaceEntry<GraphResourceEnvelope>> Tasks { get; }
    public IReadOnlyList<ValidationIssue> ValidationIssues { get; }

    public IReadOnlyList<ValidationIssue> ValidationErrors =>
        ValidationIssues.Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();

    private static GraphResourceEnvelope CloneEnvelope(GraphResourceEnvelope envelope)
        => new(envelope.ResourceKind, envelope.Id, envelope.DisplayName, envelope.Graph!)
        {
            SchemaVersion = envelope.SchemaVersion,
        };

    private static CanonicalStoryMembershipManifest CloneMembership(CanonicalStoryMembershipManifest manifest)
        => new(manifest.StoryId, manifest.OwnedResources, manifest.ReferencedResources)
        {
            SchemaVersion = manifest.SchemaVersion,
            DisplayOrder = manifest.DisplayOrder,
        };
}

/// <summary>Loads one canonical Story workspace without touching legacy roots.</summary>
public sealed class CanonicalStoryWorkspaceLoader
{
    private readonly CanonicalProjectGraphStore _store;
    private readonly ActorRepository _actors;
    private readonly ItemRepository _items;

    public CanonicalStoryWorkspaceLoader(
        CanonicalProjectGraphStore store,
        ActorRepository? actors = null,
        ItemRepository? items = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _actors = actors ?? new ActorRepository(store.ProjectDirectory);
        _items = items ?? new ItemRepository(store.ProjectDirectory);
    }

    /// <summary>
    /// Loads the canonical Story and its membership manifest first. A failure
    /// at either root boundary is propagated, so no partial workspace escapes.
    /// Missing members are represented in-place and reported as validation
    /// issues while the remaining requested members continue resolving.
    /// </summary>
    public CanonicalStoryWorkspaceSnapshot Load(string storyId)
    {
        // These are intentionally the only root loads. The repositories
        // enforce canonical schema, kind, filename, and stable identity.
        var story = _store.Stories.Load(storyId);
        var membership = _store.Memberships.Load(storyId);

        var issues = new List<ValidationIssue>();
        var actors = ResolveActors(membership, issues);
        var items = ResolveItems(
            membership.OwnedResources.Items,
            membership.ReferencedResources.Items,
            issues);
        var itemGroups = ResolveItemGroups(
            membership.OwnedResources.ItemGroups,
            membership.ReferencedResources.ItemGroups,
            issues);
        var sessions = ResolveGraphResources(
            membership.OwnedResources.Sessions,
            membership.ReferencedResources.Sessions,
            _store.Sessions,
            "Session",
            issues);
        var tasks = ResolveGraphResources(
            membership.OwnedResources.Tasks,
            membership.ReferencedResources.Tasks,
            _store.Tasks,
            "Task",
            issues);

        return new CanonicalStoryWorkspaceSnapshot(story, membership, actors, items, itemGroups, sessions, tasks, issues);
    }

    private IReadOnlyList<CanonicalStoryWorkspaceEntry<IndividualItemResource>> ResolveItems(
        IEnumerable<string> ownedIds,
        IEnumerable<string> referencedIds,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<CanonicalStoryWorkspaceEntry<IndividualItemResource>>();
        ResolveItemList(ownedIds, CanonicalStoryWorkspaceMembershipKind.Owned, result, issues);
        ResolveItemList(referencedIds, CanonicalStoryWorkspaceMembershipKind.Referenced, result, issues);
        return result;
    }

    private IReadOnlyList<CanonicalStoryWorkspaceEntry<CollectiveItemResource>> ResolveItemGroups(
        IEnumerable<string> ownedIds,
        IEnumerable<string> referencedIds,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<CanonicalStoryWorkspaceEntry<CollectiveItemResource>>();
        ResolveItemGroupList(ownedIds, CanonicalStoryWorkspaceMembershipKind.Owned, result, issues);
        ResolveItemGroupList(referencedIds, CanonicalStoryWorkspaceMembershipKind.Referenced, result, issues);
        return result;
    }

    private void ResolveItemList(
        IEnumerable<string> ids,
        CanonicalStoryWorkspaceMembershipKind membershipKind,
        ICollection<CanonicalStoryWorkspaceEntry<IndividualItemResource>> result,
        ICollection<ValidationIssue> issues)
    {
        foreach (var id in ids) result.Add(ResolveItem(id, membershipKind, issues));
    }

    private CanonicalStoryWorkspaceEntry<IndividualItemResource> ResolveItem(
        string id,
        CanonicalStoryWorkspaceMembershipKind membershipKind,
        ICollection<ValidationIssue> issues)
    {
        IndividualItemResource? resource = null;
        try { resource = _items.LoadItem(id); }
        catch (ItemNotFoundException) { issues.Add(MissingIssue("Item", id, "items")); }
        return new(id, membershipKind, resource);
    }

    private void ResolveItemGroupList(
        IEnumerable<string> ids,
        CanonicalStoryWorkspaceMembershipKind membershipKind,
        ICollection<CanonicalStoryWorkspaceEntry<CollectiveItemResource>> result,
        ICollection<ValidationIssue> issues)
    {
        foreach (var id in ids)
        {
            CollectiveItemResource? resource = null;
            try { resource = _items.LoadGroup(id); }
            catch (ItemNotFoundException) { issues.Add(MissingIssue("ItemGroup", id, "item_groups")); }
            result.Add(new(id, membershipKind, resource));
        }
    }

    private IReadOnlyList<CanonicalStoryWorkspaceEntry<ActorDocument>> ResolveActors(
        CanonicalStoryMembershipManifest membership,
        ICollection<ValidationIssue> issues)
    {
        var owned = membership.OwnedResources.Actors;
        var referenced = membership.ReferencedResources.Actors;
        var result = new List<CanonicalStoryWorkspaceEntry<ActorDocument>>(owned.Count + referenced.Count);
        ResolveActorList(owned, CanonicalStoryWorkspaceMembershipKind.Owned, result, issues);
        ResolveActorList(referenced, CanonicalStoryWorkspaceMembershipKind.Referenced, result, issues);
        return result;
    }

    private void ResolveActorList(
        IEnumerable<string> ids,
        CanonicalStoryWorkspaceMembershipKind membershipKind,
        ICollection<CanonicalStoryWorkspaceEntry<ActorDocument>> result,
        ICollection<ValidationIssue> issues)
    {
        foreach (var id in ids)
        {
            ActorDocument? actor = null;
            try
            {
                // LoadActor addresses exactly this ID path; it does not list
                // or scan unrelated Actor files.
                actor = _actors.LoadActor(id);
            }
            catch (ActorNotFoundException)
            {
                issues.Add(MissingIssue("Actor", id, "actors"));
            }

            result.Add(new CanonicalStoryWorkspaceEntry<ActorDocument>(id, membershipKind, actor));
        }
    }

    private static IReadOnlyList<CanonicalStoryWorkspaceEntry<GraphResourceEnvelope>> ResolveGraphResources(
        IEnumerable<string> ownedIds,
        IEnumerable<string> referencedIds,
        GraphResourceRepository repository,
        string kind,
        ICollection<ValidationIssue> issues)
    {
        var owned = ownedIds.ToArray();
        var referenced = referencedIds.ToArray();
        var result = new List<CanonicalStoryWorkspaceEntry<GraphResourceEnvelope>>(owned.Length + referenced.Length);
        ResolveGraphList(owned, CanonicalStoryWorkspaceMembershipKind.Owned, repository, kind, result, issues);
        ResolveGraphList(referenced, CanonicalStoryWorkspaceMembershipKind.Referenced, repository, kind, result, issues);
        return result;
    }

    private static void ResolveGraphList(
        IEnumerable<string> ids,
        CanonicalStoryWorkspaceMembershipKind membershipKind,
        GraphResourceRepository repository,
        string kind,
        ICollection<CanonicalStoryWorkspaceEntry<GraphResourceEnvelope>> result,
        ICollection<ValidationIssue> issues)
    {
        foreach (var id in ids)
        {
            GraphResourceEnvelope? resource = null;
            try
            {
                resource = repository.Load(id);
            }
            catch (GraphResourceRepositoryException exception)
                when (exception.Code == "graph.resource.repository.not_found")
            {
                issues.Add(MissingIssue(kind, id, kind.ToLowerInvariant() + "s"));
            }

            result.Add(new CanonicalStoryWorkspaceEntry<GraphResourceEnvelope>(id, membershipKind, resource));
        }
    }

    private static ValidationIssue MissingIssue(string kind, string id, string field)
        => new(
            "story.workspace.member.missing",
            $"Canonical Story {kind} member '{id}' was not found.",
            field,
            ValidationSeverity.Error,
            id);
}
