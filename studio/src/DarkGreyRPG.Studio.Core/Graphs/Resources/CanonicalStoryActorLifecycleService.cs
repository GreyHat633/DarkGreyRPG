using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Stable failure boundary for canonical Story Actor lifecycle commands.</summary>
public sealed class CanonicalStoryActorLifecycleException : Exception
{
    public CanonicalStoryActorLifecycleException(string code, string message, Exception? innerException = null)
        : base($"{code}: {message}", innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public enum CanonicalStoryActorReferenceSource
{
    Canonical,
    Legacy,
}

public enum CanonicalStoryActorMembershipKind
{
    Owned,
    Referenced,
}

public enum CanonicalStoryActorKind
{
    Individual,
    Collective,
}

/// <summary>One canonical or legacy Story membership that blocks Actor deletion.</summary>
public sealed record CanonicalStoryActorReference(
    string StoryId,
    CanonicalStoryActorReferenceSource Source,
    CanonicalStoryActorMembershipKind MembershipKind,
    string ActorId)
{
    public CanonicalStoryActorReferenceSource SourceKind => Source;
    public CanonicalStoryActorMembershipKind Kind => MembershipKind;
    public CanonicalStoryActorMembershipKind Membership => MembershipKind;
    public bool IsCanonical => Source == CanonicalStoryActorReferenceSource.Canonical;
    public bool IsLegacy => Source == CanonicalStoryActorReferenceSource.Legacy;
    public bool IsOwned => MembershipKind == CanonicalStoryActorMembershipKind.Owned;
    public bool IsReferenced => MembershipKind == CanonicalStoryActorMembershipKind.Referenced;
}

/// <summary>Read-only deletion decision for one canonically-owned Actor.</summary>
public sealed record CanonicalStoryActorDeletionPlan(
    string StoryId,
    string ActorId,
    IReadOnlyList<CanonicalStoryActorReference> References)
{
    public IReadOnlyList<CanonicalStoryActorReference> Blockers => References;
    public IReadOnlyList<CanonicalStoryActorReference> CanonicalBlockers =>
        References.Where(reference => reference.IsCanonical).ToArray();
    public IReadOnlyList<CanonicalStoryActorReference> LegacyBlockers =>
        References.Where(reference => reference.IsLegacy).ToArray();
    public IReadOnlyList<string> ReferencingStoryIds =>
        References.Select(reference => reference.StoryId).Distinct(StringComparer.Ordinal).ToArray();
    public bool CanDelete => References.Count == 0;
}

/// <summary>
/// Canonical Actor lifecycle commands. Actor files remain in the shared project
/// root actors/ directory; canonical and legacy Story memberships are scanned
/// separately for deletion safety.
/// </summary>
public sealed class CanonicalStoryActorLifecycleService
{
    private readonly CanonicalProjectGraphStore _store;
    private readonly ActorRepository _actors;
    private readonly StoryRepository _legacyStories;
    private readonly object _lifecycleGate = new();

    public CanonicalStoryActorLifecycleService(CanonicalProjectGraphStore store)
        : this(store, null, null)
    {
    }

    public CanonicalStoryActorLifecycleService(
        CanonicalProjectGraphStore store,
        ActorRepository? actorRepository,
        StoryRepository? storyRepository)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _actors = actorRepository ?? new ActorRepository(store.ProjectDirectory);
        _legacyStories = storyRepository ?? new StoryRepository(store.ProjectDirectory);
    }

    public CanonicalStoryActorLifecycleService(
        CanonicalProjectGraphStore store,
        ActorRepository actorRepository)
        : this(store, actorRepository, null)
    {
    }

    public CanonicalProjectGraphStore Store => _store;
    public ActorRepository Actors => _actors;
    public StoryRepository LegacyStories => _legacyStories;

    /// <summary>Creates and persists an Actor owned by the canonical Story.</summary>
    public ActorDocument CreateOwned(string storyId, string actorId, string displayName)
        => CreateOwnedCore(storyId, actorId, displayName, kind: null, tags: null);

    /// <summary>Creates a schema-3 individual NPC or collective Group owned by the Story.</summary>
    public ActorDocument CreateOwned(
        string storyId,
        CanonicalStoryActorKind kind,
        string actorId,
        string displayName,
        IEnumerable<string>? tags = null)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported canonical Story actor kind.");
        return CreateOwnedCore(storyId, actorId, displayName, kind, tags);
    }

