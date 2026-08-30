using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

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
    bool SupportsCanonicalActorKinds => false;

    CanonicalStoryActorKind? RequestCanonicalCreationKind(string storyDisplayName)
        => CanonicalStoryActorKind.Individual;

    CanonicalActorIdentityRequest? RequestCreateCanonical(
        CanonicalStoryActorKind kind,
        string suggestedId)
    {
        var request = RequestCreate(suggestedId);
        return request is null ? null : new CanonicalActorIdentityRequest(kind, request.Id, request.DisplayName, []);
    }

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

public sealed record CanonicalActorIdentityRequest(
    CanonicalStoryActorKind Kind,
    string Id,
    string DisplayName,
    IReadOnlyList<string> Tags);
