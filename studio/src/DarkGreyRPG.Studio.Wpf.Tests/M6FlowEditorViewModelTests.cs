using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class M6FlowEditorViewModelTests
{
    [TestMethod]
    public void BuildsSavesAndReloadsCompleteMysteryBranchFlow()
    {
        using var directory = new M6ProjectDirectory();
        var service = new ProjectService();
        var session = service.CreateProject(directory.Root, "m6_flow", "M6 Flow");
        session.Stories.CreateStory("mystery", "王城迷案");
        session.Stories.CreateStory("kingdom", "王国线");
        session.Stories.CreateStory("empire", "帝国线");
        service.CreateActorInStory("mystery", "detective", "侦探");
        service.CreateDialogueInStory("mystery", "final_confrontation", "最终质询");
        service.CreateQuestInStory("mystery", "evidence", "搜集证物");

        var editor = new StoryFlowEditorViewModel(
            session.Stories.LoadStoryDocument("mystery"),
            ["detective"], ["final_confrontation"], ["evidence"], ["mystery", "kingdom", "empire"]);

        editor.SelectOnly(editor.Nodes.Single());
        editor.DeleteSelectionCommand.Execute(null);
        editor.AddStoryStartCommand.Execute(null);
        editor.AddStartQuestCommand.Execute(null);
        editor.AddWaitQuestCompleteCommand.Execute(null);
        editor.AddActorInteractCommand.Execute(null);
        editor.AddPlayDialogueCommand.Execute(null);
        editor.AddDialogueExitBranchCommand.Execute(null);
        editor.AddEnterStoryCommand.Execute(null);
        editor.AddEnterStoryCommand.Execute(null);

        var start = editor.Nodes.Single(node => node.CanonicalType == "StoryStart");
        var startQuest = editor.Nodes.Single(node => node.CanonicalType == "StartQuest");
        var waitQuest = editor.Nodes.Single(node => node.CanonicalType == "WaitQuestComplete");
        var interact = editor.Nodes.Single(node => node.CanonicalType == "ActorInteract");
        var dialogue = editor.Nodes.Single(node => node.CanonicalType == "PlayDialogue");
        var branch = editor.Nodes.Single(node => node.CanonicalType == "DialogueExitBranch");
        var targets = editor.Nodes.Where(node => node.CanonicalType == "EnterStory").ToArray();
        targets[0].ResourceValue = "kingdom";
        targets[1].ResourceValue = "empire";

        Assert.IsTrue(editor.Connect(start.Id, "next", startQuest.Id));
        Assert.IsTrue(editor.Connect(startQuest.Id, "next", waitQuest.Id));
        Assert.IsTrue(editor.Connect(waitQuest.Id, "next", interact.Id));
        Assert.IsTrue(editor.Connect(interact.Id, "next", dialogue.Id));
        Assert.IsTrue(editor.Connect(dialogue.Id, "next", branch.Id));
        Assert.IsTrue(editor.Connect(branch.Id, "hand_over", targets[0].Id));
        Assert.IsTrue(editor.Connect(branch.Id, "conceal", targets[1].Id));
        editor.SelectOnly(branch);
        editor.MoveSelection(new Dictionary<string, (double X, double Y)> { [branch.Id] = (900, 300) });

        Assert.IsTrue(editor.IsDirty);
        Assert.IsEmpty(editor.ValidationErrors);
        session.Stories.SaveStory(editor.Document);
        Assert.IsFalse(editor.IsDirty);

        var restored = session.Stories.LoadStory("mystery");
        Assert.HasCount(8, restored.Nodes);
        Assert.HasCount(7, restored.Connections);
        Assert.AreEqual("start", restored.Entry);
        Assert.AreEqual(900, restored.Nodes.Single(node => node.Id == branch.Id).Position.X);
        Assert.IsTrue(restored.Nodes.Any(node => node.Type == "enter_story" && node.Properties["target_story_id"].GetString() == "kingdom"));
        Assert.IsTrue(restored.Nodes.Any(node => node.Type == "enter_story" && node.Properties["target_story_id"].GetString() == "empire"));
    }

    [TestMethod]
    public void SupportsCopyPasteDeleteAndUndoRedo()
    {
        using var directory = new M6ProjectDirectory();
        var service = new ProjectService();
        var session = service.CreateProject(directory.Root, "m6_history", "M6 History");
        session.Stories.CreateStory("flow", "Flow");
        var editor = new StoryFlowEditorViewModel(session.Stories.LoadStoryDocument("flow"), [], [], [], ["flow"]);
        editor.AddEndCommand.Execute(null);
        var added = editor.SelectedNode!;
        editor.CopyCommand.Execute(null);
        editor.PasteCommand.Execute(null);
        Assert.HasCount(3, editor.Nodes);
        editor.UndoCommand.Execute(null);
        Assert.HasCount(2, editor.Nodes);
        editor.RedoCommand.Execute(null);
        Assert.HasCount(3, editor.Nodes);
        editor.DeleteSelectionCommand.Execute(null);
        Assert.HasCount(2, editor.Nodes);
    }

    [TestMethod]
    public void PreservesLegacyRuntimeNodeTypesWithoutRewritingThemAsEndNodes()
    {
        var resource = new StoryResource
        {
            Id = "legacy", Entry = "condition",
            Nodes =
            [
                new StoryNodeResource { Id = "condition", Type = "quest_state", Position = new() { X = 10, Y = 20 } },
                new StoryNodeResource { Id = "branch", Type = "branch", Position = new() { X = 220, Y = 20 } },
                new StoryNodeResource { Id = "message", Type = "send_message", Position = new() { X = 440, Y = 20 } },
            ],
            Connections =
            [
                new StoryConnectionResource { From = "condition", Output = "complete", To = "branch" },
                new StoryConnectionResource { From = "branch", Output = "true", To = "message" },
            ],
        };
        var editor = new StoryFlowEditorViewModel(StoryDocument.FromResource(resource), [], [], [], ["legacy"]);

        editor.SelectOnly(editor.Nodes.Single(node => node.Id == "message"));
        editor.MoveSelection(new Dictionary<string, (double X, double Y)> { ["message"] = (480, 60) });

        var saved = editor.Document.ToResource();
        CollectionAssert.AreEquivalent(
            new[] { "quest_state", "branch", "send_message" },
            saved.Nodes.Select(node => node.Type).ToArray());
        Assert.AreEqual("Branch", editor.Nodes.Single(node => node.Id == "branch").CanonicalType);
        Assert.IsEmpty(editor.ValidationErrors);
    }

    [TestMethod]
    public void RenamingNodeUpdatesConnectionsAndConnectionDeleteCanBeUndone()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("rename"), [], [], [], ["rename"]);
        var start = editor.Nodes.Single(node => node.CanonicalType == "StoryStart");
        start.Id = "begin";

        Assert.AreEqual("begin", editor.Document.Entry);
        Assert.AreEqual("begin", editor.Connections.Single().From);
        Assert.IsEmpty(editor.ValidationErrors);

        editor.RemoveConnection(editor.Connections.Single());
        Assert.IsEmpty(editor.Connections);
        editor.UndoCommand.Execute(null);
        Assert.HasCount(1, editor.Connections);
        Assert.AreEqual("begin", editor.Connections.Single().From);
    }

    private sealed class M6ProjectDirectory : IDisposable
    {
        public M6ProjectDirectory()
        {
            Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".m6-test-data", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Root);
        }
        public string Root { get; }
        public void Dispose()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".m6-test-data"));
            if (!Root.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe M6 cleanup path.");
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
