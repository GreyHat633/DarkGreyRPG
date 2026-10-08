using DarkGreyRPG.Studio.Core.IO;
using System.Collections.ObjectModel;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Projects;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Stable failure boundary for the canonical Story lifecycle.</summary>
public sealed class CanonicalStoryLifecycleException : Exception
{
    public CanonicalStoryLifecycleException(string code, string message, Exception? innerException = null)
        : base($"{code}: {message}", innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>A structured reason why a canonical Story cannot be deleted.</summary>
public sealed record CanonicalStoryDeletionBlocker(
    string Code,
    string Message,
    string? StoryId = null,
    string? ResourceKind = null,
    string? ResourceId = null,
    string? NodeId = null)
{
    public string? SourceStoryId => StoryId;
    public string? SourceNodeId => NodeId;
    public string? Id => ResourceId;
}

/// <summary>A detached incoming Story relation observed while planning.</summary>
public sealed record CanonicalStoryDeletionIncomingTransition(
    string SourceStoryId,
    string TargetStoryId,
    string NodeId)
{
    public string EnterStoryNodeId => NodeId;
}

/// <summary>
/// Detached, deterministic, read-only deletion decision for one canonical
/// Story. Referenced members are exposed but are never deletion candidates.
/// </summary>
public sealed class CanonicalStoryDeletionPlan
{
    public CanonicalStoryDeletionPlan(
        string storyId,
        GraphResourceEnvelope story,
        CanonicalStoryMembershipManifest membership,
        IEnumerable<string> actorIds,
        IEnumerable<string> sessionIds,
        IEnumerable<string> taskIds,
        IEnumerable<string> referencedActorIds,
        IEnumerable<string> referencedSessionIds,
        IEnumerable<string> referencedTaskIds,
        IEnumerable<CanonicalStoryDeletionIncomingTransition> incomingTransitions,
        IEnumerable<CanonicalStoryDeletionBlocker> blockers,
        IEnumerable<string>? itemIds = null,
        IEnumerable<string>? itemGroupIds = null)
    {
        StoryId = storyId;
        Story = CloneEnvelope(story);
        Membership = CloneMembership(membership);
        ActorIds = Sorted(actorIds);
        SessionIds = Sorted(sessionIds);
        TaskIds = Sorted(taskIds);
        ItemIds = Sorted(itemIds);
        ItemGroupIds = Sorted(itemGroupIds);
        ReferencedActorIds = Sorted(referencedActorIds);
        ReferencedSessionIds = Sorted(referencedSessionIds);
        ReferencedTaskIds = Sorted(referencedTaskIds);
        IncomingTransitions = new ReadOnlyCollection<CanonicalStoryDeletionIncomingTransition>(
            (incomingTransitions ?? [])
                .OrderBy(item => item.SourceStoryId, StringComparer.Ordinal)
                .ThenBy(item => item.TargetStoryId, StringComparer.Ordinal)
                .ThenBy(item => item.NodeId, StringComparer.Ordinal)
                .ToArray());
        Blockers = new ReadOnlyCollection<CanonicalStoryDeletionBlocker>(
            (blockers ?? [])
                .OrderBy(item => item.Code, StringComparer.Ordinal)
                .ThenBy(item => item.StoryId, StringComparer.Ordinal)
                .ThenBy(item => item.ResourceKind, StringComparer.Ordinal)
                .ThenBy(item => item.ResourceId, StringComparer.Ordinal)
                .ThenBy(item => item.NodeId, StringComparer.Ordinal)
                .ThenBy(item => item.Message, StringComparer.Ordinal)
                .ToArray());
    }

    public string StoryId { get; }
    public GraphResourceEnvelope Story { get; }
    public CanonicalStoryMembershipManifest Membership { get; }
    public IReadOnlyList<string> ActorIds { get; }
    public IReadOnlyList<string> SessionIds { get; }
    public IReadOnlyList<string> TaskIds { get; }
    public IReadOnlyList<string> ItemIds { get; }
    public IReadOnlyList<string> ItemGroupIds { get; }
    public IReadOnlyList<string> OwnedActorIds => ActorIds;
    public IReadOnlyList<string> OwnedSessionIds => SessionIds;
    public IReadOnlyList<string> OwnedTaskIds => TaskIds;
    public IReadOnlyList<string> OwnedItemIds => ItemIds;
    public IReadOnlyList<string> OwnedItemGroupIds => ItemGroupIds;
    public IReadOnlyList<string> ReferencedActorIds { get; }
    public IReadOnlyList<string> ReferencedSessionIds { get; }
    public IReadOnlyList<string> ReferencedTaskIds { get; }
    public IReadOnlyList<CanonicalStoryDeletionIncomingTransition> IncomingTransitions { get; }
    public IReadOnlyList<CanonicalStoryDeletionBlocker> Blockers { get; }
    public IReadOnlyList<CanonicalStoryDeletionBlocker> Diagnostics => Blockers;
    public bool CanDelete => Blockers.Count == 0;

    private static IReadOnlyList<string> Sorted(IEnumerable<string>? values)
        => new ReadOnlyCollection<string>((values ?? [])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());

    private static GraphResourceEnvelope CloneEnvelope(GraphResourceEnvelope value)
        => new(value.ResourceKind, value.Id, value.DisplayName, value.Graph!) { SchemaVersion = value.SchemaVersion, Tags = value.Tags.ToArray() };

    private static CanonicalStoryMembershipManifest CloneMembership(CanonicalStoryMembershipManifest value)
        => new(value.StoryId, value.OwnedResources, value.ReferencedResources) { SchemaVersion = value.SchemaVersion };
}

/// <summary>
/// UI-independent lifecycle for one canonical Story and its owned members.
/// Compensation is ordinary in-process recovery only; it is not crash durable.
/// </summary>
public sealed class CanonicalStoryLifecycleService
{
    private readonly CanonicalProjectGraphStore _store;
    private readonly ActorRepository _actors;
    private readonly ItemRepository _items;
    private readonly Action<string> _deleteFile;
    private readonly object _lifecycleGate = new();

    public CanonicalStoryLifecycleService(
        CanonicalProjectGraphStore store,
        ActorRepository? actors = null,
        Action<string>? deleteFile = null,
        ItemRepository? items = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _actors = actors ?? new ActorRepository(store.ProjectDirectory);
        _items = items ?? new ItemRepository(store.ProjectDirectory);
        _deleteFile = deleteFile ?? File.Delete;
    }

    public CanonicalProjectGraphStore Store => _store;

    /// <summary>Allocates against local and referenced identities without changing existing Stories.</summary>
    public StoryUid AllocateStoryUid()
    {
        var visible = _store.Stories.List().Select(story => StoryUid.Parse(story.Id)).ToHashSet();
        var providers = Packaging.OfflineProviderCatalog.Load(_store.ProjectDirectory);
        if (providers.Diagnostics.Count != 0)
            throw Failure("story.lifecycle.identity_inventory.invalid", "Referenced Story identities could not be inventoried safely.");
        foreach (var resource in providers.Resources
                     .Where(resource => resource.Kind == DgrResourceKind.Story))
            visible.Add(StoryUid.Parse(resource.Id));
        return StoryUid.Create(visible);
    }

    public GraphResourceEnvelope CreateNew(string displayName) => Create(AllocateStoryUid().Value, displayName);

    /// <summary>Creates a minimum scope-valid Story Flow and empty membership.</summary>
    public GraphResourceEnvelope Create(string storyId, string displayName)
    {
        EnsureId(storyId);
        EnsureDisplayName(displayName);
        EnsureSupportedAuthorData();
        lock (_lifecycleGate)
        {
            // An occupied canonical filename remains a collision even when its bytes
            // cannot be parsed. Never overwrite a damaged root while creating a Story.
            if (File.Exists(Path.Combine(_store.StoriesDirectory, storyId + ".json"))
                || File.Exists(Path.Combine(_store.MembershipsDirectory, storyId + ".json")))
                throw Failure("story.lifecycle.collision",
                    $"Canonical Story '{storyId}' already has a Story or membership root.");
            var storyPath = _store.Stories.GetPath(storyId);
            var membershipPath = _store.Memberships.GetPath(storyId);
            if (File.Exists(storyPath) || File.Exists(membershipPath))
                throw Failure("story.lifecycle.collision",
                    $"Canonical Story '{storyId}' already has a Story or membership root.");

            var graph = new GraphDocument([GraphNodeFactory.CreateStoryStart("start")]);
            if (!GraphScopePolicy.IsValid(graph, GraphScope.StoryFlow))
                throw Failure("story.lifecycle.graph.invalid", "The minimum canonical Story Flow is not scope-valid.");

            GraphResourceEnvelope created;
            try
            {
                created = _store.Stories.Create(new GraphResourceEnvelope(
                    GraphResourceKind.Story, storyId, displayName.Trim(), graph));
            }
            catch (GraphResourceRepositoryException exception)
            {
                throw Failure("story.lifecycle.story_create_failed",
                    $"Could not create canonical Story '{storyId}'.", exception);
            }

            try
            {
                _store.Memberships.Create(new CanonicalStoryMembershipManifest(storyId));
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                try
                {
                    _store.Stories.Delete(storyId);
                    if (File.Exists(storyPath))
                        throw new IOException("Canonical Story remained after compensation.");
                }
                catch (Exception rollbackException)
                {
                    throw Failure("story.lifecycle.rollback_failed",
                        $"Membership creation failed and Story '{storyId}' could not be compensated.",
                        new AggregateException(exception, rollbackException));
                }

                throw Failure("story.lifecycle.membership_create_failed",
                    $"Could not create membership for canonical Story '{storyId}'.", exception);
            }

            try
            {
                _ = _store.Memberships.Load(storyId);
                return _store.Stories.Load(storyId);
            }
            catch (Exception exception) when (IsPersistenceFailure(exception))
            {
                throw Failure("story.lifecycle.reload_failed",
                    $"Canonical Story '{storyId}' was created but could not be reloaded.", exception);
            }
        }
    }

    public GraphResourceEnvelope CreateStory(string storyId, string displayName) => Create(storyId, displayName);
    public GraphResourceEnvelope CreateCanonicalStory(string storyId, string displayName) => Create(storyId, displayName);

    /// <summary>Builds a plan without creating directories or changing files.</summary>
    public CanonicalStoryDeletionPlan GetDeletionPlan(string storyId)
    {
        EnsureId(storyId);
        lock (_lifecycleGate)
        {
            var (story, membership) = RequirePair(storyId);
            var blockers = new List<CanonicalStoryDeletionBlocker>();
            var owned = membership.OwnedResources;
            var referenced = membership.ReferencedResources;
            ValidateOwnedFiles(storyId, owned, blockers);
            ValidateOtherCanonicalMemberships(storyId, owned, blockers);

            var transitions = new List<CanonicalStoryDeletionIncomingTransition>();
            ValidateCanonicalStoryGraphs(storyId, transitions, blockers);
            if (!GraphScopePolicy.IsValid(story.Graph!, GraphScope.StoryFlow))
                AddBlocker(blockers, "story.lifecycle.target_graph.invalid",
                    $"Canonical Story '{storyId}' has an invalid Story Flow graph.", storyId);

            return new CanonicalStoryDeletionPlan(
                storyId, story,
                membership,
                owned.Actors,
                owned.Sessions,
                owned.Tasks,
                referenced.Actors,
                referenced.Sessions,
                referenced.Tasks,
                transitions,
                blockers,
                owned.Items,
                owned.ItemGroups);
        }
    }

    public CanonicalStoryDeletionPlan BuildDeletionPlan(string storyId) => GetDeletionPlan(storyId);
    public IReadOnlyList<CanonicalStoryDeletionBlocker> GetDeletionBlockers(string storyId)
        => GetDeletionPlan(storyId).Blockers;
    public bool CanDelete(string storyId) => GetDeletionPlan(storyId).CanDelete;

    /// <summary>Plans local members together, ignoring only dependencies that are also removed.</summary>
    public IReadOnlyList<ProjectFileChange> PlanGroupDeletion(IReadOnlyCollection<string> storyIds)
    {
        var ids = storyIds.ToHashSet(StringComparer.Ordinal);
        var plans = ids.Select(GetDeletionPlan).ToArray();
        var blockers = plans.SelectMany(plan => plan.Blockers).Where(blocker =>
            blocker.Code != "story.lifecycle.owned_resource.in_use" || !ids.Contains(blocker.SourceStoryId ?? "")).ToArray();
        if (blockers.Length != 0)
            throw Failure("story.group.delete.dependencies", string.Join(Environment.NewLine, blockers.Select(b => b.Message)));
        return plans.SelectMany(SnapshotFiles).DistinctBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .Select(file => new ProjectFileChange(Path.GetRelativePath(_store.ProjectDirectory, file.Path), file.Bytes, null)).ToArray();
    }

    /// <summary>
    /// Recomputes the plan, then removes only canonical owned files. Every
    /// removed file is snapshotted and independently restored on ordinary
    /// failure, with identity validation after restoration.
    /// </summary>
    public void Delete(string storyId)
    {
        EnsureId(storyId);
        lock (_lifecycleGate)
        {
            var plan = GetDeletionPlan(storyId);
            if (!plan.CanDelete)
                throw Failure("story.lifecycle.delete.blocked",
                    $"Canonical Story '{storyId}' cannot be deleted while blockers remain.");

            var snapshots = SnapshotFiles(plan);
            var changes = snapshots
                .Select(snapshot => new ProjectFileChange(
                    Path.GetRelativePath(_store.ProjectDirectory, snapshot.Path),
                    snapshot.Bytes,
                    null))
                .ToList();
            var graphPath = _store.StoryLogicGraph.Path;
            if (File.Exists(graphPath))
            {
                var before = File.ReadAllBytes(graphPath);
                var graph = _store.StoryLogicGraph.Load();
                var remaining = graph.Connections.Where(edge => edge.SourceStoryId != storyId
                    && edge.TargetStoryId != storyId).ToArray();
                if (remaining.Length != graph.Connections.Count)
                    changes.Add(new ProjectFileChange(Path.GetRelativePath(_store.ProjectDirectory, graphPath),
                        before, System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(
                            new CanonicalStoryLogicGraph(2, remaining),
                            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }))));
            }
            try
            {
                new ProjectFileTransaction(deleteFile: path =>
                    {
                        _deleteFile(path);
                        if (File.Exists(path)) throw new IOException($"File '{path}' remained after deletion.");
                    })
                    .Apply(_store.ProjectDirectory, changes, () => _store.StoryLogicGraph.Load());
            }
            catch (ProjectFileTransactionException exception) when (IsOrdinaryFailure(exception))
            {
                if (exception.RollbackFailures.Count != 0)
                    throw Failure("story.lifecycle.rollback_failed",
                        $"Canonical Story '{storyId}' deletion failed and rollback was incomplete.", exception);
                throw Failure("story.lifecycle.delete_failed",
                    $"Canonical Story '{storyId}' deletion failed; all resource bytes were restored.", exception);
            }
            catch (Exception exception) when (IsOrdinaryFailure(exception))
            {
                throw Failure("story.lifecycle.delete_failed",
                    $"Canonical Story '{storyId}' deletion failed; all resource bytes were restored.", exception);
            }
        }
    }

