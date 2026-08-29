using System.Security.Cryptography;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Graphs.Migration;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalProjectMigrationTransactionTests
{
    [TestMethod]
    public void ApplyCreatesDetachedCanonicalFilesAndCompleteSourceBackup()
    {
        using var project = CreateProject();
        var preview = CanonicalProjectMigrationPreview.Preview(project.Root);
        var legacyBefore = preview.SourceFingerprints.ToDictionary(
            source => source.RelativePath, source => File.ReadAllBytes(source.FullPath), StringComparer.Ordinal);
        var transaction = new CanonicalProjectMigrationTransaction(
            utcNow: () => new DateTimeOffset(2026, 8, 29, 1, 2, 3, 456, TimeSpan.Zero));

        var result = transaction.Apply(preview);

        Assert.AreEqual(Path.GetFullPath(project.Root), result.Project);
        Assert.IsTrue(Directory.Exists(result.BackupPath));
        CollectionAssert.AreEqual(new[]
        {
            "resources/canonical/memberships/intro.json",
            "resources/canonical/sessions/hello.json",
            "resources/canonical/stories/intro.json",
        }, preview.ProposedWrites.Select(write => write.RelativePath).ToArray());
        CollectionAssert.AreEquivalent(preview.ProposedWrites.Select(x => x.FullPath).ToArray(), result.WrittenPaths.ToArray());
        foreach (var source in preview.SourceFingerprints)
        {
            var backup = Path.Combine(result.BackupPath, source.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(backup));
            CollectionAssert.AreEqual(legacyBefore[source.RelativePath], File.ReadAllBytes(source.FullPath));
            CollectionAssert.AreEqual(legacyBefore[source.RelativePath], File.ReadAllBytes(backup));
        }
        foreach (var candidate in preview.ProposedWrites)
        {
            Assert.AreEqual(candidate.Sha256, result.WrittenHashes[candidate.FullPath]);
            if (candidate.IsMembership)
                Assert.AreEqual(candidate.ResourceId,
                    CanonicalStoryMembershipSerializer.Deserialize(File.ReadAllText(candidate.FullPath)).StoryId);
            else
                Assert.AreEqual(candidate.ResourceId,
                    GraphResourceEnvelopeSerializer.Deserialize(File.ReadAllText(candidate.FullPath)).Id);
        }
        Assert.IsTrue(File.ReadAllText(Path.Combine(project.Root, "migration.log")).Contains("succeeded", StringComparison.Ordinal));

        var canonicalBeforeRepeat = preview.ProposedWrites.ToDictionary(
            write => write.FullPath, write => File.ReadAllBytes(write.FullPath), StringComparer.OrdinalIgnoreCase);
        var repeated = Assert.Throws<CanonicalProjectMigrationException>(() => transaction.Apply(preview));
        Assert.AreEqual("migration.transaction.preview.stale", repeated.Code);
        Assert.HasCount(1, Directory.EnumerateDirectories(Path.Combine(project.Root, ".migration-backups")).ToArray());
        foreach (var file in canonicalBeforeRepeat)
            CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key));
    }

    [TestMethod]
    public void SourceChangeAfterPreviewRejectsBeforeBackupOrCanonicalWrites()
    {
        using var project = CreateProject();
        var preview = CanonicalProjectMigrationPreview.Preview(project.Root);
        File.AppendAllText(project.ActorPath("guard"), "\n");

        var exception = Assert.Throws<CanonicalProjectMigrationException>(() =>
            new CanonicalProjectMigrationTransaction().Apply(preview));

        Assert.AreEqual("migration.transaction.preview.stale", exception.Code);
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "resources", "canonical")));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, ".migration-backups")));
    }

    [TestMethod]
    public void SourceSetAddOrRemoveAfterPreviewRejectsBeforeMutation()
    {
        using (var added = CreateProject())
        {
            var preview = CanonicalProjectMigrationPreview.Preview(added.Root);
            var actor = new ActorResource { SchemaVersion = 2, Id = "extra", DisplayName = "Extra", HomeStoryId = "intro" };
            File.WriteAllText(added.ActorPath("extra"), ActorSerializer.Serialize(actor, ActorIdPolicy.ExistingResource));

            var exception = Assert.Throws<CanonicalProjectMigrationException>(() =>
                new CanonicalProjectMigrationTransaction().Apply(preview));

            Assert.AreEqual("migration.transaction.preview.stale", exception.Code);
            Assert.IsFalse(Directory.Exists(Path.Combine(added.Root, ".migration-backups")));
        }

        using (var removed = CreateProject())
        {
            var preview = CanonicalProjectMigrationPreview.Preview(removed.Root);
            File.Delete(Path.Combine(removed.Root, "dialogues", "hello.json"));

            var exception = Assert.Throws<CanonicalProjectMigrationException>(() =>
                new CanonicalProjectMigrationTransaction().Apply(preview));

            Assert.AreEqual("migration.transaction.preview.stale", exception.Code);
            Assert.IsFalse(Directory.Exists(Path.Combine(removed.Root, ".migration-backups")));
        }
    }

    [TestMethod]
    public void DestinationCreatedAfterPreviewIsPreservedAndRejectsBeforeBackup()
    {
        using var project = CreateProject();
        var preview = CanonicalProjectMigrationPreview.Preview(project.Root);
        var destination = preview.ProposedWrites.Single(write => write.IsMembership).FullPath;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.WriteAllText(destination, "{}\n");

        var exception = Assert.Throws<CanonicalProjectMigrationException>(() =>
            new CanonicalProjectMigrationTransaction().Apply(preview));

        Assert.AreEqual("migration.transaction.preview.stale", exception.Code);
        Assert.AreEqual("{}\n", File.ReadAllText(destination));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, ".migration-backups")));
    }

    [TestMethod]
    public void NonApplicablePreviewRejectsWithoutTransactionArtifacts()
    {
        using var project = CreateProject();
        File.WriteAllText(Path.Combine(project.Root, "stories", "broken.json"), "{ invalid");
        var preview = CanonicalProjectMigrationPreview.Preview(project.Root);
        Assert.IsFalse(preview.CanApply);

        var exception = Assert.Throws<CanonicalProjectMigrationException>(() =>
            new CanonicalProjectMigrationTransaction().Apply(preview));

        Assert.AreEqual("migration.transaction.preview.invalid", exception.Code);
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, ".migration-backups")));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "resources", "canonical")));
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "migration.log")));
    }

    [TestMethod]
    public void ChangeDuringBackupIsCaughtBeforeCanonicalWrites()
    {
        using var project = CreateProject();
        var preview = CanonicalProjectMigrationPreview.Preview(project.Root);
        var injected = false;
        DateTimeOffset Clock()
        {
            if (!injected)
            {
                injected = true;
                var actor = new ActorResource { SchemaVersion = 2, Id = "late", DisplayName = "Late", HomeStoryId = "intro" };
                File.WriteAllText(project.ActorPath("late"), ActorSerializer.Serialize(actor, ActorIdPolicy.ExistingResource));
            }
            return new DateTimeOffset(2026, 8, 29, 1, 2, 3, 456, TimeSpan.Zero);
        }

        var exception = Assert.Throws<CanonicalProjectMigrationException>(() =>
            new CanonicalProjectMigrationTransaction(utcNow: Clock).Apply(preview));

        Assert.AreEqual("migration.transaction.preview.stale", exception.Code);
        Assert.IsTrue(Directory.Exists(Path.Combine(project.Root, ".migration-backups")));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "resources", "canonical")));
        Assert.IsTrue(File.ReadAllText(Path.Combine(project.Root, "migration.log")).Contains("failed", StringComparison.Ordinal));
    }

    [TestMethod]
    public void WriterFailureRemovesOnlyNewCanonicalFilesAndDirsAndKeepsBackup()
    {
        using var project = CreateProject();
        var resources = Path.Combine(project.Root, "resources");
        Directory.CreateDirectory(resources);
        var preserved = Path.Combine(resources, "keep.txt");
        File.WriteAllText(preserved, "keep");
        var preview = CanonicalProjectMigrationPreview.Preview(project.Root);
        var legacyBefore = preview.SourceFingerprints.ToDictionary(
            source => source.RelativePath, source => File.ReadAllBytes(source.FullPath), StringComparer.Ordinal);
        var writer = new FailOnWriteNumber(2);

        Assert.Throws<CanonicalProjectMigrationException>(() =>
            new CanonicalProjectMigrationTransaction(writer).Apply(preview));

        Assert.IsTrue(Directory.Exists(Path.Combine(project.Root, ".migration-backups")));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "resources", "canonical")));
        Assert.AreEqual("keep", File.ReadAllText(preserved));
        var backup = Directory.EnumerateDirectories(Path.Combine(project.Root, ".migration-backups")).Single();
        foreach (var source in preview.SourceFingerprints)
        {
            CollectionAssert.AreEqual(legacyBefore[source.RelativePath], File.ReadAllBytes(source.FullPath));
            CollectionAssert.AreEqual(legacyBefore[source.RelativePath], File.ReadAllBytes(
                Path.Combine(backup, source.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
        }
        Assert.IsTrue(File.ReadAllText(Path.Combine(project.Root, "migration.log")).Contains("failed", StringComparison.Ordinal));
    }

    private static TestProjectDirectory CreateProject()
    {
        var project = new TestProjectDirectory();
        Directory.CreateDirectory(Path.Combine(project.Root, "dialogues"));
        Directory.CreateDirectory(Path.Combine(project.Root, "stories"));
        var actor = new ActorResource { SchemaVersion = 2, Id = "guard", DisplayName = "Guard", HomeStoryId = "intro" };
        File.WriteAllText(project.ActorPath("guard"), ActorSerializer.Serialize(actor, ActorIdPolicy.ExistingResource));
        var dialogue = new DialogueResource
        {
            SchemaVersion = 2, Id = "hello", Title = "Hello", DisplayName = "Hello", HomeStoryId = "intro",
            Speakers = ["guard"], Entry = "line", Nodes = [DialogueNodeResource.Line("line", "guard", "Hello", null)],
        };
        File.WriteAllText(Path.Combine(project.Root, "dialogues", "hello.json"), DialogueSerializer.Serialize(dialogue, ActorIdPolicy.ExistingResource));
        var story = new StoryResource
        {
            SchemaVersion = 2, Id = "intro", DisplayName = "Intro", Title = "Intro", Entry = "end",
            OwnedResources = new() { Actors = ["guard"], Dialogues = ["hello"] },
            Nodes = [new StoryNodeResource { Id = "end", Type = "end" }],
        };
        File.WriteAllText(Path.Combine(project.Root, "stories", "intro.json"), StorySerializer.Serialize(story));
        return project;
    }

    private sealed class FailOnWriteNumber(int failureNumber) : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        private int _writes;

        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
        {
            if (Interlocked.Increment(ref _writes) == failureNumber)
                throw new IOException("injected write failure");
            _inner.Write(destinationPath, contents, validateTemporaryFile);
        }
    }
}
