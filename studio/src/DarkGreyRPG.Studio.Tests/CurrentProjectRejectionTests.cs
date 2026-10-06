using System.Security.Cryptography;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Projects;

namespace DarkGreyRPG.Studio.Tests;
[TestClass]
public sealed class CurrentProjectRejectionTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void RetiredProjectNeverMigratesOrReplacesTheOpenProject(int schema)
    {
        using var current = new TestProjectDirectory(false);
        using var rejected = new TestProjectDirectory();
        var service = new ProjectService(); var active = service.CreateProject(current.Root, "current", "Current");
        var project = JsonNode.Parse(File.ReadAllText(Path.Combine(rejected.Root, "project.json")))!;
        project["schema_version"] = schema; project.AsObject().Remove("identity_format");
        File.WriteAllText(Path.Combine(rejected.Root, "project.json"), project.ToJsonString());
        File.WriteAllText(Path.Combine(rejected.Root, "actors", "old.json"), "{\"schema_version\":1,\"id\":\"Old:actor\"}");
        var before = Snapshot(rejected.Root);
        Assert.ThrowsExactly<ProjectException>(() => service.OpenProject(rejected.Root));
        Assert.AreSame(active, service.CurrentProject);
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), Snapshot(rejected.Root).Keys.ToArray());
        foreach (var file in before) Assert.AreEqual(file.Value, Snapshot(rejected.Root)[file.Key]);
    }

    [TestMethod]
    public void CurrentProjectMarkerCannotAuthorizeRetiredResourceDirectories()
    {
        using var rejected = new TestProjectDirectory();
        foreach (var directory in new[] { "stories", "dialogues", "quests" })
        {
            Directory.CreateDirectory(Path.Combine(rejected.Root, directory));
            var file = Path.Combine(rejected.Root, directory, "old.json"); File.WriteAllText(file, "{}");
            var before = Snapshot(rejected.Root);
            Assert.ThrowsExactly<ProjectException>(() => new ProjectService().OpenProject(rejected.Root));
            CollectionAssert.AreEquivalent(before.Keys.ToArray(), Snapshot(rejected.Root).Keys.ToArray());
            foreach (var item in before) Assert.AreEqual(item.Value, Snapshot(rejected.Root)[item.Key]);
            File.Delete(file);
        }
    }
    private static Dictionary<string, string> Snapshot(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(file => Path.GetRelativePath(root, file), file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
}
