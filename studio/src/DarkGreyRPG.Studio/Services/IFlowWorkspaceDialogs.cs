using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Services;

public interface IFlowWorkspaceDialogs
{
    UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(StoryFlowEditorViewModel flow);
    StoryFlowRecoveryChoice ChooseRecovery(StoryFlowRecoverySnapshot snapshot) => StoryFlowRecoveryChoice.Ignore;
}

public enum StoryFlowRecoveryChoice
{
    Recover,
    Ignore,
    Delete,
}
