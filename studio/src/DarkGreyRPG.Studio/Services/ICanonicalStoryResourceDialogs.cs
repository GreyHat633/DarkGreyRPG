using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Services;

/// <summary>The identity entered when a canonical Session or Task is created.</summary>
public sealed record CanonicalGraphResourceIdentityRequest(string Id, string DisplayName);

/// <summary>A canonical graph resource selected by a Story reference picker.</summary>
public sealed record CanonicalGraphResourceChoice(
    GraphResourceKind ResourceKind,
    string Id,
    string DisplayName,
    string SourcePath)
{
    public CanonicalGraphResourceChoice(
        GraphResourceKind resourceKind,
        string id,
        string displayName)
        : this(resourceKind, id, displayName, string.Empty)
    {
    }

    public CanonicalGraphResourceChoice(GraphResourceInfo resource)
        : this(
            resource?.ResourceKind ?? throw new ArgumentNullException(nameof(resource)),
            resource.Id,
            resource.DisplayName,
            resource.SourcePath)
    {
    }

    public GraphResourceInfo Resource => new(Id, DisplayName, ResourceKind, SourcePath);
    public GraphResourceInfo Candidate => Resource;
    public string ResourceId => Id;
    public string ResourceDisplayName => DisplayName;
}

/// <summary>
/// UI boundary for canonical Story-owned Session and Task resource actions.
/// This contract intentionally has no dependency on the legacy resource dialogs.
/// </summary>
public interface ICanonicalStoryResourceDialogs
{
    CanonicalGraphResourceIdentityRequest? RequestCreate(
        GraphResourceKind resourceKind,
        string suggestedId);

    CanonicalGraphResourceChoice? PickReference(
        GraphResourceKind resourceKind,
        IReadOnlyList<GraphResourceInfo> candidates,
        string storyDisplayName);

    bool ConfirmRemoveReference(
        CanonicalGraphResourceChoice resource,
        string storyDisplayName);

    bool ConfirmDeleteOwned(CanonicalGraphResourceChoice resource);

    bool ConfirmAggregateInterfaceRemoval(
        CanonicalGraphResourceChoice resource,
        IReadOnlyList<GraphConnection> affectedConnections);

    void ShowDeleteBlocked(
        CanonicalGraphResourceChoice resource,
        IReadOnlyList<string> storyIds);
}
