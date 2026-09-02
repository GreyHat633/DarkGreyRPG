using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphNodeDefinitionSchemaTests
{
    [TestMethod]
    public void RegisteredDefinitionsExposeCanonicalFixedShapes()
    {
        AssertPorts(GraphScope.StoryFlow, "start", []);
        AssertPorts(GraphScope.StoryFlow, "terminate", ["flow_in"]);
        AssertPorts(GraphScope.StoryFlow, "session", ["flow_in"]);
        AssertPorts(GraphScope.StoryFlow, "task", ["flow_in"]);
        AssertPorts(GraphScope.StoryFlow, "condition", ["flow_in", "logic_in", "flow_true", "flow_false"]);
        AssertPorts(GraphScope.StoryFlow, "and", ["logic_out"]);
        AssertPorts(GraphScope.StoryFlow, "or", ["logic_out"]);
        AssertPorts(GraphScope.StoryFlow, "not", ["logic_in", "logic_out"]);
        AssertPorts(GraphScope.StoryFlow, "action", ["flow_in", "flow_out"]);
        AssertPorts(GraphScope.StoryFlow, "interact_actor", ["flow_in", "flow_out"]);
        AssertPorts(GraphScope.StoryFlow, "enter_region", ["flow_in", "flow_out"]);
        AssertPorts(GraphScope.StoryFlow, "enter_story", ["flow_in"]);
        AssertPorts(GraphScope.StoryFlow, "logic_input", ["logic_out"]);
        AssertPorts(GraphScope.StoryFlow, "logic_output", ["logic_in"]);
        AssertPorts(GraphScope.StoryFlow, FlowJudgmentSchema.NodeType,
            [FlowJudgmentSchema.FlowInputPortId, FlowJudgmentSchema.FlowOutputPortId, FlowJudgmentSchema.ExecutedPortId]);

        AssertPorts(GraphScope.Session, "start", ["flow_out"]);
        AssertPorts(GraphScope.Session, "line", ["flow_in", "flow_out"]);
        AssertPorts(GraphScope.Session, "choice", ["flow_in"]);
        AssertPorts(GraphScope.Session, "narration", ["flow_in", "flow_out"]);
        AssertPorts(GraphScope.Session, "condition", ["flow_in", "logic_in", "flow_true", "flow_false"]);
        AssertPorts(GraphScope.Session, FlowJudgmentSchema.NodeType,
            [FlowJudgmentSchema.FlowInputPortId, FlowJudgmentSchema.FlowOutputPortId, FlowJudgmentSchema.ExecutedPortId]);
        AssertPorts(GraphScope.Session, "and", ["logic_out"]);
        AssertPorts(GraphScope.Session, "or", ["logic_out"]);
        AssertPorts(GraphScope.Session, "not", ["logic_in", "logic_out"]);
        AssertPorts(GraphScope.Session, "logic_output", ["logic_in"]);
        AssertPorts(GraphScope.Session, "logic_input", ["logic_out"]);
        AssertPorts(GraphScope.Session, "end", ["flow_in"]);
        AssertPorts(GraphScope.Session, "legacy_jump", []);

        AssertPorts(GraphScope.Task, "activate", ["logic_out"]);
        AssertPorts(GraphScope.Task, "objective", ["logic_status"]);
        AssertPorts(GraphScope.Task, "and", ["logic_out"]);
        AssertPorts(GraphScope.Task, "or", ["logic_out"]);
        AssertPorts(GraphScope.Task, "not", ["logic_in", "logic_out"]);
        AssertPorts(GraphScope.Task, "logic_output", ["logic_in"]);
        AssertPorts(GraphScope.Task, "logic_input", ["logic_out"]);
        AssertPorts(GraphScope.Task, "settle", []);
        Assert.IsTrue(GraphNodeDefinitionRegistry.ForScope(GraphScope.Task)
            .SelectMany(definition => definition.FixedPorts)
            .All(port => port.InterfaceKind == GraphInterfaceKind.Logic));
        CollectionAssert.AreEqual(new[] { GraphInterfaceKind.Logic },
            GraphNodeDefinitionRegistry.Get(GraphScope.StoryFlow, "and")!.AllowedInterfaceKinds.ToArray());
        CollectionAssert.AreEqual(new[] { GraphInterfaceKind.Flow },
            GraphNodeDefinitionRegistry.Get(GraphScope.Session, "line")!.AllowedInterfaceKinds.ToArray());
        CollectionAssert.AreEquivalent(new[] { GraphInterfaceKind.Flow, GraphInterfaceKind.Logic },
            GraphNodeDefinitionRegistry.Get(GraphScope.Session, "choice")!.AllowedInterfaceKinds.ToArray());
    }

    [TestMethod]
    public void DefinitionsExposeTrimmedAuthoringMetadataInStableScopeOrder()
    {
        var expectedNames = new Dictionary<(GraphScope Scope, string Type), string>
        {
            [(GraphScope.StoryFlow, "start")] = "开始",
            [(GraphScope.StoryFlow, "terminate")] = "终止",
            [(GraphScope.StoryFlow, "session")] = "会话",
            [(GraphScope.StoryFlow, "task")] = "任务",
            [(GraphScope.StoryFlow, "condition")] = "条件判断",
            [(GraphScope.StoryFlow, "and")] = "与",
            [(GraphScope.StoryFlow, "or")] = "或",
            [(GraphScope.StoryFlow, "not")] = "非",
            [(GraphScope.StoryFlow, "action")] = "动作",
            [(GraphScope.StoryFlow, "interact_actor")] = "角色交互",
            [(GraphScope.StoryFlow, "enter_region")] = "进入区域",
            [(GraphScope.StoryFlow, "enter_story")] = "进入故事",
            [(GraphScope.StoryFlow, "logic_input")] = "逻辑输入",
            [(GraphScope.StoryFlow, "logic_output")] = "逻辑输出",
            [(GraphScope.StoryFlow, FlowJudgmentSchema.NodeType)] = "流程判断",
            [(GraphScope.Session, "start")] = "起始",
            [(GraphScope.Session, "line")] = "台词",
            [(GraphScope.Session, "choice")] = "选择",
            [(GraphScope.Session, "narration")] = "旁白",
            [(GraphScope.Session, "condition")] = "条件判断",
            [(GraphScope.Session, FlowJudgmentSchema.NodeType)] = "流程判断",
            [(GraphScope.Session, "and")] = "与",
            [(GraphScope.Session, "or")] = "或",
            [(GraphScope.Session, "not")] = "非",
            [(GraphScope.Session, "logic_output")] = "逻辑输出",
            [(GraphScope.Session, "logic_input")] = "逻辑输入",
            [(GraphScope.Session, "end")] = "结束",
            [(GraphScope.Session, "legacy_jump")] = "旧 Jump",
            [(GraphScope.Task, "activate")] = "激活",
            [(GraphScope.Task, "objective")] = "目标",
            [(GraphScope.Task, "and")] = "与",
            [(GraphScope.Task, "or")] = "或",
            [(GraphScope.Task, "not")] = "非",
            [(GraphScope.Task, "logic_output")] = "逻辑输出",
            [(GraphScope.Task, "logic_input")] = "逻辑输入",
            [(GraphScope.Task, "settle")] = "结算",
        };

        Assert.AreEqual(36, GraphNodeDefinitionRegistry.Definitions.Count);
        foreach (var definition in GraphNodeDefinitionRegistry.Definitions)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(definition.DisplayName));
            Assert.IsFalse(string.IsNullOrWhiteSpace(definition.Category));
            Assert.AreEqual(expectedNames[(definition.Scope, definition.Type)], definition.DisplayName);
        }

        CollectionAssert.AreEqual(
            new[] { "terminate", "and", "or", "not", "action", "logic_input", "logic_output", "condition", "flow_judgment" },
            GraphNodeDefinitionRegistry.ForAuthoringScope(GraphScope.StoryFlow).Select(item => item.Type).ToArray());
        CollectionAssert.AreEqual(
            new[] { "line", "choice", "narration", "and", "or", "not", "logic_input", "logic_output", "condition", "flow_judgment", "end" },
            GraphNodeDefinitionRegistry.ForAuthoringScope(GraphScope.Session).Select(item => item.Type).ToArray());
        CollectionAssert.AreEqual(
            new[] { "objective", "and", "or", "not", "logic_output", "logic_input" },
            GraphNodeDefinitionRegistry.ForAuthoringScope(GraphScope.Task).Select(item => item.Type).ToArray());
        Assert.IsTrue(GraphNodeDefinitionRegistry.ForAuthoringScope(GraphScope.Session).Any(item => item.Type == "choice"));
        Assert.IsFalse(GraphNodeDefinitionRegistry.ForAuthoringScope(GraphScope.Session).Any(item => item.Type is "start" or "legacy_jump"));
        Assert.IsFalse(GraphNodeDefinitionRegistry.ForAuthoringScope(GraphScope.StoryFlow).Any(item => item.Type == "start"));
        Assert.IsFalse(GraphNodeDefinitionRegistry.ForAuthoringScope(GraphScope.Task).Any(item => item.Type is "activate" or "settle"));
        foreach (var scope in new[] { GraphScope.StoryFlow, GraphScope.Session })
        {
            var logicMenu = GraphNodeDefinitionRegistry.ForAuthoringScope(scope)
                .Where(item => item.Category == "逻辑").Select(item => item.Type).ToArray();
            CollectionAssert.AreEqual(new[] { "logic_output", "condition", "flow_judgment" },
                logicMenu.TakeLast(3).ToArray());
        }
        Assert.IsFalse(GraphNodeDefinitionRegistry.ForAuthoringScope(GraphScope.Task)
            .Any(item => item.Type == FlowJudgmentSchema.NodeType));
    }

    [TestMethod]
    public void PropertiesUseExactKindsRequiredFlagsAndDefaults()
    {
        AssertProperties(GraphScope.Session, "line",
            ("speaker_actor_id", JsonValueKind.String), ("text", JsonValueKind.String));
        AssertProperties(GraphScope.Session, "choice",
            ("prompt", JsonValueKind.String), ("options", JsonValueKind.Array));
        AssertProperties(GraphScope.Session, "end",
            ("port_id", JsonValueKind.String), ("display_name", JsonValueKind.String));
        AssertProperties(GraphScope.Session, "logic_output",
            ("port_id", JsonValueKind.String), ("display_name", JsonValueKind.String));
        AssertProperties(GraphScope.Task, "logic_output",
            ("port_id", JsonValueKind.String), ("display_name", JsonValueKind.String));
        AssertProperties(GraphScope.StoryFlow, "session",
            ("resource_id", JsonValueKind.String));
        AssertProperties(GraphScope.StoryFlow, "task",
            ("resource_id", JsonValueKind.String));
        AssertProperties(GraphScope.StoryFlow, "enter_story",
            ("target_story_id", JsonValueKind.String));
        AssertProperties(GraphScope.StoryFlow, "interact_actor",
            (StoryStartSchema.ActorIdProperty, JsonValueKind.String));
        AssertProperties(GraphScope.StoryFlow, "enter_region",
            (StoryStartSchema.DimensionProperty, JsonValueKind.Number),
            (StoryStartSchema.XProperty, JsonValueKind.Number),
            (StoryStartSchema.YProperty, JsonValueKind.Number),
            (StoryStartSchema.ZProperty, JsonValueKind.Number),
            (StoryStartSchema.RadiusProperty, JsonValueKind.Number));
        var action = GraphNodeDefinitionRegistry.Get(GraphScope.StoryFlow, "action")!;
        CollectionAssert.AreEqual(new[]
        {
            CanonicalStoryActionSchema.TypeProperty,
            CanonicalStoryActionSchema.ItemIdProperty,
            CanonicalStoryActionSchema.AmountProperty,
            CanonicalStoryActionSchema.MessageProperty,
        }, action.PropertyDefinitions.Select(property => property.Name).ToArray());
        CollectionAssert.AreEqual(new[]
        {
            JsonValueKind.String, JsonValueKind.String, JsonValueKind.Number, JsonValueKind.String,
        }, action.PropertyDefinitions.Select(property => property.ValueKind).ToArray());
        Assert.IsTrue(action.PropertyDefinitions.Single(property => property.Name == CanonicalStoryActionSchema.TypeProperty).Required);
        Assert.AreEqual(CanonicalStoryActionSchema.SendMessage,
            action.PropertyDefinitions.Single(property => property.Name == CanonicalStoryActionSchema.TypeProperty)
                .DefaultValue!.Value.GetString());
        Assert.IsTrue(action.PropertyDefinitions
            .Where(property => property.Name != CanonicalStoryActionSchema.TypeProperty)
            .All(property => !property.Required && property.DefaultValue is null));

        var line = GraphNodeDefinitionRegistry.Get(GraphScope.Session, "line")!;
        Assert.IsTrue(line.PropertyDefinitions.All(property => property.Required));
        Assert.IsTrue(line.PropertyDefinitions.All(property => property.DefaultValue is { ValueKind: JsonValueKind.String }));
        Assert.AreEqual(string.Empty, line.PropertyDefinitions.Single(property => property.Name == "text").DefaultValue!.Value.GetString());
        Assert.AreEqual(0, GraphNodeDefinitionRegistry.Get(GraphScope.Session, "choice")!
            .PropertyDefinitions.Single(property => property.Name == "options").DefaultValue!.Value.GetArrayLength());
    }

    [TestMethod]
    public void FactoryCreatesFreshFixedPortsAndPropertiesWithoutDynamicSlots()
    {
        var first = GraphNodeFactory.Create(GraphScope.Session, "line", "one", "Line One");
        var second = GraphNodeFactory.Create(GraphScope.Session, "line", "two", "Line Two");
        Assert.AreEqual("Line One", first.DisplayName);
        Assert.AreEqual("Line Two", second.DisplayName);
        Assert.HasCount(2, first.Ports);
        Assert.IsTrue(first.Ports.All(port => port is not null));
        Assert.AreNotSame(first.Ports[0], second.Ports[0]);
        Assert.AreNotSame(first.Properties, second.Properties);

        first.Ports[0].DisplayName = "changed";
        first.Properties["text"] = JsonSerializer.SerializeToElement("changed");
        Assert.AreEqual("流程输入", second.Ports[0].DisplayName);
        Assert.AreEqual(string.Empty, second.Properties["text"].GetString());
        Assert.AreEqual("流程输入", GraphNodeDefinitionRegistry.Get(GraphScope.Session, "line")!.FixedPorts[0].DisplayName);
    }

    [TestMethod]
    public void FactoryUsesDefinitionNameForMissingOrBlankOverride()
    {
        Assert.AreEqual("台词", GraphNodeFactory.Create(GraphScope.Session, "line", "default").DisplayName);
        Assert.AreEqual("台词", GraphNodeFactory.Create(GraphScope.Session, "line", "blank", "  ").DisplayName);
        Assert.AreEqual("Custom", GraphNodeFactory.Create(GraphScope.Session, "line", "custom", "  Custom  ").DisplayName);
    }

    [TestMethod]
    public void FactoryStrictlyChecksScopeAndCompatibilityBoundary()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphNodeFactory.Create(GraphScope.Task, "line", "wrong"));
        Assert.ThrowsExactly<KeyNotFoundException>(() => GraphNodeFactory.Create(GraphScope.Session, "missing", "unknown"));
        Assert.ThrowsExactly<InvalidOperationException>(() => GraphNodeFactory.Create(GraphScope.Session, "legacy_jump", "legacy"));

        var legacy = GraphNodeFactory.Create(GraphScope.Session, "legacy_jump", "legacy", compatibilityMode: true);
        Assert.IsEmpty(legacy.Ports);
        Assert.IsEmpty(legacy.Properties);
        Assert.IsFalse(GraphNodeFactory.TryCreate(GraphScope.Session, "legacy_jump", "legacy", out _, compatibilityMode: false));
        Assert.IsTrue(GraphNodeFactory.TryCreate(GraphScope.Session, "legacy_jump", "legacy", out var created, compatibilityMode: true));
        Assert.IsNotNull(created);
    }

    [TestMethod]
    public void DefinitionRejectsOrdinalDuplicatePortAndPropertyIdentity()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new GraphNodeDefinition(
            "duplicate", GraphScope.StoryFlow, "Duplicate", "Test",
            fixedPorts: [
                new GraphPortDefinition("same", "A", GraphPortDirection.Output, GraphInterfaceKind.Flow),
                new GraphPortDefinition("same", "B", GraphPortDirection.Output, GraphInterfaceKind.Flow)]));
        Assert.ThrowsExactly<ArgumentException>(() => new GraphNodeDefinition(
            "duplicate", GraphScope.StoryFlow, "Duplicate", "Test",
            propertyDefinitions: [
                new GraphPropertyDefinition("same", JsonValueKind.String),
                new GraphPropertyDefinition("same", JsonValueKind.Array)]));
        Assert.ThrowsExactly<ArgumentException>(() => new GraphNodeDefinition(
            "wrong_kind", GraphScope.Task, "Wrong Kind", "Test",
            allowedInterfaceKinds: [GraphInterfaceKind.Logic],
            fixedPorts: [new GraphPortDefinition("flow", "Flow", GraphPortDirection.Output, GraphInterfaceKind.Flow)]));

        var caseDistinct = new GraphNodeDefinition(
            "case", GraphScope.StoryFlow, " Case ", " Category ",
            fixedPorts: [
                new GraphPortDefinition("same", "A", GraphPortDirection.Output, GraphInterfaceKind.Flow),
                new GraphPortDefinition("Same", "B", GraphPortDirection.Output, GraphInterfaceKind.Flow)]);
        Assert.HasCount(2, caseDistinct.FixedPorts);
        Assert.AreEqual("Case", caseDistinct.DisplayName);
        Assert.AreEqual("Category", caseDistinct.Category);
        Assert.ThrowsExactly<ArgumentException>(() => new GraphNodeDefinition(
            "blank_name", GraphScope.StoryFlow, " ", "Test"));
        Assert.ThrowsExactly<ArgumentException>(() => new GraphNodeDefinition(
            "blank_category", GraphScope.StoryFlow, "Name", " \t"));
    }

    private static void AssertPorts(GraphScope scope, string type, IReadOnlyList<string> expectedIds)
    {
        var definition = GraphNodeDefinitionRegistry.Get(scope, type);
        Assert.IsNotNull(definition);
        CollectionAssert.AreEqual(expectedIds.ToArray(), definition.FixedPorts.Select(port => port.Id).ToArray());
        Assert.IsTrue(definition.FixedPorts.Select(port => port.Id).Distinct(StringComparer.Ordinal).Count() == expectedIds.Count);
    }

    private static void AssertProperties(GraphScope scope, string type, params (string Name, JsonValueKind Kind)[] expected)
    {
        var definitions = GraphNodeDefinitionRegistry.Get(scope, type)!.PropertyDefinitions;
        CollectionAssert.AreEqual(expected.Select(item => item.Name).ToArray(), definitions.Select(item => item.Name).ToArray());
        CollectionAssert.AreEqual(expected.Select(item => item.Kind).ToArray(), definitions.Select(item => item.Kind).ToArray());
        Assert.IsTrue(definitions.All(item => item.Required && item.DefaultValue.HasValue));
    }
}
