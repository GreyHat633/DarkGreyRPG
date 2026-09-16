using System.Security.Cryptography;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class NamespaceOldProjectMigrationProbeTests
{
    [TestMethod]
    [TestCategory("ExternalReadOnlyFixture")]
    public void B3KillSlimesCopyMigratesReopensAndExportsWithoutChangingSource()
    {
        var configured = Environment.GetEnvironmentVariable("DGR_B4_MIGRATION_FIXTURE");
        if (string.IsNullOrEmpty(configured)) Assert.Inconclusive("Set DGR_B4_MIGRATION_FIXTURE to the read-only B3 project fixture.");
        var source = Path.GetFullPath(configured!);
        using var target = new TestProjectDirectory(createProjectFile: false);
        var files = SourceFiles(source);
        var hashes = files.ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
        try
        {
            foreach (var path in files)
            {
                var destination = Path.Combine(target.Root, Path.GetRelativePath(source, path));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(path, destination);
            }
            var service = new NamespaceProjectMigrationService();
            var preview = service.PreviewGlobal(target.Root, "MigrationProbe");
            service.Apply(preview);
            var result = NamespaceProjectMigrationService.ReadProject(target.Root);
            Assert.IsTrue(result.Graphs.Any(graph => graph.ResourceKind == GraphResourceKind.Story && graph.Id == "MigrationProbe:kill_slimes"));
            Assert.IsTrue(result.Actors.Any(actor => actor.GroupId == "MigrationProbe:slimes"));
            var task = result.Graphs.Single(graph => graph.ResourceKind == GraphResourceKind.Task && graph.Id == "MigrationProbe:kill_slimes");
            Assert.IsTrue(task.Graph!.Nodes.Any(node => node.Properties.TryGetValue("entity", out var entity) && entity.GetString() == "MigrationProbe:slimes"));
            var exportRoot = Environment.GetEnvironmentVariable("DGR_B4_MIGRATION_EXPORTS");
            Assert.IsFalse(string.IsNullOrWhiteSpace(exportRoot), "Set the E-drive artifact directory for the exported fixture packages.");
            var output = Path.GetFullPath(exportRoot!);
            Assert.IsTrue(output.StartsWith("E:\\Java\\MinecraftMod\\DarkGreyRPG\\.tooling\\", StringComparison.OrdinalIgnoreCase));
            Directory.CreateDirectory(output);
            foreach (var story in result.Graphs.Where(graph => graph.ResourceKind == GraphResourceKind.Story))
            {
                var package = new DgrsStoryPackageExporter(target.Root).Build(story.Id, Path.Combine(output, DgrResourceId.PackageFileName(story.Id)));
                Assert.AreEqual(story.Id, package.Manifest.StoryId);
            }
            service.Undo(preview);
            foreach (var file in files)
                CollectionAssert.AreEqual(File.ReadAllBytes(file), File.ReadAllBytes(Path.Combine(target.Root, Path.GetRelativePath(source, file))));
        }
        finally
        {
            CollectionAssert.AreEquivalent(files, SourceFiles(source), "Original project file set changed.");
            foreach (var pair in hashes)
                Assert.AreEqual(pair.Value, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pair.Key))), "Original fixture was modified.");
        }
    }

    private static string[] SourceFiles(string root)
    {
        var directories = new[] { "actors", "items", "item_groups", "resources/canonical", "resources/editor" };
        var files = new List<string> { Path.Combine(root, "project.json") };
        foreach (var directory in directories)
        {
            var path = Path.Combine(root, directory);
            if (!Directory.Exists(path)) continue;
            files.AddRange(Directory.EnumerateFiles(path, "*.json", new EnumerationOptions
            {
                RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint,
            }));
        }
        return files.Order(StringComparer.Ordinal).ToArray();
    }
}
