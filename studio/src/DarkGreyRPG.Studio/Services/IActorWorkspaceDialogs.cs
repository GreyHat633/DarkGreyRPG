using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;

namespace DarkGreyRPG.Studio.Services;

public sealed record ActorIdentityRequest(string Id, string DisplayName);

public enum ActorCreationMode
{
    Blank,
    ImportAsNew,
}

public enum ActorPickerMode
{
    Reference,
    ImportAsNew,
}

public enum UnsavedChangesChoice
{
    Save,
    Discard,
    Cancel,
}

public interface IActorWorkspaceDialogs
{
    ActorCreationMode? RequestCreationMode(string storyDisplayName);

    ActorIdentityRequest? RequestCreate(string suggestedId);

    ActorIdentityRequest? RequestImportIdentity(ActorResourceInfo source, string suggestedId);

    ActorResourceInfo? PickActor(
        IReadOnlyList<ActorResourceInfo> candidates,
        ActorPickerMode mode,
        string storyDisplayName);

    string? RequestRename(ActorResourceInfo actor, string suggestedId);

    bool ConfirmDelete(ActorResourceInfo actor);

    bool ConfirmRemoveReference(ActorResourceInfo actor, string storyDisplayName);

    void ShowReferences(ActorResourceInfo actor, IReadOnlyList<ResourceDescriptor> references);

    bool ConfirmSaveBeforeSwitch(ActorResourceInfo actor);

    UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ActorResourceInfo actor);
}