    public void DeleteStory(string storyId) => Delete(storyId);
    public void DeleteCanonicalStory(string storyId) => Delete(storyId);

    private (GraphResourceEnvelope Story, CanonicalStoryMembershipManifest Membership) RequirePair(string storyId)
    {
        EnsureSupportedAuthorData();
        try
        {
            var story = _store.Stories.Load(storyId);
            var membership = _store.Memberships.Load(storyId);
            return (story, membership);
        }
        catch (GraphResourceRepositoryException exception)
        {
            throw Failure("story.lifecycle.target_story_invalid",
                $"Canonical Story '{storyId}' could not be strictly loaded.", exception);
        }
        catch (CanonicalStoryMembershipRepositoryException exception)
        {
            throw Failure("story.lifecycle.target_membership_invalid",
                $"Canonical membership for Story '{storyId}' could not be strictly loaded.", exception);
        }
    }

    private void EnsureSupportedAuthorData()
    {
        if (ProjectAuthorDataBoundary.FindRetiredDirectory(_store.ProjectDirectory) is { } directory)
            throw Failure("story.lifecycle.project.unsupported",
                $"Retired author directory '{directory}' contains unsupported data; no files were changed.");
    }

    private void ValidateOwnedFiles(
        string storyId,
        CanonicalStoryMembershipSet owned,
        ICollection<CanonicalStoryDeletionBlocker> blockers)
    {
        foreach (var id in owned.Actors.Order(StringComparer.Ordinal))
        {
            try { _ = _actors.LoadActor(id); }
            catch (Exception exception) when (exception is ActorRepositoryException or ActorDataException)
            { AddBlocker(blockers, "story.lifecycle.owned_file.invalid", $"Owned Actor '{id}' is missing or invalid.", storyId, "actor", id); }
        }
        ValidateOwnedGraphFiles(storyId, owned.Sessions, _store.Sessions, "session", blockers);
        ValidateOwnedGraphFiles(storyId, owned.Tasks, _store.Tasks, "task", blockers);
        ValidateOwnedItemFiles(storyId, owned.Items, item => _items.LoadItem(item), "item", blockers);
        ValidateOwnedItemFiles(storyId, owned.ItemGroups, group => _items.LoadGroup(group), "item_group", blockers);
    }

