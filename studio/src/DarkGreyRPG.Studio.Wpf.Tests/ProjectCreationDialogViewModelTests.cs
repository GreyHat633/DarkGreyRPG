using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ProjectCreationDialogViewModelTests
{
    [TestMethod]
    public void RequiresDestinationIdentityAndDisplayName()
    {
        var viewModel = ProjectCreationDialogViewModel.ForCreate(string.Empty);
        viewModel.ParentDirectory = string.Empty;
        viewModel.ProjectFolderName = string.Empty;
        viewModel.Id = string.Empty;
        viewModel.DisplayName = string.Empty;

        Assert.IsFalse(viewModel.CanConfirm);
        StringAssert.Contains(viewModel.ValidationText, "父文件夹");
        StringAssert.Contains(viewModel.ValidationText, "项目文件夹名");
        StringAssert.Contains(viewModel.ValidationText, "项目 ID");
        StringAssert.Contains(viewModel.ValidationText, "显示名称");
    }

    [TestMethod]
    public void ParentAndFolderNameProduceAbsoluteDestination()
    {
        var parent = Path.Combine(Path.GetTempPath(), "darkgrey-project-tests");
        var viewModel = ProjectCreationDialogViewModel.ForCreate(parent);
        viewModel.ProjectFolderName = "new-project";

        Assert.AreEqual(Path.GetFullPath(Path.Combine(parent, "new-project")), viewModel.DestinationDirectory);
        Assert.IsTrue(viewModel.CanConfirm);
    }

    [TestMethod]
    public void FullDestinationCanReplaceParentAndFolderName()
    {
        var viewModel = ProjectCreationDialogViewModel.ForCreate(string.Empty);
        var destination = Path.Combine(Path.GetTempPath(), "full-destination");
        viewModel.FullDestination = destination;

        Assert.AreEqual(Path.GetFullPath(destination), viewModel.DestinationDirectory);
        Assert.IsTrue(viewModel.CanConfirm);
    }

    [TestMethod]
    public void InvalidIdIsShownImmediatelyAndSuggestionIsExplicit()
    {
        var viewModel = ProjectCreationDialogViewModel.ForCreate(Path.GetTempPath());
        viewModel.Id = "  Town Hall!  ";

        Assert.IsFalse(viewModel.CanConfirm);
        Assert.AreEqual("town_hall", viewModel.NormalizedSuggestion);
        Assert.IsTrue(viewModel.HasSuggestion);
        Assert.AreEqual("  Town Hall!  ", viewModel.Id);

        viewModel.ApplySuggestionCommand.Execute(null);

        Assert.AreEqual("town_hall", viewModel.Id);
        Assert.IsTrue(viewModel.CanConfirm);
    }

    [TestMethod]
    public void InvalidProjectFolderNameIsRejected()
    {
        var viewModel = ProjectCreationDialogViewModel.ForCreate(Path.GetTempPath());
        viewModel.ProjectFolderName = "nested\\project";

        Assert.IsFalse(viewModel.CanConfirm);
        StringAssert.Contains(viewModel.ValidationText, "项目文件夹名");
    }
}
