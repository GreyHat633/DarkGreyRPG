using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

/// <summary>P0 evidence: persistence already retains duplicate Start drafts; AddNode does not.</summary>
[TestClass]
public sealed class StoryTransferDraftBoundaryTests
{
    [TestMethod]
    public void DuplicateStartDraftSurvivesRepositoryRoundTripAndKeepsStructuralProblem()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = new GraphResourceRepository(Path.Combine(project.Root, "stories"), GraphResourceKind.Story);
        var graph = new GraphDocument([new GraphNode("original", "start", "Original"), new GraphNode("copied", "start", "Copied")]);
        repository.Create(new GraphResourceEnvelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Draft", graph));
        var reloaded = repository.Load("ST-2345-6789-ABCD-EFGH").Graph!;
        CollectionAssert.AreEqual(new[] { "original", "copied" }, reloaded.Nodes.Select(node => node.Id).ToArray());
        Assert.IsTrue(GraphScopePolicy.Validate(reloaded, GraphScope.StoryFlow)
            .Any(issue => issue.Code == "graph.scope.required_node.duplicate"));
        Assert.IsFalse(GraphScopePolicy.IsValid(reloaded, GraphScope.StoryFlow));
    }

    [TestMethod]
    public void OrdinaryAddNodeRejectsSecondStartWithoutChangingTheTarget()
    {
        var graph = new GraphDocument([new GraphNode("original", "start", "Original")]);
        var edit = new GraphEditSession(graph, GraphScope.StoryFlow);
        Assert.IsFalse(edit.AddNode(new GraphNode("copied", "start", "Copied")));
        CollectionAssert.AreEqual(new[] { "original" }, graph.Nodes.Select(node => node.Id).ToArray());
        Assert.IsTrue(edit.LastValidationIssues.Any(issue => issue.Code == "graph.scope.required_node.duplicate"));
        Assert.IsFalse(edit.CanUndo);
    }
}