    private static void ValidateOwnedItemFiles<T>(
        string storyId,
        IEnumerable<string> ids,
        Func<string, T> load,
        string kind,
        ICollection<CanonicalStoryDeletionBlocker> blockers)
    {
        foreach (var id in ids.Order(StringComparer.Ordinal))
        {
            try { _ = load(id); }
            catch (Exception exception) when (exception is ItemRepositoryException or ItemValidationException or ItemDataException)
            { AddBlocker(blockers, "story.lifecycle.owned_file.invalid", $"Owned {kind} '{id}' is missing or invalid.", storyId, kind, id); }
        }
    }

    private static void ValidateOwnedGraphFiles(
        string storyId,
        IEnumerable<string> ids,
        GraphResourceRepository repository,
        string kind,
        ICollection<CanonicalStoryDeletionBlocker> blockers)
    {
        foreach (var id in ids.Order(StringComparer.Ordinal))
        {
            try { _ = repository.Load(id); }
            catch (GraphResourceRepositoryException exception)
            { AddBlocker(blockers, "story.lifecycle.owned_file.invalid", $"Owned {kind} '{id}' is missing or invalid.", storyId, kind, id, exception); }
        }
    }

    private void ValidateOtherCanonicalMemberships(
        string ownerId,
        CanonicalStoryMembershipSet owned,
        ICollection<CanonicalStoryDeletionBlocker> blockers)
    {
        foreach (var path in EnumerateJsonPaths(_store.MembershipsDirectory, "story.lifecycle.membership.enumerate_failed"))
        {
            var id = LogicalId(path, membership: true);
            if (string.Equals(id, ownerId, StringComparison.Ordinal)) continue;
            CanonicalStoryMembershipManifest manifest;
            try { manifest = _store.Memberships.Load(id); }
            catch (CanonicalStoryMembershipRepositoryException exception)
            {
                AddBlocker(blockers, "story.lifecycle.membership.invalid",
                    $"Canonical membership '{id}' is malformed and could hide an owned-resource blocker.", id, inner: exception);
                continue;
            }
            AddMembershipConflicts(ownerId, owned.Actors, manifest.OwnedResources.Actors, manifest.ReferencedResources.Actors, "actor", id, blockers);
            AddMembershipConflicts(ownerId, owned.Items, manifest.OwnedResources.Items, manifest.ReferencedResources.Items, "item", id, blockers);
            AddMembershipConflicts(ownerId, owned.ItemGroups, manifest.OwnedResources.ItemGroups, manifest.ReferencedResources.ItemGroups, "item_group", id, blockers);
            AddMembershipConflicts(ownerId, owned.Sessions, manifest.OwnedResources.Sessions, manifest.ReferencedResources.Sessions, "session", id, blockers);
            AddMembershipConflicts(ownerId, owned.Tasks, manifest.OwnedResources.Tasks, manifest.ReferencedResources.Tasks, "task", id, blockers);
        }
    }

