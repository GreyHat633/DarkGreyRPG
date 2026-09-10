using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class NamespaceProjectMigrationServiceTests
{
    [TestMethod]
    public void PreviewIsReadOnlyThenApplyReopensFullIdsAndPreservesEditorLayouts()
    {
        using var project = Fixture();
        var original = Files(project.Root);
        var service = new NamespaceProjectMigrationService();
        var preview = service.PreviewGlobal(project.Root, "Author");
        AssertFilesEqual(original, Files(project.Root));
        service.Apply(preview);
        var reopened = NamespaceProjectMigrationService.ReadProject(project.Root);
        Assert.AreEqual("Author:story", reopened.Graphs.Single().Id);
        Assert.AreEqual("Author:actor", reopened.Actors.Single().Id);
        Assert.AreEqual("Author", reopened.Policy!.GlobalNamespace);
        var layouts = new CanonicalGraphLayoutStore(project.Root).Load(GraphResourceKind.Story, "Author:story");
        Assert.AreEqual(12, layouts["start"].X);
        var storyLayout = new DarkGreyRPG.Studio.Core.Stories.ProjectGraphLayoutStore(project.Root).Load();
        Assert.AreEqual(34, storyLayout.Nodes["Author:story"].Y);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "resources/canonical/stories/story.json")));
        Assert.IsTrue(File.Exists(Path.Combine(project.Root, "resources/canonical/stories", DgrResourceId.RelativeJsonPath("Author:story"))));
        Assert.AreEqual("untouched", File.ReadAllText(Path.Combine(project.Root, "keep.txt")));
        var archive = Path.Combine(project.Root, "build", DgrResourceId.PackageFileName("Author:story"));
        var exported = new DarkGreyRPG.Studio.Core.Packaging.DgrsStoryPackageExporter(project.Root).Build("Author:story", archive);
        Assert.AreEqual("Author:story", exported.Manifest.StoryId);
        service.Apply(service.PreviewCustom(project.Root, "Author:story", "Custom"));
        service.Apply(service.PreviewGlobal(project.Root, "Changed"));
        Assert.AreEqual("Custom:actor", NamespaceProjectMigrationService.ReadProject(project.Root).Actors.Single().Id);
        service.Apply(service.PreviewReturnToGlobal(project.Root, "Custom:story"));
        Assert.AreEqual("Changed:actor", NamespaceProjectMigrationService.ReadProject(project.Root).Actors.Single().Id);
    }

    [TestMethod]
    public void StalePreviewAndWriteFailureLeaveProjectBytesUnchanged()
    {
        using var project = Fixture();
        var service = new NamespaceProjectMigrationService();
        var preview = service.PreviewGlobal(project.Root, "Author");
        File.AppendAllText(Path.Combine(project.Root, "actors/actor.json"), " ");
        var stale = Files(project.Root);
        Assert.Throws<InvalidOperationException>(() => service.Apply(preview));
        AssertFilesEqual(stale, Files(project.Root));
        var calls = 0;
        var failing = new NamespaceProjectMigrationService(new NamespaceFileTransaction((path, bytes) =>
        {
            if (++calls == 2) throw new IOException("injected write failure");
            File.WriteAllBytes(path, bytes);
        }));
        var fresh = failing.PreviewGlobal(project.Root, "Author");
        Assert.Throws<IOException>(() => failing.Apply(fresh));
        AssertFilesEqual(stale, Files(project.Root));
    }

    [TestMethod]
    public void InvalidFinalGraphFailsDuringPreviewWithoutWrites()
    {
        using var project = Fixture();
        var path = Path.Combine(project.Root, "resources/canonical/stories/story.json");
        var graph = GraphResourceEnvelope.FromJson(File.ReadAllText(path));
        graph.Graph = new GraphDocument();
        File.WriteAllText(path, graph.ToJson());
        var before = Files(project.Root);
        Assert.Throws<InvalidDataException>(() => new NamespaceProjectMigrationService().PreviewGlobal(project.Root, "Author"));
        AssertFilesEqual(before, Files(project.Root));
    }

    private static TestProjectDirectory Fixture()
    {
        var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        var start = GraphNodeFactory.CreateStoryStart("start");
        start.Ports.Clear();
        StoryStartSchema.InitializeDefault(start, "actor_port", StoryStartSchema.ActorInteraction, "actor");
        store.Stories.Create(new(GraphResourceKind.Story, "story", "Story", new GraphDocument([start])));
        store.Memberships.Create(new("story", new() { Actors = ["actor"] }));
        File.WriteAllText(Path.Combine(project.Root, "actors/actor.json"), ActorSerializer.Serialize(
            new IndividualActorResource { NpcId = "actor", DisplayName = "actor", HomeStoryId = "story" }, ActorIdPolicy.ExistingResource));
        new CanonicalGraphLayoutStore(project.Root).Save(GraphResourceKind.Story, "story",
            new Dictionary<string, DarkGreyRPG.Studio.Core.Stories.ProjectGraphNodeLayout> { ["start"] = new() { X = 12, Y = 34 } });
        new DarkGreyRPG.Studio.Core.Stories.ProjectGraphLayoutStore(project.Root).Save(
            new Dictionary<string, DarkGreyRPG.Studio.Core.Stories.ProjectGraphNodeLayout> { ["story"] = new() { X = 12, Y = 34 } });
        File.WriteAllText(Path.Combine(project.Root, "keep.txt"), "untouched");
        return project;
    }

    private static Dictionary<string, byte[]> Files(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);
    private static void AssertFilesEqual(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), actual.Keys.ToArray());
        foreach (var pair in expected) CollectionAssert.AreEqual(pair.Value, actual[pair.Key], pair.Key);
    }
}
