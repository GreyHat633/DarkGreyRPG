using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class M2CoreTests
{
    [TestMethod]
    public void OpeningLegacyProjectRejectsWithoutMigratingOrChangingBytes()
    {
        using var directory = new TestProjectDirectory();
        var projectPath = Path.Combine(directory.Root, "project.json");
        var actorPath = directory.ActorPath("old_actor");
        File.WriteAllText(projectPath, """{"schema_version":1,"id":"test_project","display_name":"Legacy"}""");
        File.WriteAllText(actorPath, """{"schema_version":1,"id":"old_actor","display_name":"旧角色","tags":[]}""");
        var projectBytes = File.ReadAllBytes(projectPath);
        var actorBytes = File.ReadAllBytes(actorPath);
        for (var attempt = 0; attempt < 2; attempt++)
            Assert.ThrowsExactly<ProjectException>(() => new ProjectService().OpenProject(directory.Root));
        CollectionAssert.AreEqual(projectBytes, File.ReadAllBytes(projectPath));
        CollectionAssert.AreEqual(actorBytes, File.ReadAllBytes(actorPath));
        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Root, ".migration-backups")));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "stories", "uncategorized.json")));
    }

    [TestMethod]
    public void CurrentImportCreatesIndependentActorAndMembership()
    {
        using var directory = new TestProjectDirectory();
        const string owner = "ST-2345-6789-ABCD-EFGH", other = "ST-JKLM-NPQR-STUV-WXYZ";
        var store = new CanonicalProjectGraphStore(directory.Root);
        var stories = new CanonicalStoryLifecycleService(store);
        stories.Create(owner, "Owner"); stories.Create(other, "Other");
        var service = new ProjectService(); service.OpenProject(directory.Root);
        var source = service.CreateActorInStory(owner, owner + "~actor~original", "Original");
        var imported = service.ImportActorAsNew(source.Id, other + "~actor~copy", other);
        var actors = new CanonicalStoryActorLifecycleService(store);
        Assert.AreNotEqual(source.Id, imported.Id);
        Assert.AreEqual(other, imported.HomeStoryId);
        CollectionAssert.Contains(store.Memberships.Load(other).OwnedResources.Actors, imported.Id);
        actors.AddReference(owner, imported.Id);
        Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(() => service.DeleteActor(imported.Id));
        actors.RemoveReference(owner, imported.Id);
        service.DeleteActor(imported.Id);
        Assert.IsTrue(File.Exists(actors.Actors.GetActorPath(source.Id)));
        Assert.IsFalse(File.Exists(actors.Actors.GetActorPath(imported.Id)));
    }

    [TestMethod]
    public void RejectedLegacyProjectLeavesExistingMembershipUntouched()
    {
        using var directory = new TestProjectDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "project.json"), """{"schema_version":1,"id":"test_project","display_name":"Legacy"}""");
        var path = Path.Combine(directory.Root, "stories", "uncategorized.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"schema_version":2,"id":"uncategorized","owned_resources":{"actors":["already_owned"],"dialogues":["dialogue_one"],"quests":["quest_one"]},"referenced_resources":{"actors":["external_actor"]}}""");
        var bytes = File.ReadAllBytes(path);
        Assert.ThrowsExactly<ProjectException>(() => new ProjectService().OpenProject(directory.Root));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "migration.log")));
    }

    [TestMethod]
    public void ProjectServiceKeepsCurrentActorOwnershipAndDeleteGuardConsistent()
    {
        using var directory = new TestProjectDirectory();
        const string owner = "ST-2345-6789-ABCD-EFGH", other = "ST-JKLM-NPQR-STUV-WXYZ";
        var store = new CanonicalProjectGraphStore(directory.Root);
        var stories = new CanonicalStoryLifecycleService(store);
        stories.Create(owner, "Owner"); stories.Create(other, "Other");
        var service = new ProjectService(); service.OpenProject(directory.Root);
        var actor = service.CreateActorInStory(owner, owner + "~actor~owned", "Owned");
        CollectionAssert.Contains(store.Memberships.Load(owner).OwnedResources.Actors, actor.Id);
        var actors = new CanonicalStoryActorLifecycleService(store);
        actors.AddReference(other, actor.Id);
        Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(() => service.DeleteActor(actor.Id));
        Assert.IsTrue(File.Exists(actors.Actors.GetActorPath(actor.Id)));
        actors.RemoveReference(other, actor.Id);
        service.DeleteActor(actor.Id);
        Assert.IsFalse(File.Exists(actors.Actors.GetActorPath(actor.Id)));
        CollectionAssert.DoesNotContain(store.Memberships.Load(owner).OwnedResources.Actors, actor.Id);
    }

    [TestMethod]
    public void StoryNodePropertiesPreserveRuntimeJsonValueTypes()
    {
        var node = Core.Graphs.Definitions.GraphNodeFactory.Create(Core.Graphs.Definitions.GraphScope.StoryFlow, "title", "title");
        node.Properties["duration_seconds"] = JsonSerializer.SerializeToElement(4.5);
        node.Properties["description"] = JsonSerializer.SerializeToElement("Typed properties");
        var story = new GraphResourceEnvelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Typed", new Core.Graphs.GraphDocument([node]));
        var roundTrip = GraphResourceEnvelopeSerializer.Deserialize(GraphResourceEnvelopeSerializer.Serialize(story));
        var properties = roundTrip.Graph!.Nodes.Single().Properties;
        Assert.AreEqual(JsonValueKind.String, properties["description"].ValueKind);
        Assert.AreEqual(4.5, properties["duration_seconds"].GetDouble());
    }

    [TestMethod]
    public void RejectedLegacyOpenDoesNotStartMigrationWrites()
    {
        using var directory = new TestProjectDirectory();
        var projectPath = Path.Combine(directory.Root, "project.json");
        var actorPath = directory.ActorPath("old_actor");
        File.WriteAllText(projectPath, "{\"schema_version\":1,\"id\":\"test_project\",\"display_name\":\"Test Project\"}");
        File.WriteAllText(actorPath, "{\"schema_version\":1,\"id\":\"old_actor\",\"display_name\":\"旧角色\",\"notes\":\"\",\"tags\":[]}");
        var originalProject = File.ReadAllBytes(projectPath);
        var originalActor = File.ReadAllBytes(actorPath);

        Assert.ThrowsExactly<ProjectException>(() => new ProjectService(new ThrowingWriter()).OpenProject(directory.Root));

        CollectionAssert.AreEqual(originalProject, File.ReadAllBytes(projectPath));
        CollectionAssert.AreEqual(originalActor, File.ReadAllBytes(actorPath));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "migration.log")));
    }

    private sealed class ThrowingWriter : IAtomicFileWriter
    {
        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null) =>
            throw new IOException("intentional migration failure");
    }
}