    private static void AddMembershipConflicts(
        string ownerId,
        IEnumerable<string> owned,
        IEnumerable<string> otherOwned,
        IEnumerable<string> otherReferenced,
        string kind,
        string otherStoryId,
        ICollection<CanonicalStoryDeletionBlocker> blockers)
    {
        foreach (var id in owned.Intersect(otherOwned.Concat(otherReferenced), StringComparer.Ordinal).Order(StringComparer.Ordinal))
            AddBlocker(blockers, "story.lifecycle.owned_resource.in_use",
                $"Owned {kind} '{id}' is listed by canonical Story '{otherStoryId}'.", otherStoryId, kind, id);
    }

    private void ValidateCanonicalStoryGraphs(
        string targetId,
        ICollection<CanonicalStoryDeletionIncomingTransition> transitions,
        ICollection<CanonicalStoryDeletionBlocker> blockers)
    {
        var storyPaths = EnumerateJsonPaths(_store.StoriesDirectory, "story.lifecycle.story.enumerate_failed");
        var membershipPaths = EnumerateJsonPaths(_store.MembershipsDirectory, "story.lifecycle.membership.enumerate_failed");
        var ids = storyPaths.Select(path => LogicalId(path, membership: false))
            .Concat(membershipPaths.Select(path => LogicalId(path, membership: true)))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        foreach (var id in ids)
        {
            GraphResourceEnvelope? story = null;
            try { story = _store.Stories.Load(id); }
            catch (GraphResourceRepositoryException exception)
            {
                AddBlocker(blockers, "story.lifecycle.incoming_graph.invalid",
                    $"Canonical Story '{id}' is malformed and could hide an incoming transition.", id, inner: exception);
                continue;
            }
            try { _ = _store.Memberships.Load(id); }
            catch (CanonicalStoryMembershipRepositoryException exception)
            {
                AddBlocker(blockers, "story.lifecycle.incoming_graph.incomplete",
                    $"Canonical Story '{id}' has no valid membership and could hide an incoming transition.", id, inner: exception);
                continue;
            }

            var graph = story.Graph!;
            var scopeIssues = GraphScopePolicy.Validate(graph, GraphScope.StoryFlow)
                .Where(issue => issue.Severity == DarkGreyRPG.Studio.Core.Validation.ValidationSeverity.Error)
                .ToArray();
            if (scopeIssues.Length != 0)
            {
                AddBlocker(blockers, "story.lifecycle.incoming_graph.invalid",
                    $"Canonical Story '{id}' has an invalid graph and could hide an incoming transition.", id);
            }

        }
    }

