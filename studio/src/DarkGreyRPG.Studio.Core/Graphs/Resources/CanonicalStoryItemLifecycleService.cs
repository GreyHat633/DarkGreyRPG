using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

public enum CanonicalStoryItemKind
{
    Individual,
    Collective,
    Item = Individual,
    ItemGroup = Collective,
}

public sealed class CanonicalStoryItemLifecycleException : Exception
{
    public CanonicalStoryItemLifecycleException(string code, string message, Exception? innerException = null)
        : base($"{code}: {message}", innerException) => Code = code;

    public string Code { get; }
}

public sealed record CanonicalStoryItemReference(
    string StoryId,
    CanonicalStoryWorkspaceMembershipKind MembershipKind);

public sealed record CanonicalStoryItemDeletionPlan(
    string StoryId,
    CanonicalStoryItemKind Kind,
    string ItemId,
    IReadOnlyList<CanonicalStoryItemReference> References)
{
    public IReadOnlyList<CanonicalStoryItemReference> Blockers => References;
    public bool CanDelete => References.Count == 0;
    public IReadOnlyList<string> ReferencingStoryIds =>
        References.Select(reference => reference.StoryId).Distinct(StringComparer.Ordinal).ToArray();
}

/// <summary>Atomic canonical Story membership lifecycle for Item IDs and Group IDs.</summary>
public sealed class CanonicalStoryItemLifecycleService
{
    private readonly CanonicalProjectGraphStore _store;
    private readonly ItemRepository _items;
    private readonly object _gate = new();

    public CanonicalStoryItemLifecycleService(
        CanonicalProjectGraphStore store,
        ItemRepository? items = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _items = items ?? new ItemRepository(store.ProjectDirectory);
    }

    public CanonicalProjectGraphStore Store => _store;
    public ItemRepository Items => _items;

