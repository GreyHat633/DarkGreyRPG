
namespace DarkGreyRPG.Studio.Services;

public sealed record ProjectCreationRequest(
    string ProjectDirectory,
    string Id,
    string DisplayName)
{
    public string DestinationDirectory => ProjectDirectory;
}

public interface IProjectWorkspaceDialogs
{
    ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null);

    /// <summary>Resolves one close decision for all unsaved workspace documents.</summary>
    UnsavedChangesChoice ConfirmCloseWithUnsavedChanges();

    bool ConfirmDeleteStory(
        string storyId,
        string displayName,
        IReadOnlyList<string> resourcesToDelete);

    bool ConfirmDeleteCanonicalStory(
        string storyId,
        string displayName,
        IReadOnlyList<string> resourcesToDelete)
        => ConfirmDeleteStory(storyId, displayName, resourcesToDelete);

}
