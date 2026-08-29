using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.IO;
using System.Text;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Stable failure boundary for canonical Story resource lifecycle commands.</summary>
public sealed class CanonicalStoryResourceLifecycleException : Exception
{
    public CanonicalStoryResourceLifecycleException(string code, string message, Exception? innerException = null)
        : base($"{code}: {message}", innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>A canonical resource membership which prevents an owned resource from being deleted.</summary>
public sealed record CanonicalStoryResourceReference(string StoryId, GraphResourceKind ResourceKind, string ResourceId);

/// <summary>Read-only deletion decision for one owned Session or Task.</summary>
public sealed record CanonicalStoryResourceDeletionPlan(
    string StoryId,
    GraphResourceKind ResourceKind,
    string ResourceId,
    IReadOnlyList<CanonicalStoryResourceReference> References)
{
    public IReadOnlyList<CanonicalStoryResourceReference> Blockers => References;
    public IReadOnlyList<string> ReferencingStoryIds => References.Select(reference => reference.StoryId).ToArray();
    public bool CanDelete => References.Count == 0;
}

/// <summary>
/// Core-only transactions for canonical Session and Task resources and their
/// Story membership. The service deliberately knows no UI, legacy roots, or
/// crash-durable multi-file transaction protocol.
/// </summary>
public sealed class CanonicalStoryResourceLifecycleService
{
    private readonly CanonicalProjectGraphStore _store;
    private readonly object _lifecycleGate = new();

    public CanonicalStoryResourceLifecycleService(CanonicalProjectGraphStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public CanonicalProjectGraphStore Store => _store;

    /// <summary>Creates a blank, scope-valid Session or Task and owns it in one Story.</summary>
    public GraphResourceEnvelope CreateOwned(
        string storyId,
        GraphResourceKind resourceKind,
        string resourceId,
        string displayName)
    {
        var scope = ScopeFor(resourceKind);
        lock (_lifecycleGate)
        {
            var (story, membership) = RequireStory(storyId);
            EnsureResourceId(resourceId);
            EnsureDisplayName(displayName);
            var members = Members(membership, resourceKind);
            if (members.Owned.Contains(resourceId, StringComparer.Ordinal)
                || members.Referenced.Contains(resourceId, StringComparer.Ordinal))
                throw Failure("story.resource.membership.collision",
                    $"Resource '{resourceId}' is already present in Story '{storyId}' membership.");

            var graph = BlankGraph(scope);
            var envelope = new GraphResourceEnvelope(resourceKind, resourceId, displayName, graph);
            var repository = Repository(resourceKind);
            GraphResourceEnvelope created;
            try
            {
                created = repository.Create(envelope);
            }
            catch (GraphResourceRepositoryException exception)
            {
                throw TranslateRepository(exception, "create", resourceKind, resourceId);
            }

            var updated = AddOwned(membership, resourceKind, resourceId);
            try
            {
                _store.Memberships.Replace(updated);
            }
            catch (Exception exception) when (exception is CanonicalStoryMembershipRepositoryException or IOException or UnauthorizedAccessException)
            {
                TryDeleteCreated(repository, resourceId, exception, resourceKind);
                throw Failure("story.resource.lifecycle.membership_replace_failed",
                    $"Could not record owned {KindText(resourceKind)} '{resourceId}' in Story '{storyId}'.", exception);
            }

            return created;
        }
    }

    public GraphResourceEnvelope CreateOwned(string storyId, string resourceKind, string resourceId, string displayName)
        => CreateOwned(storyId, GraphResourceEnvelopeSerializer.ParseResourceKind(resourceKind), resourceId, displayName);

    public GraphResourceEnvelope CreateOwnedSession(string storyId, string resourceId, string displayName)
        => CreateOwned(storyId, GraphResourceKind.Session, resourceId, displayName);

    public GraphResourceEnvelope CreateOwnedTask(string storyId, string resourceId, string displayName)
        => CreateOwned(storyId, GraphResourceKind.Task, resourceId, displayName);

    /// <summary>Adds one existing resource to another Story's referenced list.</summary>
    public void AddReference(string storyId, GraphResourceKind resourceKind, string resourceId)
    {
        var repository = Repository(resourceKind);
        lock (_lifecycleGate)
        {
            var (_, membership) = RequireStory(storyId);
            EnsureResourceId(resourceId);
            try
            {
                _ = repository.Load(resourceId);
            }
            catch (GraphResourceRepositoryException exception)
            {
                throw TranslateRepository(exception, "reference", resourceKind, resourceId);
            }

            var members = Members(membership, resourceKind);
            if (members.Owned.Contains(resourceId, StringComparer.Ordinal))
                throw Failure("story.resource.reference.owned",
                    $"Resource '{resourceId}' is owned by Story '{storyId}' and cannot be referenced there.");
            if (members.Referenced.Contains(resourceId, StringComparer.Ordinal))
                throw Failure("story.resource.reference.duplicate",
                    $"Resource '{resourceId}' is already referenced by Story '{storyId}'.");

            var updated = AddReferenced(membership, resourceKind, resourceId);
            ReplaceMembership(updated, "reference", storyId, resourceKind, resourceId);
        }
    }

    public void AddReference(string storyId, string resourceKind, string resourceId)
        => AddReference(storyId, GraphResourceEnvelopeSerializer.ParseResourceKind(resourceKind), resourceId);

    public void AddSessionReference(string storyId, string resourceId)
        => AddReference(storyId, GraphResourceKind.Session, resourceId);

    public void AddTaskReference(string storyId, string resourceId)
        => AddReference(storyId, GraphResourceKind.Task, resourceId);

    /// <summary>Removes an existing referenced membership without touching its resource.</summary>
    public void RemoveReference(string storyId, GraphResourceKind resourceKind, string resourceId)
    {
        _ = ScopeFor(resourceKind);
        lock (_lifecycleGate)
        {
            var (_, membership) = RequireStory(storyId);
            EnsureResourceId(resourceId);
            var members = Members(membership, resourceKind);
            if (members.Owned.Contains(resourceId, StringComparer.Ordinal))
                throw Failure("story.resource.reference.owned",
                    $"Resource '{resourceId}' is owned by Story '{storyId}', not a removable reference.");
            if (!members.Referenced.Contains(resourceId, StringComparer.Ordinal))
                throw Failure("story.resource.reference.not_found",
                    $"Referenced {KindText(resourceKind)} '{resourceId}' was not found in Story '{storyId}'.");

            var updated = RemoveReferenced(membership, resourceKind, resourceId);
            ReplaceMembership(updated, "remove_reference", storyId, resourceKind, resourceId);
        }
    }

    public void RemoveReference(string storyId, string resourceKind, string resourceId)
        => RemoveReference(storyId, GraphResourceEnvelopeSerializer.ParseResourceKind(resourceKind), resourceId);

    public void RemoveSessionReference(string storyId, string resourceId)
        => RemoveReference(storyId, GraphResourceKind.Session, resourceId);

    public void RemoveTaskReference(string storyId, string resourceId)
        => RemoveReference(storyId, GraphResourceKind.Task, resourceId);

    /// <summary>Lists all other Stories which reference the resource.</summary>
    public IReadOnlyList<CanonicalStoryResourceReference> EnumerateReferencingStories(
        string ownerStoryId,
        GraphResourceKind resourceKind,
        string resourceId)
    {
        _ = ScopeFor(resourceKind);
        lock (_lifecycleGate)
        {
            var (_, ownerMembership) = RequireStory(ownerStoryId);
            EnsureResourceId(resourceId);
            EnsureOwned(ownerMembership, ownerStoryId, resourceKind, resourceId);
            EnsureResourceExists(resourceKind, resourceId);
            return EnumerateReferences(ownerStoryId, resourceKind, resourceId);
        }
    }

    public IReadOnlyList<CanonicalStoryResourceReference> EnumerateOtherStoryReferences(
        string ownerStoryId,
        GraphResourceKind resourceKind,
        string resourceId)
        => EnumerateReferencingStories(ownerStoryId, resourceKind, resourceId);

    public IReadOnlyList<string> GetReferencingStoryIds(
        string ownerStoryId,
        GraphResourceKind resourceKind,
        string resourceId)
        => EnumerateReferencingStories(ownerStoryId, resourceKind, resourceId)
            .Select(reference => reference.StoryId).ToArray();

    public IReadOnlyList<string> EnumerateReferencingStoryIds(
        string ownerStoryId,
        GraphResourceKind resourceKind,
        string resourceId)
        => GetReferencingStoryIds(ownerStoryId, resourceKind, resourceId);

    /// <summary>Builds a deletion plan; it never changes resource or membership files.</summary>
    public CanonicalStoryResourceDeletionPlan GetDeletionPlan(
        string ownerStoryId,
        GraphResourceKind resourceKind,
        string resourceId)
    {
        var references = EnumerateReferencingStories(ownerStoryId, resourceKind, resourceId);
        return new CanonicalStoryResourceDeletionPlan(ownerStoryId, resourceKind, resourceId, references);
    }

    public CanonicalStoryResourceDeletionPlan GetDeletionPlan(
        string ownerStoryId,
        string resourceKind,
        string resourceId)
        => GetDeletionPlan(ownerStoryId, GraphResourceEnvelopeSerializer.ParseResourceKind(resourceKind), resourceId);

    public IReadOnlyList<string> GetDeletionBlockers(
        string ownerStoryId,
        GraphResourceKind resourceKind,
        string resourceId)
        => GetDeletionPlan(ownerStoryId, resourceKind, resourceId).ReferencingStoryIds;

    public bool CanDeleteOwned(string ownerStoryId, GraphResourceKind resourceKind, string resourceId)
        => GetDeletionPlan(ownerStoryId, resourceKind, resourceId).CanDelete;

    /// <summary>
    /// Deletes an owned resource and its owner membership only when no other
    /// Story references it. A membership failure compensates the resource
    /// delete using the detached pre-delete envelope.
    /// </summary>
    public void DeleteOwned(string ownerStoryId, GraphResourceKind resourceKind, string resourceId)
    {
        var repository = Repository(resourceKind);
        lock (_lifecycleGate)
        {
            var (_, membership) = RequireStory(ownerStoryId);
            EnsureResourceId(resourceId);
            EnsureOwned(membership, ownerStoryId, resourceKind, resourceId);
            GraphResourceEnvelope snapshot;
            string resourcePath;
            byte[] resourceBytes;
            try
            {
                snapshot = repository.Load(resourceId);
                resourcePath = repository.GetPath(resourceId);
                resourceBytes = File.ReadAllBytes(resourcePath);
            }
            catch (GraphResourceRepositoryException exception)
            {
                throw TranslateRepository(exception, "delete", resourceKind, resourceId);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw Failure("story.resource.lifecycle.resource_read_failed",
                    $"Could not snapshot {KindText(resourceKind)} '{resourceId}' before deletion.", exception);
            }

            var blockers = EnumerateReferences(ownerStoryId, resourceKind, resourceId);
            if (blockers.Count != 0)
                throw Failure("story.resource.delete.blocked",
                    $"Owned {KindText(resourceKind)} '{resourceId}' is still referenced by: {string.Join(", ", blockers.Select(item => item.StoryId))}.");

            var updated = RemoveOwned(membership, resourceKind, resourceId);
            try
            {
                repository.Delete(resourceId);
            }
            catch (GraphResourceRepositoryException exception)
            {
                throw TranslateRepository(exception, "delete", resourceKind, resourceId);
            }

            try
            {
                _store.Memberships.Replace(updated);
            }
            catch (Exception exception) when (exception is CanonicalStoryMembershipRepositoryException or IOException or UnauthorizedAccessException)
            {
                try
                {
                    RestoreExactResource(resourcePath, resourceBytes, snapshot);
                }
                catch (Exception restoreException)
                {
                    throw Failure("story.resource.lifecycle.rollback_failed",
                        $"Membership update failed and deleted {KindText(resourceKind)} '{resourceId}' could not be restored.",
                        new AggregateException(exception, restoreException));
                }

                throw Failure("story.resource.lifecycle.membership_replace_failed",
                    $"Could not remove owned {KindText(resourceKind)} '{resourceId}' from Story '{ownerStoryId}'.", exception);
            }
        }
    }

    public void DeleteOwned(string ownerStoryId, string resourceKind, string resourceId)
        => DeleteOwned(ownerStoryId, GraphResourceEnvelopeSerializer.ParseResourceKind(resourceKind), resourceId);

    public void DeleteOwnedSession(string ownerStoryId, string resourceId)
        => DeleteOwned(ownerStoryId, GraphResourceKind.Session, resourceId);

    public void DeleteOwnedTask(string ownerStoryId, string resourceId)
        => DeleteOwned(ownerStoryId, GraphResourceKind.Task, resourceId);

    private (GraphResourceEnvelope Story, CanonicalStoryMembershipManifest Membership) RequireStory(string storyId)
    {
        try
        {
            var story = _store.Stories.Load(storyId);
            var membership = _store.Memberships.Load(storyId);
            return (story, membership);
        }
        catch (GraphResourceRepositoryException exception)
        {
            throw Failure(exception.Code == "graph.resource.repository.not_found"
                    ? "story.resource.story.not_found"
                    : "story.resource.story.load_failed",
                $"Canonical Story '{storyId}' could not be loaded.", exception);
        }
        catch (CanonicalStoryMembershipRepositoryException exception)
        {
            throw Failure(exception.Code == "story.membership.repository.not_found"
                    ? "story.resource.membership.not_found"
                    : "story.resource.membership.load_failed",
                $"Canonical membership for Story '{storyId}' could not be loaded.", exception);
        }
    }

    private IReadOnlyList<CanonicalStoryResourceReference> EnumerateReferences(
        string ownerStoryId,
        GraphResourceKind resourceKind,
        string resourceId)
    {
        var result = new List<CanonicalStoryResourceReference>();
        IReadOnlyList<CanonicalStoryMembershipInfo> memberships;
        try
        {
            memberships = _store.Memberships.List();
        }
        catch (CanonicalStoryMembershipRepositoryException exception)
        {
            throw Failure(
                "story.resource.membership.load_failed",
                "Canonical Story memberships could not be enumerated.",
                exception);
        }

        foreach (var info in memberships)
        {
            if (string.Equals(info.StoryId, ownerStoryId, StringComparison.Ordinal))
                continue;
            CanonicalStoryMembershipManifest manifest;
            try
            {
                manifest = _store.Memberships.Load(info.StoryId);
            }
            catch (CanonicalStoryMembershipRepositoryException exception)
            {
                throw Failure("story.resource.membership.load_failed",
                    $"Canonical membership for Story '{info.StoryId}' could not be loaded.", exception);
            }

            var members = Members(manifest, resourceKind);
            if (members.Referenced.Contains(resourceId, StringComparer.Ordinal)
                || members.Owned.Contains(resourceId, StringComparer.Ordinal))
                result.Add(new(info.StoryId, resourceKind, resourceId));
        }

        return result;
    }

    private void EnsureOwned(CanonicalStoryMembershipManifest membership, string storyId, GraphResourceKind kind, string resourceId)
    {
        var members = Members(membership, kind);
        if (members.Referenced.Contains(resourceId, StringComparer.Ordinal))
            throw Failure("story.resource.delete.referenced",
                $"Resource '{resourceId}' is referenced by Story '{storyId}' and is not owned there.");
        if (!members.Owned.Contains(resourceId, StringComparer.Ordinal))
            throw Failure("story.resource.ownership.required",
                $"Resource '{resourceId}' is not owned by Story '{storyId}'.");
    }

    private void EnsureResourceExists(GraphResourceKind kind, string id)
    {
        try { _ = Repository(kind).Load(id); }
        catch (GraphResourceRepositoryException exception) { throw TranslateRepository(exception, "inspect", kind, id); }
    }

    private static void RestoreExactResource(string path, byte[] bytes, GraphResourceEnvelope expected)
    {
        var originalJson = Encoding.UTF8.GetString(bytes);
        new AtomicFileWriter().Write(path, originalJson, temporaryPath =>
        {
            var restored = GraphResourceEnvelopeSerializer.Deserialize(File.ReadAllText(temporaryPath));
            if (restored.ResourceKind != expected.ResourceKind
                || !string.Equals(restored.Id, expected.Id, StringComparison.Ordinal))
                throw new GraphResourceRepositoryException(
                    "graph.resource.repository.staged_id.changed",
                    "The restored canonical resource identity changed during rollback.");
        });
    }

    private void ReplaceMembership(CanonicalStoryMembershipManifest updated, string operation, string storyId, GraphResourceKind kind, string id)
    {
        try { _store.Memberships.Replace(updated); }
        catch (CanonicalStoryMembershipRepositoryException exception)
        {
            throw Failure("story.resource.lifecycle.membership_replace_failed",
                $"Could not {operation} {KindText(kind)} '{id}' in Story '{storyId}'.", exception);
        }
    }

    private static GraphDocument BlankGraph(GraphScope scope)
    {
        GraphNode[] nodes = scope switch
        {
            GraphScope.Session => [GraphNodeFactory.Create(scope, "start", "start")],
            GraphScope.Task => [CreateInitialTaskSettleNode()],
            _ => throw Failure("story.resource.kind.unsupported", "Story resources cannot be created by this service."),
        };
        var graph = new GraphDocument(nodes);
        if (!GraphScopePolicy.IsValid(graph, scope))
            throw Failure("story.resource.graph.invalid", $"Blank {scope} graph did not pass scope validation.");
        return graph;
    }

    private static GraphNode CreateInitialTaskSettleNode()
    {
        var result = new GraphNodeAuthoringService().Create(
            new GraphDocument(),
            GraphScope.Task,
            "settle",
            "settle");
        if (!result.IsSuccess)
        {
            var issue = result.Issues.FirstOrDefault();
            throw Failure("story.resource.graph.invalid",
                issue?.Message ?? "Task settlement node could not be initialized.");
        }

        return result.Candidate!;
    }

    private static GraphScope ScopeFor(GraphResourceKind kind)
        => kind switch
        {
            GraphResourceKind.Session => GraphScope.Session,
            GraphResourceKind.Task => GraphScope.Task,
            GraphResourceKind.Story => throw Failure("story.resource.kind.unsupported", "Story resources are not managed by this service."),
            _ => throw Failure("story.resource.kind.unsupported", $"Resource kind '{kind}' is not managed by this service."),
        };

    private GraphResourceRepository Repository(GraphResourceKind kind)
    {
        _ = ScopeFor(kind);
        return kind == GraphResourceKind.Session ? _store.Sessions : _store.Tasks;
    }

    private static (List<string> Owned, List<string> Referenced) Members(CanonicalStoryMembershipManifest manifest, GraphResourceKind kind)
    {
        var owned = manifest.OwnedResources;
        var referenced = manifest.ReferencedResources;
        return kind switch
        {
            GraphResourceKind.Session => (owned.Sessions, referenced.Sessions),
            GraphResourceKind.Task => (owned.Tasks, referenced.Tasks),
            GraphResourceKind.Story => throw Failure("story.resource.kind.unsupported", "Story membership is not managed by this service."),
            _ => throw Failure("story.resource.kind.unsupported", $"Resource kind '{kind}' is not managed by this service."),
        };
    }

    private static CanonicalStoryMembershipManifest AddOwned(CanonicalStoryMembershipManifest source, GraphResourceKind kind, string id)
    {
        var owned = source.OwnedResources;
        var referenced = source.ReferencedResources;
        (kind == GraphResourceKind.Session ? owned.Sessions : owned.Tasks).Add(id);
        return NewManifest(source, owned, referenced);
    }

    private static CanonicalStoryMembershipManifest AddReferenced(CanonicalStoryMembershipManifest source, GraphResourceKind kind, string id)
    {
        var owned = source.OwnedResources;
        var referenced = source.ReferencedResources;
        (kind == GraphResourceKind.Session ? referenced.Sessions : referenced.Tasks).Add(id);
        return NewManifest(source, owned, referenced);
    }

    private static CanonicalStoryMembershipManifest RemoveOwned(CanonicalStoryMembershipManifest source, GraphResourceKind kind, string id)
    {
        var owned = source.OwnedResources;
        var referenced = source.ReferencedResources;
        (kind == GraphResourceKind.Session ? owned.Sessions : owned.Tasks).Remove(id);
        return NewManifest(source, owned, referenced);
    }

    private static CanonicalStoryMembershipManifest RemoveReferenced(CanonicalStoryMembershipManifest source, GraphResourceKind kind, string id)
    {
        var owned = source.OwnedResources;
        var referenced = source.ReferencedResources;
        (kind == GraphResourceKind.Session ? referenced.Sessions : referenced.Tasks).Remove(id);
        return NewManifest(source, owned, referenced);
    }

    private static CanonicalStoryMembershipManifest NewManifest(
        CanonicalStoryMembershipManifest source,
        CanonicalStoryMembershipSet owned,
        CanonicalStoryMembershipSet referenced)
        => new(source.StoryId, owned, referenced) { SchemaVersion = source.SchemaVersion };

    private static void EnsureResourceId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw Failure("story.resource.id.invalid", "Canonical resource ID is required.");
        if (id.Any(character => !(char.IsLower(character) || char.IsDigit(character) || character is '_' or '-') ))
            throw Failure("story.resource.id.invalid", $"Canonical resource ID '{id}' is invalid.");
    }

    private static void EnsureDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw Failure("story.resource.display_name.required", "Canonical resource display name is required.");
    }

    private static void TryDeleteCreated(GraphResourceRepository repository, string id, Exception original, GraphResourceKind kind)
    {
        try { repository.Delete(id); }
        catch (Exception rollbackException)
        {
            throw Failure("story.resource.lifecycle.rollback_failed",
                $"Membership update failed and newly-created {KindText(kind)} '{id}' could not be removed.",
                new AggregateException(original, rollbackException));
        }
    }

    private static CanonicalStoryResourceLifecycleException TranslateRepository(
        GraphResourceRepositoryException exception,
        string operation,
        GraphResourceKind kind,
        string id)
    {
        var code = exception.Code switch
        {
            "graph.resource.repository.not_found" => "story.resource.resource.not_found",
            "graph.resource.repository.collision" => "story.resource.resource.collision",
            "graph.resource.repository.id.invalid" => "story.resource.id.invalid",
            _ => $"story.resource.lifecycle.resource_{operation}_failed",
        };
        return Failure(code, $"Could not {operation} {KindText(kind)} '{id}'.", exception);
    }

    private static string KindText(GraphResourceKind kind) => GraphResourceEnvelopeSerializer.FormatResourceKind(kind);

    private static CanonicalStoryResourceLifecycleException Failure(string code, string message, Exception? inner = null)
        => new(code, message, inner);
}
