using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class M4ActorDialogViewModelTests
{
    [TestMethod]
    public void CreationChoiceDefaultsBlankAndCanSelectImport()
    {
        var viewModel = new ActorCreationChoiceViewModel("王城迷案");

        Assert.IsTrue(viewModel.IsBlank);
        Assert.IsFalse(viewModel.IsImportAsNew);

        viewModel.IsImportAsNew = true;

        Assert.AreEqual(ActorCreationMode.ImportAsNew, viewModel.SelectedMode);
        Assert.IsFalse(viewModel.IsBlank);
    }

    [TestMethod]
    public void PickerSearchesCandidatesAndExplainsReferenceVersusImport()
    {
        var actors = new[]
        {
            new ActorResourceInfo("detective", "侦探", "detective.json", ["mystery"]),
            new ActorResourceInfo("guard", "卫兵", "guard.json", ["capital"]),
        };
        var reference = new ActorResourcePickerViewModel(actors, ActorPickerMode.Reference, "王国线");
        var import = new ActorResourcePickerViewModel(actors, ActorPickerMode.ImportAsNew, "帝国线");

        reference.SearchText = "mystery";
        Assert.AreEqual("detective", reference.FilteredActors.Single().Id);
        Assert.IsTrue(reference.Explanation.Contains("共享同一个资源", StringComparison.Ordinal));
        Assert.IsTrue(import.Explanation.Contains("后续修改互不影响", StringComparison.Ordinal));

        reference.SelectedActor = reference.FilteredActors.Single();
        Assert.IsTrue(reference.CanConfirm);
    }

    [TestMethod]
    public void ImportIdentityMakesIndependentCopySemanticsExplicit()
    {
        var viewModel = ActorIdentityDialogViewModel.ForImport("侦探", "detective_copy");

        Assert.AreEqual("detective_copy", viewModel.Id);
        Assert.AreEqual("侦探", viewModel.DisplayName);
        Assert.IsTrue(viewModel.Description.Contains("独立资源", StringComparison.Ordinal));
        Assert.IsTrue(viewModel.Description.Contains("不会影响原角色", StringComparison.Ordinal));
        Assert.IsTrue(viewModel.CanConfirm);
    }
}