    private IReadOnlyList<FileSnapshot> SnapshotFiles(CanonicalStoryDeletionPlan plan)
    {
        var snapshots = new List<FileSnapshot>();
        AddSnapshot(snapshots, _store.Stories.GetPath(plan.StoryId), id => _store.Stories.Load(id), plan.StoryId);
        AddSnapshot(snapshots, _store.Memberships.GetPath(plan.StoryId), id => _store.Memberships.Load(id), plan.StoryId);
        foreach (var id in plan.ActorIds)
            AddSnapshot(snapshots, _actors.GetActorPath(id), id => _actors.LoadActor(id), id);
        foreach (var id in plan.ItemIds)
            AddSnapshot(snapshots, _items.GetItemPath(id), id => _items.LoadItem(id), id);
        foreach (var id in plan.ItemGroupIds)
            AddSnapshot(snapshots, _items.GetGroupPath(id), id => _items.LoadGroup(id), id);
        foreach (var id in plan.SessionIds)
            AddSnapshot(snapshots, _store.Sessions.GetPath(id), id => _store.Sessions.Load(id), id);
        foreach (var id in plan.TaskIds)
            AddSnapshot(snapshots, _store.Tasks.GetPath(id), id => _store.Tasks.Load(id), id);
        return snapshots;
    }

