using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ExternalReferencePackageExportTests
{
    [TestMethod]
    public void SeparateAuthorPackagesKeepExternalReferenceAndSameLocalIds()
    {
        using var provider = new TestProjectDirectory();
        using var consumer = new TestProjectDirectory();
        CreateAuthor(provider.Root, "Provider", false);
        CreateAuthor(consumer.Root, "Consumer", true);
        var providerArchive = Path.Combine(provider.Root, "build/provider.dgrs");
        var consumerArchive = Path.Combine(consumer.Root, "build/consumer.dgrs");
        var exporter = new DgrsStoryPackageExporter(consumer.Root);
        new DgrsStoryPackageExporter(provider.Root).Build("ST-2345-6789-ABCD-EFGH", providerArchive);
        new OfflineReferencePackageService().AddOrUpdateContainer(consumer.Root, providerArchive);
        var result = exporter.Build("ST-JKLM-NPQR-STUV-WXYZ", consumerArchive);
        Assert.HasCount(2, result.Manifest.RequiredResources.Actors);
        WriteActor(consumer.Root, "Provider", "Conflicting definition");
        var before = File.ReadAllBytes(consumerArchive);
        Assert.Throws<StoryPackageException>(() => exporter.Build("ST-JKLM-NPQR-STUV-WXYZ", consumerArchive));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(consumerArchive));

    }

    private static string Story(string author) => author == "Provider" ? "ST-2345-6789-ABCD-EFGH" : "ST-JKLM-NPQR-STUV-WXYZ";

    private static void CreateAuthor(string root, string author, bool external)
    {
        var store = new CanonicalProjectGraphStore(root);
        var start = GraphNodeFactory.CreateStoryStart("start");
        if (external)
        {
            start.Ports.Clear();
            StoryStartSchema.InitializeDefault(start, "actor", StoryStartSchema.ActorInteraction, "ST-2345-6789-ABCD-EFGH~actor~guard");
        }
        store.Stories.Create(new(GraphResourceKind.Story, Story(author), author, new GraphDocument([start])));
        store.Memberships.Create(new(Story(author), new() { Actors = [Story(author) + "~actor~guard"] },
            external ? new() { Actors = ["ST-2345-6789-ABCD-EFGH~actor~guard"] } : new()));
        WriteActor(root, author, author);
    }

    private static void WriteActor(string root, string author, string displayName)
    {
        var id = Story(author) + "~actor~guard";
        var path = Path.Combine(root, "actors", DarkGreyRPG.Studio.Core.Identity.ResourceAddress.FromKey(id).RelativeDefinitionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, DarkGreyRPG.Studio.Core.Actors.ActorSerializer.Serialize(
            new DarkGreyRPG.Studio.Core.Actors.IndividualActorResource { NpcId = id, DisplayName = displayName, HomeStoryId = Story(author) },
            DarkGreyRPG.Studio.Core.Actors.ActorIdPolicy.ExistingResource));
    }

    [TestMethod]
    public void ExplicitMissingReferencesRejectExportWithoutInventedDefinitions()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new(GraphResourceKind.Story, "ST-JKLM-NPQR-STUV-WXYZ", "Consumer", new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        store.Memberships.Create(new("ST-JKLM-NPQR-STUV-WXYZ", referencedResources: new()
        {
            Actors = ["ST-2345-6789-ABCD-EFGH~actor~actor"], Items = ["ST-2345-6789-ABCD-EFGH~item~item"], ItemGroups = ["ST-2345-6789-ABCD-EFGH~item_group~group"],
            Sessions = ["ST-2345-6789-ABCD-EFGH~session~session"], Tasks = ["ST-2345-6789-ABCD-EFGH~task~task"],
        }));
        var output = Path.Combine(project.Root, "build", "external.dgrs");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllText(output, "previous artifact");
        Assert.Throws<StoryPackageException>(() => new DgrsStoryPackageExporter(project.Root).Build("ST-JKLM-NPQR-STUV-WXYZ", output));
        Assert.AreEqual("previous artifact", File.ReadAllText(output));
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~actor~actor", store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").ReferencedResources.Actors.Single());
    }

    [TestMethod]
    public void MissingOwnedResourcesStillRejectExport()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new(GraphResourceKind.Story, "ST-JKLM-NPQR-STUV-WXYZ", "Consumer", new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        store.Memberships.Create(new("ST-JKLM-NPQR-STUV-WXYZ", new() { Actors = ["ST-JKLM-NPQR-STUV-WXYZ~actor~missing"] }));
        Assert.ThrowsExactly<StoryPackageException>(() => new DgrsStoryPackageExporter(project.Root)
            .Build("ST-JKLM-NPQR-STUV-WXYZ", Path.Combine(project.Root, "build/missing.dgrs")));
    }
    [TestMethod]
    public void CaseDistinctDisplayNamesExportEveryDistinctAllocatedIdentity()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        var story = new CanonicalStoryLifecycleService(store).CreateNew("Display names");
        var actors = new CanonicalStoryActorLifecycleService(store);
        var items = new CanonicalStoryItemLifecycleService(store);
        var owner = DarkGreyRPG.Studio.Core.Identity.StoryUid.Parse(story.Id);
        for (var index = 0; index < 2; index++)
        {
            var label = index == 0 ? "Guard" : "guard";
            string Key(DarkGreyRPG.Studio.Core.Identity.ResourceKind kind, string local) => new DarkGreyRPG.Studio.Core.Identity.ResourceAddress(owner, kind, local + index).ToKey();
            actors.CreateOwned(story.Id, CanonicalStoryActorKind.Individual, Key(DarkGreyRPG.Studio.Core.Identity.ResourceKind.Actor, "guard"), label);
            actors.CreateOwned(story.Id, CanonicalStoryActorKind.Collective, Key(DarkGreyRPG.Studio.Core.Identity.ResourceKind.Actor, "group"), label);
            items.CreateOwned(story.Id, CanonicalStoryItemKind.Individual, Key(DarkGreyRPG.Studio.Core.Identity.ResourceKind.Item, "token"), label);
            items.CreateOwned(story.Id, CanonicalStoryItemKind.Collective, Key(DarkGreyRPG.Studio.Core.Identity.ResourceKind.ItemGroup, "tokens"), label);
        }
        var archive = Path.Combine(project.Root, "build", "distinct-names.dgrs");
        var result = new DgrsStoryPackageExporter(project.Root).Build(story.Id, archive);
        Assert.HasCount(4, result.Manifest.RequiredResources.Actors);
        Assert.HasCount(2, result.Manifest.RequiredResources.Items);
        Assert.HasCount(2, result.Manifest.RequiredResources.ItemGroups);
    }
}
