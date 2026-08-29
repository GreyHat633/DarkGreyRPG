using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalGraphResourceSaveCoordinatorTests
{
    [TestMethod]
    public void ReplaceRoutesEachKindAndReloadsDetachedEnvelope()
    {
        using var project = new TemporaryProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Path);
        var coordinator = new CanonicalGraphResourceSaveCoordinator(store);

        foreach (var (kind, id, name, addType) in new[]
        {
            (GraphResourceKind.Story, "story", "Story", "start"),
            (GraphResourceKind.Session, "session", "Session", "line"),
            (GraphResourceKind.Task, "task", "Task", "objective"),
        })
        {
            var original = Envelope(kind, id, name);
            Repository(store, kind).Create(original);
            using var editor = new CanonicalGraphResourceEditorViewModel(original);
            Assert.IsTrue(editor.Host.AddNode(GraphNodeFactory.Create(editor.Scope, addType, "node")));

            var saved = coordinator.Replace(editor);

            Assert.AreEqual(kind, saved.ResourceKind);
            Assert.AreEqual(id, saved.Id);
            Assert.AreEqual("node", saved.Graph!.Nodes.Single().Id);
            Assert.IsFalse(editor.IsDirty);
            Assert.AreNotSame(editor.Document.Graph, saved.Graph);
            Assert.AreEqual("node", Repository(store, kind).Load(id).Graph!.Nodes.Single().Id);
        }
    }

    [TestMethod]
    public void CleanEditorIsAnExplicitNoOpAndReturnsDetachedSnapshot()
    {
        using var project = new TemporaryProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Path);
        var envelope = Envelope(GraphResourceKind.Session, "session", "Original");
        store.Sessions.Create(envelope);
        using var editor = new CanonicalGraphResourceEditorViewModel(envelope);
        var coordinator = new CanonicalGraphResourceSaveCoordinator(
            new CanonicalProjectGraphStore(project.Path, new ThrowingWriter()));

        var result = coordinator.Replace(editor);

        Assert.AreEqual("session", result.Id);
        Assert.AreEqual("Original", result.DisplayName);
        Assert.IsFalse(editor.IsDirty);
        Assert.AreNotSame(editor.Document.Graph, result.Graph);
        Assert.AreEqual("Original", store.Sessions.Load("session").DisplayName);
    }

    [TestMethod]
    public void FailedReplacePreservesDiskAndEditorDirtyState()
    {
        using var project = new TemporaryProjectDirectory();
        var initialStore = new CanonicalProjectGraphStore(project.Path);
        var original = Envelope(GraphResourceKind.Session, "session", "Original");
        initialStore.Sessions.Create(original);
        var path = initialStore.Sessions.GetPath("session");
        var before = File.ReadAllText(path);

        using var editor = new CanonicalGraphResourceEditorViewModel(original);
        Assert.IsTrue(editor.Host.AddNode(GraphNodeFactory.Create(GraphScope.Session, "line", "node")));
        Assert.IsTrue(editor.IsDirty);
        var failingStore = new CanonicalProjectGraphStore(project.Path, new ThrowingWriter());
        var coordinator = new CanonicalGraphResourceSaveCoordinator(failingStore);

        var exception = Assert.ThrowsExactly<GraphResourceRepositoryException>(
            () => coordinator.Replace(editor));

        Assert.AreEqual("graph.resource.repository.write.failed", exception.Code);
        Assert.AreEqual(before, File.ReadAllText(path));
        Assert.IsTrue(editor.IsDirty);
        Assert.AreEqual("Original", initialStore.Sessions.Load("session").DisplayName);
        Assert.IsEmpty(initialStore.Sessions.Load("session").Graph!.Nodes);
    }

    private static GraphResourceRepository Repository(
        CanonicalProjectGraphStore store,
        GraphResourceKind kind)
        => kind switch
        {
            GraphResourceKind.Story => store.Stories,
            GraphResourceKind.Session => store.Sessions,
            GraphResourceKind.Task => store.Tasks,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    private static GraphResourceEnvelope Envelope(
        GraphResourceKind kind,
        string id,
        string displayName)
        => new(kind, id, displayName, new GraphDocument());

    private sealed class ThrowingWriter : IAtomicFileWriter
    {
        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
            => throw new IOException("simulated write failure");
    }

    private sealed class TemporaryProjectDirectory : IDisposable
    {
        public TemporaryProjectDirectory()
        {
            Path = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "temp",
                "darkgrey-rpg-save-coordinator-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
