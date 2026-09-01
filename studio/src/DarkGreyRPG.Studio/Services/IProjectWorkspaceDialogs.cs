using DarkGreyRPG.Studio.Core.Graphs.Migration;

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

    /// <summary>Shows the explicit migration preview and returns true only on confirmation.</summary>
    bool ConfirmCanonicalProjectMigration(CanonicalProjectMigrationPreviewResult preview)
        => ShowCanonicalProjectMigration(preview);

    // Alias for hosts that name this operation as displaying rather than confirming.
    bool ShowCanonicalProjectMigration(CanonicalProjectMigrationPreviewResult preview) => false;
}
