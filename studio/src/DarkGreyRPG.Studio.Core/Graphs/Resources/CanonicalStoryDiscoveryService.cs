namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>A stable diagnostic attached to one discovered canonical Story identity.</summary>
public sealed record CanonicalStoryDiscoveryIssue(
    string StoryId,
    string Code,
    string Message);

/// <summary>
/// One canonical Story identity discovered from the union of the Story and
/// membership roots. A root is null when it was missing or failed strict
/// repository validation.
/// </summary>
public sealed record CanonicalStoryDiscoveryItem(
    string Id,
    string DisplayName,
    GraphResourceEnvelope? Story,
    CanonicalStoryMembershipManifest? Membership,
    bool HasStoryRoot,
    bool HasMembershipRoot,
    bool IsComplete,
    bool IsValid,
    IReadOnlyList<CanonicalStoryDiscoveryIssue> Issues);

/// <summary>A read-only snapshot of all canonical Story identities.</summary>
public sealed class CanonicalStoryDiscoverySnapshot
{
    public CanonicalStoryDiscoverySnapshot(
        IReadOnlyList<CanonicalStoryDiscoveryItem> items)
    {
        Items = (items ?? throw new ArgumentNullException(nameof(items))).ToArray();
        Issues = Items.SelectMany(item => item.Issues).ToArray();
    }

    public IReadOnlyList<CanonicalStoryDiscoveryItem> Items { get; }
    public IReadOnlyList<CanonicalStoryDiscoveryIssue> Issues { get; }
}

/// <summary>
/// Discovers canonical Story identities without consulting legacy Story files
/// or creating any canonical directories. Present roots are loaded through the
/// strict repositories so malformed and mismatched roots remain visible as
/// diagnostics instead of being silently omitted.
/// </summary>
public sealed class CanonicalStoryDiscoveryService
{
    private const string MissingRootCode = "story.discovery.root.missing";
    private const string InvalidRootCode = "story.discovery.root.invalid";

    private readonly CanonicalProjectGraphStore _store;

    public CanonicalStoryDiscoveryService(CanonicalProjectGraphStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public CanonicalStoryDiscoverySnapshot Discover()
    {
        var storyIds = ReadLogicalIds(_store.StoriesDirectory, path =>
            GraphResourceEnvelopeSerializer.Deserialize(File.ReadAllText(path)).Id);
        var membershipIds = ReadLogicalIds(_store.MembershipsDirectory, path =>
            CanonicalStoryMembershipSerializer.Deserialize(File.ReadAllText(path)).StoryId);

        var ids = storyIds
            .Concat(membershipIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var items = ids.Select(DiscoverItem).ToArray();
        return new CanonicalStoryDiscoverySnapshot(items);
    }

    private CanonicalStoryDiscoveryItem DiscoverItem(string id)
    {
        // Do not route presence checks through repository.GetPath: an invalid
        // filename is itself a discoverable identity and strict path helpers
        // intentionally reject it before a root can be diagnosed.
        var hasStoryRoot = HasRoot(_store.Stories, id);
        var hasMembershipRoot = HasRoot(_store.Memberships, id);
        var issues = new List<CanonicalStoryDiscoveryIssue>(2);
        GraphResourceEnvelope? story = null;
        CanonicalStoryMembershipManifest? membership = null;

        if (hasStoryRoot)
        {
            try
            {
                story = _store.Stories.Load(id);
            }
            catch (GraphResourceRepositoryException)
            {
                issues.Add(InvalidRoot(id, "story"));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(InvalidRoot(id, "story"));
            }
        }
        else
        {
            issues.Add(MissingRoot(id, "story"));
        }

        if (hasMembershipRoot)
        {
            try
            {
                membership = _store.Memberships.Load(id);
            }
            catch (CanonicalStoryMembershipRepositoryException)
            {
                issues.Add(InvalidRoot(id, "membership"));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(InvalidRoot(id, "membership"));
            }
        }
        else
        {
            issues.Add(MissingRoot(id, "membership"));
        }

        // Completeness describes the pair's physical presence; validity is
        // kept separate so a pair of malformed roots remains distinguishable
        // from a Story that has only one root.
        var isComplete = hasStoryRoot && hasMembershipRoot;
        return new CanonicalStoryDiscoveryItem(
            id,
            story?.DisplayName ?? id,
            story,
            membership,
            hasStoryRoot,
            hasMembershipRoot,
            isComplete,
            story is not null && membership is not null && issues.Count == 0,
            issues.ToArray());
    }

    private static IReadOnlyList<string> ReadLogicalIds(string directory, Func<string, string> readId)
    {
        var ids = new List<string>();
        foreach (var path in CanonicalResourceFileSystem.EnumerateJsonFiles(directory))
        {
            try
            {
                var id = readId(path);
                // Bare B3 resources retain their filename identity, including
                // the diagnostic for a payload/filename mismatch.
                ids.Add(DarkGreyRPG.Studio.Core.Identity.DgrResourceId.IsFullId(id)
                    ? id : Path.GetFileNameWithoutExtension(path));
            }
            catch (Exception exception) when (exception is GraphResourceEnvelopeException
                or CanonicalStoryMembershipException
                or IOException or UnauthorizedAccessException)
            {
                // Preserve the old diagnostic identity for malformed files;
                // valid files are always keyed by their parsed content ID.
                var fallback = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrWhiteSpace(fallback)) ids.Add(fallback);
            }
        }
        return ids;
    }

    private static bool HasRoot(GraphResourceRepository repository, string id)
    {
        try { return File.Exists(repository.GetPath(id)); }
        catch (GraphResourceRepositoryException) { return true; }
    }

    private static bool HasRoot(CanonicalStoryMembershipRepository repository, string id)
    {
        try { return File.Exists(repository.GetPath(id)); }
        catch (CanonicalStoryMembershipRepositoryException) { return true; }
    }

    private static CanonicalStoryDiscoveryIssue MissingRoot(string id, string root)
        => new(id, MissingRootCode, $"Canonical Story '{id}' is missing required root '{root}'.");

    private static CanonicalStoryDiscoveryIssue InvalidRoot(string id, string root)
        => new(id, InvalidRootCode, $"Canonical Story '{id}' has an invalid root '{root}'.");
}
