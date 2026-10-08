using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ResourceCopyRemapperTests
{
    private const string A = "ST-2345-6789-ABCD-EFGH", B = "ST-JKLM-NPQR-STUV-WXYZ", C = "ST-AAAA-BBBB-CCCC-DDDD";
    private static string Key(string owner, ResourceKind kind, string local) => new ResourceAddress(StoryUid.Parse(owner), kind, local).ToKey();
    private static GraphNode Node(string id, string type, object properties) => new(id, type, id,
        properties: JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(JsonSerializer.Serialize(properties)));

    [TestMethod]
    public void TypedGraphReferencesChangeWithoutChangingProseNodeOrPortIdentity()
    {
        var actor = Key(A, ResourceKind.Actor, "boss"); var copiedActor = Key(B, ResourceKind.Actor, "boss");
        var task = Key(A, ResourceKind.Task, "boss"); var copiedTask = Key(B, ResourceKind.Task, "boss");
        var map = new ResourceCopyRemapper();
        map.Add(DgrResourceKind.Story, A, B); map.Add(DgrResourceKind.Actor, actor, copiedActor); map.Add(DgrResourceKind.Task, task, copiedTask);
        var graph = new GraphDocument([
            Node("boss", "start", new { triggers = new[] { new { trigger_type = "interact_actor", display_name = "boss", port_id = "boss", trigger_properties = new { actor_id = actor } } } }),
            Node("task", "task", new { resource_id = task }),
            Node("message", "action", new { action_type = "send_message", message = actor }),
        ]);
        var before = new GraphResourceEnvelope(GraphResourceKind.Story, A, "boss", graph);
        var after = map.Rewrite(before);
        Assert.AreEqual(B, after.Id); Assert.AreEqual("boss", after.DisplayName); Assert.AreEqual("boss", after.Graph!.Nodes[0].Id);
        var trigger = after.Graph.Nodes[0].Properties["triggers"][0];
        Assert.AreEqual(copiedActor, trigger.GetProperty("trigger_properties").GetProperty("actor_id").GetString());
        Assert.AreEqual("boss", trigger.GetProperty("port_id").GetString());
        Assert.AreEqual("boss", trigger.GetProperty("display_name").GetString());
        Assert.AreEqual(copiedTask, after.Graph.Nodes[1].Properties["resource_id"].GetString());
        Assert.AreEqual(actor, after.Graph.Nodes[2].Properties["message"].GetString());
        Assert.AreEqual(task, before.Graph!.Nodes[1].Properties["resource_id"].GetString());
    }

    [TestMethod]
    public void MembershipReferencesFollowCopyMapWhileUnmappedOwnedIdentityStays()
    {
        var oldActor = Key(A, ResourceKind.Actor, "boss"); var newActor = Key(B, ResourceKind.Actor, "boss");
        var oldItem = Key(A, ResourceKind.Item, "coin"); var newItem = Key(B, ResourceKind.Item, "coin");
        var ownActor = Key(C, ResourceKind.Actor, "boss");
        var map = new ResourceCopyRemapper(); map.Add(DgrResourceKind.Actor, oldActor, newActor); map.Add(DgrResourceKind.Item, oldItem, newItem);
        var source = new CanonicalStoryMembershipManifest(C, new() { Actors = [ownActor] }, new() { Actors = [oldActor], Items = [oldItem] })
            { DisplayOrder = new() { Actors = [ownActor, oldActor], Items = ["item:" + oldItem] } };
        var result = map.Rewrite(source);
        Assert.AreEqual(C, result.StoryId);
        CollectionAssert.AreEqual(new[] { ownActor }, result.OwnedResources.Actors);
        CollectionAssert.AreEqual(new[] { newActor }, result.ReferencedResources.Actors);
        CollectionAssert.AreEqual(new[] { "item:" + newItem }, result.DisplayOrder.Items);
        CollectionAssert.AreEqual(new[] { oldActor }, source.ReferencedResources.Actors);
    }

    [TestMethod]
    public void DestinationCollisionAndConflictingOwnerFailBeforeMutation()
    {
        var from = Key(A, ResourceKind.Actor, "boss"); var to = Key(B, ResourceKind.Actor, "boss");
        var map = new ResourceCopyRemapper(); map.Add(DgrResourceKind.Actor, from, to);
        Assert.Throws<InvalidOperationException>(() => map.ValidateCollisions([new(DgrResourceKind.Actor, from), new(DgrResourceKind.Actor, to)]));
        Assert.Throws<InvalidOperationException>(() => map.Add(DgrResourceKind.Actor, from, Key(C, ResourceKind.Actor, "boss")));
        map.ValidateCollisions([new(DgrResourceKind.Actor, from), new(DgrResourceKind.Task, Key(B, ResourceKind.Task, "boss"))]);
        Assert.Throws<ArgumentException>(() => map.Add(DgrResourceKind.Actor, "Author:boss", to));
    }

    [TestMethod]
    public void TaskTargetsAndSessionSpeakerCopyButDescriptionsStayAndNativeTargetsReject()
    {
        var actor = Key(A, ResourceKind.Actor, "slimes"); var copiedActor = Key(B, ResourceKind.Actor, "slimes");
        var items = Key(A, ResourceKind.ItemGroup, "coins"); var copiedItems = Key(B, ResourceKind.ItemGroup, "coins");
        var map = new ResourceCopyRemapper(); map.Add(DgrResourceKind.Actor, actor, copiedActor); map.Add(DgrResourceKind.ItemGroup, items, copiedItems);
        var task = map.Rewrite(new GraphResourceEnvelope(GraphResourceKind.Task, Key(A, ResourceKind.Task, "task"), "slimes", new([
            Node("kill", "objective", new { objective_type = "kill_entity", entity = actor, description = actor }),
            Node("collect", "objective", new { objective_type = "collect_item", item = items }),
        ])));
        Assert.AreEqual(copiedActor, task.Graph!.Nodes[0].Properties["entity"].GetString());
        Assert.AreEqual(actor, task.Graph.Nodes[0].Properties["description"].GetString());
        Assert.AreEqual(copiedItems, task.Graph.Nodes[1].Properties["item"].GetString());
        Assert.Throws<ArgumentException>(() => map.Rewrite(new GraphResourceEnvelope(GraphResourceKind.Task,
            Key(A, ResourceKind.Task, "bad"), "Bad", new([
                Node("native", "objective", new { objective_type = "kill_entity", entity = "minecraft:slime" })]))));
        var session = map.Rewrite(new GraphResourceEnvelope(GraphResourceKind.Session, Key(A, ResourceKind.Session, "session"), "slimes", new([
            Node("line", "line", new { speaker_actor_id = actor, pages = new[] { new { page_id = "page", text = actor } } })])));
        Assert.AreEqual(copiedActor, session.Graph!.Nodes[0].Properties["speaker_actor_id"].GetString());
        Assert.AreEqual(actor, session.Graph.Nodes[0].Properties["pages"][0].GetProperty("text").GetString());
    }
}
