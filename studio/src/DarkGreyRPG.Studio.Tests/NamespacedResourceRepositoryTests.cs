using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class NamespacedResourceRepositoryTests
{
    [TestMethod]
    public void GraphRepositoryUsesContentIdsForNestedArbitraryFilesAndReopenCrud()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var root = Path.Combine(project.Root, "resources", "canonical", "stories");
        var repository = new GraphResourceRepository(root, GraphResourceKind.Story);
        var first = new GraphResourceEnvelope(GraphResourceKind.Story, "demo:alpha", "Alpha", new GraphDocument());
        repository.Create(first);

        var arbitraryPath = Path.Combine(root, "custom", "renamed-source.json");
        Directory.CreateDirectory(Path.GetDirectoryName(arbitraryPath)!);
        File.WriteAllText(arbitraryPath,
            GraphResourceEnvelopeSerializer.Serialize(
                new GraphResourceEnvelope(GraphResourceKind.Story, "other:alpha", "Other", new GraphDocument())));

        Assert.AreEqual(Path.GetFullPath(arbitraryPath), repository.GetPath("other:alpha"));
        CollectionAssert.AreEquivalent(new[] { "demo:alpha", "other:alpha" },
            repository.List().Select(item => item.Id).ToArray());

        var reopened = new GraphResourceRepository(root, GraphResourceKind.Story);
        reopened.Replace(new GraphResourceEnvelope(GraphResourceKind.Story, "other:alpha", "Replaced", new GraphDocument()));
        Assert.AreEqual("Replaced", reopened.Load("other:alpha").DisplayName);
        reopened.Delete("other:alpha");
        Assert.IsFalse(File.Exists(arbitraryPath));
    }

    [TestMethod]
    public void DuplicateLogicalIdsFailClosedAcrossNestedPaths()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var root = Path.Combine(project.Root, "resources", "canonical", "tasks");
        var first = new GraphResourceEnvelope(GraphResourceKind.Task, "demo:duplicate", "One", new GraphDocument());
        var json = GraphResourceEnvelopeSerializer.Serialize(first);
        Directory.CreateDirectory(Path.Combine(root, "a"));
        Directory.CreateDirectory(Path.Combine(root, "b"));
        File.WriteAllText(Path.Combine(root, "a", "one.json"), json);
        File.WriteAllText(Path.Combine(root, "b", "two.json"), json);

        var repository = new GraphResourceRepository(root, GraphResourceKind.Task);
        Assert.AreEqual("graph.resource.repository.duplicate_id",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Load("demo:duplicate")).Code);
        Assert.AreEqual("graph.resource.repository.duplicate_id",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.List()).Code);
    }

    [TestMethod]
    public void ActorItemAndStoryRepositoriesAcceptNamespacedIdsAndKeepRoots()
    {
        using var project = new TestProjectDirectory();

        var actors = new ActorRepository(project.Root);
        var actor = actors.SaveActor(actors.CreateActor("demo:merchant", "商人"));
        Assert.AreEqual(Path.GetFullPath(Path.Combine(project.Root, "actors", DgrResourceId.RelativeJsonPath(actor.Id))), actor.SourcePath);
        Assert.AreEqual(actor.Id, new ActorRepository(project.Root).LoadActor(actor.Id).Id);

        var items = new ItemRepository(project.Root);
        items.SaveItem(items.CreateItem("demo:key", "钥匙"));
        Assert.AreEqual("demo:key", new ItemRepository(project.Root).LoadItem("demo:key").ItemId);
        Assert.AreEqual(Path.GetFullPath(Path.Combine(project.Root, "items", DgrResourceId.RelativeJsonPath("demo:key"))),
            items.GetItemPath("demo:key"));

        var stories = new StoryRepository(project.Root);
        stories.CreateStory("demo:quest", "任务");
        Assert.AreEqual("demo:quest", new StoryRepository(project.Root).LoadStory("demo:quest").Id);
        Assert.AreEqual(Path.GetFullPath(Path.Combine(project.Root, "stories", DgrResourceId.RelativeJsonPath("demo:quest"))),
            stories.GetStoryPath("demo:quest"));
    }

    [TestMethod]
    public void MembershipRepositoryUsesParsedStoryIdForArbitraryNestedFile()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var root = Path.Combine(project.Root, "resources", "canonical", "memberships");
        var arbitraryPath = Path.Combine(root, "nested", "membership-source.json");
        Directory.CreateDirectory(Path.GetDirectoryName(arbitraryPath)!);
        File.WriteAllText(arbitraryPath,
            CanonicalStoryMembershipSerializer.Serialize(new CanonicalStoryMembershipManifest("demo:story")));

        var repository = new CanonicalStoryMembershipRepository(root);
        Assert.AreEqual("demo:story", repository.Load("demo:story").StoryId);
        Assert.AreEqual(Path.GetFullPath(arbitraryPath), repository.GetPath("demo:story"));
        repository.Replace(new CanonicalStoryMembershipManifest("demo:story")
        {
            DisplayOrder = new CanonicalStoryDisplayOrder { Actors = ["demo:actor"] },
        });
        repository.Delete("demo:story");
        Assert.IsFalse(File.Exists(arbitraryPath));
    }
    [TestMethod]
    public void CaseDistinctActorGroupItemAndStoryIdsCoexistOnWindowsAndReopenExactly()
    {
        using var project = new TestProjectDirectory();
        var actors = new ActorRepository(project.Root);
        var items = new ItemRepository(project.Root);
        var stories = new StoryRepository(project.Root);
        foreach (var local in new[] { "Guard", "guard", "GUARD" })
        {
            actors.SaveActor(actors.CreateActor("Team:" + local, local));
            actors.SaveActor(actors.CreateCollective("Team:" + local + "Group", local + "Group"));
            items.SaveItem(items.CreateItem("Team:" + local, local));
            stories.CreateStory("Team:" + local, local);
        }
        foreach (var local in new[] { "Guard", "guard", "GUARD" })
        {
            Assert.AreEqual(local, new ActorRepository(project.Root).LoadActor("Team:" + local).DisplayName);
            Assert.AreEqual(local + "Group", new ActorRepository(project.Root).LoadActor("Team:" + local + "Group").DisplayName);
            Assert.AreEqual(local, new ItemRepository(project.Root).LoadItem("Team:" + local).DisplayName);
            Assert.AreEqual(local, new StoryRepository(project.Root).LoadStory("Team:" + local).DisplayName);
        }
    }
}
