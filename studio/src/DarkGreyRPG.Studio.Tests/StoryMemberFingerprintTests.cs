using System.Text.Json;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryMemberFingerprintTests
{
    [TestMethod]
    public void PresentationOrderIsIgnoredButTaskSettlementPriorityIsSemantic()
    {
        const string uid = "ST-2345-6789-ABCD-EFGH";
        foreach (var (kind, type, semantic) in new[] {
            ("story", "terminate", false), ("story", "logic_output", false),
            ("session", "end", false), ("session", "logic_output", false),
            ("task", "settle", true), ("task", "logic_output", false) })
        {
            var required = new StoryPackageRequiredResources();
            (kind == "story" ? required.CanonicalStories : kind == "session" ? required.Sessions : required.Tasks).Add("resource.json");
            var manifest = new StoryPackageManifest { StoryId = uid, RequiredResources = required };
            string Hash(int order)
            {
                var identity = kind == "story" ? JsonSerializer.SerializeToElement(uid)
                    : JsonSerializer.SerializeToElement(new { story_uid = uid, kind, local_id = "sample" });
                var data = JsonSerializer.SerializeToUtf8Bytes(new { id = identity, graph = new {
                    nodes = new[] { new { id = "boundary", type, properties = new { port_id = "stable", display_name = "Result", display_order = order } } },
                    connections = Array.Empty<object>() } });
                return StoryPackageSemanticFingerprint.Compute(manifest, new Dictionary<string, byte[]> { ["resource.json"] = data });
            }
            if (semantic) Assert.AreNotEqual(Hash(0), Hash(1), type);
            else Assert.AreEqual(Hash(0), Hash(1), kind + "/" + type);
        }
    }

    [TestMethod]
    public void SharedJavaAndStudioFingerprintVectorsMatchIndependentReference()
    {
        var count = 0;
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "FingerprintVectors.jsonl")))
        {
            using var document = JsonDocument.Parse(line);
            var vector = document.RootElement;
            string Compute()
            {
                var records = vector.GetProperty("records").EnumerateArray().Select(record => new StoryMemberContent(
                    record.GetProperty("role").GetString()!, record.GetProperty("identity").GetString()!,
                    JsonSerializer.Deserialize<JsonElement>(record.GetProperty("content").GetString()!))).ToArray();
                return StoryMemberFingerprint.Compute(StoryUid.Parse(vector.GetProperty("uid").GetString()!), records);
            }
            if (vector.GetProperty("valid").GetBoolean()) Assert.AreEqual(vector.GetProperty("hash").GetString(), Compute(), $"Vector {count}");
            else
            {
                var rejected = false;
                try { Compute(); }
                catch (Exception error) when (error is InvalidDataException or JsonException or FormatException) { rejected = true; }
                Assert.IsTrue(rejected, $"Invalid vector {count} was accepted");
            }
            count++;
        }
        Assert.IsGreaterThanOrEqualTo(30, count);
    }
}