    private ActorDocument CreateOwnedCore(
        string storyId,
        string actorId,
        string displayName,
        CanonicalStoryActorKind? kind,
        IEnumerable<string>? tags)
    {
        lock (_lifecycleGate)
        {
            var (_, membership) = RequireCanonicalStory(storyId);
            EnsureActorId(actorId);
            EnsureDisplayName(displayName);
            var membershipPath = _store.Memberships.GetPath(storyId);
            byte[] membershipBytes;
            try
            {
                membershipBytes = File.ReadAllBytes(membershipPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw Failure("story.actor.lifecycle.membership_read_failed",
                    $"Could not snapshot canonical membership for Story '{storyId}'.", exception);
            }
            var members = membership.OwnedResources;
            var referenced = membership.ReferencedResources;
            if (members.Actors.Contains(actorId, StringComparer.Ordinal)
                || referenced.Actors.Contains(actorId, StringComparer.Ordinal))
            {
                throw Failure("story.actor.membership.collision",
                    $"Actor '{actorId}' is already present in canonical Story '{storyId}' membership.");
            }

            ActorDocument document;
            try
            {
                document = kind switch
                {
                    CanonicalStoryActorKind.Individual => _actors.CreateIndividual(actorId, displayName),
                    CanonicalStoryActorKind.Collective => _actors.CreateCollective(actorId, displayName),
                    _ => _actors.CreateActor(actorId, displayName),
                };
                if (tags is not null) document.SetTags(tags);
                document.HomeStoryId = storyId;
                document = _actors.SaveActor(document);
            }
            catch (ActorRepositoryException exception)
            {
                throw TranslateActor(exception, "create", actorId);
            }

            members.Actors.Add(actorId);
            var updated = NewManifest(membership, members, referenced);
            try
            {
                _store.Memberships.Replace(updated);
            }
            catch (Exception exception) when (IsMembershipFailure(exception))
            {
                TryRollbackCreatedActor(actorId, storyId, membershipPath, membershipBytes, exception);
                throw Failure("story.actor.lifecycle.membership_replace_failed",
                    $"Could not record owned Actor '{actorId}' in canonical Story '{storyId}'.", exception);
            }

            return document;
        }
    }

    public ActorDocument CreateOwnedActor(string storyId, string actorId, string displayName)
        => CreateOwned(storyId, actorId, displayName);

    /// <summary>Adds an existing Actor to a canonical Story's references.</summary>
    public void AddReference(string storyId, string actorId)
    {
        lock (_lifecycleGate)
        {
            var (_, membership) = RequireCanonicalStory(storyId);
            EnsureActorId(actorId);
            EnsureActorExists(actorId, "reference");
            var owned = membership.OwnedResources;
            var referenced = membership.ReferencedResources;
            if (owned.Actors.Contains(actorId, StringComparer.Ordinal))
                throw Failure("story.actor.reference.owned",
                    $"Actor '{actorId}' is owned by canonical Story '{storyId}' and cannot be referenced there.");
            if (referenced.Actors.Contains(actorId, StringComparer.Ordinal))
                throw Failure("story.actor.reference.duplicate",
                    $"Actor '{actorId}' is already referenced by canonical Story '{storyId}'.");

            referenced.Actors.Add(actorId);
            ReplaceMembership(
                NewManifest(membership, owned, referenced),
                "reference", storyId, actorId);
        }
    }

    public void AddActorReference(string storyId, string actorId) => AddReference(storyId, actorId);

    /// <summary>Removes a referenced membership even if the Actor file is missing.</summary>
    public void RemoveReference(string storyId, string actorId)
    {
        lock (_lifecycleGate)
        {
            var (_, membership) = RequireCanonicalStory(storyId);
            EnsureActorId(actorId);
            var owned = membership.OwnedResources;
            var referenced = membership.ReferencedResources;
            if (owned.Actors.Contains(actorId, StringComparer.Ordinal))
                throw Failure("story.actor.reference.owned",
                    $"Actor '{actorId}' is owned by canonical Story '{storyId}', not a removable reference.");
            if (!referenced.Actors.Contains(actorId, StringComparer.Ordinal))
                throw Failure("story.actor.reference.not_found",
                    $"Referenced Actor '{actorId}' was not found in canonical Story '{storyId}'.");

            referenced.Actors.Remove(actorId);
            ReplaceMembership(
                NewManifest(membership, owned, referenced),
                "remove reference", storyId, actorId);
        }
    }

    public void RemoveActorReference(string storyId, string actorId) => RemoveReference(storyId, actorId);

    /// <summary>Enumerates canonical and legacy memberships which block deletion.</summary>
    public IReadOnlyList<CanonicalStoryActorReference> EnumerateBlockers(string ownerStoryId, string actorId)
    {
        lock (_lifecycleGate)
        {
            var (_, ownerMembership) = RequireCanonicalStory(ownerStoryId);
            EnsureActorId(actorId);
            EnsureActorExists(actorId, "inspect");
            EnsureOwned(ownerMembership, ownerStoryId, actorId);
            return EnumerateBlockersCore(ownerStoryId, actorId);
        }
    }

    public IReadOnlyList<CanonicalStoryActorReference> EnumerateReferencingStories(string ownerStoryId, string actorId)
        => EnumerateBlockers(ownerStoryId, actorId);

    public IReadOnlyList<CanonicalStoryActorReference> EnumerateOtherStoryReferences(string ownerStoryId, string actorId)
        => EnumerateBlockers(ownerStoryId, actorId);

    public IReadOnlyList<string> GetReferencingStoryIds(string ownerStoryId, string actorId)
        => EnumerateBlockers(ownerStoryId, actorId)
            .Select(reference => reference.StoryId).Distinct(StringComparer.Ordinal).ToArray();

    /// <summary>Builds a deletion plan without changing Actor or membership files.</summary>
    public CanonicalStoryActorDeletionPlan GetDeletionPlan(string ownerStoryId, string actorId)
    {
        var blockers = EnumerateBlockers(ownerStoryId, actorId);
        return new CanonicalStoryActorDeletionPlan(ownerStoryId, actorId, blockers);
    }

    public IReadOnlyList<CanonicalStoryActorReference> GetDeletionBlockers(string ownerStoryId, string actorId)
        => GetDeletionPlan(ownerStoryId, actorId).Blockers;

    public bool CanDeleteOwned(string ownerStoryId, string actorId)
        => GetDeletionPlan(ownerStoryId, actorId).CanDelete;

    /// <summary>
    /// Deletes the Actor file first, then removes canonical ownership. If the
    /// membership replacement fails, exact Actor bytes are restored in-process.
    /// </summary>
    public void DeleteOwned(string ownerStoryId, string actorId)
    {
        lock (_lifecycleGate)
        {
            var (_, membership) = RequireCanonicalStory(ownerStoryId);
            EnsureActorId(actorId);
            EnsureOwned(membership, ownerStoryId, actorId);
            var actorPath = Path.Combine(_actors.ActorsDirectory, actorId + ".json");
            var membershipPath = _store.Memberships.GetPath(ownerStoryId);
            byte[] actorBytes;
            byte[] membershipBytes;
            try
            {
                _ = _actors.LoadActor(actorId);
                actorBytes = File.ReadAllBytes(actorPath);
                membershipBytes = File.ReadAllBytes(membershipPath);
            }
            catch (ActorRepositoryException exception)
            {
                throw TranslateActor(exception, "delete", actorId);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw Failure("story.actor.lifecycle.actor_read_failed",
                    $"Could not snapshot Actor '{actorId}' before deletion.", exception);
            }

            var blockers = EnumerateBlockersCore(ownerStoryId, actorId);
            if (blockers.Count != 0)
                throw Failure("story.actor.delete.blocked",
                    $"Owned Actor '{actorId}' is still present in: {string.Join(", ", blockers.Select(item => item.StoryId))}.");

            var owned = membership.OwnedResources;
            var referenced = membership.ReferencedResources;
            owned.Actors.Remove(actorId);
            var updated = NewManifest(membership, owned, referenced);
            try
            {
                _actors.DeleteActor(actorId);
            }
            catch (ActorRepositoryException exception)
            {
                throw TranslateActor(exception, "delete", actorId);
            }

            try
            {
                _store.Memberships.Replace(updated);
            }
            catch (Exception exception) when (IsMembershipFailure(exception))
            {
                try
                {
                    RestoreExactActor(actorPath, actorBytes, actorId);
                    RestoreExactMembership(membershipPath, membershipBytes, ownerStoryId);
                }
                catch (Exception restoreException)
                {
                    throw Failure("story.actor.lifecycle.rollback_failed",
                        $"Membership update failed and deleted Actor '{actorId}' could not be restored.",
                        new AggregateException(exception, restoreException));
                }

                throw Failure("story.actor.lifecycle.membership_replace_failed",
                    $"Could not remove owned Actor '{actorId}' from canonical Story '{ownerStoryId}'.", exception);
            }
        }
    }

    public void DeleteOwnedActor(string ownerStoryId, string actorId) => DeleteOwned(ownerStoryId, actorId);

    private (GraphResourceEnvelope Story, CanonicalStoryMembershipManifest Membership) RequireCanonicalStory(string storyId)
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
                    ? "story.actor.story.not_found"
                    : "story.actor.story.load_failed",
                $"Canonical Story '{storyId}' could not be loaded.", exception);
        }
        catch (CanonicalStoryMembershipRepositoryException exception)
        {
            throw Failure(exception.Code == "story.membership.repository.not_found"
                    ? "story.actor.membership.not_found"
                    : "story.actor.membership.load_failed",
                $"Canonical membership for Story '{storyId}' could not be loaded.", exception);
        }
    }

    private IReadOnlyList<CanonicalStoryActorReference> EnumerateBlockersCore(string ownerStoryId, string actorId)
    {
        var result = new List<CanonicalStoryActorReference>();
        IReadOnlyList<CanonicalStoryMembershipInfo> memberships;
        try
        {
            memberships = _store.Memberships.List();
        }
        catch (CanonicalStoryMembershipRepositoryException exception)
        {
            throw Failure("story.actor.membership.load_failed",
                "Canonical Story memberships could not be enumerated.", exception);
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
                throw Failure("story.actor.membership.load_failed",
                    $"Canonical membership for Story '{info.StoryId}' could not be loaded.", exception);
            }

            var owned = manifest.OwnedResources.Actors;
            var referenced = manifest.ReferencedResources.Actors;
            if (owned.Contains(actorId, StringComparer.Ordinal))
                result.Add(new(info.StoryId, CanonicalStoryActorReferenceSource.Canonical,
                    CanonicalStoryActorMembershipKind.Owned, actorId));
            if (referenced.Contains(actorId, StringComparer.Ordinal))
                result.Add(new(info.StoryId, CanonicalStoryActorReferenceSource.Canonical,
                    CanonicalStoryActorMembershipKind.Referenced, actorId));
        }

        IReadOnlyList<StoryResource> legacyStories;
        try
        {
            legacyStories = _legacyStories.ListStories();
        }
        catch (Exception exception) when (exception is StoryDataException or IOException or UnauthorizedAccessException)
        {
            throw Failure("story.actor.legacy_story.load_failed",
                "Legacy Story JSON could not be enumerated.", exception);
        }

        foreach (var story in legacyStories)
        {
            if (!string.Equals(story.Id, ownerStoryId, StringComparison.Ordinal)
                && story.OwnedResources.Actors.Contains(actorId, StringComparer.Ordinal))
                result.Add(new(story.Id, CanonicalStoryActorReferenceSource.Legacy,
                    CanonicalStoryActorMembershipKind.Owned, actorId));
            if (story.ReferencedResources.Actors.Contains(actorId, StringComparer.Ordinal))
                result.Add(new(story.Id, CanonicalStoryActorReferenceSource.Legacy,
                    CanonicalStoryActorMembershipKind.Referenced, actorId));
        }

        return result;
    }

    private void EnsureActorExists(string actorId, string operation)
    {
        try
        {
            _ = _actors.LoadActor(actorId);
        }
        catch (ActorNotFoundException exception)
        {
            throw Failure("story.actor.actor.not_found",
                $"Could not {operation} Actor '{actorId}' because its file was not found.", exception);
        }
        catch (ActorDataException exception)
        {
            throw Failure("story.actor.actor.load_failed",
                $"Could not {operation} Actor '{actorId}'.", exception);
        }
        catch (ActorRepositoryException exception)
        {
            throw TranslateActor(exception, operation, actorId);
        }
    }

    private static void EnsureOwned(CanonicalStoryMembershipManifest membership, string storyId, string actorId)
    {
        var owned = membership.OwnedResources.Actors;
        var referenced = membership.ReferencedResources.Actors;
        if (referenced.Contains(actorId, StringComparer.Ordinal))
            throw Failure("story.actor.delete.referenced",
                $"Actor '{actorId}' is referenced by canonical Story '{storyId}' and is not owned there.");
        if (!owned.Contains(actorId, StringComparer.Ordinal))
            throw Failure("story.actor.ownership.required",
                $"Actor '{actorId}' is not owned by canonical Story '{storyId}'.");
    }

    private void ReplaceMembership(CanonicalStoryMembershipManifest updated, string operation, string storyId, string actorId)
    {
        try
        {
            _store.Memberships.Replace(updated);
        }
        catch (Exception exception) when (IsMembershipFailure(exception))
        {
            throw Failure("story.actor.lifecycle.membership_replace_failed",
                $"Could not {operation} Actor '{actorId}' in canonical Story '{storyId}'.", exception);
        }
    }

    private static CanonicalStoryMembershipManifest NewManifest(
        CanonicalStoryMembershipManifest source,
        CanonicalStoryMembershipSet owned,
        CanonicalStoryMembershipSet referenced)
        => new(source.StoryId, owned, referenced) { SchemaVersion = source.SchemaVersion };

    private static void EnsureActorId(string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorId)
            || actorId.Any(character => !(char.IsLower(character) || char.IsDigit(character) || character is '_' or '-')))
        {
            throw Failure("story.actor.id.invalid", $"Actor ID '{actorId}' is invalid.");
        }
    }

    private static void EnsureDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw Failure("story.actor.display_name.required", "Actor display name is required.");
    }

    private static bool IsMembershipFailure(Exception exception)
        => exception is CanonicalStoryMembershipRepositoryException or IOException or UnauthorizedAccessException;

    private void TryRollbackCreatedActor(
        string actorId,
        string storyId,
        string membershipPath,
        byte[] membershipBytes,
        Exception original)
    {
        try
        {
            _actors.DeleteActor(actorId);
            RestoreExactMembership(membershipPath, membershipBytes, storyId);
        }
        catch (Exception rollbackException)
        {
            throw Failure("story.actor.lifecycle.rollback_failed",
                $"Membership update failed and newly-created Actor '{actorId}' could not be removed.",
                new AggregateException(original, rollbackException));
        }
    }

    private static void RestoreExactActor(string path, byte[] bytes, string actorId)
    {
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        new AtomicFileWriter().Write(path, json, temporaryPath =>
        {
            var restored = ActorSerializer.Deserialize(File.ReadAllText(temporaryPath), path);
            if (!string.Equals(restored.Id, actorId, StringComparison.Ordinal))
                throw new ActorRepositoryException("The restored Actor ID changed during rollback.");
        });
    }

    private static void RestoreExactMembership(string path, byte[] bytes, string storyId)
    {
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        new AtomicFileWriter().Write(path, json, temporaryPath =>
        {
            var restored = CanonicalStoryMembershipManifest.FromJson(File.ReadAllText(temporaryPath));
            if (!string.Equals(restored.StoryId, storyId, StringComparison.Ordinal))
                throw new CanonicalStoryMembershipRepositoryException(
                    "story.membership.rollback.story_id.changed",
                    "The restored canonical membership Story ID changed during rollback.");
        });
    }

    private static CanonicalStoryActorLifecycleException TranslateActor(
        ActorRepositoryException exception,
        string operation,
        string actorId)
    {
        var code = exception switch
        {
            ActorNotFoundException => "story.actor.actor.not_found",
            ActorCollisionException => "story.actor.actor.collision",
            _ => $"story.actor.lifecycle.actor_{operation}_failed",
        };
        return Failure(code, $"Could not {operation} Actor '{actorId}'.", exception);
    }

    private static CanonicalStoryActorLifecycleException Failure(
        string code,
        string message,
        Exception? inner = null)
        => new(code, message, inner);
}
