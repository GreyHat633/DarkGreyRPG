using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class M5EditorViewModelTests
{
    [TestMethod]
    public void CurrentSessionPagesAndTaskDescriptionSaveAndReopenThroughShell()
    {
        using var fixture = new Fixture();
        var shell = fixture.Open();
        var workspace = shell.CanonicalStoryWorkspace!;
        var session = workspace.SessionItems.Single();
        workspace.OpenGraphResource(session);
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        Assert.IsTrue(session.Editor.Host.AddNode(line));
        workspace.SelectGraphNode(session.Editor.Host.Nodes.Single(node => node.NodeId == "line"));
        var inspector = workspace.NodeInspector!;
        inspector.LinePages[0].Text = "案件开始。";
        session.Editor.UndoCommand.Execute(null);
        Assert.AreEqual(string.Empty, inspector.LinePages[0].Text);
        session.Editor.RedoCommand.Execute(null);
        Assert.AreEqual("案件开始。", inspector.LinePages[0].Text);
        shell.SaveCurrentResourceCommand.Execute(null);
        Assert.IsFalse(session.Editor.IsDirty, shell.StatusMessage);
        var task = workspace.TaskItems.Single();
        workspace.OpenGraphResource(task);
        task.Editor.TaskDescription = "调查现场。\n找到关键证据。";
        shell.SaveCurrentResourceCommand.Execute(null);
        Assert.IsFalse(task.Editor.IsDirty, shell.StatusMessage);
        var reopened = fixture.Open();
        var restored = reopened.CanonicalStoryWorkspace!;
        restored.OpenGraphResource(restored.SessionItems.Single());
        restored.SelectGraphNode(restored.ActiveGraphHost.Nodes.Single(node => node.NodeId == "line"));
        Assert.AreEqual("案件开始。", restored.NodeInspector!.LinePages[0].Text);
        restored.OpenGraphResource(restored.TaskItems.Single());
        Assert.AreEqual("调查现场。\n找到关键证据。", restored.ActiveEditor.TaskDescription);
        Assert.IsEmpty(reopened.Actors);
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void ReopeningDoesNotImportUnsavedEditorChanges(GraphResourceKind kind)
    {
        using var fixture = new Fixture();
        var shell = fixture.Open();
        var workspace = shell.CanonicalStoryWorkspace!;
        var item = workspace.SessionItems.Concat(workspace.TaskItems).Single(item => item.ResourceKind == kind);
        var repository = kind == GraphResourceKind.Session ? fixture.Store.Sessions : fixture.Store.Tasks;
        var before = File.ReadAllBytes(repository.GetPath(item.Id));
        workspace.OpenGraphResource(item);
        Assert.IsTrue(item.Editor.Host.AddNode(GraphNodeFactory.Create(item.Editor.Scope, "logic_output", "unsaved")));
        Assert.IsTrue(item.Editor.IsDirty);
        var reopened = fixture.Open().CanonicalStoryWorkspace!;
        var restored = reopened.SessionItems.Concat(reopened.TaskItems).Single(item => item.ResourceKind == kind);
        Assert.IsFalse(restored.Editor.Host.Nodes.Any(node => node.NodeId == "unsaved"));
        Assert.IsFalse(restored.Editor.IsDirty);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(repository.GetPath(item.Id)));
    }

    private sealed class Fixture : IDisposable
    {
        private const string Owner = "ST-2345-6789-ABCD-EFGH";
        private string Root { get; } = Path.Combine(AppContext.BaseDirectory, ".current-m5", Guid.NewGuid().ToString("N"));
        public CanonicalProjectGraphStore Store { get; }
        public Fixture()
        {
            new ProjectService().CreateProject(Root, "editors", "Editors");
            Store = new(Root);
            new CanonicalStoryLifecycleService(Store).Create(Owner, "Story");
            var lifecycle = new CanonicalStoryResourceLifecycleService(Store);
            lifecycle.CreateOwned(Owner, GraphResourceKind.Session, Owner + "~session~intro", "Intro");
            lifecycle.CreateOwned(Owner, GraphResourceKind.Task, Owner + "~task~case", "Case");
        }
        public ShellViewModel Open()
        {
            var shell = new ShellViewModel(new ProjectService(), new Picker(Root));
            shell.OpenProjectCommand.Execute(null);
            shell.OpenStory(shell.ProjectHome.Stories.Single());
            return shell;
        }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private sealed class Picker(string root) : IProjectFolderPicker { public string? PickProjectFolder() => root; }
}
