using System.Security.Cryptography;
using System.Text;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Graphs.Migration;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalProjectMigrationPreviewTests
{
    [TestMethod]
    public void PreviewIsReadOnlyDeterministicAndProducesCanonicalCandidates()
    {
        using var project = new TestProjectDirectory();
        Directory.CreateDirectory(Path.Combine(project.Root, "dialogues"));
        Directory.CreateDirectory(Path.Combine(project.Root, "quests"));
        Directory.CreateDirectory(Path.Combine(project.Root, "stories"));

        var actor = new ActorResource { SchemaVersion = 2, Id = "guard", DisplayName = "Guard", HomeStoryId = "intro" };
        File.WriteAllText(project.ActorPath("guard"), ActorSerializer.Serialize(actor, ActorIdPolicy.ExistingResource));
        var dialogue = new DialogueResource
        {
            SchemaVersion = 2, Id = "shared", Title = "Greeting", DisplayName = "Greeting",
            HomeStoryId = "intro", Speakers = ["guard"], Entry = "line",
            Nodes = [DialogueNodeResource.Line("line", "guard", "Hello", null)],
        };
        File.WriteAllText(Path.Combine(project.Root, "dialogues", "shared.json"), DialogueSerializer.Serialize(dialogue, ActorIdPolicy.ExistingResource));
        var quest = new QuestResource
        {
            SchemaVersion = 2, Id = "shared", Title = "Errand", DisplayName = "Errand", Description = "Do it",
            HomeStoryId = "intro", Objectives = [QuestObjectiveResource.Kill("kill", "Kill", "minecraft:zombie", 1)],
            ObjectiveGroups = [new ObjectiveGroupResource { Id = "group", Mode = "ALL", Objectives = ["kill"] }],
        };
        File.WriteAllText(Path.Combine(project.Root, "quests", "shared.json"), QuestSerializer.Serialize(quest, ActorIdPolicy.ExistingResource));
        var story = new StoryResource
        {
            SchemaVersion = 2, Id = "intro", DisplayName = "Intro", Title = "Intro", Entry = "end",
            OwnedResources = new() { Actors = ["guard"], Dialogues = ["shared"], Quests = ["shared"] },
            Nodes = [new StoryNodeResource { Id = "end", Type = "end" }],
        };
        File.WriteAllText(Path.Combine(project.Root, "stories", "intro.json"), StorySerializer.Serialize(story));

        var before = Snapshot(project.Root);
        var first = CanonicalProjectMigrationPreview.PreviewProject(project.Root);
        var second = CanonicalProjectMigrationPreview.PreviewProject(project.Root);

        Assert.IsTrue(first.CanApply, string.Join("; ", first.Issues.Select(x => x.Code)));
        Assert.HasCount(4, first.ProposedWrites);
        CollectionAssert.AreEqual(new[]
        {
            "resources/canonical/memberships/intro.json",
            "resources/canonical/sessions/shared.json",
            "resources/canonical/stories/intro.json",
            "resources/canonical/tasks/shared.json",
        }, first.ProposedWrites.Select(x => x.RelativePath).ToArray());
        CollectionAssert.AreEqual(first.ProposedWrites.Select(x => x.RelativePath).ToArray(), second.ProposedWrites.Select(x => x.RelativePath).ToArray());
        CollectionAssert.AreEqual(first.ProposedWrites.Select(x => x.Sha256).ToArray(), second.ProposedWrites.Select(x => x.Sha256).ToArray());
        var membership = first.MembershipCandidates.Single().Manifest;
        CollectionAssert.AreEqual(new[] { "guard" }, membership.OwnedResources.Actors);
        CollectionAssert.AreEqual(new[] { "shared" }, membership.OwnedResources.Sessions);
        CollectionAssert.AreEqual(new[] { "shared" }, membership.OwnedResources.Tasks);
        membership.StoryId = "mutated";
        Assert.AreEqual("intro", first.MembershipCandidates.Single().Manifest.StoryId);
        var sessionEnvelope = first.DialoguePreviews.Single().Envelope!;
        sessionEnvelope.Id = "mutated";
        Assert.AreEqual("shared", first.DialoguePreviews.Single().Envelope!.Id);
        foreach (var candidate in first.ProposedWrites)
            Assert.AreEqual(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(candidate.Contents))).ToLowerInvariant(), candidate.Sha256);
        Assert.AreEqual(before, Snapshot(project.Root));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "resources", "canonical")));
    }

    [TestMethod]
    public void PreviewFailsClosedOnMalformedDuplicateMismatchMissingReferencesAndExistingCanonicalData()
    {
        using var project = new TestProjectDirectory();
        Directory.CreateDirectory(Path.Combine(project.Root, "dialogues"));
        Directory.CreateDirectory(Path.Combine(project.Root, "quests"));
        Directory.CreateDirectory(Path.Combine(project.Root, "stories"));
        var canonicalStories = Path.Combine(project.Root, "resources", "canonical", "stories");
        Directory.CreateDirectory(canonicalStories);
        File.WriteAllText(Path.Combine(canonicalStories, "existing.json"), "{}\n");
        File.WriteAllText(project.ActorPath("broken"), "{ not-json");

        var dialogue = new DialogueResource
        {
            SchemaVersion = 2, Id = "shared", Title = "Shared", DisplayName = "Shared",
            HomeStoryId = "intro", Speakers = ["missing_actor"], Entry = "line",
            Nodes = [DialogueNodeResource.Line("line", "missing_actor", "Hello", null)],
        };
        var dialogueJson = DialogueSerializer.Serialize(dialogue, ActorIdPolicy.ExistingResource);
        File.WriteAllText(Path.Combine(project.Root, "dialogues", "wrong_name.json"), dialogueJson);
        File.WriteAllText(Path.Combine(project.Root, "dialogues", "duplicate.json"), dialogueJson);

        var unsupportedQuest = new QuestResource
        {
            SchemaVersion = 2, Id = "unsupported", Title = "Unsupported", DisplayName = "Unsupported", Description = "Unsupported",
            HomeStoryId = "intro",
            Objectives = [QuestObjectiveResource.Reach("reach", "Reach", 0, 1, 2, 3, 4)],
            ObjectiveGroups = [new ObjectiveGroupResource { Id = "all", Mode = "ALL", Objectives = ["reach"] }],
        };
        File.WriteAllText(Path.Combine(project.Root, "quests", "unsupported.json"),
            QuestSerializer.Serialize(unsupportedQuest, ActorIdPolicy.ExistingResource));

        var story = new StoryResource
        {
            SchemaVersion = 2, Id = "intro", DisplayName = "Intro", Title = "Intro", Entry = "end",
            OwnedResources = new() { Actors = ["ghost"], Dialogues = ["missing_dialogue"], Quests = ["missing_quest"] },
            Nodes = [new StoryNodeResource { Id = "end", Type = "end" }],
        };
        File.WriteAllText(Path.Combine(project.Root, "stories", "intro.json"), StorySerializer.Serialize(story));

        var result = CanonicalProjectMigrationPreview.Preview(project.Root);

        Assert.IsFalse(result.CanApply);
        var codes = result.Issues.Select(issue => issue.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var code in new[]
        {
            "migration.project.canonical.nonempty",
            "migration.project.actor.parse",
            "migration.project.dialogue.filename.mismatch",
            "migration.project.dialogue.id.duplicate",
            "migration.project.dialogue.actor.missing",
            "migration.quest.objective.type.unsupported",
            "migration.project.membership.actor.missing",
            "migration.project.membership.session.missing",
            "migration.project.membership.task.missing",
            "migration.project.write.path.duplicate",
        }) CollectionAssert.Contains(codes.ToArray(), code, code);
    }

    [TestMethod]
    public void PreviewAppliesTrackedAcceptanceProjectDeterministicallyWithoutWriting()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, ".tooling", "2.1-acceptance", "DarkGrey-2.1-Acceptance");
        var before = Snapshot(project);
        var result = CanonicalProjectMigrationPreview.Preview(project);

        Assert.IsTrue(result.CanApply, string.Join("; ", result.Issues.Select(x => x.Code)));
        Assert.IsNotEmpty(result.StoryPreviews);
        Assert.IsNotEmpty(result.MembershipCandidates);
        Assert.IsTrue(result.StoryPreviews.Single(x => x.Id == "royal_mystery").CanApply);
        Assert.HasCount(10, result.ProposedWrites);
        CollectionAssert.AreEqual(new[]
        {
            "resources/canonical/memberships/empire_route.json",
            "resources/canonical/memberships/kingdom_route.json",
            "resources/canonical/memberships/royal_mystery.json",
            "resources/canonical/memberships/uncategorized.json",
            "resources/canonical/sessions/final_confrontation.json",
            "resources/canonical/stories/empire_route.json",
            "resources/canonical/stories/kingdom_route.json",
            "resources/canonical/stories/royal_mystery.json",
            "resources/canonical/stories/uncategorized.json",
            "resources/canonical/tasks/evidence.json",
        }, result.ProposedWrites.Select(x => x.RelativePath).ToArray());
        Assert.AreEqual(result.ProposedWrites.Select(x => x.RelativePath).ToArray().Length,
            result.ProposedWrites.Select(x => x.RelativePath).Distinct(StringComparer.Ordinal).Count());
        var repeated = CanonicalProjectMigrationPreview.Preview(project);
        CollectionAssert.AreEqual(result.ProposedWrites.Select(x => x.RelativePath).ToArray(),
            repeated.ProposedWrites.Select(x => x.RelativePath).ToArray());
        CollectionAssert.AreEqual(result.ProposedWrites.Select(x => x.Sha256).ToArray(),
            repeated.ProposedWrites.Select(x => x.Sha256).ToArray());
        Assert.AreEqual(before, Snapshot(project));
    }

    private static string Snapshot(string root)
        => string.Join("\n", Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains("bin", StringComparison.OrdinalIgnoreCase) && !path.Contains("obj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => Path.GetRelativePath(root, path) + "=" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, ".tooling", "2.1-acceptance"))) return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the tracked 2.1 acceptance project.");
    }
}
