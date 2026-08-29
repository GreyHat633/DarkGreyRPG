using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphResourceRepositoryTests
{
    [TestMethod]
    public void ExplicitKindRepositoriesCreateListAndLoadDetachedResources()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        foreach (var kind in new[] { GraphResourceKind.Story, GraphResourceKind.Session, GraphResourceKind.Task })
        {
            var directory = Path.Combine(project.Root, kind.ToString().ToLowerInvariant());
            var repository = new GraphResourceRepository(directory, kind);
            var id = kind.ToString().ToLowerInvariant() + "_one";
            var source = new GraphDocument([new GraphNode("node", "unknown", "Node")]);

            var created = repository.Create(new GraphResourceEnvelope(kind, id, "显示名", source));
            source.Nodes.Clear();
            var listed = repository.List().Single();
            var loaded = repository.Load(id);

            Assert.AreEqual(kind, created.ResourceKind);
            Assert.AreEqual(id, listed.Id);
            Assert.AreEqual(Path.GetFullPath(repository.GetPath(id)), listed.SourcePath);
            Assert.AreEqual("node", loaded.Graph!.Nodes.Single().Id);
        }
    }

    [TestMethod]
    public void CreateAndReplaceHaveExplicitCollisionAndExistenceSemantics()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project, GraphResourceKind.Session);
        var first = Envelope(GraphResourceKind.Session, "session", "First");
        repository.Create(first);

        Assert.AreEqual("graph.resource.repository.collision",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Create(first)).Code);
        Assert.AreEqual("graph.resource.repository.not_found",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(
                () => repository.Replace(Envelope(GraphResourceKind.Session, "missing", "Missing"))).Code);
        Assert.AreEqual("graph.resource.repository.kind.mismatch",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(
                () => repository.Create(Envelope(GraphResourceKind.Task, "task", "Task"))).Code);
        Assert.AreEqual("graph.resource.repository.data.invalid",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Create(
                new GraphResourceEnvelope(GraphResourceKind.Session, "bad", "Bad", new GraphDocument())
                {
                    SchemaVersion = 2,
                })).Code);

        repository.Replace(Envelope(GraphResourceKind.Session, "session", "Replaced"));
        Assert.AreEqual("Replaced", repository.Load("session").DisplayName);
    }

    [TestMethod]
    public void InvalidIdsLegacyRootsAndFilenameMismatchFailClosed()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project, GraphResourceKind.Story);

        foreach (var id in new[] { "../escape", "Upper", "with space", string.Empty })
            Assert.AreEqual("graph.resource.repository.id.invalid",
                Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Load(id)).Code);

        Directory.CreateDirectory(repository.ResourceDirectory);
        File.WriteAllText(repository.GetPath("legacy"),
            "{\"schema_version\":2,\"id\":\"legacy\",\"title\":\"Old\",\"nodes\":[]}");
        Assert.AreEqual("graph.resource.repository.data.invalid",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Load("legacy")).Code);

        File.WriteAllText(repository.GetPath("file_id"),
            GraphResourceEnvelopeSerializer.Serialize(Envelope(GraphResourceKind.Story, "other_id", "Other")));
        Assert.AreEqual("graph.resource.repository.filename.mismatch",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Load("file_id")).Code);
    }

    [TestMethod]
    public void AvailableIdsAndDeleteRemainInsideExplicitDirectory()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project, GraphResourceKind.Task);
        Assert.AreEqual("task", repository.GetAvailableId("task"));
        repository.Create(Envelope(GraphResourceKind.Task, "task", "Task"));
        Assert.AreEqual("task_2", repository.GetAvailableId("task"));

        repository.Delete("task");

        Assert.IsFalse(File.Exists(repository.GetPath("task")));
        Assert.AreEqual("graph.resource.repository.not_found",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Delete("task")).Code);
    }

    [TestMethod]
    public void FailedAtomicReplacePreservesExistingFile()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project, GraphResourceKind.Session);
        repository.Create(Envelope(GraphResourceKind.Session, "session", "Original"));
        var path = repository.GetPath("session");
        var before = File.ReadAllText(path);
        var failing = new GraphResourceRepository(
            repository.ResourceDirectory,
            GraphResourceKind.Session,
            new ThrowingWriter());

        var exception = Assert.ThrowsExactly<GraphResourceRepositoryException>(
            () => failing.Replace(Envelope(GraphResourceKind.Session, "session", "Changed")));

        Assert.AreEqual("graph.resource.repository.write.failed", exception.Code);
        Assert.AreEqual(before, File.ReadAllText(path));
    }

    private static GraphResourceRepository Repository(
        TestProjectDirectory project,
        GraphResourceKind kind)
        => new(Path.Combine(project.Root, "canonical", kind.ToString().ToLowerInvariant()), kind);

    private static GraphResourceEnvelope Envelope(GraphResourceKind kind, string id, string name)
        => new(kind, id, name, new GraphDocument());

    private sealed class ThrowingWriter : IAtomicFileWriter
    {
        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
            => throw new IOException("simulated write failure");
    }
}
