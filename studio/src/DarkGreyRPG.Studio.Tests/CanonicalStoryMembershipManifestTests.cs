using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryMembershipManifestTests
{
    [TestMethod]
    public void RoundTripPreservesExactRootNestedShapeAndArrayOrder()
    {
        var manifest = Manifest();
        var json = manifest.ToJson();
        var restored = CanonicalStoryMembershipManifest.FromJson(json);
        using var document = JsonDocument.Parse(json);

        CollectionAssert.AreEqual(
            new[] { "schema_version", "story_id", "owned_resources", "referenced_resources" },
            document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        CollectionAssert.AreEqual(
            new[] { "actors", "items", "item_groups", "sessions", "tasks" },
            document.RootElement.GetProperty("owned_resources").EnumerateObject()
                .Select(property => property.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "actor_b", "actor_a" }, restored.OwnedResources.Actors);
        CollectionAssert.AreEqual(new[] { "item_b", "item_a" }, restored.OwnedResources.Items);
        CollectionAssert.AreEqual(new[] { "group_a" }, restored.ReferencedResources.ItemGroups);
        CollectionAssert.AreEqual(new[] { "session_a", "session_b" }, restored.ReferencedResources.Sessions);
        Assert.IsTrue(json.EndsWith("}\n", StringComparison.Ordinal));
        Assert.AreNotEqual('\n', json[^2]);
    }

    [TestMethod]
    public void ReorderedValidSchemaTwoFieldsAreAcceptedAndCanonicalizedOnSerialization()
    {
        var canonical = Manifest().ToJson(indented: false).TrimEnd();
        var reordered = canonical.Replace(
            "\"actors\":[\"actor_b\",\"actor_a\"],\"items\":[\"item_b\",\"item_a\"],\"item_groups\":[],\"sessions\":[],\"tasks\":[\"task_a\"]",
            "\"tasks\":[\"task_a\"],\"item_groups\":[],\"actors\":[\"actor_b\",\"actor_a\"],\"sessions\":[],\"items\":[\"item_b\",\"item_a\"]",
            StringComparison.Ordinal);

        var restored = CanonicalStoryMembershipManifest.FromJson(reordered);

        Assert.AreEqual(canonical + "\n", restored.ToJson(indented: false));
    }

    [TestMethod]
    public void ConstructorAndGettersDoNotAliasCallerMembershipLists()
    {
        var owned = new CanonicalStoryMembershipSet { Actors = ["actor_a"] };
        var manifest = new CanonicalStoryMembershipManifest("story", owned);
        owned.Actors.Add("caller_only");
        var read = manifest.OwnedResources;
        read.Actors.Add("getter_only");

        CollectionAssert.AreEqual(new[] { "actor_a" }, manifest.OwnedResources.Actors);
    }

    [TestMethod]
    public void UnknownMissingAndLegacyRootFieldsFailClosed()
    {
        var valid = Manifest().ToJson(indented: false).TrimEnd();
        foreach (var (json, code) in new[]
        {
            (valid.Replace("\"story_id\":", "\"extra\":true,\"story_id\":", StringComparison.Ordinal), "story.membership.root.member.unsupported"),
            (valid.Replace(",\"referenced_resources\":{\"actors\":[],\"items\":[],\"item_groups\":[\"group_a\"],\"sessions\":[\"session_a\",\"session_b\"],\"tasks\":[\"task_b\"]}", string.Empty, StringComparison.Ordinal), "story.membership.root.member.required"),
            (valid.Replace("\"schema_version\":2", "\"schema_version\":99", StringComparison.Ordinal), "story.membership.schema_version.unsupported"),
            (valid.Replace("\"actors\":[\"actor_b\",\"actor_a\"]", "\"actors\":[\"actor_b\",\"actor_a\"],\"dialogues\":[]", StringComparison.Ordinal), "story.membership.set.member.unsupported"),
            ("{\"schema_version\":2,\"id\":\"legacy\",\"owned_resources\":{}}", "story.membership.root.member.unsupported"),
        })
            Assert.AreEqual(code,
                Assert.ThrowsExactly<CanonicalStoryMembershipException>(
                    () => CanonicalStoryMembershipSerializer.Deserialize(json)).Code);
    }

    [TestMethod]
    public void InvalidIdsDuplicatesAndOwnershipOverlapHaveStableKindCodes()
    {
        var invalidStory = Manifest();
        invalidStory.StoryId = "../story";
        AssertCode(invalidStory, "story.membership.story_id.invalid");

        var duplicate = Manifest();
        duplicate.OwnedResources = new CanonicalStoryMembershipSet { Sessions = ["same", "same"] };
        AssertCode(duplicate, "story.membership.session.id.duplicate");

        var overlap = Manifest();
        overlap.OwnedResources = new CanonicalStoryMembershipSet { Tasks = ["task_a"] };
        overlap.ReferencedResources = new CanonicalStoryMembershipSet { Tasks = ["task_a"] };
        AssertCode(overlap, "story.membership.task.ownership.overlap");

        var invalidActor = Manifest();
        invalidActor.OwnedResources = new CanonicalStoryMembershipSet { Actors = ["Actor"] };
        AssertCode(invalidActor, "story.membership.actor.id.invalid");
    }

    [TestMethod]
    public void NullOrNonStringMembershipArraysAreRejected()
    {
        var valid = Manifest().ToJson(indented: false).TrimEnd();
        Assert.AreEqual("story.membership.list.invalid",
            Assert.ThrowsExactly<CanonicalStoryMembershipException>(() =>
                CanonicalStoryMembershipSerializer.Deserialize(
                    valid.Replace("\"tasks\":[\"task_a\"]", "\"tasks\":null", StringComparison.Ordinal))).Code);
        Assert.AreEqual("story.membership.id.type",
            Assert.ThrowsExactly<CanonicalStoryMembershipException>(() =>
                CanonicalStoryMembershipSerializer.Deserialize(
                    valid.Replace("\"tasks\":[\"task_a\"]", "\"tasks\":[3]", StringComparison.Ordinal))).Code);
    }

    [TestMethod]
    public void LegacySchemaOneKeepsItsExactShapeOnOrdinarySerialization()
    {
        var legacy = new CanonicalStoryMembershipManifest(
            "legacy",
            new CanonicalStoryMembershipSet { Actors = ["actor"], Sessions = ["session"], Tasks = ["task"] })
        { SchemaVersion = CanonicalStoryMembershipManifest.LegacySchemaVersion };

        var json = legacy.ToJson(indented: false);
        using var document = JsonDocument.Parse(json);
        var fields = document.RootElement.GetProperty("owned_resources").EnumerateObject()
            .Select(property => property.Name).ToArray();
        CollectionAssert.AreEqual(new[] { "actors", "sessions", "tasks" }, fields);
        var restored = CanonicalStoryMembershipManifest.FromJson(json);
        Assert.AreEqual(CanonicalStoryMembershipManifest.LegacySchemaVersion, restored.SchemaVersion);
        Assert.AreEqual(json, restored.ToJson(indented: false));
    }

    [TestMethod]
    public void LegacySchemaOneCannotSilentlyDropItemMembership()
    {
        var legacy = new CanonicalStoryMembershipManifest(
            "legacy",
            new CanonicalStoryMembershipSet { Items = ["key"] })
        { SchemaVersion = CanonicalStoryMembershipManifest.LegacySchemaVersion };

        Assert.AreEqual("story.membership.schema_version.legacy_items",
            Assert.ThrowsExactly<CanonicalStoryMembershipException>(() => legacy.ToJson()).Code);
    }

    private static CanonicalStoryMembershipManifest Manifest()
        => new(
            "story",
            new CanonicalStoryMembershipSet
            {
                Actors = ["actor_b", "actor_a"],
                Items = ["item_b", "item_a"],
                ItemGroups = [],
                Sessions = [],
                Tasks = ["task_a"],
            },
            new CanonicalStoryMembershipSet
            {
                Actors = [],
                Items = [],
                ItemGroups = ["group_a"],
                Sessions = ["session_a", "session_b"],
                Tasks = ["task_b"],
            });

    private static void AssertCode(CanonicalStoryMembershipManifest manifest, string code)
        => Assert.AreEqual(code,
            Assert.ThrowsExactly<CanonicalStoryMembershipException>(
                () => CanonicalStoryMembershipSerializer.Serialize(manifest)).Code);
}
