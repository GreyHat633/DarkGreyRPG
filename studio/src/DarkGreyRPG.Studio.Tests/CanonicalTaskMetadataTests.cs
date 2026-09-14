using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalTaskMetadataTests
{
    [TestMethod]
    public void DescriptionSurvivesTaskRepositoryAndPackageExport()
    {
        using var project = new TestProjectDirectory();
        new DarkGreyRPG.Studio.Core.Stories.StoryRepository(project.Root).CreateStory("story", "Story");
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument([new GraphNode("start", "start", "Start")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("story"));
        var task = new CanonicalStoryResourceLifecycleService(store).CreateOwned("story", GraphResourceKind.Task, "task", "任务");
        task.TaskMetadata = new CanonicalTaskMetadata("整体任务说明\n保留第二行");
        store.Tasks.Replace(task);
        var output = Path.Combine(project.Root, "out");
        new DarkGreyRPG.Studio.Core.Packaging.StoryPackageExporter(project.Root).Build("story", output);
        var path = Directory.GetFiles(Path.Combine(output, "resources", "canonical", "tasks"), "*.json", SearchOption.AllDirectories).Single();
        Assert.AreEqual(task.TaskMetadata.Description, GraphResourceEnvelope.FromJson(File.ReadAllText(path)).TaskMetadata!.Description);
    }

    [TestMethod]
    public void FiveObjectiveTypesRoundTripAndRegionRejectsInvalidCoordinates()
    {
        foreach (var type in CanonicalTaskObjectiveSchema.ObjectiveTypes)
        {
            var node = GraphNodeFactory.Create(GraphScope.Task, "objective", "target");
            Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeType(node, type, "guard", out _));
            node.Properties["description"] = System.Text.Json.JsonSerializer.SerializeToElement("Test objective");
            if (type == "submit_item") node.Properties["actor_id"] = System.Text.Json.JsonSerializer.SerializeToElement("guard");
            if (type == "kill_entity") node.Properties["entity"] = System.Text.Json.JsonSerializer.SerializeToElement("guard");
            if (type is "collect_item" or "submit_item") node.Properties["item"] = System.Text.Json.JsonSerializer.SerializeToElement("apple");
            Assert.IsTrue(CanonicalTaskObjectiveSchema.IsValid(node), type);
            var envelope = new GraphResourceEnvelope(GraphResourceKind.Task, "task", "Task", new GraphDocument([node]));
            Assert.AreEqual(envelope.ToJson(), GraphResourceEnvelope.FromJson(envelope.ToJson()).ToJson());
            Assert.IsTrue(node.Ports.All(port => port.InterfaceKind == GraphInterfaceKind.Logic));
            if (type == "reach_region")
            {
                node.Properties.Remove("dimension_note");
                node.Properties["radius"] = System.Text.Json.JsonSerializer.SerializeToElement(0);
                Assert.IsTrue(CanonicalTaskObjectiveSchema.IsValid(node));
                node.Properties["radius"] = System.Text.Json.JsonSerializer.SerializeToElement(-1);
                Assert.IsFalse(CanonicalTaskObjectiveSchema.IsValid(node));
                node.Properties["dimension_id"] = System.Text.Json.JsonSerializer.SerializeToElement("bad");
                Assert.IsFalse(CanonicalTaskObjectiveSchema.IsValid(node));
            }
        }
    }

    [TestMethod]
    public void TaskDescriptionSurvivesEnvelopeAndEditableDocumentRoundTrip()
    {
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Task, "task", "任务", new GraphDocument())
            { TaskMetadata = new CanonicalTaskMetadata("背景说明\n第二段🙂") };
        var reopened = GraphResourceEnvelope.FromJson(envelope.ToJson());
        var document = GraphResourceScopeAdapter.OpenDocument(reopened, GraphScope.Task);
        Assert.AreEqual("背景说明\n第二段🙂", document.ToEnvelope().TaskMetadata!.Description);
        document.SetTaskMetadata(new CanonicalTaskMetadata("修改"));
        Assert.AreEqual("背景说明\n第二段🙂", envelope.TaskMetadata.Description);
        Assert.AreEqual("修改", document.ToEnvelope().TaskMetadata!.Description);
    }

    [TestMethod]
    public void OptionalMetadataDoesNotChangeOldResourcesOrLeakToOtherKinds()
    {
        foreach (var kind in Enum.GetValues<GraphResourceKind>())
        {
            var envelope = new GraphResourceEnvelope(kind, "resource", "资源", new GraphDocument());
            Assert.IsFalse(envelope.ToJson().Contains("task_metadata"));
            Assert.IsNull(GraphResourceEnvelope.FromJson(envelope.ToJson()).TaskMetadata);
            if (kind == GraphResourceKind.Task) continue;
            envelope.TaskMetadata = new CanonicalTaskMetadata("说明");
            Assert.ThrowsExactly<GraphResourceEnvelopeException>(() => envelope.ToJson());
        }
        var prefix = "{\"schema_version\":1,\"resource_kind\":\"task\",\"id\":\"t\",\"display_name\":\"T\",\"graph\":{\"nodes\":[],\"connections\":[]},\"task_metadata\":";
        foreach (var invalid in new[] { "null", "{}", "{\"description\":3}", "{\"description\":\"x\",\"other\":true}", "{\"description\":\"x\",\"description\":\"y\"}" })
            Assert.ThrowsExactly<GraphResourceEnvelopeException>(() => GraphResourceEnvelope.FromJson(prefix + invalid + "}"));
    }
}
