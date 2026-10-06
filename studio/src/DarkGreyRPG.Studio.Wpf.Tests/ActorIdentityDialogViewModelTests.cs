using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Core.Projects;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ActorIdentityDialogViewModelTests
{
    [TestMethod]
    public void InvalidLegacyIdentityHasNoRepairOrRenameEntry()
    {
        var viewModel = ActorIdentityDialogViewModel.ForCreate("  Teacher NPC!  ");
        Assert.IsFalse(viewModel.CanConfirm);
        Assert.IsFalse(viewModel.HasSuggestion);
        viewModel.ApplySuggestionCommand.Execute(null);
        Assert.AreEqual("  Teacher NPC!  ", viewModel.Id);
        Assert.IsNull(typeof(ActorIdentityDialogViewModel).GetMethod("ForRename"));
    }

    [TestMethod]
    public void CurrentActorIdentityIsFixedAndDisplayNameIsRequired()
    {
        const string id = "ST-2345-6789-ABCD-EFGH~actor~teacher";
        var create = ActorIdentityDialogViewModel.ForCreate(id);
        Assert.IsTrue(create.CanConfirm);
        create.Id = "other";
        create.EditableId = "another";
        Assert.AreEqual(id, create.Id);
        create.DisplayName = " ";
        Assert.IsFalse(create.CanConfirm);
        create.DisplayName = "老师";
        Assert.IsTrue(create.CanConfirm);
    }

    [TestMethod]
    public void StoryIdentityIsReadOnlyWhileDisplayNameRemainsEditable()
    {
        const string uid = "ST-2345-6789-ABCD-EFGH";
        var viewModel = ResourceIdentityDialogViewModel.ForCreate(ProjectResourceType.Story, uid);
        Assert.IsTrue(viewModel.IsStoryIdentity);
        Assert.AreEqual(string.Empty, viewModel.DisplayName);
        Assert.IsFalse(viewModel.CanConfirm);
        viewModel.Id = "ST-JKLM-NPQR-STUV-WXYZ";
        viewModel.EditableId = "Author:other";
        viewModel.ApplySuggestionCommand.Execute(null);
        Assert.AreEqual(uid, viewModel.Id);
        Assert.IsFalse(viewModel.HasSuggestion);
        viewModel.DisplayName = "";
        Assert.IsFalse(viewModel.CanConfirm);
        viewModel.DisplayName = "新名称";
        Assert.IsTrue(viewModel.CanConfirm);
        Assert.AreEqual(uid, viewModel.Id);
    }

    [TestMethod]
    public void StoryCreationRejectsLegacyIdentities()
    {
        foreach (var id in new[] { "new_story", "GreyHat_:new_story", "ST-0000-0000-0000-0000" })
        {
            var viewModel = ResourceIdentityDialogViewModel.ForCreate(ProjectResourceType.Story, id);
            Assert.IsFalse(viewModel.CanConfirm, id);
            Assert.IsFalse(viewModel.HasSuggestion, id);
        }
    }
}
