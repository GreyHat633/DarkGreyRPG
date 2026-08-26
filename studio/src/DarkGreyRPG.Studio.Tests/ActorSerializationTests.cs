using DarkGreyRPG.Studio.Core.Actors;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ActorSerializationTests
{
    [TestMethod]
    public void SerializeUsesRuntimeFieldNamesAndUtf8FriendlyText()
    {
        var resource = new ActorResource
        {
            Id = "teacher",
            DisplayName = "老师",
            Notes = "学校中的任务 NPC",
            Tags = ["school", "quest"],
        };

        var json = ActorSerializer.Serialize(resource, ActorIdPolicy.NewResource);

        StringAssert.Contains(json, "\"schema_version\": 1");
        StringAssert.Contains(json, "\"display_name\": \"老师\"");
        Assert.IsTrue(json.EndsWith('\n'));
        Assert.IsFalse(json.Contains("SchemaVersion", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DeserializeReadsExistingRuntimeActor()
    {
        const string json = """
            {
              "schema_version": 1,
              "id": "tavern_owner",
              "display_name": "酒馆老板",
              "notes": "Phase 1 actor binding acceptance target.",
              "tags": ["example", "tavern"]
            }
            """;

        var resource = ActorSerializer.Deserialize(json, "tavern_owner.json");

        Assert.AreEqual("tavern_owner", resource.Id);
        Assert.AreEqual("酒馆老板", resource.DisplayName);
        CollectionAssert.AreEqual(new[] { "example", "tavern" }, resource.Tags);
    }

    [TestMethod]
    public void SerializeAndDeserializeRoundTrip()
    {
        var original = new ActorResource
        {
            Id = "teacher",
            DisplayName = "老师",
            Notes = "学校中的任务 NPC",
            Tags = ["school", "quest"],
        };

        var loaded = ActorSerializer.Deserialize(
            ActorSerializer.Serialize(original, ActorIdPolicy.NewResource),
            "teacher.json");

        Assert.AreEqual(original.Id, loaded.Id);
        Assert.AreEqual(original.DisplayName, loaded.DisplayName);
        Assert.AreEqual(original.Notes, loaded.Notes);
        CollectionAssert.AreEqual(original.Tags, loaded.Tags);
    }

    [TestMethod]
    public void DeserializeRejectsUnknownFields()
    {
        const string json = """
            {
              "schema_version": 1,
              "id": "teacher",
              "display_name": "Teacher",
              "notes": "",
              "tags": [],
              "health": 20
            }
            """;

        Assert.ThrowsExactly<ActorDataException>(() => ActorSerializer.Deserialize(json));
    }

    [TestMethod]
    public void DeserializeRejectsFileNameMismatch()
    {
        const string json = """
            {
              "schema_version": 1,
              "id": "teacher",
              "display_name": "Teacher",
              "notes": "",
              "tags": []
            }
            """;

        Assert.ThrowsExactly<ActorValidationException>(
            () => ActorSerializer.Deserialize(json, "wrong_name.json"));
    }
}