    public ItemResource CreateOwned(
        string storyId,
        CanonicalStoryItemKind kind,
        string itemId,
        string displayName,
        IReadOnlyList<string>? tags = null)
    {
        lock (_gate)
        {
            EnsureKind(kind);
            var membership = RequireStory(storyId);
            EnsureAbsent(membership, storyId, kind, itemId);
            var resourcePath = kind == CanonicalStoryItemKind.Individual
                ? _items.GetItemPath(itemId)
                : _items.GetGroupPath(itemId);
            var resourceExisted = File.Exists(resourcePath);
            var membershipPath = _store.Memberships.GetPath(storyId);
            byte[] originalMembership;
            try { originalMembership = File.ReadAllBytes(membershipPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw Failure("story.item.membership_snapshot_failed",
                    $"Could not snapshot Story membership for '{storyId}'.", exception);
            }
            ItemResource resource;
            try
            {
                // CreateItem/CreateGroup performs the collision check. Build a
                // fresh instance for the optional tags because the item model
                // deliberately exposes init-only metadata.
                resource = kind == CanonicalStoryItemKind.Individual
                    ? _items.CreateItem(itemId, displayName)
                    : _items.CreateGroup(itemId, displayName);
                resource = resource switch
                {
                    IndividualItemResource individual => _items.SaveItem(new IndividualItemResource
                    {
                        ItemId = individual.ItemId,
                        DisplayName = individual.DisplayName,
                        Tags = NormalizeTags(tags),
                    }),
                    CollectiveItemResource collective => _items.SaveGroup(new CollectiveItemResource
                    {
                        GroupId = collective.GroupId,
                        DisplayName = collective.DisplayName,
                        Tags = NormalizeTags(tags),
                    }),
                    _ => throw new ItemValidationException([]),
                };
            }
            catch (Exception exception) when (IsItemFailure(exception))
            {
                if (!resourceExisted)
                {
                    var deleteFailure = TryDelete(kind, itemId);
                    if (deleteFailure is not null)
                    {
                        throw Failure("story.item.rollback_failed",
                            $"Could not remove failed {Label(kind)} '{itemId}'.",
                            new AggregateException(exception, deleteFailure));
                    }
                }
                throw Failure("story.item.create_failed", $"Could not create {Label(kind)} '{itemId}'.", exception);
            }

            var owned = membership.OwnedResources;
            Select(owned, kind).Add(itemId);
            try
            {
                Replace(membership, owned, membership.ReferencedResources);
            }
            catch (Exception exception) when (IsMembershipFailure(exception))
            {
                var rollbackFailures = new List<Exception>();
                if (!resourceExisted)
                {
                    var deleteFailure = TryDelete(kind, itemId);
                    if (deleteFailure is not null) rollbackFailures.Add(deleteFailure);
                }
                var restoreFailure = TryRestoreMembership(membershipPath, originalMembership);
                if (restoreFailure is not null) rollbackFailures.Add(restoreFailure);
                if (rollbackFailures.Count > 0)
                {
                    throw Failure("story.item.rollback_failed",
                        $"Could not roll back failed creation of {Label(kind)} '{itemId}'.",
                        new AggregateException(new[] { exception }.Concat(rollbackFailures)));
                }
                throw Failure("story.item.membership_replace_failed",
                    $"Could not record owned {Label(kind)} '{itemId}' in Story '{storyId}'.", exception);
            }
            return resource;
        }
    }

    public IndividualItemResource CreateOwnedItem(
        string storyId, string itemId, string displayName, IReadOnlyList<string>? tags = null)
        => (IndividualItemResource)CreateOwned(storyId, CanonicalStoryItemKind.Individual, itemId, displayName, tags);

    public CollectiveItemResource CreateOwnedGroup(
        string storyId, string groupId, string displayName, IReadOnlyList<string>? tags = null)
        => (CollectiveItemResource)CreateOwned(storyId, CanonicalStoryItemKind.Collective, groupId, displayName, tags);

    public CollectiveItemResource CreateOwnedCollective(
        string storyId, string groupId, string displayName, IReadOnlyList<string>? tags = null)
        => CreateOwnedGroup(storyId, groupId, displayName, tags);

    public void AddReference(string storyId, CanonicalStoryItemKind kind, string itemId)
    {
        lock (_gate)
        {
            EnsureKind(kind);
            var membership = RequireStory(storyId);
            EnsureExists(kind, itemId);
            var owned = membership.OwnedResources;
            var referenced = membership.ReferencedResources;
            if (Select(owned, kind).Contains(itemId, StringComparer.Ordinal))
                throw Failure("story.item.reference.owned", $"{Label(kind)} '{itemId}' is already owned by Story '{storyId}'.");
            if (Select(referenced, kind).Contains(itemId, StringComparer.Ordinal))
                throw Failure("story.item.reference.duplicate", $"{Label(kind)} '{itemId}' is already referenced by Story '{storyId}'.");
            Select(referenced, kind).Add(itemId);
            ReplaceMembership(membership, owned, referenced, storyId, itemId);
        }
    }

    public void AddItemReference(string storyId, string itemId)
        => AddReference(storyId, CanonicalStoryItemKind.Individual, itemId);

    public void AddItemGroupReference(string storyId, string groupId)
        => AddReference(storyId, CanonicalStoryItemKind.Collective, groupId);

    public void AddCollectiveReference(string storyId, string groupId)
        => AddItemGroupReference(storyId, groupId);

    public void RemoveReference(string storyId, CanonicalStoryItemKind kind, string itemId)
    {
        lock (_gate)
        {
            EnsureKind(kind);
            var membership = RequireStory(storyId);
            var owned = membership.OwnedResources;
            var referenced = membership.ReferencedResources;
            if (Select(owned, kind).Contains(itemId, StringComparer.Ordinal))
                throw Failure("story.item.reference.owned", $"{Label(kind)} '{itemId}' is owned, not referenced.");
            if (!Select(referenced, kind).Remove(itemId))
                throw Failure("story.item.reference.not_found", $"Referenced {Label(kind)} '{itemId}' was not found.");
            ReplaceMembership(membership, owned, referenced, storyId, itemId);
        }
    }

    public void RemoveItemReference(string storyId, string itemId)
        => RemoveReference(storyId, CanonicalStoryItemKind.Individual, itemId);

    public void RemoveItemGroupReference(string storyId, string groupId)
        => RemoveReference(storyId, CanonicalStoryItemKind.Collective, groupId);

    public void RemoveCollectiveReference(string storyId, string groupId)
        => RemoveItemGroupReference(storyId, groupId);

    public CanonicalStoryItemDeletionPlan GetDeletionPlan(
        string ownerStoryId,
        CanonicalStoryItemKind kind,
        string itemId)
    {
        lock (_gate)
        {
            EnsureKind(kind);
            var owner = RequireStory(ownerStoryId);
            EnsureOwned(owner, ownerStoryId, kind, itemId);
            EnsureExists(kind, itemId);
            var references = new List<CanonicalStoryItemReference>();
            IReadOnlyList<CanonicalStoryMembershipInfo> memberships;
            try { memberships = _store.Memberships.List(); }
            catch (CanonicalStoryMembershipRepositoryException exception)
            {
                throw Failure("story.item.membership_list_failed", "Could not inspect Story memberships.", exception);
            }
            foreach (var info in memberships)
            {
                if (string.Equals(info.StoryId, ownerStoryId, StringComparison.Ordinal)) continue;
                var manifest = RequireStory(info.StoryId);
                if (Select(manifest.OwnedResources, kind).Contains(itemId, StringComparer.Ordinal))
                    references.Add(new(info.StoryId, CanonicalStoryWorkspaceMembershipKind.Owned));
                if (Select(manifest.ReferencedResources, kind).Contains(itemId, StringComparer.Ordinal))
                    references.Add(new(info.StoryId, CanonicalStoryWorkspaceMembershipKind.Referenced));
            }
            return new(ownerStoryId, kind, itemId, references);
        }
    }

    public CanonicalStoryItemDeletionPlan GetItemDeletionPlan(string storyId, string itemId)
        => GetDeletionPlan(storyId, CanonicalStoryItemKind.Individual, itemId);

    public CanonicalStoryItemDeletionPlan GetItemGroupDeletionPlan(string storyId, string groupId)
        => GetDeletionPlan(storyId, CanonicalStoryItemKind.Collective, groupId);

    public void DeleteOwned(string ownerStoryId, CanonicalStoryItemKind kind, string itemId)
    {
        lock (_gate)
        {
            EnsureKind(kind);
            var membership = RequireStory(ownerStoryId);
            EnsureOwned(membership, ownerStoryId, kind, itemId);
            var plan = GetDeletionPlan(ownerStoryId, kind, itemId);
            if (!plan.CanDelete)
                throw Failure("story.item.delete.blocked",
                    $"{Label(kind)} '{itemId}' is still used by: {string.Join(", ", plan.ReferencingStoryIds)}.");

            var itemPath = kind == CanonicalStoryItemKind.Individual
                ? _items.GetItemPath(itemId)
                : _items.GetGroupPath(itemId);
            byte[] originalBytes;
            try { originalBytes = File.ReadAllBytes(itemPath); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw Failure("story.item.read_failed", $"Could not snapshot {Label(kind)} '{itemId}'.", exception);
            }

            try
            {
                if (kind == CanonicalStoryItemKind.Individual) _items.DeleteItem(itemId);
                else _items.DeleteGroup(itemId);
            }
            catch (Exception exception) when (IsItemFailure(exception))
            {
                throw Failure("story.item.delete_failed", $"Could not delete {Label(kind)} '{itemId}'.", exception);
            }

            var owned = membership.OwnedResources;
            Select(owned, kind).Remove(itemId);
            try { Replace(membership, owned, membership.ReferencedResources); }
            catch (Exception exception) when (IsMembershipFailure(exception))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(itemPath)!);
                    File.WriteAllBytes(itemPath, originalBytes);
                }
                catch (Exception restoreException)
                {
                    throw Failure("story.item.rollback_failed", $"Could not restore deleted {Label(kind)} '{itemId}'.",
                        new AggregateException(exception, restoreException));
                }
                throw Failure("story.item.membership_replace_failed",
                    $"Could not remove owned {Label(kind)} '{itemId}' from Story '{ownerStoryId}'.", exception);
            }
        }
    }

    public void DeleteOwnedItem(string storyId, string itemId)
        => DeleteOwned(storyId, CanonicalStoryItemKind.Individual, itemId);

    public void DeleteOwnedGroup(string storyId, string groupId)
        => DeleteOwned(storyId, CanonicalStoryItemKind.Collective, groupId);

    private CanonicalStoryMembershipManifest RequireStory(string storyId)
    {
        try
        {
            _ = _store.Stories.Load(storyId);
            return _store.Memberships.Load(storyId);
        }
        catch (Exception exception) when (exception is GraphResourceRepositoryException
            or CanonicalStoryMembershipRepositoryException)
        {
            throw Failure("story.item.story_load_failed", $"Canonical Story '{storyId}' could not be loaded.", exception);
        }
    }

    private void EnsureAbsent(CanonicalStoryMembershipManifest membership, string storyId,
        CanonicalStoryItemKind kind, string itemId)
    {
        if (Select(membership.OwnedResources, kind).Contains(itemId, StringComparer.Ordinal)
            || Select(membership.ReferencedResources, kind).Contains(itemId, StringComparer.Ordinal))
            throw Failure("story.item.membership.collision", $"{Label(kind)} '{itemId}' is already in Story '{storyId}'.");
    }

    private void EnsureExists(CanonicalStoryItemKind kind, string itemId)
    {
        try
        {
            if (kind == CanonicalStoryItemKind.Individual) _ = _items.LoadItem(itemId);
            else _ = _items.LoadGroup(itemId);
        }
        catch (Exception exception) when (IsItemFailure(exception))
        {
            throw Failure("story.item.not_found", $"{Label(kind)} '{itemId}' could not be loaded.", exception);
        }
    }

    private static void EnsureOwned(CanonicalStoryMembershipManifest membership, string storyId,
        CanonicalStoryItemKind kind, string itemId)
    {
        if (Select(membership.ReferencedResources, kind).Contains(itemId, StringComparer.Ordinal))
            throw Failure("story.item.delete.referenced", $"{Label(kind)} '{itemId}' is referenced by Story '{storyId}'.");
        if (!Select(membership.OwnedResources, kind).Contains(itemId, StringComparer.Ordinal))
            throw Failure("story.item.ownership.required", $"{Label(kind)} '{itemId}' is not owned by Story '{storyId}'.");
    }

    private void ReplaceMembership(CanonicalStoryMembershipManifest original,
        CanonicalStoryMembershipSet owned, CanonicalStoryMembershipSet referenced,
        string storyId, string itemId)
    {
        try { Replace(original, owned, referenced); }
        catch (Exception exception) when (IsMembershipFailure(exception))
        {
            throw Failure("story.item.membership_replace_failed",
                $"Could not update {LabelFromMembership(owned, referenced, itemId)} '{itemId}' in Story '{storyId}'.", exception);
        }
    }

    private void Replace(CanonicalStoryMembershipManifest original,
        CanonicalStoryMembershipSet owned, CanonicalStoryMembershipSet referenced)
        => _store.Memberships.Replace(new(original.StoryId, owned, referenced)
        {
            SchemaVersion = CanonicalStoryMembershipManifest.CurrentSchemaVersion,
        });

    private Exception? TryDelete(CanonicalStoryItemKind kind, string itemId)
    {
        try
        {
            if (kind == CanonicalStoryItemKind.Individual) _items.DeleteItem(itemId);
            else _items.DeleteGroup(itemId);
        }
        catch (ItemNotFoundException) { }
        catch (Exception exception) { return exception; }
        return null;
    }

    private static Exception? TryRestoreMembership(string path, byte[] originalBytes)
    {
        try { File.WriteAllBytes(path, originalBytes); }
        catch (Exception exception) { return exception; }
        return null;
    }

    private static List<string> Select(CanonicalStoryMembershipSet set, CanonicalStoryItemKind kind)
        => kind == CanonicalStoryItemKind.Individual ? set.Items : set.ItemGroups;

    private static void EnsureKind(CanonicalStoryItemKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw Failure("story.item.kind.invalid", $"Unsupported Story item kind '{kind}'.");
    }

    private static List<string> NormalizeTags(IReadOnlyList<string>? tags)
    {
        if (tags is null) return [];
        return tags.Select(tag => tag?.Trim() ?? string.Empty)
            .ToList();
    }

    private static string Label(CanonicalStoryItemKind kind)
        => kind == CanonicalStoryItemKind.Individual ? "Item" : "Item Group";

    private static string LabelFromMembership(CanonicalStoryMembershipSet owned,
        CanonicalStoryMembershipSet referenced, string itemId)
        => owned.Items.Contains(itemId, StringComparer.Ordinal)
            || referenced.Items.Contains(itemId, StringComparer.Ordinal) ? "Item" : "Item Group";

    private static bool IsItemFailure(Exception exception)
        => exception is ItemRepositoryException or ItemValidationException or ItemDataException;

    private static bool IsMembershipFailure(Exception exception)
        => exception is CanonicalStoryMembershipRepositoryException
            or CanonicalStoryMembershipException or IOException or UnauthorizedAccessException;

    private static CanonicalStoryItemLifecycleException Failure(string code, string message, Exception? inner = null)
        => new(code, message, inner);
}
