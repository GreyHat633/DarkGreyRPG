using DarkGreyRPG.Studio.Core.Projects;

namespace DarkGreyRPG.Studio.Services;

public sealed record ResourceIdentityRequest(string Id, string DisplayName);

public enum ResourceCreationMode
{
    Blank,
    ImportAsNew,
}

public enum ResourcePickerMode
{
    Reference,
    ImportAsNew,
}

public interface IResourceWorkspaceDialogs
{
    ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string storyDisplayName);

    ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId);

    ResourceIdentityRequest? RequestImportIdentity(
        ProjectResourceType type,
        ResourceDescriptor source,
        string suggestedId);

    ResourceDescriptor? PickResource(
        ProjectResourceType type,
        IReadOnlyList<ResourceDescriptor> candidates,
        ResourcePickerMode mode,
        string storyDisplayName);

    bool ConfirmDelete(ResourceDescriptor resource);

    bool ConfirmDiscardDraft(ResourceDescriptor resource);

    bool ConfirmRemoveReference(ResourceDescriptor resource, string storyDisplayName);

    void ShowReferences(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references);

    bool ConfirmSaveBeforeSwitch(ResourceDescriptor resource);

    UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ResourceDescriptor resource);
}
