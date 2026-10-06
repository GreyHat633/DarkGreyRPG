using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Actors;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ActorSerializationTests
{
    private const string Owner = "ST-2345-6789-ABCD-EFGH";
    private static IndividualActorResource Individual() => new()
    {
        NpcId = Owner + "~actor~teacher", HomeStoryId = Owner,
        DisplayName = "老师", Tags = ["school", "quest"],
    };
    private static string Json() => ActorSerializer.Serialize(Individual(), ActorIdPolicy.NewResource);

    [TestMethod]
    public void SerializeUsesCurrentStructuredIdentityAndUtf8FriendlyText()
    {
        var json = Json(); var root = JsonNode.Parse(json)!;
        Assert.AreEqual(5, root["schema_version"]!.GetValue<int>());
        Assert.AreEqual("story-uid-v1", root["identity_format"]!.GetValue<string>());
        Assert.AreEqual(Owner, root["npc_id"]!["story_uid"]!.GetValue<string>());
        Assert.AreEqual("actor", root["npc_id"]!["kind"]!.GetValue<string>());
        Assert.AreEqual("teacher", root["npc_id"]!["local_id"]!.GetValue<string>());
        StringAssert.Contains(json, "老师"); Assert.IsTrue(json.EndsWith('\n'));
        Assert.IsNull(root["notes"]); Assert.IsNull(root["id"]);
    }

    [TestMethod]
    public void SerializeAndDeserializeRoundTrip()
    {
        var original = Individual(); var loaded = ActorSerializer.Deserialize(Json());
        Assert.AreEqual(original.Id, loaded.Id); Assert.AreEqual(original.HomeStoryId, loaded.HomeStoryId);
        Assert.AreEqual(original.DisplayName, loaded.DisplayName); CollectionAssert.AreEqual(original.Tags, loaded.Tags);
        Assert.IsInstanceOfType<IndividualActorResource>(loaded);
    }

    [TestMethod]
    public void IdentityIsIndependentOfPhysicalFileName()
    {
        Assert.AreEqual(Individual().Id, ActorSerializer.Deserialize(Json(), "unrelated-file.json").Id);
    }

    [TestMethod]
    public void DeserializeRejectsUnknownFields()
    {
        var root = JsonNode.Parse(Json())!; root["health"] = 20;
        Assert.ThrowsExactly<ActorDataException>(() => ActorSerializer.Deserialize(root.ToJsonString()));
    }

    [TestMethod]
    public void CollectiveUsesStructuredGroupIdentity()
    {
        var json = ActorSerializer.Serialize(new CollectiveActorResource
        { GroupId = Owner + "~actor~guards", HomeStoryId = Owner, DisplayName = "卫兵", Tags = ["capital"] }, ActorIdPolicy.NewResource);
        var root = JsonNode.Parse(json)!;
        Assert.IsNull(root["npc_id"]); Assert.AreEqual(Owner, root["group_id"]!["story_uid"]!.GetValue<string>());
        Assert.IsInstanceOfType<CollectiveActorResource>(ActorSerializer.Deserialize(json));
    }

    [TestMethod]
    public void RetiredSchemasAreRejectedWithoutMigration()
    {
        foreach (var schema in new[] {1, 2, 3, 4})
        {
            var root = JsonNode.Parse(Json())!; root["schema_version"] = schema;
            var error = Assert.ThrowsExactly<ActorValidationException>(() => ActorSerializer.Deserialize(root.ToJsonString()));
            StringAssert.Contains(error.Message, "unsupported");
        }
    }

    [TestMethod]
    public void CurrentSchemaRejectsBothIdentitiesAndLegacyFields()
    {
        var both = JsonNode.Parse(Json())!; both["group_id"] = both["npc_id"]!.DeepClone();
        Assert.ThrowsExactly<ActorValidationException>(() => ActorSerializer.Deserialize(both.ToJsonString()));
        var notes = JsonNode.Parse(Json())!; notes["notes"] = "legacy";
        Assert.ThrowsExactly<ActorValidationException>(() => ActorSerializer.Deserialize(notes.ToJsonString()));
    }

    [TestMethod]
    public void CurrentSchemaRejectsOldNamespaceAndMismatchedOwner()
    {
        var old = JsonNode.Parse(Json())!; old["npc_id"] = "Old:teacher";
        Assert.ThrowsExactly<ActorDataException>(() => ActorSerializer.Deserialize(old.ToJsonString()));
        var wrong = JsonNode.Parse(Json())!; wrong["home_story_id"] = "ST-JKLM-NPQR-STUV-WXYZ";
        Assert.ThrowsExactly<ActorValidationException>(() => ActorSerializer.Deserialize(wrong.ToJsonString()));
    }

    [TestMethod]
    public void DuplicateIdentityFieldsAreRejected()
    {
        var json = Json().Replace("\"local_id\": \"teacher\"", "\"local_id\": \"teacher\", \"local_id\": \"other\"", StringComparison.Ordinal);
        Assert.ThrowsExactly<ActorDataException>(() => ActorSerializer.Deserialize(json));
    }
}
