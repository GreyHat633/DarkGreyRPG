using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryDeletionCoreTests
{
    [TestMethod]
    public void DeleteEmptyStoryRemovesOnlyItsJsonFile()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        service.CurrentProject!.Stories.CreateStory("empty", "Empty");
        service.CurrentProject.Stories.CreateStory("keep", "Keep");

        service.DeleteStory("empty");

        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "stories", "empty.json")));
        Assert.IsTrue(File.Exists(Path.Combine(directory.Root, "stories", "keep.json")));
        Assert.IsTrue(File.Exists(Path.Combine(directory.Root, "stories", "uncategorized.json")));
    }

    [TestMethod]
    public void EmptyUncategorizedStoryCanBeDeleted()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);

        Assert.IsTrue(service.CanDeleteStory("uncategorized"));

        service.DeleteStory("uncategorized");

        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "stories", "uncategorized.json")));

        service.CloseProject(discardUnsavedChanges: true);
        service.OpenProject(directory.Root);

        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "stories", "uncategorized.json")));
        Assert.IsFalse(service.CurrentProject!.Stories.ListStories().Any(story => story.Id == "uncategorized"));
    }

    [TestMethod]
    public void StoryWithOwnedResourcesIsDeletedWithItsHomeResources()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        service.CurrentProject!.Stories.CreateStory("owned", "Owned");
        var actor = service.CreateActorInStory("owned", "guard", "Guard");

        var blockers = service.GetStoryDeletionBlockers("owned");

        var plan = service.GetStoryDeletionPlan("owned");

        CollectionAssert.AreEqual(new[] { actor.Id }, plan.ActorIds.ToArray());
        Assert.IsEmpty(plan.Blockers);
        service.DeleteStory("owned");
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "actors", actor.Id + ".json")));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "stories", "owned.json")));
    }

    [TestMethod]
    public void StoryTargetedByEnterStoryCannotBeDeleted()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        service.CurrentProject!.Stories.CreateStory("target", "Target");
        service.CurrentProject.Stories.SaveStory(new StoryResource
        {
            Id = "source",
            DisplayName = "Source",
            Title = "Source",
            FlowRef = "source",
            Entry = "start",
            Nodes =
            [
                new() { Id = "start", Type = "StoryStart" },
                new()
                {
                    Id = "enter",
                    Type = "EnterStory",
                    Properties = new(StringComparer.Ordinal)
                    {
                        ["target_story_id"] = JsonSerializer.SerializeToElement("target"),
                    },
                },
            ],
            Connections = [new() { From = "start", Output = "next", To = "enter" }],
        });

        var blockers = service.GetStoryDeletionBlockers("target");

        Assert.IsTrue(blockers.Any(blocker => blocker.Contains("source/enter", StringComparison.Ordinal)));
        Assert.ThrowsExactly<ProjectException>(() => service.DeleteStory("target"));
        Assert.IsTrue(File.Exists(Path.Combine(directory.Root, "stories", "target.json")));
    }

    [TestMethod]
    public void RepositoryDeleteValidatesIdBeforeResolvingPath()
    {
        using var directory = new TestProjectDirectory();
        var outside = Path.Combine(directory.Root, "outside.json");
        File.WriteAllText(outside, "not a story");

        var repository = new StoryRepository(directory.Root);

        Assert.ThrowsExactly<StoryRepositoryException>(() => repository.DeleteStory("../outside"));
        Assert.IsTrue(File.Exists(outside));
    }

    [TestMethod]
    public void OpenRepairsCurrentResourceMembershipToHomeStory()
    {
        using var directory = new TestProjectDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "project.json"),
            "{\"schema_version\":2,\"id\":\"test_project\",\"display_name\":\"Test Project\"}");
        var stories = new StoryRepository(directory.Root);
        stories.SaveStory(StoryResource.CreateUncategorized());
        stories.CreateStory("chapter", "Chapter");
        File.WriteAllText(directory.ActorPath("guard"), ActorSerializer.Serialize(new ActorResource
        {
            SchemaVersion = ActorResource.CurrentSchemaVersion,
            Id = "guard",
            DisplayName = "Guard",
            HomeStoryId = "chapter",
        }, ActorIdPolicy.ExistingResource));
        stories.SaveStory(new StoryResource
        {
            Id = "uncategorized", DisplayName = "未分类", Title = "未分类", FlowRef = "uncategorized", Entry = "end",
            Nodes = [new() { Id = "end", Type = "END" }],
            OwnedResources = new StoryMembership { Actors = ["guard"] },
        });

        var service = new ProjectService();
        service.OpenProject(directory.Root);

        CollectionAssert.DoesNotContain(service.CurrentProject!.Stories.LoadStory("uncategorized").OwnedResources.Actors, "guard");
        CollectionAssert.Contains(service.CurrentProject.Stories.LoadStory("chapter").OwnedResources.Actors, "guard");
        service.CloseProject(discardUnsavedChanges: true);
        service.OpenProject(directory.Root);
        CollectionAssert.Contains(service.CurrentProject!.Stories.LoadStory("chapter").OwnedResources.Actors, "guard");
    }

    [TestMethod]
    public void LegacyDialogueAndQuestMigrationRecordsUncategorizedOwnership()
    {
        using var directory = new TestProjectDirectory();
        File.WriteAllText(Path.Combine(directory.Root, "project.json"),
            "{\"schema_version\":2,\"id\":\"test_project\",\"display_name\":\"Test Project\"}");
        Directory.CreateDirectory(Path.Combine(directory.Root, "dialogues"));
        Directory.CreateDirectory(Path.Combine(directory.Root, "quests"));
        File.WriteAllText(Path.Combine(directory.Root, "dialogues", "legacy.json"), """
            {"schema_version":1,"id":"legacy","title":"Legacy","speakers":[],"entry":"end","nodes":[{"id":"end","type":"end","result":"done"}],"metadata":{"notes":"","tags":[]}}
            """);
        File.WriteAllText(Path.Combine(directory.Root, "quests", "legacy.json"), """
            {"schema_version":1,"id":"legacy","title":"Legacy","description":"Legacy","objectives":[{"id":"kill","type":"kill_entity","description":"Kill","entity":"slime","required":1}],"objective_groups":[{"id":"all","mode":"ALL","objectives":["kill"]}],"metadata":{"notes":"","tags":[]}}
            """);

        var service = new ProjectService();
        service.OpenProject(directory.Root);

        var story = service.CurrentProject!.Stories.LoadStory("uncategorized");
        CollectionAssert.Contains(story.OwnedResources.Dialogues, "legacy");
        CollectionAssert.Contains(story.OwnedResources.Quests, "legacy");
        StringAssert.Contains(File.ReadAllText(Path.Combine(directory.Root, "dialogues", "legacy.json")), "\"home_story_id\"");
        StringAssert.Contains(File.ReadAllText(Path.Combine(directory.Root, "quests", "legacy.json")), "\"home_story_id\"");

        var plan = service.GetStoryDeletionPlan("uncategorized");
        CollectionAssert.Contains(plan.DialogueIds.ToArray(), "legacy");
        CollectionAssert.Contains(plan.QuestIds.ToArray(), "legacy");
        Assert.IsEmpty(plan.Blockers);

        service.DeleteStory("uncategorized");

        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "stories", "uncategorized.json")));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "dialogues", "legacy.json")));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "quests", "legacy.json")));
    }

    [TestMethod]
    public void ExternalResourceReferenceBlocksCascadeWithoutChangingFiles()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        service.CurrentProject!.Stories.CreateStory("owned", "Owned");
        service.CurrentProject.Stories.CreateStory("other", "Other");
        var actor = service.CreateActorInStory("owned", "guard", "Guard");
        service.AddActorReference("other", actor.Id);
        var storyBytes = File.ReadAllBytes(Path.Combine(directory.Root, "stories", "owned.json"));
        var actorBytes = File.ReadAllBytes(directory.ActorPath(actor.Id));

        var plan = service.GetStoryDeletionPlan("owned");

        Assert.IsFalse(plan.Blockers.Count == 0);
        Assert.ThrowsExactly<ProjectException>(() => service.DeleteStory("owned"));
        CollectionAssert.AreEqual(storyBytes, File.ReadAllBytes(Path.Combine(directory.Root, "stories", "owned.json")));
        CollectionAssert.AreEqual(actorBytes, File.ReadAllBytes(directory.ActorPath(actor.Id)));
    }

    [TestMethod]
    public void UnsavedOwnedResourceBlocksCascadeWithoutChangingFiles()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        service.CurrentProject!.Stories.CreateStory("owned", "Owned");
        var actor = service.CreateActorInStory("owned", "guard", "Guard");
        actor.Notes = "unsaved";
        var storyPath = Path.Combine(directory.Root, "stories", "owned.json");
        var storyBytes = File.ReadAllBytes(storyPath);
        var actorBytes = File.ReadAllBytes(directory.ActorPath(actor.Id));

        var plan = service.GetStoryDeletionPlan("owned");

        Assert.IsTrue(plan.Blockers.Any(blocker => blocker.Contains("未保存", StringComparison.Ordinal)));
        Assert.ThrowsExactly<ProjectException>(() => service.DeleteStory("owned"));
        CollectionAssert.AreEqual(storyBytes, File.ReadAllBytes(storyPath));
        CollectionAssert.AreEqual(actorBytes, File.ReadAllBytes(directory.ActorPath(actor.Id)));
    }
}
