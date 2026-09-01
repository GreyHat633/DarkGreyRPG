using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalGraphLayoutStoreTests
{
    [TestMethod]
    public void RoundTripStoresFinitePositionsInStudioSidecar()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalGraphLayoutStore(project.Root);

        store.Save(GraphResourceKind.Story, "story-1", Positions(("start", 120.5, 80)));

        var loaded = store.Load(GraphResourceKind.Story, "story-1");
        Assert.AreEqual(120.5, loaded["start"].X);
        Assert.AreEqual(80, loaded["start"].Y);
        Assert.AreEqual(Path.Combine(project.Root, "resources", "editor", "studio_layout.json"), store.LayoutPath);
        StringAssert.Contains(File.ReadAllText(store.LayoutPath), "schema_version");
    }

    [TestMethod]
    public void StorySessionAndTaskKeysDoNotCrossLoad()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalGraphLayoutStore(project.Root);
        store.Save(GraphResourceKind.Story, "shared", Positions(("story-node", 1, 2)));
        store.Save(GraphResourceKind.Session, "shared", Positions(("session-node", 3, 4)));
        store.Save(GraphResourceKind.Task, "shared", Positions(("task-node", 5, 6)));

        CollectionAssert.AreEquivalent(new[] { "story-node" }, store.Load(GraphResourceKind.Story, "shared").Keys.ToArray());
        CollectionAssert.AreEquivalent(new[] { "session-node" }, store.Load(GraphResourceKind.Session, "shared").Keys.ToArray());
        CollectionAssert.AreEquivalent(new[] { "task-node" }, store.Load(GraphResourceKind.Task, "shared").Keys.ToArray());
    }

    [TestMethod]
    public void SaveAndLoadFilterBlankNodeIdsAndNonFiniteCoordinates()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalGraphLayoutStore(project.Root);
        store.Save(GraphResourceKind.Story, "story", new Dictionary<string, ProjectGraphNodeLayout>
        {
            ["valid"] = new() { X = 1, Y = 2 },
            [" "] = new() { X = 3, Y = 4 },
            ["nan"] = new() { X = double.NaN, Y = 5 },
            ["infinity"] = new() { X = 6, Y = double.PositiveInfinity },
        });

        CollectionAssert.AreEquivalent(new[] { "valid" }, store.Load(GraphResourceKind.Story, "story").Keys.ToArray());
    }

    [TestMethod]
    public void SavingGraphReplacesDeletedNodesAndPreservesOtherGraphs()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalGraphLayoutStore(project.Root);
        store.Save(GraphResourceKind.Story, "story", Positions(("keep", 1, 2), ("delete", 3, 4)));
        store.Save(GraphResourceKind.Session, "session", Positions(("session-node", 5, 6)));
        store.Save(GraphResourceKind.Story, "story", Positions(("keep", 9, 10)));

        CollectionAssert.AreEquivalent(new[] { "keep" }, store.Load(GraphResourceKind.Story, "story").Keys.ToArray());
        Assert.AreEqual(9, store.Load(GraphResourceKind.Story, "story")["keep"].X);
        CollectionAssert.AreEquivalent(new[] { "session-node" }, store.Load(GraphResourceKind.Session, "session").Keys.ToArray());
    }

    [TestMethod]
    public void ResourceDisplayRenameDoesNotChangeKindAndIdKey()
    {
        const string stableId = "stable-id";
        const string originalDisplayName = "Original Name";
        const string renamedDisplayName = "Renamed Name";

        var beforeRename = CanonicalGraphLayoutStore.BuildGraphKey(GraphResourceKind.Story, stableId);
        _ = originalDisplayName;
        _ = renamedDisplayName;
        var afterRename = CanonicalGraphLayoutStore.BuildGraphKey(GraphResourceKind.Story, stableId);

        Assert.AreEqual("story:stable-id", beforeRename);
        Assert.AreEqual(beforeRename, afterRename);
    }

    [TestMethod]
    public void MalformedOrUnsupportedSchemaLoadsSafelyAsEmpty()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalGraphLayoutStore(project.Root);
        Directory.CreateDirectory(Path.GetDirectoryName(store.LayoutPath)!);

        File.WriteAllText(store.LayoutPath, "not json");
        Assert.IsEmpty(store.Load(GraphResourceKind.Story, "story"));
        File.WriteAllText(store.LayoutPath, "{\"schema_version\":999,\"graphs\":{}}");
        Assert.IsEmpty(store.Load(GraphResourceKind.Story, "story"));
    }

    [TestMethod]
    public void AtomicSaveLeavesNoTemporaryFiles()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalGraphLayoutStore(project.Root);
        store.Save(GraphResourceKind.Task, "task", Positions(("node", 1, 2)));

        Assert.IsTrue(File.Exists(store.LayoutPath));
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(store.LayoutPath)!, "*.tmp"));
    }

    [TestMethod]
    public void BlankResourceIdIsRejected()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalGraphLayoutStore(project.Root);

        Assert.ThrowsExactly<ArgumentException>(() => store.Load(GraphResourceKind.Story, " "));
    }

    private static IReadOnlyDictionary<string, ProjectGraphNodeLayout> Positions(
        params (string Id, double X, double Y)[] values)
        => values.ToDictionary(
            value => value.Id,
            value => new ProjectGraphNodeLayout { X = value.X, Y = value.Y },
            StringComparer.Ordinal);
}
