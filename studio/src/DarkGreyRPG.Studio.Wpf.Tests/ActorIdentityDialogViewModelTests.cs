using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Core.Projects;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ActorIdentityDialogViewModelTests
{
    [TestMethod]
    public void InvalidIdOffersRuntimeCompatibleSuggestion()
    {
        var viewModel = ActorIdentityDialogViewModel.ForCreate("  Teacher NPC!  ");

        Assert.IsFalse(viewModel.CanConfirm);
        Assert.AreEqual("teacher_npc", viewModel.NormalizedSuggestion);
        Assert.IsTrue(viewModel.HasSuggestion);

        viewModel.ApplySuggestionCommand.Execute(null);

        Assert.AreEqual("teacher_npc", viewModel.Id);
        Assert.IsTrue(viewModel.CanConfirm);
        Assert.AreEqual(string.Empty, viewModel.ValidationText);
    }

    [TestMethod]
    public void CreateRequiresDisplayNameButRenameDoesNot()
    {
        var create = ActorIdentityDialogViewModel.ForCreate("teacher");
        create.DisplayName = " ";
        var rename = ActorIdentityDialogViewModel.ForRename("teacher", "teacher_renamed");

        Assert.IsFalse(create.CanConfirm);
        Assert.IsTrue(rename.CanConfirm);
        Assert.IsFalse(rename.IsDisplayNameVisible);
    }

    [TestMethod]
    public void StoryIdentityUsesExplicitStoryCopyAndValidation()
    {
        var viewModel = ResourceIdentityDialogViewModel.ForCreate(ProjectResourceType.Story, "new_story");

        Assert.AreEqual("Story", viewModel.TypeLabel);
        Assert.AreEqual("新建 Story", viewModel.Title);
        Assert.AreEqual("新剧情", viewModel.DisplayName);
        Assert.IsTrue(viewModel.Description.Contains("独立剧情", StringComparison.Ordinal));
        Assert.IsTrue(viewModel.CanConfirm);

        viewModel.Id = "Bad Story!";
        Assert.IsFalse(viewModel.CanConfirm);
    }
}
