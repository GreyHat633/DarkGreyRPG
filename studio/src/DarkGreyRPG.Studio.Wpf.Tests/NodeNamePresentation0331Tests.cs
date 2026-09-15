using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class NodeNamePresentation0331Tests
{
    [TestMethod]
    public void EveryBoundaryNameUpdatesTitleAndSupportsUndoRedoWithoutChangingValidation()
    {
        foreach (var definition in GraphNodeDefinitionRegistry.All.Where(d => d.Type is "terminate" or "end" or "logic_input" or "logic_output"))
        {
            var node = GraphNodeFactory.Create(definition.Scope, definition.Type, "node");
            var kind = definition.Scope switch { GraphScope.Session => GraphResourceKind.Session, GraphScope.Task => GraphResourceKind.Task, _ => GraphResourceKind.Story };
            using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(kind, "resource", "Resource", new GraphDocument([node])));
            using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
            inspector.BoundaryDisplayName = "完成[A]";
            Assert.AreEqual($"{definition.DisplayName}「完成[A]」", inspector.DisplayName);
            Assert.AreEqual("完成[A]", editor.Host.Graph.Nodes.Single().Properties["display_name"].GetString());
            inspector.BoundaryDisplayName = definition.DisplayName;
            Assert.AreEqual($"{definition.DisplayName}「{definition.DisplayName}」", inspector.DisplayName);
            Assert.IsTrue(editor.Host.Undo());
            Assert.AreEqual($"{definition.DisplayName}「完成[A]」", inspector.DisplayName);
            Assert.IsTrue(editor.Host.Redo());
            Assert.AreEqual($"{definition.DisplayName}「{definition.DisplayName}」", inspector.DisplayName);
            inspector.BoundaryDisplayName = "";
            Assert.AreEqual($"{definition.DisplayName}「{definition.DisplayName}」", inspector.DisplayName);
            Assert.IsNotEmpty(editor.Host.LastValidationIssues);
        }
    }

    [TestMethod]
    public void ReferenceNodeTitlesUseCornerBracketsWithoutASpace()
    {
        foreach (var type in new[] { "session", "task" })
        {
            var node = GraphNodeFactory.Create(GraphScope.StoryFlow, type, "node", "自定义名称");
            var host = new GraphEditorHostViewModel(new GraphDocument([node]), GraphScope.StoryFlow);
            Assert.AreEqual($"{GraphNodeDefinitionRegistry.Get(GraphScope.StoryFlow, type)!.DisplayName}「自定义名称」", host.Nodes.Single().DisplayName);
        }
    }
}
