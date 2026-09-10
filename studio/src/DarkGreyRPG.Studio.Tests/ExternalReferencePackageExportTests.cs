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
        new DgrsStoryPackageExporter(provider.Root).Build("Provider:story", providerArchive);
        var result = exporter.Build("Consumer:story", consumerArchive);
        Assert.AreEqual(1, result.Manifest.RequiredResources.Actors.Count);
        WriteActor(consumer.Root, "Provider", "Conflicting definition");
        var conflictArchive = Path.Combine(consumer.Root, "build/conflict.dgrs");
        var conflict = exporter.Build("Consumer:story", conflictArchive);
        Assert.AreEqual(2, conflict.Manifest.RequiredResources.Actors.Count);
        var destination = Environment.GetEnvironmentVariable("DGR_B4_EXTERNAL_EXPORTS");
        if (string.IsNullOrEmpty(destination)) return;
        var path = Path.GetFullPath(destination);
        if (!path.StartsWith("E:\\Java\\MinecraftMod\\DarkGrey_RPG\\.tooling\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("External fixture artifacts must stay in repository .tooling.");
        Directory.CreateDirectory(path);
        File.Copy(providerArchive, Path.Combine(path, "provider.dgrs"), true);
        File.Copy(consumerArchive, Path.Combine(path, "consumer.dgrs"), true);
        File.Copy(conflictArchive, Path.Combine(path, "conflict.dgrs"), true);
    }

    private static void CreateAuthor(string root, string author, bool external)
    {
        var store = new CanonicalProjectGraphStore(root);
        var start = GraphNodeFactory.CreateStoryStart("start");
        if (external)
        {
            start.Ports.Clear();
            StoryStartSchema.InitializeDefault(start, "actor", StoryStartSchema.ActorInteraction, "Provider:guard");
        }
        store.Stories.Create(new(GraphResourceKind.Story, author + ":story", author, new GraphDocument([start])));
        store.Memberships.Create(new(author + ":story", new() { Actors = [author + ":guard"] },
            external ? new() { Actors = ["Provider:guard"] } : new()));
        WriteActor(root, author, author);
    }

    private static void WriteActor(string root, string author, string displayName)
    {
        var id = author + ":guard";
        var path = Path.Combine(root, "actors", DarkGreyRPG.Studio.Core.Identity.DgrResourceId.RelativeJsonPath(id));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, DarkGreyRPG.Studio.Core.Actors.ActorSerializer.Serialize(
            new DarkGreyRPG.Studio.Core.Actors.IndividualActorResource { NpcId = id, DisplayName = displayName, HomeStoryId = author + ":story" },
            DarkGreyRPG.Studio.Core.Actors.ActorIdPolicy.ExistingResource));
    }

    [TestMethod]
    public void ExplicitMissingFullIdReferencesExportWithoutInventedDefinitions()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new(GraphResourceKind.Story, "Consumer:story", "Consumer", new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        store.Memberships.Create(new("Consumer:story", referencedResources: new()
        {
            Actors = ["Provider:actor"], Items = ["Provider:item"], ItemGroups = ["Provider:group"],
            Sessions = ["Provider:session"], Tasks = ["Provider:task"],
        }));
        var result = new DgrsStoryPackageExporter(project.Root).Build("Consumer:story", Path.Combine(project.Root, "build/external.dgrs"));
        var resources = result.Manifest.RequiredResources;
        Assert.AreEqual(0, resources.Actors.Count + resources.Items.Count + resources.ItemGroups.Count + resources.Sessions.Count + resources.Tasks.Count);
        Assert.AreEqual("Provider:actor", store.Memberships.Load("Consumer:story").ReferencedResources.Actors.Single());
    }

    [TestMethod]
    public void MissingOwnedResourcesStillRejectExport()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new(GraphResourceKind.Story, "Consumer:story", "Consumer", new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        store.Memberships.Create(new("Consumer:story", new() { Actors = ["Consumer:missing"] }));
        Assert.ThrowsExactly<StoryPackageException>(() => new DgrsStoryPackageExporter(project.Root)
            .Build("Consumer:story", Path.Combine(project.Root, "build/missing.dgrs")));
    }
    [TestMethod]
    public void CaseSensitiveResourceSetExportsEveryDistinctIdentity()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        var actors = new DarkGreyRPG.Studio.Core.Actors.ActorRepository(project.Root);
        var items = new DarkGreyRPG.Studio.Core.Items.ItemRepository(project.Root);
        foreach (var local in new[] { "Guard", "guard" })
        {
            actors.SaveActor(actors.CreateIndividual("Team:" + local, local));
            actors.SaveActor(actors.CreateCollective("Team:" + local + "Group", local + "Group"));
        }
        foreach (var local in new[] { "Token", "token" })
        {
            items.SaveItem(items.CreateItem("Team:" + local, local));
            items.SaveGroup(items.CreateGroup("Team:" + local + "s", local + "s"));
        }
        store.Stories.Create(new(GraphResourceKind.Story, "Team:CaseStory", "Case-sensitive identities",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        store.Memberships.Create(new("Team:CaseStory", new()
        {
            Actors = ["Team:Guard", "Team:guard", "Team:GuardGroup", "Team:guardGroup"],
            Items = ["Team:Token", "Team:token"], ItemGroups = ["Team:Tokens", "Team:tokens"],
        }));
        var archive = Path.Combine(project.Root, "build/case-sensitive.dgrs");
        var result = new DgrsStoryPackageExporter(project.Root).Build("Team:CaseStory", archive);
        Assert.AreEqual(4, result.Manifest.RequiredResources.Actors.Count);
        Assert.AreEqual(2, result.Manifest.RequiredResources.Items.Count);
        Assert.AreEqual(2, result.Manifest.RequiredResources.ItemGroups.Count);
        var destination = Environment.GetEnvironmentVariable("DGR_B4_CASE_ARCHIVE");
        if (string.IsNullOrWhiteSpace(destination)) return;
        var path = Path.GetFullPath(destination);
        Assert.IsTrue(path.StartsWith("E:\\Java\\MinecraftMod\\DarkGrey_RPG\\.tooling\\", StringComparison.OrdinalIgnoreCase));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(archive, path, true);
    }
}
