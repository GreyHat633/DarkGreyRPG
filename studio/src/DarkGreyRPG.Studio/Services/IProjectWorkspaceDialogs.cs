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
    bool ConfirmDeleteStory(
        string storyId,
        string displayName,
        IReadOnlyList<string> resourcesToDelete);
}
