using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class M2CoreTests
{
    [TestMethod]
    public void OpeningLegacyProjectMigratesOnceAndIsByteStable()
    {
        using var directory = new TestProjectDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "project.json"), """
            { "schema_version": 1, "id": "test_project", "display_name": "Test Project" }
            """);
        File.WriteAllText(directory.ActorPath("old_actor"), """
            { "schema_version": 1, "id": "old_actor", "display_name": "旧角色", "notes": "", "tags": [] }
            """);

        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var projectBytes = File.ReadAllBytes(Path.Combine(directory.Root, "project.json"));
        var actorBytes = File.ReadAllBytes(directory.ActorPath("old_actor"));
        var story = StorySerializer.Read(Path.Combine(directory.Root, "stories", "uncategorized.json"));

        Assert.AreEqual(2, service.CurrentProject!.Project.SchemaVersion);
        Assert.AreEqual("uncategorized", service.OpenActor("old_actor").HomeStoryId);
        CollectionAssert.AreEqual(new[] { "old_actor" }, story.OwnedResources.Actors);
        Assert.AreEqual(1, Directory.GetDirectories(Path.Combine(directory.Root, ".migration-backups")).Length);

        new ProjectService().OpenProject(directory.Root);
        CollectionAssert.AreEqual(projectBytes, File.ReadAllBytes(Path.Combine(directory.Root, "project.json")));
        CollectionAssert.AreEqual(actorBytes, File.ReadAllBytes(directory.ActorPath("old_actor")));
        Assert.AreEqual(1, Directory.GetDirectories(Path.Combine(directory.Root, ".migration-backups")).Length);
    }

    [TestMethod]
    public void RegistryImportCreatesIndependentActorAndMembership()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var original = service.CreateActor("original", "Original");
        service.SaveActor(original);
        var registry = new ProjectResourceRegistry(directory.Root);

        var imported = registry.ImportAsNew("original", "copy");

        Assert.AreEqual("copy", imported.Id);
        Assert.AreNotEqual(imported.Id, registry.Actors.LoadActor("original").Id);
        Assert.IsTrue(registry.CanDelete(ProjectResourceType.Actor, "copy"));
        Assert.IsNotNull(registry.GetHomeStory(ProjectResourceType.Actor, "copy"));

        registry.Stories.CreateStory("other_story", "Other Story");
        registry.AddReference("other_story", ProjectResourceType.Actor, "copy");
        Assert.IsFalse(registry.CanDelete(ProjectResourceType.Actor, "copy"));
        CollectionAssert.AreEqual(
            new[] { "other_story" },
            registry.GetReferences(ProjectResourceType.Actor, "copy").Select(reference => reference.Id).ToArray());

        registry.RemoveReference("other_story", ProjectResourceType.Actor, "copy");
        Assert.IsTrue(registry.CanDelete(ProjectResourceType.Actor, "copy"));
    }

    [TestMethod]
    public void MigrationMergesExistingMembershipWithoutLosingResources()
    {
        using var directory = new TestProjectDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "project.json"),
            "{\"schema_version\":1,\"id\":\"test_project\",\"display_name\":\"Test Project\"}");
        File.WriteAllText(directory.ActorPath("legacy_actor"),
            "{\"schema_version\":1,\"id\":\"legacy_actor\",\"display_name\":\"Legacy\",\"notes\":\"\",\"tags\":[]}");
        File.WriteAllText(directory.ActorPath("current_actor"),
            "{\"schema_version\":2,\"id\":\"current_actor\",\"display_name\":\"Current\",\"notes\":\"\",\"tags\":[],\"home_story_id\":\"uncategorized\"}");
        var stories = new StoryRepository(directory.Root);
        var existing = StoryResource.CreateUncategorized();
        stories.SaveStory(new StoryResource
        {
            Id = existing.Id,
            DisplayName = existing.DisplayName,
            Description = "preserved",
            Tags = ["existing"],
            EntryPresentation = new StoryEntryPresentation
            {
                Mode = "chapter_title",
                Eyebrow = "Above",
                Title = "Title",
                DurationSeconds = 3.5,
            },
            OwnedResources = new StoryMembership
            {
                Actors = ["already_owned"],
                Dialogues = ["dialogue_one"],
                Quests = ["quest_one"],
            },
            ReferencedResources = new StoryMembership { Actors = ["external_actor"] },
            FlowRef = existing.FlowRef,
            Title = existing.Title,
            Entry = existing.Entry,
            Nodes = existing.Nodes,
            Connections = existing.Connections,
        });

        new ProjectService().OpenProject(directory.Root);

        var migrated = stories.LoadStory("uncategorized");
        CollectionAssert.AreEqual(
            new[] { "already_owned", "current_actor", "legacy_actor" },
            migrated.OwnedResources.Actors.ToArray());
        CollectionAssert.AreEqual(new[] { "dialogue_one" }, migrated.OwnedResources.Dialogues.ToArray());
        CollectionAssert.AreEqual(new[] { "quest_one" }, migrated.OwnedResources.Quests.ToArray());
        CollectionAssert.AreEqual(new[] { "external_actor" }, migrated.ReferencedResources.Actors.ToArray());
        Assert.AreEqual("chapter_title", migrated.EntryPresentation.Mode);
        Assert.AreEqual(3.5, migrated.EntryPresentation.DurationSeconds);
    }

    [TestMethod]
    public void ProjectServiceKeepsActorOwnershipAndDeleteGuardConsistent()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        var session = service.OpenProject(directory.Root);
        session.Stories.CreateStory("second_story", "Second Story");

        var actor = service.CreateActor("owned_actor", "Owned Actor");
        service.SaveActor(actor);
        CollectionAssert.Contains(session.Stories.LoadStory("uncategorized").OwnedResources.Actors, "owned_actor");

        session.Registry.AddReference("second_story", ProjectResourceType.Actor, "owned_actor");
        Assert.ThrowsExactly<ProjectException>(() => service.DeleteActor("owned_actor"));
        Assert.IsTrue(File.Exists(directory.ActorPath("owned_actor")));

        session.Registry.RemoveReference("second_story", ProjectResourceType.Actor, "owned_actor");
        service.DeleteActor("owned_actor");
        Assert.IsFalse(File.Exists(directory.ActorPath("owned_actor")));
        CollectionAssert.DoesNotContain(session.Stories.LoadStory("uncategorized").OwnedResources.Actors, "owned_actor");
    }

    [TestMethod]
    public void EntryPresentationSerializesAsStructuredConfiguration()
    {
        var json = StorySerializer.Serialize(new StoryResource
        {
            Id = "presentation_story",
            DisplayName = "Presentation",
            EntryPresentation = new StoryEntryPresentation
            {
                Mode = "title",
                Eyebrow = "Fate changed",
                Title = "Royal Mystery",
                DurationSeconds = 4.0,
            },
        });

        StringAssert.Contains(json, "\"entry_presentation\": {");
        var roundTrip = StorySerializer.Deserialize(json);
        Assert.AreEqual("title", roundTrip.EntryPresentation.Mode);
        Assert.AreEqual("Fate changed", roundTrip.EntryPresentation.Eyebrow);
        Assert.AreEqual(4.0, roundTrip.EntryPresentation.DurationSeconds);
    }

    [TestMethod]
    public void StoryNodePropertiesPreserveRuntimeJsonValueTypes()
    {
        var story = StorySerializer.Deserialize("""
            {
              "schema_version": 1,
              "id": "typed_properties",
              "title": "Typed Properties",
              "entry": "give_item",
              "nodes": [
                {
                  "id": "give_item",
                  "type": "give_item",
                  "position": { "x": 12.5, "y": 24 },
                  "properties": {
                    "item": "minecraft:emerald",
                    "metadata": 0,
                    "amount": 3,
                    "enabled": true
                  }
                }
              ],
              "connections": [],
              "metadata": { "notes": "", "tags": [] }
            }
            """);

        var properties = story.Nodes.Single().Properties;
        Assert.AreEqual(JsonValueKind.String, properties["item"].ValueKind);
        Assert.AreEqual(0, properties["metadata"].GetInt32());
        Assert.AreEqual(3, properties["amount"].GetInt32());
        Assert.IsTrue(properties["enabled"].GetBoolean());

        var roundTrip = StorySerializer.Deserialize(StorySerializer.Serialize(story));
        Assert.AreEqual(3, roundTrip.Nodes.Single().Properties["amount"].GetInt32());
    }

    [TestMethod]
    public void MigrationFailureRestoresOriginalFiles()
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
        Assert.IsTrue(File.Exists(Path.Combine(directory.Root, "migration.log")));
    }

    private sealed class ThrowingWriter : IAtomicFileWriter
    {
        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null) =>
            throw new IOException("intentional migration failure");
    }
}
