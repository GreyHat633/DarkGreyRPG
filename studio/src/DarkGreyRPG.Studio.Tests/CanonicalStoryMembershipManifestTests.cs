using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryMembershipManifestTests
{
    private const string Owner = "ST-2345-6789-ABCD-EFGH", External = "ST-JKLM-NPQR-STUV-WXYZ";
    private static string Address(string kind, string local, bool external = false) => (external ? External : Owner) + "~" + kind + "~" + local;
    private static CanonicalStoryMembershipManifest Manifest() => new(Owner,
        new() { Actors = [Address("actor", "b"), Address("actor", "a")], Items = [Address("item", "b"), Address("item", "a")], Tasks = [Address("task", "a")] },
        new() { ItemGroups = [Address("item_group", "a", true)], Sessions = [Address("session", "a", true), Address("session", "b", true)] })
        { DisplayOrder = new() { Actors = [Address("actor", "a"), Address("actor", "b")], Items = ["item_group:" + Address("item_group", "a", true), "item:" + Address("item", "b")] } };

    [TestMethod]
    public void RoundTripPreservesExactRootNestedShapeAndArrayOrder()
    {
        var manifest = Manifest(); var json = manifest.ToJson(); var restored = CanonicalStoryMembershipManifest.FromJson(json);
        using var document = JsonDocument.Parse(json);
        CollectionAssert.AreEqual(new[] { "schema_version", "identity_format", "story_id", "owned_resources", "referenced_resources", "display_order" }, document.RootElement.EnumerateObject().Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "actors", "items", "item_groups", "sessions", "tasks" }, document.RootElement.GetProperty("owned_resources").EnumerateObject().Select(p => p.Name).ToArray());
        Assert.AreEqual(JsonValueKind.Object, document.RootElement.GetProperty("owned_resources").GetProperty("actors")[0].ValueKind);
        CollectionAssert.AreEqual(manifest.OwnedResources.Actors, restored.OwnedResources.Actors);
        CollectionAssert.AreEqual(manifest.OwnedResources.Items, restored.OwnedResources.Items);
        CollectionAssert.AreEqual(manifest.ReferencedResources.Sessions, restored.ReferencedResources.Sessions);
        CollectionAssert.AreEqual(manifest.DisplayOrder.Items, restored.DisplayOrder.Items);
        Assert.IsTrue(json.EndsWith("}\n", StringComparison.Ordinal)); Assert.AreNotEqual('\n', json[^2]);
    }

    [TestMethod]
    public void ReorderedCurrentFieldsCanonicalizeWithoutChangingIdentity()
    {
        var original = Manifest().ToJson(false); var root = JsonNode.Parse(original)!.AsObject();
        var reordered = new JsonObject(root.Reverse().Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
        Assert.AreEqual(original, CanonicalStoryMembershipManifest.FromJson(reordered.ToJsonString()).ToJson(false));
    }

    [TestMethod]
    public void ConstructorAndGettersDoNotAliasCallerMembershipLists()
    {
        var owned = new CanonicalStoryMembershipSet { Actors = [Address("actor", "a")] };
        var manifest = new CanonicalStoryMembershipManifest(Owner, owned);
        owned.Actors.Add(Address("actor", "caller")); var read = manifest.OwnedResources; read.Actors.Add(Address("actor", "getter"));
        CollectionAssert.AreEqual(new[] { Address("actor", "a") }, manifest.OwnedResources.Actors);
    }

    [TestMethod]
    public void UnknownMissingAndDuplicateRootFieldsFailClosed()
    {
        RejectMutation(root => root["extra"] = true, "story.membership.root.member.unsupported");
        RejectMutation(root => root.AsObject().Remove("display_order"), "story.membership.root.member.required");
        RejectMutation(root => root["owned_resources"]!["dialogues"] = new JsonArray(), "story.membership.set.member.unsupported");
        var duplicated = Manifest().ToJson(false).Replace("\"schema_version\":4", "\"schema_version\":4,\"schema_version\":4", StringComparison.Ordinal);
        Assert.ThrowsExactly<CanonicalStoryMembershipException>(() => CanonicalStoryMembershipManifest.FromJson(duplicated));
    }

    [TestMethod]
    public void InvalidIdsDuplicatesAndOwnershipOverlapHaveStableKindCodes()
    {
        var bad = Manifest(); bad.StoryId = "../story"; AssertCode(bad, "story.membership.story_id.invalid");
        bad = Manifest(); bad.OwnedResources = new() { Sessions = [Address("session", "same"), Address("session", "same")] }; AssertCode(bad, "story.membership.session.id.duplicate");
        bad = Manifest(); bad.OwnedResources = new() { Tasks = [Address("task", "same")] }; bad.ReferencedResources = new() { Tasks = [Address("task", "same")] }; AssertCode(bad, "story.membership.task.ownership.overlap");
        bad = Manifest(); bad.OwnedResources = new() { Actors = ["Old:Actor"] }; AssertCode(bad, "story.membership.actor.id.invalid");
    }

    [TestMethod]
    public void NullNumericAndStringAddressesAreRejected()
    {
        foreach (var replacement in new[] { "null", "[3]", "[\"Old:task\"]" })
        {
            var root = JsonNode.Parse(Manifest().ToJson())!; root["owned_resources"]!["tasks"] = JsonNode.Parse(replacement);
            Assert.ThrowsExactly<CanonicalStoryMembershipException>(() => CanonicalStoryMembershipManifest.FromJson(root.ToJsonString()));
        }
    }

    [TestMethod]
    public void RetiredSchemasCannotSerializeOrDeserialize()
    {
        foreach (var version in new[] {1, 2, 3, 99})
        {
            var old = Manifest(); old.SchemaVersion = version; AssertCode(old, "story.membership.schema_version.unsupported");
            RejectMutation(root => root["schema_version"] = version, "story.membership.schema_version.unsupported");
        }
    }

    [TestMethod]
    public void OwnedAddressesCannotChangeOwnerOrResourceKind()
    {
        var wrong = Manifest(); wrong.OwnedResources = new() { Items = [Address("item", "a", true)] }; AssertCode(wrong, "story.membership.owner.mismatch");
        wrong = Manifest(); wrong.OwnedResources = new() { Actors = [Address("item", "a")] }; AssertCode(wrong, "story.membership.kind.mismatch");
    }

    [TestMethod]
    public void IdentityMarkerIsMandatoryAndExact()
    {
        RejectMutation(root => root.AsObject().Remove("identity_format"), "story.membership.root.member.required");
        RejectMutation(root => root["identity_format"] = "old", "story.membership.identity_format.unsupported");
    }

    private static void RejectMutation(Action<JsonNode> mutate, string code)
    {
        var root = JsonNode.Parse(Manifest().ToJson())!; mutate(root);
        Assert.AreEqual(code, Assert.ThrowsExactly<CanonicalStoryMembershipException>(() => CanonicalStoryMembershipManifest.FromJson(root.ToJsonString())).Code);
    }
    private static void AssertCode(CanonicalStoryMembershipManifest manifest, string code)
        => Assert.AreEqual(code, Assert.ThrowsExactly<CanonicalStoryMembershipException>(() => manifest.ToJson()).Code);
}
