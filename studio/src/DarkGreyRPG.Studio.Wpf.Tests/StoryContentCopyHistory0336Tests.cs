using System.IO;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class StoryContentCopyHistory0336Tests
{
    [TestMethod]
    public void CopyIsOneHistoryEntryAndResourceTreeTracksUndoRedo()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "temp", "copy0336-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var store = new CanonicalProjectGraphStore(root);
            var stories = new CanonicalStoryLifecycleService(store);
            var source = stories.CreateNew("Source"); var target = stories.CreateNew("Target");
            var session = new ResourceAddress(StoryUid.Parse(source.Id), ResourceKind.Session, "session").ToKey();
            new CanonicalStoryResourceLifecycleService(store).CreateOwned(source.Id, GraphResourceKind.Session, session, "Session");
            using var workspace = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load(target.Id), new CanonicalGraphLayoutStore(root));
            var before = File.ReadAllBytes(store.Stories.GetPath(target.Id));
            var originalMaxX = workspace.StoryEditor.Host.Nodes.Max(node => node.X);
            var plan = new StoryContentCopyService().Prepare(root, source.Id, target.Id,
                workspace.StoryEditor.CreateLayoutSnapshot().ToDictionary(pair => pair.Key, pair => new DarkGreyRPG.Studio.Core.Stories.ProjectGraphNodeLayout { X = pair.Value.X, Y = pair.Value.Y }));
            StoryContentCopyHistory.Apply(workspace, store, plan);
            Assert.AreEqual(2, workspace.StoryEditor.Host.Nodes.Count(node => node.Type == "start"));
            Assert.AreEqual(1, workspace.SessionItems.Count);
            Assert.IsGreaterThan(originalMaxX + 300, workspace.StoryEditor.Host.Nodes.Single(node => plan.StoryNodeIds.Values.Contains(node.NodeId)).X);
            Assert.IsFalse(workspace.StoryEditor.IsDirty);
            Assert.IsTrue(workspace.StoryEditor.Host.Undo());
            Assert.AreEqual(1, workspace.StoryEditor.Host.Nodes.Count(node => node.Type == "start"));
            Assert.AreEqual(0, workspace.SessionItems.Count);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Stories.GetPath(target.Id)));
            Assert.IsTrue(workspace.StoryEditor.Host.Redo());
            Assert.AreEqual(1, workspace.SessionItems.Count);
            Assert.AreEqual(2, workspace.StoryEditor.Host.Nodes.Count(node => node.Type == "start"));
            var editor = workspace.StoryEditor;
            Assert.IsTrue(GraphScopePolicy.Validate(editor.Document.Graph, editor.Scope).Any(issue => issue.Code == "graph.scope.required_node.duplicate"));
            Assert.ThrowsExactly<StoryPackageException>(() => new StoryPackageExporter(root).Build(target.Id, Path.Combine(root, "export")));
            var copiedStart = plan.StoryNodeIds.Values.Single();
            Assert.IsTrue(editor.Host.SetNodeProperty(copiedStart, "auto_start", false));
            Assert.IsTrue(editor.CanSave);
            new CanonicalGraphResourceSaveCoordinator(store).Replace(editor);
            Assert.AreEqual(2, store.Stories.Load(target.Id).Graph!.Nodes.Count(node => node.Type == "start"));
            var layoutStore = new CanonicalGraphLayoutStore(root);
            layoutStore.Save(GraphResourceKind.Story, source.Id, new Dictionary<string, DarkGreyRPG.Studio.Core.Stories.ProjectGraphNodeLayout>
                { [source.Graph!.Nodes.Single().Id] = new() { X = 987, Y = 654 } });
            Assert.IsTrue(editor.Host.Undo()); // undo the edit whose saved bytes differ from the copy
            Assert.IsTrue(editor.Host.Undo()); // undo the complete copy
            CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Stories.GetPath(target.Id)));
            Assert.AreEqual(987d, layoutStore.Load(GraphResourceKind.Story, source.Id).Values.Single().X);
            Assert.IsTrue(editor.Host.Redo());
            Assert.IsTrue(editor.Host.RemoveNode(copiedStart, true));
            Assert.IsFalse(editor.Host.RemoveNode(editor.Host.Nodes.Single(node => node.Type == "start").NodeId, true));
            new CanonicalGraphResourceSaveCoordinator(store).Replace(editor);
            Assert.AreEqual(1, store.Stories.Load(target.Id).Graph!.Nodes.Count(node => node.Type == "start"));
            Assert.IsTrue(editor.Host.Undo());
            Assert.AreEqual(2, editor.Host.Nodes.Count(node => node.Type == "start"));
            Assert.IsTrue(editor.Host.RemoveNodes(editor.Host.Nodes.Select(node => node.NodeId).ToArray(), true));
            Assert.AreEqual(1, editor.Host.Nodes.Count(node => node.Type == "start"));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
