using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

// The flat Dialogue/Quest draft persistence contract was retired. Current
// resource creation persists a minimal Session/Task; graph edits stay detached
// until saved. UI draft discard and Undo/Redo are covered in WPF tests.
[TestClass]
public sealed class GateEFCoreTests
{
    private const string Owner = "ST-2345-6789-ABCD-EFGH", Other = "ST-JKLM-NPQR-STUV-WXYZ";
    private static string Id(GraphResourceKind kind) => Owner + "~" + kind.ToString().ToLowerInvariant() + "~resource";
    private static GraphResourceRepository Repository(CanonicalProjectGraphStore store, GraphResourceKind kind)
        => kind == GraphResourceKind.Session ? store.Sessions : store.Tasks;
    private static CanonicalProjectGraphStore Setup(string root)
    {
        var store = new CanonicalProjectGraphStore(root);
        var stories = new CanonicalStoryLifecycleService(store);
        stories.Create(Owner, "Owner"); stories.Create(Other, "Other"); return store;
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void CurrentCreationPersistsSafeDefaultAndMembershipAcrossReopen(GraphResourceKind kind)
    {
        using var project = new TestProjectDirectory(); var store = Setup(project.Root);
        var resource = new CanonicalStoryResourceLifecycleService(store).CreateOwned(Owner, kind, Id(kind), "Blank");
        Assert.IsTrue(GraphScopePolicy.IsValid(resource.Graph!, kind == GraphResourceKind.Session ? GraphScope.Session : GraphScope.Task));
        Assert.IsEmpty(store.Memberships.Load(Owner).OwnedResources.Actors);
        new ProjectService().OpenProject(project.Root);
        var reopened = new CanonicalProjectGraphStore(project.Root);
        Assert.AreEqual("Blank", Repository(reopened, kind).Load(Id(kind)).DisplayName);
        Assert.IsTrue((kind == GraphResourceKind.Session ? reopened.Memberships.Load(Owner).OwnedResources.Sessions : reopened.Memberships.Load(Owner).OwnedResources.Tasks).Contains(Id(kind)));
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void DetachedEditsDoNotWriteOrMutateAnotherLoadedCopy(GraphResourceKind kind)
    {
        using var project = new TestProjectDirectory(); var store = Setup(project.Root);
        new CanonicalStoryResourceLifecycleService(store).CreateOwned(Owner, kind, Id(kind), "Original");
        var repository = Repository(store, kind); var bytes = File.ReadAllBytes(repository.GetPath(Id(kind)));
        var draft = repository.Load(Id(kind)); draft.DisplayName = "Unsaved";
        var graph = draft.Graph!; graph.Nodes[0].Properties["draft_test"] = System.Text.Json.JsonSerializer.SerializeToElement(true); draft.Graph = graph;
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(repository.GetPath(Id(kind))));
        Assert.AreEqual("Original", repository.Load(Id(kind)).DisplayName);
        Assert.IsFalse(repository.Load(Id(kind)).Graph!.Nodes[0].Properties.ContainsKey("draft_test"));
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void DuplicateCreateNeverOverwritesExistingResourceOrMembership(GraphResourceKind kind)
    {
        using var project = new TestProjectDirectory(); var store = Setup(project.Root);
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwned(Owner, kind, Id(kind), "Original");
        var path = Repository(store, kind).GetPath(Id(kind)); var resource = File.ReadAllBytes(path);
        var membership = File.ReadAllBytes(store.Memberships.GetPath(Owner));
        Assert.ThrowsExactly<CanonicalStoryResourceLifecycleException>(() => service.CreateOwned(Owner, kind, Id(kind), "Replacement"));
        CollectionAssert.AreEqual(resource, File.ReadAllBytes(path));
        CollectionAssert.AreEqual(membership, File.ReadAllBytes(store.Memberships.GetPath(Owner)));
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void WrongOwnerCannotClaimResourceAddress(GraphResourceKind kind)
    {
        using var project = new TestProjectDirectory(); var store = Setup(project.Root);
        var before = File.ReadAllBytes(store.Memberships.GetPath(Other));
        var error = Assert.ThrowsExactly<CanonicalStoryResourceLifecycleException>(() => new CanonicalStoryResourceLifecycleService(store).CreateOwned(Other, kind, Id(kind), "Wrong"));
        Assert.AreEqual("resource.owner.mismatch", error.Code);
        Assert.IsFalse(File.Exists(Repository(store, kind).GetPath(Id(kind))));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Memberships.GetPath(Other)));
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void MembershipWriteFailureRemovesCreatedResourceAndRetrySucceeds(GraphResourceKind kind)
    {
        using var project = new TestProjectDirectory(); var store = Setup(project.Root);
        var before = File.ReadAllBytes(store.Memberships.GetPath(Owner));
        var failing = new CanonicalProjectGraphStore(project.Root, new FailMembershipWriter());
        Assert.ThrowsExactly<CanonicalStoryResourceLifecycleException>(() => new CanonicalStoryResourceLifecycleService(failing).CreateOwned(Owner, kind, Id(kind), "Retry"));
        Assert.IsFalse(File.Exists(Repository(store, kind).GetPath(Id(kind))));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Memberships.GetPath(Owner)));
        new CanonicalStoryResourceLifecycleService(store).CreateOwned(Owner, kind, Id(kind), "Retry");
        Assert.AreEqual("Retry", Repository(store, kind).Load(Id(kind)).DisplayName);
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void ReferencesDoNotChangeOwnerOrResourceBytes(GraphResourceKind kind)
    {
        using var project = new TestProjectDirectory(); var store = Setup(project.Root);
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwned(Owner, kind, Id(kind), "Shared");
        var path = Repository(store, kind).GetPath(Id(kind)); var bytes = File.ReadAllBytes(path);
        var membership = File.ReadAllBytes(store.Memberships.GetPath(Owner));
        service.AddReference(Other, kind, Id(kind)); service.RemoveReference(Other, kind, Id(kind));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
        CollectionAssert.AreEqual(membership, File.ReadAllBytes(store.Memberships.GetPath(Owner)));
    }

    private sealed class FailMembershipWriter : IAtomicFileWriter
    {
        public void Write(string path, string contents, Action<string>? validateTemporaryFile = null)
        {
            if (path.Contains(Path.DirectorySeparatorChar + "memberships" + Path.DirectorySeparatorChar)) throw new IOException("injected membership failure");
            new AtomicFileWriter().Write(path, contents, validateTemporaryFile);
        }
    }
}
