using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphResourceMetadataTests
{
    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void OptionalTagsSurviveSnapshotsCopyAndNewIdentityPackageImport(GraphResourceKind kind)
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        var story = new CanonicalStoryLifecycleService(store).CreateNew("Story");
        var addressKind = kind == GraphResourceKind.Session ? ResourceKind.Session : ResourceKind.Task;
        var id = new ResourceAddress(StoryUid.Parse(story.Id), addressKind, "resource").ToKey();
        var resource = new CanonicalStoryResourceLifecycleService(store).CreateOwned(story.Id, kind, id, "Resource", ["old", "中文"]);
        if (kind == GraphResourceKind.Task) resource.Graph = new DarkGreyRPG.Studio.Core.Graphs.GraphDocument();
        var document = GraphResourceScopeAdapter.OpenDocument(resource, GraphResourceScopeAdapter.GetScope(kind));
        CollectionAssert.AreEqual(new[] { "old", "中文" }, document.ToEnvelope().Tags.ToArray());
        var copy = GraphResourceEnvelope.FromJson(document.ToEnvelope().ToJson());
        var copyId = new ResourceAddress(StoryUid.Parse(story.Id), addressKind, "detached_copy").ToKey();
        var remap = new ResourceCopyRemapper(); remap.Add(ResourceCopyRemapper.Kind(kind), id, copyId);
        var detached = remap.Rewrite(copy);
        Assert.AreEqual(copyId, detached.Id); Assert.AreEqual(id, resource.Id);
        CollectionAssert.AreEqual(new[] { "old", "中文" }, detached.Tags.ToArray());
        copy.Tags = [];
        Assert.IsFalse(copy.ToJson().Contains("\"tags\""));
        Assert.IsEmpty(GraphResourceEnvelope.FromJson(copy.ToJson()).Tags);
        var repository = kind == GraphResourceKind.Session ? store.Sessions : store.Tasks;
        resource.DisplayName = "Edited"; resource.Tags = ["new"]; repository.Replace(resource);
        Assert.AreEqual(id, repository.Load(id).Id);
        CollectionAssert.AreEqual(new[] { "new" }, repository.Load(id).Tags.ToArray());
        var package = Path.Combine(project.Root, "build", "metadata.dgrs");
        new DgrsStoryPackageExporter(project.Root).Build(story.Id, package);
        using var imported = new TestProjectDirectory();
        var plan = new OfflineStoryPackageImportService().Import(imported.Root, package);
        var importedStore = new CanonicalProjectGraphStore(imported.Root);
        var members = importedStore.Memberships.Load(plan.ImportedStoryId).OwnedResources;
        var importedId = (kind == GraphResourceKind.Session ? members.Sessions : members.Tasks).Single();
        Assert.AreNotEqual(id, importedId);
        var importedResource = (kind == GraphResourceKind.Session ? importedStore.Sessions : importedStore.Tasks).Load(importedId);
        CollectionAssert.AreEqual(new[] { "new" }, importedResource.Tags.ToArray());
        Assert.AreEqual("Edited", importedResource.DisplayName);
        Assert.AreEqual(id, repository.Load(id).Id);
        resource.Tags = []; repository.Replace(resource);
        Assert.IsEmpty(repository.Load(id).Tags);
    }
}
