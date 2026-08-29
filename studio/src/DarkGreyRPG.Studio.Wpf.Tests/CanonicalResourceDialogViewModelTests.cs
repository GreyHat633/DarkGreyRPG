using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalResourceDialogViewModelTests
{
    [TestMethod]
    public void IdentityAcceptsSessionAndTaskWithChineseLabels()
    {
        var session = CanonicalResourceIdentityDialogViewModel.ForCreate(GraphResourceKind.Session, "session_id");
        var task = CanonicalResourceIdentityDialogViewModel.ForCreate(GraphResourceKind.Task, "task_id");

        Assert.AreEqual("会话", session.ChineseTypeLabel);
        Assert.AreEqual("任务", task.ChineseTypeLabel);
        Assert.AreNotEqual("对话", session.ChineseTypeLabel);
        Assert.IsTrue(session.CanConfirm);
        Assert.IsTrue(task.CanConfirm);
    }

    [TestMethod]
    public void IdentityRejectsUnsupportedKindsAndBlankDisplayName()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CanonicalResourceIdentityDialogViewModel(GraphResourceKind.Story, "story"));

        var viewModel = new CanonicalResourceIdentityDialogViewModel(GraphResourceKind.Session, "valid_id");
        viewModel.DisplayName = "  ";
        Assert.IsFalse(viewModel.CanConfirm);
        StringAssert.Contains(viewModel.ValidationText, "显示名称不能为空");
    }

    [TestMethod]
    public void IdentityUsesNewResourceActorIdPolicy()
    {
        var viewModel = new CanonicalResourceIdentityDialogViewModel(GraphResourceKind.Task, "legacy.id");

        Assert.IsFalse(viewModel.CanConfirm);
        StringAssert.Contains(viewModel.ValidationText, "legacy.id");
        viewModel.Id = "Good Id";
        Assert.AreEqual("good_id", viewModel.NormalizedSuggestion);
        viewModel.ApplySuggestionCommand.Execute(null);
        Assert.AreEqual("good_id", viewModel.Id);
        Assert.IsTrue(viewModel.CanConfirm);
    }

    [TestMethod]
    public void PickerFiltersByIdAndDisplayNameAndExposesEmptyState()
    {
        var candidates = new[]
        {
            new GraphResourceInfo("alpha", "First Session", GraphResourceKind.Session, Path.Combine(AppContext.BaseDirectory, "temp", "alpha.json")),
            new GraphResourceInfo("beta", "Second Session", GraphResourceKind.Session, Path.Combine(AppContext.BaseDirectory, "temp", "beta.json")),
        };
        var viewModel = new CanonicalResourcePickerViewModel(GraphResourceKind.Session, candidates, "Story");

        Assert.HasCount(2, viewModel.FilteredResources);
        viewModel.SearchText = "second";
        Assert.HasCount(1, viewModel.FilteredResources);
        Assert.AreEqual("beta", viewModel.FilteredResources[0].Id);
        viewModel.SearchText = "missing";
        Assert.IsTrue(viewModel.IsEmpty);
        Assert.IsFalse(viewModel.CanConfirm);
    }

    [TestMethod]
    public void PickerOnlyConfirmsAnExplicitSelection()
    {
        var candidate = new GraphResourceInfo("task", "Task", GraphResourceKind.Task, Path.Combine(AppContext.BaseDirectory, "temp", "task.json"));
        var viewModel = new CanonicalResourcePickerViewModel(GraphResourceKind.Task, [candidate], "Story");

        Assert.IsFalse(viewModel.CanConfirm);
        viewModel.SelectedResource = candidate;
        Assert.IsTrue(viewModel.CanConfirm);
        Assert.AreSame(candidate, viewModel.SelectedCandidate);
        viewModel.SearchText = "not-visible";
        Assert.IsNull(viewModel.SelectedResource);
        Assert.IsFalse(viewModel.CanConfirm);
    }

    [TestMethod]
    public void PickerRejectsUnsupportedKindsAndMismatchedCandidates()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new CanonicalResourcePickerViewModel(GraphResourceKind.Story, [], "Story"));

        var story = new GraphResourceInfo("story", "Story", GraphResourceKind.Story, Path.Combine(AppContext.BaseDirectory, "temp", "story.json"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            new CanonicalResourcePickerViewModel(GraphResourceKind.Session, [story], "Story"));
    }
}
