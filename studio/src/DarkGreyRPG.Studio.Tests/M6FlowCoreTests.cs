using System.Text.Json;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class M6FlowCoreTests
{
    [TestMethod]
    public void DocumentSupportsEditSnapshotAndDirtyState()
    {
        var document = StoryDocument.CreateNew("mystery", "王城迷案");
        Assert.IsTrue(document.IsDirty);
        Assert.IsTrue(document.AddConnection(new StoryConnectionResource { From = "start", Output = "next", To = "end" }) is false);
        var snapshot = document.CreateSnapshot();
        Assert.IsTrue(document.MoveNode("end", 320, 180));
        Assert.IsTrue(document.SetNodeProperty("end", "note", "finish"));
        Assert.AreNotEqual(0, document.ToResource().Nodes.Single(n => n.Id == "end").Position.X);
        document.RestoreSnapshot(snapshot);
        Assert.AreEqual(0, document.ToResource().Nodes.Single(n => n.Id == "end").Position.X);
        Assert.IsTrue(document.IsDirty);
    }

    [TestMethod]
    public void ValidatorRejectsInvalidReferencesConnectionsAndPositions()
    {
        var resource = new StoryResource
        {
            Id = "bad",
            Entry = "missing",
            Nodes = [new StoryNodeResource { Id = "a", Type = "actor_interact", Position = new() { X = double.NaN }, Properties = [] }, new StoryNodeResource { Id = "a", Type = "unknown" }],
            Connections = [new StoryConnectionResource { From = "a", Output = "next", To = "missing" }, new StoryConnectionResource { From = "a", Output = "next", To = "a" }],
        };
        var issues = StoryValidator.Validate(resource);
        Assert.AreNotEqual(0, issues.Count);
        StringAssert.Contains(string.Join("|", issues.Select(i => i.Code)), "story.node.position.invalid");
        StringAssert.Contains(string.Join("|", issues.Select(i => i.Code)), "story.node.actor.required");
        StringAssert.Contains(string.Join("|", issues.Select(i => i.Code)), "story.connection.output.duplicate");
        var actorIssue = issues.Single(issue => issue.Code == "story.node.actor.required");
        Assert.AreEqual("a", actorIssue.NodeId);
        Assert.AreEqual("actor_id", actorIssue.Field);
    }

    [TestMethod]
    public void WangChengMianAnBranchFlowValidates()
    {
        var nodes = new[]
        {
            Node("start", "story_start"), Node("quest", "start_quest", ("quest_id", "collect_evidence")),
            Node("wait", "wait_quest_complete", ("quest_id", "collect_evidence")), Node("detective", "interact_actor", ("actor_id", "detective")),
            Node("dialogue", "play_dialogue", ("dialogue_id", "final_interrogation")), Node("branch", "dialogue_exit_branch"),
            Node("kingdom", "enter_story", ("target_story_id", "kingdom_line")), Node("empire", "enter_story", ("target_story_id", "empire_line")),
            Node("end", "end_story"),
        };
        var connections = new[]
        {
            C("start", "next", "quest"), C("quest", "next", "wait"), C("wait", "complete", "detective"), C("detective", "next", "dialogue"),
            C("dialogue", "next", "branch"), C("branch", "hand_over", "kingdom"), C("branch", "conceal", "empire"),
        };
        var resource = new StoryResource { Id = "wangcheng_mystery", DisplayName = "王城迷案", Title = "王城迷案", Entry = "start", Nodes = [.. nodes], Connections = [.. connections], Tags = ["acceptance"], Metadata = new() { Notes = "keep me" } };
        var issues = StoryValidator.Validate(resource);
        Assert.IsFalse(issues.Any(i => i.Severity == DarkGreyRPG.Studio.Core.Validation.ValidationSeverity.Error), string.Join("; ", issues.Select(i => i.Message)));
    }

    [TestMethod]
    public void RepositorySavesAtomicallyAndReloadsNonFlowFields()
    {
        using var directory = new TestProjectDirectory();
        var repository = new StoryRepository(directory.Root);
        var document = StoryDocument.CreateNew("atomic", "Atomic story");
        document.Description = "preserve description";
        document.Tags.Add("tag");
        document.EntryPresentation = new StoryEntryPresentation { Mode = "hero", Eyebrow = "case", Title = "Atomic", DurationSeconds = 3.5 };
        repository.SaveStory(document);
        Assert.IsFalse(document.IsDirty);
        var reloaded = repository.LoadStoryDocument("atomic");
        Assert.AreEqual("preserve description", reloaded.Description);
        CollectionAssert.Contains(reloaded.Tags.ToList(), "tag");
        Assert.AreEqual(3.5, reloaded.EntryPresentation.DurationSeconds);
        Assert.AreEqual("end", reloaded.ToResource().Nodes.Single(n => n.Id == "end").Type);
    }

    private static StoryNodeResource Node(string id, string type, params (string Key, string Value)[] properties)
        => new() { Id = id, Type = type, Properties = properties.ToDictionary(x => x.Key, x => JsonSerializer.SerializeToElement(x.Value), StringComparer.Ordinal) };
    private static StoryConnectionResource C(string from, string output, string to) => new() { From = from, Output = output, To = to };
}