    private static void AddSnapshot<T>(ICollection<FileSnapshot> snapshots, string path, Func<string, T> validate, string id)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw Failure("story.lifecycle.snapshot_failed", $"Could not snapshot '{path}'.", exception); }
        try { _ = validate(id); }
        catch (Exception exception) { throw Failure("story.lifecycle.snapshot_failed", $"Could not validate '{path}'.", exception); }
        snapshots.Add(new FileSnapshot(path, bytes, id, value => _ = validate(value)));
    }

    private static IReadOnlyList<Exception> RestoreSnapshots(IEnumerable<FileSnapshot> snapshots)
    {
        var failures = new List<Exception>();
        foreach (var snapshot in snapshots)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(snapshot.Path)!);
                File.WriteAllBytes(snapshot.Path, snapshot.Bytes);
                snapshot.Validate(snapshot.Id);
                if (!File.ReadAllBytes(snapshot.Path).SequenceEqual(snapshot.Bytes))
                    throw new IOException($"Restored bytes for '{snapshot.Path}' changed.");
            }
            catch (Exception exception) { failures.Add(exception); }
        }
        return failures;
    }

    private static IReadOnlyList<string> EnumerateJsonPaths(string directory, string code)
    {
        if (!Directory.Exists(directory)) return [];
        try { return CanonicalResourceFileSystem.EnumerateJsonFiles(directory); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw Failure(code, $"Could not enumerate canonical directory '{directory}'.", exception); }
    }

    private static void AddBlocker(
        ICollection<CanonicalStoryDeletionBlocker> blockers,
        string code,
        string message,
        string? storyId = null,
        string? resourceKind = null,
        string? resourceId = null,
        Exception? inner = null,
        string? nodeId = null)
        => blockers.Add(new(code, message, storyId, resourceKind, resourceId, nodeId));

    private static void EnsureId(string id)
    {
        if (!StoryUid.IsValid(id))
            throw Failure("story.lifecycle.id.invalid", $"Story ID '{id}' must be a current-format Story UID.");
    }

    private static string LogicalId(string path, bool membership)
    {
        try
        {
            return membership
                ? CanonicalStoryMembershipSerializer.Deserialize(File.ReadAllText(path)).StoryId
                : GraphResourceEnvelopeSerializer.Deserialize(File.ReadAllText(path)).Id;
        }
        catch (Exception exception) when (exception is GraphResourceEnvelopeException
            or CanonicalStoryMembershipException
            or IOException or UnauthorizedAccessException)
        {
            return Path.GetFileNameWithoutExtension(path) ?? string.Empty;
        }
    }

    private static void EnsureDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw Failure("story.lifecycle.display_name.required", "Story display name is required.");
    }

    private static bool IsPersistenceFailure(Exception exception)
        => exception is GraphResourceRepositoryException or CanonicalStoryMembershipRepositoryException
            or IOException or UnauthorizedAccessException;

    private static bool IsOrdinaryFailure(Exception exception)
        => exception is not (OutOfMemoryException or StackOverflowException or AccessViolationException
            or ThreadAbortException);

    private static CanonicalStoryLifecycleException Failure(string code, string message, Exception? inner = null)
        => new(code, message, inner);

    private sealed record FileSnapshot(string Path, byte[] Bytes, string Id, Action<string> Validate);
}
