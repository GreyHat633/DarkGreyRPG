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
            var id = kind == GraphResourceKind.Story ? "ST-2345-6789-ABCD-EFGH" : "ST-2345-6789-ABCD-EFGH~" + kind.ToString().ToLowerInvariant() + "~one";
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
        var first = Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "First");
        repository.Create(first);

        Assert.AreEqual("graph.resource.repository.collision",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Create(first)).Code);
        Assert.AreEqual("graph.resource.repository.not_found",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(
                () => repository.Replace(Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~missing", "Missing"))).Code);
        Assert.AreEqual("graph.resource.repository.kind.mismatch",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(
                () => repository.Create(Envelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task", "Task"))).Code);
        Assert.AreEqual("graph.resource.repository.data.invalid",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Create(
                new GraphResourceEnvelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~bad", "Bad", new GraphDocument())
                {
                    SchemaVersion = 1,
                })).Code);

        repository.Replace(Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Replaced"));
        Assert.AreEqual("Replaced", repository.Load("ST-2345-6789-ABCD-EFGH~session~session").DisplayName);
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
        File.WriteAllText(repository.GetPath("ST-2345-6789-ABCD-EFG2"),
            "{\"schema_version\":2,\"id\":\"legacy\",\"title\":\"Old\",\"nodes\":[]}");
        Assert.AreEqual("graph.resource.repository.data.invalid",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Load("ST-2345-6789-ABCD-EFG2")).Code);

        File.Delete(Path.Combine(repository.ResourceDirectory, "ST-2345-6789-ABCD-EFG2.json"));
        File.WriteAllText(repository.GetPath("ST-2345-6789-ABCD-EFG3"),
            GraphResourceEnvelopeSerializer.Serialize(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFG4", "Other")));
        Assert.AreEqual("graph.resource.repository.path.occupied",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Load("ST-2345-6789-ABCD-EFG3")).Code);
        Assert.AreEqual("ST-2345-6789-ABCD-EFG4", repository.Load("ST-2345-6789-ABCD-EFG4").Id);
        Assert.AreEqual("graph.resource.repository.path.occupied", Assert.ThrowsExactly<GraphResourceRepositoryException>(
            () => repository.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFG3", "Cannot overwrite"))).Code);
        Assert.AreEqual("Other", repository.Load("ST-2345-6789-ABCD-EFG4").DisplayName);
    }

    [TestMethod]
    public void AvailableIdsAndDeleteRemainInsideExplicitDirectory()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project, GraphResourceKind.Task);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~task~task", repository.GetAvailableId("ST-2345-6789-ABCD-EFGH~task~task"));
        repository.Create(Envelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task", "Task"));
        var allocated = repository.GetAvailableId("ST-2345-6789-ABCD-EFGH~task~task");
        Assert.AreNotEqual("ST-2345-6789-ABCD-EFGH~task~task", allocated);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", DarkGreyRPG.Studio.Core.Identity.ResourceAddress.FromKey(allocated).StoryUid.Value);

        repository.Delete("ST-2345-6789-ABCD-EFGH~task~task");

        Assert.IsFalse(File.Exists(repository.GetPath("ST-2345-6789-ABCD-EFGH~task~task")));
        Assert.AreEqual("graph.resource.repository.not_found",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Delete("ST-2345-6789-ABCD-EFGH~task~task")).Code);
    }

    [TestMethod]
    public void FailedAtomicReplacePreservesExistingFile()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project, GraphResourceKind.Session);
        repository.Create(Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Original"));
        var path = repository.GetPath("ST-2345-6789-ABCD-EFGH~session~session");
        var before = File.ReadAllText(path);
        var failing = new GraphResourceRepository(
            repository.ResourceDirectory,
            GraphResourceKind.Session,
            new ThrowingWriter());

        var exception = Assert.ThrowsExactly<GraphResourceRepositoryException>(
            () => failing.Replace(Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Changed")));

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
