using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Services;

/// <summary>The identity entered when a Story creates an Item or Item Group.</summary>
public sealed record ItemIdentityRequest(
    CanonicalStoryItemKind Kind,
    string Id,
    string DisplayName,
    IReadOnlyList<string> Tags);

public enum ItemCreationMode
{
    Individual,
    Collective,
}

/// <summary>An Item or Item Group offered by the reference picker.</summary>
public sealed record ItemWorkspaceChoice(
    CanonicalStoryItemKind Kind,
    string Id,
    string DisplayName,
    string SourcePath,
    IReadOnlyList<string> Tags)
{
    public ItemWorkspaceChoice(ItemResourceInfo info)
        : this(
            string.Equals((info ?? throw new ArgumentNullException(nameof(info))).Type, IndividualItemResource.ResourceType, StringComparison.Ordinal)
                ? CanonicalStoryItemKind.Individual
                : CanonicalStoryItemKind.Collective,
            info.Id,
            info.DisplayName,
            info.Path,
            info.Tags)
    {
        if (!string.Equals(info.Type, IndividualItemResource.ResourceType, StringComparison.Ordinal)
            && !string.Equals(info.Type, CollectiveItemResource.ResourceType, StringComparison.Ordinal))
            throw new ArgumentException("Item choice has an unsupported resource type.", nameof(info));
    }

    public ItemResourceInfo ResourceInfo => new(Id, DisplayName, SourcePath, Tags,
        Kind == CanonicalStoryItemKind.Individual
            ? IndividualItemResource.ResourceType
            : CollectiveItemResource.ResourceType);
    public CanonicalStoryItemKind ItemKind => Kind;
    public string ResourceId => Id;
    public string ResourceDisplayName => DisplayName;
    public ItemResourceInfo Candidate => ResourceInfo;
}

/// <summary>UI boundary for canonical Story Item and Item Group actions.</summary>
public interface IItemWorkspaceDialogs
{
    string? RequestDisplayName(string resourceLabel, string id, string currentDisplayName) => null;

    ItemCreationMode? RequestCreationMode(string storyDisplayName);

    ItemIdentityRequest? RequestCreate(
        CanonicalStoryItemKind kind,
        string suggestedId);

    ItemWorkspaceChoice? PickReference(
        IReadOnlyList<ItemResourceInfo> candidates,
        string storyDisplayName);

    bool ConfirmRemoveReference(ItemWorkspaceChoice resource, string storyDisplayName);

    bool ConfirmDeleteOwned(ItemWorkspaceChoice resource);

    void ShowDeleteBlocked(ItemWorkspaceChoice resource, IReadOnlyList<string> storyIds);
}
