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

    [TestMethod]
    public void Schema3IndividualUsesOnlyIdentityFields()
    {
        var json = ActorSerializer.Serialize(new IndividualActorResource
        {
            NpcId = "tavern_boss",
            DisplayName = "酒馆老板",
            Tags = ["tavern"],
            HomeStoryId = "intro",
        }, ActorIdPolicy.NewResource);

        StringAssert.Contains(json, "\"schema_version\": 4");
        StringAssert.Contains(json, "\"type\": \"individual\"");
        StringAssert.Contains(json, "\"npc_id\": \"tavern_boss\"");
        Assert.IsFalse(json.Contains("\"id\"", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("\"notes\"", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("health", StringComparison.Ordinal));
        Assert.IsInstanceOfType<IndividualActorResource>(ActorSerializer.Deserialize(json, "tavern_boss.json"));
    }

    [TestMethod]
    public void Schema3CollectiveUsesGroupIdentity()
    {
        var json = ActorSerializer.Serialize(new CollectiveActorResource
        {
            GroupId = "guards",
            DisplayName = "卫兵",
            Tags = ["capital"],
            HomeStoryId = "intro",
        }, ActorIdPolicy.NewResource);

        StringAssert.Contains(json, "\"type\": \"collective\"");
        StringAssert.Contains(json, "\"group_id\": \"guards\"");
        Assert.IsFalse(json.Contains("npc_id", StringComparison.Ordinal));
        Assert.IsInstanceOfType<CollectiveActorResource>(ActorSerializer.Deserialize(json, "guards.json"));
    }

    [TestMethod]
    public void LegacySchemasRetainShapeAndNotesOnOrdinaryRoundTrip()
    {
        const string schema1 = """
            { "schema_version": 1, "id": "old", "display_name": "Old", "notes": "keep", "tags": [] }
            """;
        const string schema2 = """
            { "schema_version": 2, "id": "old2", "display_name": "Old 2", "notes": "keep 2", "tags": [], "home_story_id": "intro" }
            """;

        foreach (var (json, fileName, schema) in new[] { (schema1, "old.json", 1), (schema2, "old2.json", 2) })
        {
            var resource = ActorSerializer.Deserialize(json, fileName);
            var document = ActorDocument.FromResource(resource, fileName);
            var saved = ActorSerializer.Serialize(document.ToResource(), ActorIdPolicy.ExistingResource);
            StringAssert.Contains(saved, $"\"schema_version\": {schema}");
            StringAssert.Contains(saved, "\"notes\": \"keep");
            Assert.IsFalse(saved.Contains("\"type\"", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void Schema3RejectsBothIdentitiesAndLegacyFields()
    {
        const string both = """
            { "schema_version": 4, "type": "individual", "npc_id": "hero", "group_id": "heroes", "display_name": "Hero", "tags": [], "home_story_id": "intro" }
            """;
        const string notes = """
            { "schema_version": 4, "type": "individual", "npc_id": "hero", "display_name": "Hero", "tags": [], "home_story_id": "intro", "notes": "legacy" }
            """;

        Assert.ThrowsExactly<ActorValidationException>(() => ActorSerializer.Deserialize(both));
        Assert.ThrowsExactly<ActorValidationException>(() => ActorSerializer.Deserialize(notes));
    }
}
