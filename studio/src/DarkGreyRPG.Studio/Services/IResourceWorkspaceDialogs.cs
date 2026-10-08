using DarkGreyRPG.Studio.Core.Projects;

namespace DarkGreyRPG.Studio.Services;

public sealed record StoryCreationRequest(string DisplayName);

public enum ResourcePickerMode { CopyIntoStory }

/// <summary>Shared current resource picker used to select a local copy destination.</summary>
public interface IResourceWorkspaceDialogs
{
    StoryCreationRequest? RequestCreateStory(string allocatedStoryUid);
    ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates,
        ResourcePickerMode mode, string storyDisplayName);
}
