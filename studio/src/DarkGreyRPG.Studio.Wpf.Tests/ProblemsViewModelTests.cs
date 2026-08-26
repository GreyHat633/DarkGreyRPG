using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ProblemsViewModelTests
{
    [TestMethod]
    public void ReplaceFromValidationIssuesPreservesOrderAndDetails()
    {
        var viewModel = new ProblemsViewModel();
        var changes = new List<string?>();
        var collectionChanges = 0;
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        viewModel.Problems.CollectionChanged += (_, _) => collectionChanges++;
        var issues = new[]
        {
            new ValidationIssue("actor.id.duplicate", "ID already exists.", "Id", ValidationSeverity.Error),
            new ValidationIssue("actor.display-name.empty", "Display name is empty.", "DisplayName", ValidationSeverity.Warning),
        };

        viewModel.ReplaceFromValidationIssues(issues, "actor/tavern_owner");

        CollectionAssert.AreEqual(
            new[] { "actor.id.duplicate", "actor.display-name.empty" },
            viewModel.Problems.Select(problem => problem.Code).ToArray());
        Assert.AreEqual(ValidationSeverity.Error, viewModel.Problems[0].Severity);
        Assert.AreEqual("actor/tavern_owner", viewModel.Problems[0].Source);
        Assert.AreEqual("Id", viewModel.Problems[0].Field);
        Assert.AreEqual(1, viewModel.ErrorCount);
        Assert.AreEqual(1, viewModel.WarningCount);
        Assert.IsTrue(viewModel.HasProblems);
        Assert.IsTrue(viewModel.HasErrors);
        Assert.IsGreaterThan(0, collectionChanges);
        CollectionAssert.Contains(changes, nameof(ProblemsViewModel.ErrorCount));
    }

    [TestMethod]
    public void ReplaceAndClearUpdateObservableCounts()
    {
        var viewModel = new ProblemsViewModel();
        viewModel.Replace(
        [
            new ProblemItem(ValidationSeverity.Warning, "project.directory.docs.missing", "Directory missing."),
        ]);

        Assert.AreEqual(1, viewModel.Count);
        Assert.AreEqual(1, viewModel.WarningCount);

        viewModel.Clear();

        Assert.AreEqual(0, viewModel.Count);
        Assert.IsFalse(viewModel.HasProblems);
        Assert.IsFalse(viewModel.HasErrors);
    }
}
