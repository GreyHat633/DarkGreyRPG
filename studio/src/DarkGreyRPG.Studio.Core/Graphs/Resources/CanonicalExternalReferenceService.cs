using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Stable diagnostics for external reference membership changes.</summary>
public sealed class CanonicalExternalReferenceException : Exception
{
    public CanonicalExternalReferenceException(string code, string message, Exception? innerException = null)
        : base($"{code}: {message}", innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// Adds an explicitly namespaced external resource to one Story membership.
/// The referenced definition may be unavailable in this project and is never
/// created, renamed, or otherwise changed by this service.
/// </summary>
public sealed class CanonicalExternalReferenceService
{
    private readonly CanonicalProjectGraphStore _store;
    private readonly object _writeGate = new();

    public CanonicalExternalReferenceService(CanonicalProjectGraphStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public CanonicalProjectGraphStore Store => _store;

    public void AddReference(string storyId, DgrResourceKind kind, string fullId)
    {
        EnsureSupportedKind(kind);
        if (!DgrResourceId.IsFullId(fullId))
            throw Failure("story.external_reference.id.invalid",
                $"External {KindText(kind)} ID '{fullId}' must be a valid full DGR ID.");

        lock (_writeGate)
        {
            CanonicalStoryMembershipManifest membership;
            try
            {
                membership = _store.Memberships.Load(storyId);
            }
            catch (CanonicalStoryMembershipRepositoryException exception)
            {
                throw Failure(
                    exception.Code == "story.membership.repository.not_found"
                        ? "story.external_reference.membership.not_found"
                        : "story.external_reference.membership.load_failed",
                    $"Canonical membership for Story '{storyId}' could not be loaded.", exception);
            }

            var owned = membership.OwnedResources;
            var referenced = membership.ReferencedResources;
            var (ownedIds, referencedIds) = Members(owned, referenced, kind);
            if (ownedIds.Contains(fullId, StringComparer.Ordinal))
                throw Failure("story.external_reference.owned",
                    $"Resource '{fullId}' is owned by Story '{storyId}' and cannot be referenced there.");
            if (referencedIds.Contains(fullId, StringComparer.Ordinal))
                throw Failure("story.external_reference.duplicate",
                    $"Resource '{fullId}' is already referenced by Story '{storyId}'.");

            referencedIds.Add(fullId);
            var updated = new CanonicalStoryMembershipManifest(membership.StoryId, owned, referenced)
            {
                SchemaVersion = membership.SchemaVersion,
                DisplayOrder = membership.DisplayOrder,
            };
            try
            {
                _store.Memberships.Replace(updated);
            }
            catch (CanonicalStoryMembershipRepositoryException exception)
            {
                throw Failure("story.external_reference.membership_replace_failed",
                    $"Could not add external {KindText(kind)} '{fullId}' to Story '{storyId}'.", exception);
            }
        }
    }

    private static void EnsureSupportedKind(DgrResourceKind kind)
    {
        if (kind == DgrResourceKind.Story)
            throw Failure("story.external_reference.kind.unsupported",
                "Story resources cannot be added as external references.");
        if (kind is not (DgrResourceKind.Actor or DgrResourceKind.Item or DgrResourceKind.ItemGroup
            or DgrResourceKind.Session or DgrResourceKind.Task))
            throw Failure("story.external_reference.kind.unsupported",
                $"Resource kind '{kind}' cannot be added as an external reference.");
    }

    private static (List<string> Owned, List<string> Referenced) Members(
        CanonicalStoryMembershipSet owned,
        CanonicalStoryMembershipSet referenced,
        DgrResourceKind kind)
        => kind switch
        {
            DgrResourceKind.Actor => (owned.Actors, referenced.Actors),
            DgrResourceKind.Item => (owned.Items, referenced.Items),
            DgrResourceKind.ItemGroup => (owned.ItemGroups, referenced.ItemGroups),
            DgrResourceKind.Session => (owned.Sessions, referenced.Sessions),
            DgrResourceKind.Task => (owned.Tasks, referenced.Tasks),
            _ => throw Failure("story.external_reference.kind.unsupported",
                $"Resource kind '{kind}' cannot be added as an external reference."),
        };

    private static string KindText(DgrResourceKind kind) => kind switch
    {
        DgrResourceKind.Actor => "Actor",
        DgrResourceKind.Item => "Item",
        DgrResourceKind.ItemGroup => "ItemGroup",
        DgrResourceKind.Session => "Session",
        DgrResourceKind.Task => "Task",
        _ => kind.ToString(),
    };

    private static CanonicalExternalReferenceException Failure(string code, string message,
        Exception? innerException = null)
        => new(code, message, innerException);
}
