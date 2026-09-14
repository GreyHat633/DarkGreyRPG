using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Migration;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalLegacyMigrationPreviewTests
{
    [TestMethod]
    public void DialoguePreviewMapsLineChoiceJumpAndNamedEndWithoutMutatingSource()
    {
        var source = new DialogueResource
        {
            Id = "dialogue",
            Title = "Legacy Dialogue",
            DisplayName = "Legacy Display",
            Speakers = ["npc"],
            Entry = "jump",
            Nodes =
            [
                DialogueNodeResource.Jump("jump", "line"),
                DialogueNodeResource.Line("line", "npc", "Hello", "choice"),
                DialogueNodeResource.Choice("choice", "Choose", [new DialogueChoiceResource { Text = "Yes", Next = "end" }]),
                DialogueNodeResource.End("end", "accepted"),
            ],
        };
        var before = DialogueSerializer.Serialize(source, ActorIdPolicy.ExistingResource);

        var result = CanonicalLegacyMigrationPreview.PreviewDialogue(source);

        Assert.IsTrue(result.CanApply, string.Join("; ", result.Issues.Select(issue => issue.Code)));
        Assert.AreEqual("dialogue", result.SourceKindName);
        Assert.IsNotNull(result.Envelope);
        var graph = result.Envelope!.Graph!;
        Assert.AreEqual(1, graph.Nodes.Count(node => node.Type == "start"));
        Assert.IsNotNull(graph.Nodes.SingleOrDefault(node => node.Id == "line" && node.Type == "line"));
        Assert.IsNotNull(graph.Nodes.SingleOrDefault(node => node.Id == "choice" && node.Type == "choice"));
        Assert.IsNotNull(graph.Nodes.SingleOrDefault(node => node.Id == "jump" && node.Type == "legacy_jump"));
        var end = graph.Nodes.Single(node => node.Id == "end");
        Assert.AreEqual("accepted", end.Properties["port_id"].GetString());
        Assert.IsTrue(graph.Connections.Any(edge => edge.FromNodeId == "start" && edge.ToNodeId == "line"));
        Assert.IsTrue(graph.Connections.Any(edge => edge.FromNodeId == "line" && edge.ToNodeId == "choice"));
        Assert.IsTrue(graph.Connections.Any(edge => edge.FromNodeId == "choice" && edge.ToNodeId == "end"));
        Assert.AreEqual(before, DialogueSerializer.Serialize(source, ActorIdPolicy.ExistingResource));
    }

    [TestMethod]
    public void QuestPreviewMapsSingleAllAnyAndSequenceDeterministically()
    {
        foreach (var mode in new[] { "ALL", "ANY", "SEQUENCE" })
        {
            var source = Quest(mode);
            var first = CanonicalLegacyMigrationPreview.PreviewQuest(source);
            var second = CanonicalLegacyMigrationPreview.PreviewQuest(source);
            Assert.IsTrue(first.CanApply, string.Join("; ", first.Issues.Select(issue => issue.Code)));
            Assert.AreEqual(first.Envelope!.ToJson(), second.Envelope!.ToJson());
            var graph = first.Envelope.Graph!;
            Assert.HasCount(1, graph.Nodes.Where(node => node.Type == "settle"));
            Assert.IsFalse(graph.Nodes.Any(node => node.Type == "activate"));
            Assert.AreEqual(2, graph.Nodes.Count(node => node.Type == "objective"));
            Assert.IsTrue(graph.Nodes.Single(node => node.Type == "objective" && node.Id == "one").Properties.ContainsKey("description"));
            Assert.IsTrue(graph.Nodes.Single(node => node.Type == "settle").Ports.Any(port => port.Id == "completion"));
            if (mode == "ALL") Assert.IsTrue(graph.Nodes.Any(node => node.Type == "and"));
            if (mode == "ANY") Assert.IsTrue(graph.Nodes.Any(node => node.Type == "or"));
            if (mode == "SEQUENCE") Assert.IsTrue(graph.Connections.Any(edge => edge.FromNodeId == "one" && edge.ToNodeId == "two"));
        }
    }

    [TestMethod]
    public void InvalidInputReturnsStableIssuesAndNoEnvelope()
    {
        var source = new QuestResource
        {
            Id = "quest",
            Title = "Quest",
            DisplayName = "Quest",
            Description = "Broken",
            Objectives = [QuestObjectiveResource.Reach("reach", "Reach", 0, 1, 2, 3, 1)],
            ObjectiveGroups = [new ObjectiveGroupResource { Id = "all", Mode = "ALL", Objectives = ["reach"] }],
        };
        var result = CanonicalLegacyMigrationPreview.PreviewQuest(source);
        Assert.IsFalse(result.CanApply);
        Assert.IsNull(result.Envelope);
        Assert.AreEqual("migration.quest.objective.type.unsupported", result.Issues.Single(issue => issue.Code == "migration.quest.objective.type.unsupported").Code);
    }

    [TestMethod]
    public void TrackedAcceptanceDialogueAndQuestPreviewWithoutSemanticLoss()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, ".tooling", "2.1-acceptance", "DarkGrey-2.1-Acceptance");
        var dialogue = DialogueSerializer.Read(Path.Combine(project, "dialogues", "final_confrontation.json"));
        var quest = QuestSerializer.Read(Path.Combine(project, "quests", "evidence.json"));

        var session = CanonicalLegacyMigrationPreview.PreviewDialogue(dialogue);
        var task = CanonicalLegacyMigrationPreview.PreviewQuest(quest);

        Assert.IsTrue(session.CanApply, string.Join("; ", session.Issues.Select(issue => issue.Code)));
        Assert.IsTrue(task.CanApply, string.Join("; ", task.Issues.Select(issue => issue.Code)));
        Assert.AreEqual("final_confrontation", session.Envelope!.Id);
        Assert.AreEqual("证物就在你手里。现在，作出选择。",
            session.Envelope.Graph!.Nodes.Single(node => node.Id == "question").Properties["text"].GetString());
        CollectionAssert.AreEquivalent(new[] { "hand_over", "conceal" },
            session.Envelope.Graph.Nodes.Where(node => node.Type == "end")
                .Select(node => node.Properties["port_id"].GetString()).ToArray());
        Assert.AreEqual("evidence", task.Envelope!.Id);
        var objective = task.Envelope.Graph!.Nodes.Single(node => node.Id == "collect_evidence");
        Assert.AreEqual("收集案件证物", objective.Properties["description"].GetString());
        Assert.AreEqual("-1", objective.Properties["metadata"].GetProperty("damage").GetString());
    }

    [TestMethod]
    public void SyntheticNodeIdsDoNotReplaceLegacyObjectiveIds()
    {
        var source = new QuestResource
        {
            Id = "quest",
            Title = "Quest",
            DisplayName = "Quest",
            Description = "Keep IDs",
            Objectives =
            [
                QuestObjectiveResource.Kill("activate", "Activate objective", "minecraft:zombie", 1),
                QuestObjectiveResource.Kill("settle", "Settle objective", "minecraft:skeleton", 1),
            ],
            ObjectiveGroups = [new ObjectiveGroupResource { Id = "sequence", Mode = "SEQUENCE", Objectives = ["activate", "settle"] }],
        };

        var result = CanonicalLegacyMigrationPreview.PreviewQuest(source);

        Assert.IsTrue(result.CanApply, string.Join("; ", result.Issues.Select(issue => issue.Code)));
        var graph = result.Envelope!.Graph!;
        Assert.IsTrue(graph.Nodes.Any(node => node.Id == "activate" && node.Type == "objective"));
        Assert.IsTrue(graph.Nodes.Any(node => node.Id == "settle" && node.Type == "objective"));
        Assert.IsTrue(graph.Nodes.Any(node => node.Id == "settle_migration" && node.Type == "settle"));
    }

    [TestMethod]
    public void StoryPreviewCollapsesDialogueAndQuestPairsUsingChildBoundaries()
    {
        var sessionStart = GraphNodeFactory.Create(GraphScope.Session, "start", "start");
        var sessionEnd = GraphNodeFactory.Create(GraphScope.Session, "end", "done");
        sessionEnd.Properties["port_id"] = JsonSerializer.SerializeToElement("done");
        sessionEnd.Properties["display_name"] = JsonSerializer.SerializeToElement("Done");
        var session = new GraphResourceEnvelope(GraphResourceKind.Session, "shared", "Dialogue",
            new GraphDocument([sessionStart, sessionEnd], [new GraphConnection("start", "flow_out", "done", "flow_in", GraphInterfaceKind.Flow)]));

        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("completion", "Complete", true, GraphInterfaceKind.Logic));
        var task = new GraphResourceEnvelope(GraphResourceKind.Task, "shared", "Quest",
            new GraphDocument([settle]));

        var story = new StoryResource
        {
            Id = "story", DisplayName = "Story", Title = "Story", Entry = "start",
            Nodes =
            [
                new() { Id = "start", Type = "story_start" },
                new() { Id = "play", Type = "play_dialogue", Properties = new() { ["dialogue_id"] = JsonSerializer.SerializeToElement("shared") } },
                new() { Id = "branch", Type = "dialogue_exit_branch", Properties = new() { ["exit_names"] = JsonSerializer.SerializeToElement("done") } },
                new() { Id = "quest", Type = "start_quest", Properties = new() { ["quest_id"] = JsonSerializer.SerializeToElement("shared") } },
                new() { Id = "wait", Type = "quest_completed", Properties = new() { ["quest_id"] = JsonSerializer.SerializeToElement("shared") } },
                new() { Id = "end", Type = "end" },
            ],
            Connections =
            [
                new() { From = "start", Output = "next", To = "play" }, new() { From = "play", Output = "next", To = "branch" },
                new() { From = "branch", Output = "done", To = "quest" }, new() { From = "quest", Output = "next", To = "wait" },
                new() { From = "wait", Output = "next", To = "end" },
            ],
        };

        var before = StorySerializer.Serialize(story);
        var result = CanonicalLegacyMigrationPreview.PreviewStory(story, new[] { session, task });
        var repeated = CanonicalLegacyMigrationPreview.PreviewStory(story, new[] { session, task });

        Assert.IsTrue(result.CanApply, string.Join("; ", result.Issues.Select(issue => issue.Code)));
        Assert.AreEqual(result.Envelope!.ToJson(), repeated.Envelope!.ToJson());
        Assert.AreEqual(before, StorySerializer.Serialize(story));
        var graph = result.Envelope!.Graph!;
        Assert.AreEqual("session", graph.Nodes.Single(node => node.Id == "play").Type);
        Assert.AreEqual("task", graph.Nodes.Single(node => node.Id == "quest").Type);
        Assert.IsTrue(graph.Connections.Any(edge => edge.FromNodeId == "play" && edge.FromPortId == "done" && edge.ToNodeId == "quest"));
        Assert.IsTrue(graph.Connections.Any(edge => edge.FromNodeId == "quest" && edge.FromPortId == "completion" && edge.ToNodeId == "end"));
    }

    [TestMethod]
    public void TrackedRoyalMysteryRequiresExplicitRemovalOfRetiredStandaloneNodes()
    {
        var root = FindRepositoryRoot();
        var project = Path.Combine(root, ".tooling", "2.1-acceptance", "DarkGrey-2.1-Acceptance");
        var story = StorySerializer.Read(Path.Combine(project, "stories", "royal_mystery.json"));
        var before = StorySerializer.Serialize(story);
        var children = new[]
        {
            CanonicalLegacyMigrationPreview.PreviewDialogue(DialogueSerializer.Read(Path.Combine(project, "dialogues", "final_confrontation.json"))).Envelope!,
            CanonicalLegacyMigrationPreview.PreviewQuest(QuestSerializer.Read(Path.Combine(project, "quests", "evidence.json"))).Envelope!,
        };
        var result = CanonicalLegacyMigrationPreview.PreviewStory(story, children);
        Assert.IsFalse(result.CanApply);
        Assert.IsNull(result.Envelope);
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == "migration.story.standalone_event.removed"));
        Assert.AreEqual(before, StorySerializer.Serialize(story));
    }

    [TestMethod]
    public void RetiredStandaloneNodesCannotBeReintroducedByMigration()
    {
        foreach (var type in new[] { "ActorInteract", "EnterRegion", "EnterStory" })
        {
            var source = new StoryResource
            {
                Id = "old_story", DisplayName = "Old", Title = "Old", Entry = "start",
                Nodes = [new() { Id = "start", Type = "StoryStart" }, new() { Id = "old", Type = type }],
                Connections = [new() { From = "start", Output = "next", To = "old" }],
            };
            var before = StorySerializer.Serialize(source);
            var result = CanonicalLegacyMigrationPreview.PreviewStory(source);
            Assert.IsFalse(result.CanApply, type);
            Assert.IsNull(result.Envelope);
            Assert.IsTrue(result.Issues.Any(issue => issue.Code == "migration.story.standalone_event.removed"), type);
            Assert.AreEqual(before, StorySerializer.Serialize(source));
        }
    }

    [TestMethod]
    public void StoryPreviewRejectsDialogueBranchThatDropsAChildExit()
    {
        var start = GraphNodeFactory.Create(GraphScope.Session, "start", "start");
        var accepted = GraphNodeFactory.Create(GraphScope.Session, "end", "accepted");
        accepted.Properties["port_id"] = JsonSerializer.SerializeToElement("accepted");
        accepted.Properties["display_name"] = JsonSerializer.SerializeToElement("Accepted");
        var declined = GraphNodeFactory.Create(GraphScope.Session, "end", "declined");
        declined.Properties["port_id"] = JsonSerializer.SerializeToElement("declined");
        declined.Properties["display_name"] = JsonSerializer.SerializeToElement("Declined");
        var session = new GraphResourceEnvelope(GraphResourceKind.Session, "dialogue", "Dialogue",
            new GraphDocument([start, accepted, declined]));
        var story = new StoryResource
        {
            Id = "story", DisplayName = "Story", Title = "Story", Entry = "start",
            Nodes =
            [
                new() { Id = "start", Type = "story_start" },
                new() { Id = "play", Type = "play_dialogue", Properties = new() { ["dialogue_id"] = JsonSerializer.SerializeToElement("dialogue") } },
                new() { Id = "branch", Type = "dialogue_exit_branch", Properties = new() { ["exit_names"] = JsonSerializer.SerializeToElement("accepted, declined") } },
                new() { Id = "end", Type = "end" },
            ],
            Connections =
            [
                new() { From = "start", Output = "next", To = "play" },
                new() { From = "play", Output = "next", To = "branch" },
                new() { From = "branch", Output = "accepted", To = "end" },
            ],
        };

        var result = CanonicalLegacyMigrationPreview.PreviewStory(story, new[] { session });

        Assert.IsFalse(result.CanApply);
        Assert.IsTrue(result.Issues.Any(issue => issue.Code == "migration.story.dialogue.topology.ambiguous" && issue.NodeId == "branch"));
    }

    private static QuestResource Quest(string mode)
        => new()
        {
            Id = "quest",
            Title = "Quest",
            DisplayName = "Quest",
            Description = "Do it",
            Objectives =
            [
                QuestObjectiveResource.Kill("one", "Kill one", "minecraft:zombie", 1),
                QuestObjectiveResource.Collect("two", "Collect two", "minecraft:stone", 3, 2),
            ],
            ObjectiveGroups = [new ObjectiveGroupResource { Id = "group", Mode = mode, Objectives = ["one", "two"] }],
        };

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, ".tooling", "2.1-acceptance")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate the tracked 2.1 acceptance project.");
    }
}
