using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ResourceRenameMapTests
{
    [TestMethod]
    public void TypedGraphReferencesChangeWithoutChangingProseNodeOrPortIdentity()
    {
        var map = new ResourceRenameMap();
        map.Add(DgrResourceKind.Story, "story", "Author:story");
        map.Add(DgrResourceKind.Actor, "boss", "Author:boss");
        map.Add(DgrResourceKind.Task, "boss", "Other:boss");
        var graph = new GraphDocument([
            Node("boss", "start", "{\"triggers\":[{\"trigger_type\":\"interact_actor\",\"display_name\":\"boss\",\"port_id\":\"boss\",\"trigger_properties\":{\"actor_id\":\"boss\"}}]}"),
            Node("task", "task", "{\"resource_id\":\"boss\"}"),
            Node("message", "action", "{\"action_type\":\"send_message\",\"message\":\"boss\"}"),
        ]);
        var before = new GraphResourceEnvelope(GraphResourceKind.Story, "story", "boss", graph);
        var after = map.Rewrite(before);
        Assert.AreEqual("Author:story", after.Id);
        Assert.AreEqual("boss", after.DisplayName);
        Assert.AreEqual("boss", after.Graph!.Nodes[0].Id);
        var trigger = after.Graph.Nodes[0].Properties["triggers"][0];
        Assert.AreEqual("Author:boss", trigger.GetProperty("trigger_properties").GetProperty("actor_id").GetString());
        Assert.AreEqual("boss", trigger.GetProperty("port_id").GetString());
        Assert.AreEqual("boss", trigger.GetProperty("display_name").GetString());
        Assert.AreEqual("Other:boss", after.Graph.Nodes[1].Properties["resource_id"].GetString());
        Assert.AreEqual("boss", after.Graph.Nodes[2].Properties["message"].GetString());
        Assert.AreEqual("boss", before.Graph!.Nodes[1].Properties["resource_id"].GetString());
    }

    [TestMethod]
    public void CustomMembershipReferencesFollowGlobalRenameWhileOwnedIdentityStays()
    {
        var map = new ResourceRenameMap();
        map.Add(DgrResourceKind.Actor, "Old:boss", "New:boss");
        map.Add(DgrResourceKind.Item, "Old:coin", "New:coin");
        var source = new CanonicalStoryMembershipManifest("Custom:story",
            new() { Actors = ["Custom:boss"] }, new() { Actors = ["Old:boss"], Items = ["Old:coin"] })
            { DisplayOrder = new() { Actors = ["Custom:boss", "Old:boss"], Items = ["item:Old:coin"] } };
        var result = map.Rewrite(source);
        Assert.AreEqual("Custom:story", result.StoryId);
        CollectionAssert.AreEqual(new[] { "Custom:boss" }, result.OwnedResources.Actors);
        CollectionAssert.AreEqual(new[] { "New:boss" }, result.ReferencedResources.Actors);
        CollectionAssert.AreEqual(new[] { "item:New:coin" }, result.DisplayOrder.Items);
        CollectionAssert.AreEqual(new[] { "Old:boss" }, source.ReferencedResources.Actors);
    }

    [TestMethod]
    public void DestinationCollisionAndConflictingSharedOwnerFailBeforeMutation()
    {
        var map = new ResourceRenameMap();
        map.Add(DgrResourceKind.Actor, "Old:boss", "New:boss");
        Assert.Throws<InvalidOperationException>(() => map.ValidateCollisions([
            new(DgrResourceKind.Actor, "Old:boss"), new(DgrResourceKind.Actor, "New:boss")]));
        Assert.Throws<InvalidOperationException>(() => map.Add(DgrResourceKind.Actor, "Old:boss", "Third:boss"));
        map.ValidateCollisions([new(DgrResourceKind.Actor, "Old:boss"), new(DgrResourceKind.Task, "New:boss")]);
    }

    [TestMethod]
    public void TaskTargetsAndSessionSpeakerRewriteButDescriptionsAndNativeTargetsStay()
    {
        var map = new ResourceRenameMap();
        map.Add(DgrResourceKind.Actor, "slimes", "Author:slimes");
        map.Add(DgrResourceKind.ItemGroup, "coins", "Author:coins");
        var task = map.Rewrite(new GraphResourceEnvelope(GraphResourceKind.Task, "task", "slimes", new GraphDocument([
            Node("kill", "objective", "{\"objective_type\":\"kill_entity\",\"entity\":\"slimes\",\"description\":\"slimes\"}"),
            Node("native", "objective", "{\"objective_type\":\"kill_entity\",\"entity\":\"minecraft:slime\"}"),
            Node("collect", "objective", "{\"objective_type\":\"collect_item\",\"item\":\"coins\"}"),
        ])));
        Assert.AreEqual("Author:slimes", task.Graph!.Nodes[0].Properties["entity"].GetString());
        Assert.AreEqual("slimes", task.Graph.Nodes[0].Properties["description"].GetString());
        Assert.AreEqual("minecraft:slime", task.Graph.Nodes[1].Properties["entity"].GetString());
        Assert.AreEqual("Author:coins", task.Graph.Nodes[2].Properties["item"].GetString());
        var session = map.Rewrite(new GraphResourceEnvelope(GraphResourceKind.Session, "session", "slimes", new GraphDocument([
            Node("line", "line", "{\"speaker_actor_id\":\"slimes\",\"text\":\"slimes\"}")])));
        Assert.AreEqual("Author:slimes", session.Graph!.Nodes[0].Properties["speaker_actor_id"].GetString());
        Assert.AreEqual("slimes", session.Graph.Nodes[0].Properties["text"].GetString());
    }

    private static GraphNode Node(string id, string type, string properties)
        => new(id, type, id, properties: JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(properties));
}
