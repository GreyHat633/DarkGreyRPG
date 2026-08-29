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
            new[] { "actors", "sessions", "tasks" },
            document.RootElement.GetProperty("owned_resources").EnumerateObject()
                .Select(property => property.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "actor_b", "actor_a" }, restored.OwnedResources.Actors);
        CollectionAssert.AreEqual(new[] { "session_a", "session_b" }, restored.ReferencedResources.Sessions);
        Assert.IsTrue(json.EndsWith("}\n", StringComparison.Ordinal));
        Assert.AreNotEqual('\n', json[^2]);
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
            (valid.Replace(",\"referenced_resources\":{\"actors\":[],\"sessions\":[\"session_a\",\"session_b\"],\"tasks\":[\"task_b\"]}", string.Empty, StringComparison.Ordinal), "story.membership.root.member.required"),
            (valid.Replace("\"schema_version\":1", "\"schema_version\":2", StringComparison.Ordinal), "story.membership.schema_version.unsupported"),
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

    private static CanonicalStoryMembershipManifest Manifest()
        => new(
            "story",
            new CanonicalStoryMembershipSet
            {
                Actors = ["actor_b", "actor_a"],
                Sessions = [],
                Tasks = ["task_a"],
            },
            new CanonicalStoryMembershipSet
            {
                Actors = [],
                Sessions = ["session_a", "session_b"],
                Tasks = ["task_b"],
            });

    private static void AssertCode(CanonicalStoryMembershipManifest manifest, string code)
        => Assert.AreEqual(code,
            Assert.ThrowsExactly<CanonicalStoryMembershipException>(
                () => CanonicalStoryMembershipSerializer.Serialize(manifest)).Code);
}
