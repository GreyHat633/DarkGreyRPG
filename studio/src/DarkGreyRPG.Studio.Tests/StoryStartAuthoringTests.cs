using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryStartAuthoringTests
{
    [TestMethod]
    public void FactoryCreatesOneValidOpaqueDefaultTriggerAndOncePolicy()
    {
        var node = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "opaque_start");
        Assert.IsTrue(StoryStartSchema.IsValid(node));
        var trigger = StoryStartSchema.ReadTriggers(node).Single();
        Assert.AreEqual("opaque_start", trigger.PortId);
        Assert.AreEqual("启动条件 1", trigger.DisplayName);
        Assert.AreEqual(StoryStartSchema.RegionEntry, trigger.TriggerType);
        Assert.AreEqual(StoryStartSchema.Once, node.Properties[StoryStartSchema.RepeatPolicyProperty].GetString());
        Assert.AreEqual("opaque_start", node.Ports.Single().Id);
    }

    [TestMethod]
    public void TriggerEditsKeepMetadataAndPortsAtomicAndRemoveEdgesOnConfirmation()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "first");
        var graph = new GraphDocument([start, GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "end")],
            [new GraphConnection("start", "second", "end", "flow_in", GraphInterfaceKind.Flow)]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow, dynamicPortIdSource: () => "second");

        Assert.IsTrue(session.AddStoryStartTrigger("start", "角色", StoryStartSchema.ActorInteraction,
            new Dictionary<string, JsonElement> { ["actor_id"] = JsonSerializer.SerializeToElement("guard") }));
        Assert.IsTrue(session.RenameStoryStartTrigger("start", "second", "老板"));
        Assert.IsTrue(session.ReorderStoryStartTrigger("start", "second", 0));
        Assert.AreEqual("second", graph.Nodes.Single(node => node.Id == "start").Ports.OrderBy(port => port.Order).First().Id);
        Assert.IsFalse(session.RemoveStoryStartTrigger("start", "second"));
        Assert.AreEqual("graph.story.start.trigger.references.confirmation_required", session.LastValidationIssues.Single().Code);
        Assert.IsTrue(session.RemoveStoryStartTrigger("start", "second", confirmReferencedRemoval: true));
        Assert.HasCount(1, graph.Nodes.Single(node => node.Id == "start").Ports);
        Assert.IsEmpty(graph.Connections);
        Assert.IsTrue(StoryStartSchema.IsValid(graph.Nodes.Single(node => node.Id == "start")));
        Assert.IsTrue(session.Undo());
        Assert.HasCount(1, graph.Connections);
    }

    [TestMethod]
    public void UnsupportedTriggerAndDisabledRepeatPolicyFailClosed()
    {
        var node = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "stable");
        node.Properties[StoryStartSchema.TriggersProperty] = JsonSerializer.SerializeToElement(new[]
        {
            new { port_id = "stable", display_name = "x", trigger_type = "unsupported", trigger_properties = new { }, order = 0 },
        });
        CollectionAssert.Contains(StoryStartSchema.Validate(node).Select(issue => issue.Code).ToArray(),
            "graph.story.start.trigger.type.unsupported");

        var graph = new GraphDocument([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "stable")]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        Assert.IsFalse(session.SetStoryStartRepeatPolicy("start", StoryStartSchema.Daily));
        Assert.AreEqual("graph.story.start.repeat_policy.unsupported", session.LastValidationIssues.Single().Code);
        Assert.IsTrue(session.SetStoryStartRepeatPolicy("start", StoryStartSchema.Repeatable));
        Assert.AreEqual(StoryStartSchema.Repeatable, graph.Nodes.Single().Properties[StoryStartSchema.RepeatPolicyProperty].GetString());
    }

    [TestMethod]
    public void TriggerPayloadsAreStrictAndTypeChangesAreAtomic()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "opaque");
        var graph = new GraphDocument([start]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);

        Assert.IsTrue(session.SetStoryStartTriggerType("start", "opaque", StoryStartSchema.ActorInteraction, "guard"));
        Assert.IsFalse(session.SetStoryStartTriggerProperties("start", "opaque",
            new Dictionary<string, JsonElement>()));
        Assert.AreEqual("graph.story.start.trigger.properties.required", session.LastValidationIssues.Single().Code);
        Assert.IsTrue(session.SetStoryStartTriggerType("start", "opaque", StoryStartSchema.RegionEntry));
        var slot = StoryStartSchema.ReadTriggers(graph.Nodes.Single()).Single();
        Assert.AreEqual(StoryStartSchema.RegionEntry, slot.TriggerType);
        Assert.HasCount(5, slot.TriggerProperties.EnumerateObject());
        Assert.IsFalse(session.SetStoryStartTriggerType("start", "opaque", StoryStartSchema.EnterStory));
        Assert.AreEqual("graph.story.start.trigger.type.unsupported", session.LastValidationIssues.Single().Code);
        Assert.AreEqual("opaque", graph.Nodes.Single().Ports.Single().Id);
    }
}
