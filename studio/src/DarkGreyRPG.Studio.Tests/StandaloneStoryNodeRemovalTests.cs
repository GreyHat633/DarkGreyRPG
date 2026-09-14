using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StandaloneStoryNodeRemovalTests
{
    [TestMethod]
    public void RetiredStandaloneNodesCannotBeAuthoredLoadedOrExported()
    {
        foreach (var type in new[] { "interact_actor", "enter_region", "enter_story" })
        {
            Assert.IsNull(GraphNodeDefinitionRegistry.Get(GraphScope.StoryFlow, type));
            foreach (var compatibility in new[] { false, true })
                Assert.IsFalse(GraphScopePolicy.CanCreateNode(GraphScope.StoryFlow, type, compatibility));
            var json = $$$"""
                {"schema_version":1,"resource_kind":"story","id":"removed","display_name":"Removed",
                 "graph":{"nodes":[{"id":"old","type":"{{{type}}}","display_name":"Old","ports":[],"properties":{}}],"connections":[]}}
                """;
            Assert.ThrowsExactly<GraphResourceEnvelopeException>(() => GraphResourceEnvelope.FromJson(json));
            var envelope = new GraphResourceEnvelope(GraphResourceKind.Story, "removed", "Removed",
                new GraphDocument([new GraphNode("old", type, "Old")]));
            Assert.ThrowsExactly<GraphResourceEnvelopeException>(() => envelope.ToJson());
        }
    }

    [TestMethod]
    public void ExistingStartTriggerTypesAndRepeatPolicyRoundTripUnchanged()
    {
        foreach (var type in StoryStartSchema.SupportedTriggerTypes)
        {
            var start = new GraphNode("start", "start", "开始");
            StoryStartSchema.InitializeDefault(start, "entry", type,
                actorId: type == StoryStartSchema.ActorInteraction ? "guard" : null,
                logicPortId: type == StoryStartSchema.Logic ? "condition" : null);
            start.Properties[StoryStartSchema.RepeatPolicyProperty] = JsonSerializer.SerializeToElement(StoryStartSchema.Repeatable);
            var before = GraphSerializer.Serialize(new GraphDocument([start]));
            var envelope = new GraphResourceEnvelope(GraphResourceKind.Story, "kept", "Kept", new GraphDocument([start]));
            var reopened = GraphResourceEnvelope.FromJson(envelope.ToJson()).Graph!;
            Assert.AreEqual(before, GraphSerializer.Serialize(reopened), type);
            Assert.IsEmpty(StoryStartSchema.Validate(reopened.Nodes.Single()), type);
        }
        Assert.IsTrue(CanonicalTaskObjectiveSchema.ObjectiveTypes.Contains("interact_actor"));
    }
}
