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

    [TestMethod]
    public void ReplaceForSourcePreservesOtherSourcesAndUpdatesCounts()
    {
        var viewModel = new ProblemsViewModel();
        viewModel.ReplaceForSource(
            "project-graph",
            [new ProblemItem(ValidationSeverity.Warning, "graph.cycle", "Cycle detected.")]);
        viewModel.ReplaceForSource(
            "story/intro/flow",
            [
                new ProblemItem(ValidationSeverity.Error, "flow.missing", "Node is missing."),
                new ProblemItem(ValidationSeverity.Warning, "flow.unreachable", "Node is unreachable."),
            ]);

        viewModel.ReplaceForSource(
            "story/intro/flow",
            [new ProblemItem(ValidationSeverity.Error, "flow.invalid", "Flow is invalid.", Source: "old")]);

        Assert.AreEqual(2, viewModel.Count);
        Assert.AreEqual(1, viewModel.ErrorCount);
        Assert.AreEqual(1, viewModel.WarningCount);
        Assert.IsTrue(viewModel.HasProblems);
        Assert.IsTrue(viewModel.HasErrors);
        CollectionAssert.AreEquivalent(
            new[] { "project-graph", "story/intro/flow" },
            viewModel.Problems.Select(problem => problem.Source).ToArray());
        Assert.AreEqual("story/intro/flow", viewModel.Problems.Single(problem => problem.Code == "flow.invalid").Source);
        Assert.IsFalse(viewModel.Problems.Any(problem => problem.Code == "flow.missing"));
        Assert.IsTrue(viewModel.Problems.Any(problem => problem.Code == "graph.cycle"));
    }

    [TestMethod]
    public void RemoveSourceAndClearAllOnlyAffectRequestedProblems()
    {
        var viewModel = new ProblemsViewModel();
        viewModel.ReplaceForSource(
            "project-graph",
            [new ProblemItem(ValidationSeverity.Error, "graph.missing", "Story is missing.")]);
        viewModel.ReplaceForSource(
            "story/intro/flow",
            [new ProblemItem(ValidationSeverity.Warning, "flow.warning", "Flow warning.")]);

        viewModel.RemoveSource("project-graph");

        Assert.AreEqual(1, viewModel.Count);
        Assert.AreEqual(0, viewModel.ErrorCount);
        Assert.AreEqual(1, viewModel.WarningCount);
        Assert.IsTrue(viewModel.HasProblems);
        Assert.IsFalse(viewModel.HasErrors);
        Assert.AreEqual("story/intro/flow", viewModel.Problems.Single().Source);

        viewModel.ClearAll();

        Assert.AreEqual(0, viewModel.Count);
        Assert.AreEqual(0, viewModel.ErrorCount);
        Assert.AreEqual(0, viewModel.WarningCount);
        Assert.IsFalse(viewModel.HasProblems);
        Assert.IsFalse(viewModel.HasErrors);
    }

    [TestMethod]
    public void ReplaceForSourceTreePreservesNodeSourcesAndRemovesStaleDescendants()
    {
        var viewModel = new ProblemsViewModel();
        viewModel.ReplaceForSource("project-graph", [new ProblemItem(ValidationSeverity.Warning, "graph", "keep")]);
        viewModel.ReplaceForSourceTree(
            "story/intro/flow",
            [
                new ProblemItem(ValidationSeverity.Error, "node.a", "A", Source: "story/intro/flow/a"),
                new ProblemItem(ValidationSeverity.Warning, "flow", "Flow"),
            ]);

        viewModel.ReplaceForSourceTree(
            "story/intro/flow",
            [new ProblemItem(ValidationSeverity.Error, "node.b", "B", Source: "story/intro/flow/b")]);

        CollectionAssert.AreEquivalent(
            new[] { "project-graph", "story/intro/flow/b" },
            viewModel.Problems.Select(problem => problem.Source).ToArray());
        Assert.IsFalse(viewModel.Problems.Any(problem => problem.Code is "node.a" or "flow"));
        Assert.Throws<ArgumentException>(() => viewModel.ReplaceForSourceTree(
            "story/intro/flow",
            [new ProblemItem(ValidationSeverity.Error, "bad", "Bad", Source: "story/other/flow/node")]));
    }
}
