using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class Title0330InspectorTests
{
    [TestMethod]
    public void WaitDefaultsToTrueForOldAndNewNodesAndSupportsUndo()
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "title", "title");
        Assert.IsTrue(node.Properties["wait_for_completion"].GetBoolean());
        node.Properties.Remove("wait_for_completion");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.IsTrue(inspector.TitleWaitForCompletion);
        inspector.TitleWaitForCompletion = false;
        Assert.IsFalse(inspector.TitleWaitForCompletion);
        Assert.IsTrue(editor.Host.Undo());
        Assert.IsTrue(inspector.TitleWaitForCompletion);
    }

    [TestMethod]
    public void TitleIsStoryOnlyWithValidatedTimingAndUndo()
    {
        Assert.IsNull(GraphNodeDefinitionRegistry.Get(GraphScope.Session, "title"));
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "title", "title");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.IsTrue(inspector.IsTitle);
        inspector.TitleMain = "第一章";
        inspector.TitleSubtitle = "新的旅程";
        inspector.TitleStay = "2.5";
        Assert.AreEqual("2.5", inspector.TitleStay);
        inspector.TitleStay = "-1";
        Assert.AreEqual("2.5", inspector.TitleStay);
        Assert.IsFalse(string.IsNullOrEmpty(inspector.TitleError));
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual("3", inspector.TitleStay);
        inspector.TitleMain = "";
        Assert.AreEqual("第一章", inspector.TitleMain);
        Assert.AreEqual("新的旅程", inspector.TitleSubtitle);
    }
}
