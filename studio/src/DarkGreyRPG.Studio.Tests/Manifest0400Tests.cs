using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class Manifest0400Tests
{
    private const string StoryA = "ST-2345-6789-ABCD-EFGH";
    private const string StoryB = "ST-JKLM-NPQR-STUV-WXYZ";

    [TestMethod]
    [DataRow("dialogues", "[]")]
    [DataRow("quests", "[]")]
    [DataRow("dialogues", "[\"dialogues/old.json\"]")]
    [DataRow("quests", "[\"quests/old.json\"]")]
    public void SingleAndGroupRejectRetiredFieldsWithoutChangingSource(string field, string value)
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        var lifecycle = new CanonicalStoryLifecycleService(store);
        lifecycle.Create(StoryA, "A");
        lifecycle.Create(StoryB, "B");
        var output = Path.Combine(project.Root, "Export");
        var current = new StoryPackageExporter(project.Root).Build(StoryA, output, "0.4.0.0").Manifest;
        var json = JsonNode.Parse(current.ToJson())!;
        Assert.AreEqual(3, json["schema_version"]!.GetValue<int>());
        Assert.AreEqual(3, json["format_version"]!.GetValue<int>());
        Assert.IsNull(json["required_resources"]![field]);
        json["required_resources"]![field] = JsonNode.Parse(value);
        var invalid = json.ToJsonString();
        Assert.ThrowsExactly<StoryPackageException>(() => StoryPackageManifest.Parse(invalid));
        Assert.AreEqual(invalid, json.ToJsonString());
        var other = new StoryPackageExporter(project.Root).Build(StoryB, Path.Combine(project.Root, "Other"), "0.4.0.0").Manifest;
        var group = new DgrsGroupManifest { DisplayName = "Group", Members = [current, other] };
        var groupJson = JsonNode.Parse(group.ToJson())!;
        Assert.AreEqual(3, groupJson["format_version"]!.GetValue<int>());
        groupJson["members"]![0]!["required_resources"]![field] = JsonNode.Parse(value);
        Assert.ThrowsExactly<StoryPackageException>(() => DgrsGroupManifest.Parse(groupJson.ToJsonString()));
        Assert.AreEqual(current.ToJson(), StoryPackageManifest.Read(Path.Combine(output, "manifest.json")).ToJson());
    }

    [TestMethod]
    public void CurrentManifestRejectsMissingIdentityNestedDuplicatesAndPriorVersions()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        new CanonicalStoryLifecycleService(store).Create(StoryA, "A");
        var manifest = new StoryPackageExporter(project.Root).Build(StoryA, Path.Combine(project.Root, "Export"), "0.4.0.0").Manifest;
        foreach (var field in new[] { "identity_format", "format", "format_version", "producer", "producer_version", "schema_version", "package_id", "package_version", "story_id", "story_schema_version", "required_resources" })
        {
            var json = JsonNode.Parse(manifest.ToJson())!;
            json.AsObject().Remove(field);
            Assert.ThrowsExactly<StoryPackageException>(() => StoryPackageManifest.Parse(json.ToJsonString()), field);
        }
        foreach (var field in new[] { "format_version", "schema_version", "story_schema_version" })
        {
            var json = JsonNode.Parse(manifest.ToJson())!;
            json[field] = 2;
            Assert.ThrowsExactly<StoryPackageException>(() => StoryPackageManifest.Parse(json.ToJsonString()), field);
        }
        var duplicate = manifest.ToJson().Replace("\"actors\": []", "\"actors\": [], \"actors\": []", StringComparison.Ordinal);
        Assert.AreNotEqual(manifest.ToJson(), duplicate);
        Assert.ThrowsExactly<StoryPackageException>(() => StoryPackageManifest.Parse(duplicate));
        var optional = JsonNode.Parse(manifest.ToJson())!;
        optional["required_resources"]!.AsObject().Remove("actors");
        Assert.IsEmpty(StoryPackageManifest.Parse(optional.ToJsonString()).RequiredResources.Actors);
    }
}
