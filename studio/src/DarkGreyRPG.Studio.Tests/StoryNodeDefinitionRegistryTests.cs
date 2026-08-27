using System.Text.Json;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Stories.Definitions;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryNodeDefinitionRegistryTests
{
    [TestMethod]
    public void RegistryCoversCanonicalTypesAndAuthorMetadata()
    {
        Assert.AreEqual(20, StoryNodeDefinitionRegistry.Definitions.Count);
        Assert.AreEqual(20, StoryNodeDefinitionRegistry.Definitions.Select(d => d.CanonicalType).Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual("StoryStart", StoryNodeDefinitionRegistry.CanonicalizeType(" START "));
        var branch = StoryNodeDefinitionRegistry.Get("branch");
        Assert.IsNotNull(branch);
        Assert.AreEqual("条件分支", branch.DisplayName);
        Assert.AreEqual("条件", branch.Category);
        CollectionAssert.AreEqual(new[] { "true", "false" }, branch.ResolveStaticOutputs());
        Assert.AreEqual("剧情", StoryNodeDefinitionRegistry.Get("enter_story")!.Category);
        Assert.AreEqual("剧情", StoryNodeDefinitionRegistry.Get("end_story")!.Category);
        Assert.AreEqual("结束", StoryNodeDefinitionRegistry.Get("end")!.Category);
        CollectionAssert.AreEqual(new[] { "触发", "条件", "对话", "任务", "动作 / 奖励", "流程控制", "剧情", "结束" }.OrderBy(category => category, StringComparer.Ordinal).ToArray(),
            StoryNodeDefinitionRegistry.Definitions.Select(definition => definition.Category).Distinct(StringComparer.Ordinal).OrderBy(category => category, StringComparer.Ordinal).ToArray());
    }

    [TestMethod]
    public void BooleanAndTerminalOutputsFollowContract()
    {
        var booleanOutputs = StoryNodeDefinitionRegistry.ResolveOutputs("has_item");
        CollectionAssert.AreEqual(new[] { "true", "false" }, booleanOutputs.ToArray());
        Assert.IsFalse(StoryNodeDefinitionRegistry.IsOutputAllowed("quest_state", "complete"));
        CollectionAssert.AreEqual(Array.Empty<string>(), StoryNodeDefinitionRegistry.ResolveOutputs("end" ).ToArray());
        Assert.IsTrue(StoryNodeDefinitionRegistry.Get("end")!.IsTerminal);
    }

    [TestMethod]
    public void DynamicOutputsDeduplicateAndPreserveLoadedNames()
    {
        var exits = new Dictionary<string, JsonElement> { ["exit_names"] = JsonSerializer.SerializeToElement("accept, refuse, accept") };
        var dialogue = StoryNodeDefinitionRegistry.ResolveOutputs("dialogue_exit_branch", exits, ["refuse", "threaten"]);
        CollectionAssert.AreEqual(new[] { "accept", "refuse", "threaten" }, dialogue.ToArray());

        var sequenceProperties = new Dictionary<string, JsonElement> { ["step_count"] = JsonSerializer.SerializeToElement(3) };
        var sequence = StoryNodeDefinitionRegistry.ResolveOutputs("sequence", sequenceProperties, ["10", "2", "1"]);
        CollectionAssert.AreEqual(new[] { "1", "2", "3", "10" }, sequence.ToArray());
        CollectionAssert.AreEqual(new[] { "1" }, StoryNodeDefinitionRegistry.ResolveOutputs("sequence").ToArray());
    }

    [TestMethod]
    public void PlayDialoguePreservesLoadedLegacyResultPorts()
    {
        var outputs = StoryNodeDefinitionRegistry.ResolveOutputs("play_dialogue", loadedOutputs: ["accept", "refuse", "accept"]);
        CollectionAssert.AreEqual(new[] { "next", "accept", "refuse" }, outputs.ToArray());

        var waitOutputs = StoryNodeDefinitionRegistry.ResolveOutputs("quest_completed", loadedOutputs: ["complete", "invalid"]);
        CollectionAssert.AreEqual(new[] { "next", "complete" }, waitOutputs.ToArray());
    }

    [TestMethod]
    public void ValidatorRejectsTerminalSourcesAndInvalidOutputs()
    {
        static StoryNodeResource Node(string id, string type) => new() { Id = id, Type = type };
        var resource = new StoryResource
        {
            Id = "registry",
            Entry = "start",
            Nodes = [Node("start", "story_start"), Node("end", "end")],
            Connections = [new() { From = "end", Output = "next", To = "start" }, new() { From = "start", Output = "false", To = "end" }],
        };
        var codes = StoryValidator.Validate(resource).Select(issue => issue.Code).ToArray();
        CollectionAssert.Contains(codes, "story.connection.from.terminal");
        CollectionAssert.Contains(codes, "story.connection.output.invalid");
    }
}

file static class StoryNodeDefinitionTestExtensions
{
    public static string[] ResolveStaticOutputs(this StoryNodeDefinition definition) => definition.StaticOutputs.Select(port => port.Name).ToArray();
}
