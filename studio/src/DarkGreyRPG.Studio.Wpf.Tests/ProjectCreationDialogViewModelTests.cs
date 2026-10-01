using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ProjectCreationDialogViewModelTests
{
    [TestMethod]
    public void UppercaseProjectIdIsAcceptedWithoutNormalization()
    {
        var viewModel = ProjectCreationDialogViewModel.ForCreate(AppContext.BaseDirectory);
        viewModel.Id = "TestProject2";
        Assert.IsTrue(viewModel.CanConfirm, viewModel.ValidationText);
        Assert.IsFalse(viewModel.HasSuggestion);
        Assert.AreEqual("TestProject2", viewModel.Id);
    }

    [TestMethod]
    public void RequiresDestinationIdentityAndDisplayName()
    {
        var viewModel = ProjectCreationDialogViewModel.ForCreate(string.Empty);
        viewModel.FullDestination = string.Empty;
        viewModel.Id = string.Empty;
        viewModel.DisplayName = string.Empty;

        Assert.IsFalse(viewModel.CanConfirm);
        StringAssert.Contains(viewModel.ValidationText, "项目路径");
        StringAssert.Contains(viewModel.ValidationText, "项目 ID");
        StringAssert.Contains(viewModel.ValidationText, "显示名称");
    }

    [TestMethod]
    public void DefaultPathIsFilledInAndCanBeReplacedByAnExternalLocation()
    {
        var parent = Path.Combine(Path.GetTempPath(), "darkgrey-project-tests");
        var viewModel = ProjectCreationDialogViewModel.ForCreate(parent);
        Assert.AreEqual(Path.GetFullPath(Path.Combine(parent, "Project")), viewModel.FullDestination);
        var external = Path.Combine(Path.GetTempPath(), "自选位置", "My Project");
        viewModel.FullDestination = external;
        Assert.AreEqual(Path.GetFullPath(external), viewModel.DestinationDirectory);
        Assert.IsTrue(viewModel.CanConfirm);
    }

    [TestMethod]
    public void DefaultDestinationAvoidsExistingProjectsAndFiles()
    {
        var parent = Path.Combine(Path.GetTempPath(), "ProjectCreation", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(parent, "Project"));
        File.WriteAllText(Path.Combine(parent, "Project_2"), "occupied");
        try
        {
            Assert.AreEqual(Path.Combine(parent, "Project_3"), ProjectCreationDialogViewModel.ForCreate(parent).FullDestination);
        }
        finally { Directory.Delete(parent, true); }
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
    public void RelativeOrMalformedProjectPathIsRejected()
    {
        var viewModel = ProjectCreationDialogViewModel.ForCreate(Path.GetTempPath());
        foreach (var path in new[] { "relative-project", "..\\project", "E:\\invalid\0path", "E:\\invalid<name", "E:\\invalid|name" })
        {
            viewModel.FullDestination = path;
            Assert.IsFalse(viewModel.CanConfirm, path);
            Assert.AreEqual(string.Empty, viewModel.DestinationDirectory);
            StringAssert.Contains(viewModel.ValidationText, "项目路径");
        }
    }
}
