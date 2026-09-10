using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
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
    public void OptionalTagsSurviveSnapshotsIdentityMigrationAndPackageImport(GraphResourceKind kind)
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new(GraphResourceKind.Story, "story", "Story", new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        store.Memberships.Create(new("story"));
        var resource = new CanonicalStoryResourceLifecycleService(store).CreateOwned("story", kind, "resource", "Resource", ["old", "中文"]);
        var document = GraphResourceScopeAdapter.OpenDocument(resource, GraphResourceScopeAdapter.GetScope(kind));
        CollectionAssert.AreEqual(new[] { "old", "中文" }, document.ToEnvelope().Tags.ToArray());
        var copy = GraphResourceEnvelope.FromJson(document.ToEnvelope().ToJson());
        CollectionAssert.AreEqual(new[] { "old", "中文" }, copy.Tags.ToArray());
        copy.Tags = [];
        var oldJson = copy.ToJson();
        Assert.IsFalse(oldJson.Contains("\"tags\""));
        Assert.AreEqual(0, GraphResourceEnvelope.FromJson(oldJson).Tags.Count);
        var service = new NamespaceProjectMigrationService();
        service.Apply(service.PreviewGlobal(project.Root, "Author"));
        var resourceKind = kind == GraphResourceKind.Session ? DgrResourceKind.Session : DgrResourceKind.Task;
        service.Apply(service.PreviewResourceRename(project.Root, resourceKind, "Author:resource", "Author:Renamed", "Edited", ["new"]));
        var repository = kind == GraphResourceKind.Session ? store.Sessions : store.Tasks;
        CollectionAssert.AreEqual(new[] { "new" }, repository.Load("Author:Renamed").Tags.ToArray());
        Assert.AreEqual("Edited", repository.Load("Author:Renamed").DisplayName);
        var package = Path.Combine(project.Root, "build", "metadata.dgrs");
        new DgrsStoryPackageExporter(project.Root).Build("Author:story", package);
        using var imported = new TestProjectDirectory();
        new OfflineStoryPackageImportService().Import(imported.Root, package);
        var importedStore = new CanonicalProjectGraphStore(imported.Root);
        var importedResource = (kind == GraphResourceKind.Session ? importedStore.Sessions : importedStore.Tasks).Load("Author:Renamed");
        CollectionAssert.AreEqual(new[] { "new" }, importedResource.Tags.ToArray());
        service.Apply(service.PreviewResourceRename(project.Root, resourceKind, "Author:Renamed", "Author:Cleared", "Edited", []));
        Assert.AreEqual(0, repository.Load("Author:Cleared").Tags.Count);
        new CanonicalStoryResourceLifecycleService(store).CreateOwned("Author:story", kind, "Author:occupied", "Occupied");
        var before = Directory.GetFiles(project.Root, "*.json", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
        Assert.Throws<InvalidOperationException>(() => service.PreviewResourceRename(project.Root, resourceKind, "Author:Cleared", "Author:occupied", "Fail", ["fail"]));
        foreach (var pair in before) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key));
    }
}
