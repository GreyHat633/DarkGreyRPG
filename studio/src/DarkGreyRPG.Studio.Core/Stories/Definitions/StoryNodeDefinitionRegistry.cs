using System.Collections.ObjectModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Core.Stories.Definitions;

/// <summary>
/// The single Core registry for Runtime story node types. It owns aliases,
/// persisted spellings, author-facing metadata, and output-port semantics.
/// </summary>
public static class StoryNodeDefinitionRegistry
{
    private static readonly IReadOnlyList<StoryNodeDefinition> _definitions = new ReadOnlyCollection<StoryNodeDefinition>(BuildDefinitions().ToArray());
    private static readonly IReadOnlyDictionary<string, StoryNodeDefinition> _byType = BuildTypeIndex(_definitions);

    public static IReadOnlyList<StoryNodeDefinition> Definitions => _definitions;
    public static IReadOnlyList<StoryNodeDefinition> All => _definitions;
    public static IReadOnlyCollection<string> SupportedTypes { get; } = new ReadOnlyCollection<string>(_definitions.Select(d => d.CanonicalType).ToArray());

    public static string? CanonicalizeType(string? type) =>
        type is not null && _byType.TryGetValue(type.Trim(), out var definition) ? definition.CanonicalType : null;

    public static bool TryGet(string? type, out StoryNodeDefinition definition)
    {
        if (type is not null && _byType.TryGetValue(type.Trim(), out definition!)) return true;
        definition = null!;
        return false;
    }

    public static StoryNodeDefinition? Get(string? type) => TryGet(type, out var definition) ? definition : null;
    public static StoryNodeDefinition? Find(string? type) => Get(type);
    public static StoryNodeDefinition? GetDefinition(string? type) => Get(type);
    public static bool TryGetDefinition(string? type, out StoryNodeDefinition definition) => TryGet(type, out definition);

    public static IReadOnlyList<string> ResolveOutputs(StoryNodeResource node, IEnumerable<StoryConnectionResource>? loadedConnections = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        return ResolveOutputs(node.Type, node.Properties, ConnectionsForNode(node.Id, loadedConnections));
    }

    public static IReadOnlyList<string> ResolveOutputs(
        string? type,
        IReadOnlyDictionary<string, JsonElement>? properties = null,
        IEnumerable<string>? loadedOutputs = null)
    {
        var definition = Get(type);
        if (definition is null || definition.IsTerminal) return [];
        // Loaded names are opaque persisted values. Keep their exact spelling so
        // compatibility ports can be inspected, deleted, and saved losslessly.
        var loaded = (loadedOutputs ?? []).Where(output => !string.IsNullOrWhiteSpace(output)).Distinct(StringComparer.Ordinal).ToArray();
        return definition.OutputStrategy switch
        {
            StoryNodeOutputStrategy.Boolean => ["true", "false"],
            StoryNodeOutputStrategy.DialogueExits => MergeNamed(ParseExitNames(properties), loaded),
            StoryNodeOutputStrategy.Sequence => ResolveSequence(properties, loaded),
            StoryNodeOutputStrategy.LegacyPreserved => MergeNamed(["next"], loaded),
            StoryNodeOutputStrategy.SingleNext => MergeNamed(
                ["next"],
                loaded.Where(output => definition.CompatibilityOutputs.Contains(output, StringComparer.Ordinal))),
            StoryNodeOutputStrategy.Terminal => [],
            _ => [],
        };
    }

    public static IReadOnlyList<string> ResolveOutputs(
        string? type,
        IReadOnlyDictionary<string, string>? properties,
        IEnumerable<string>? loadedOutputs = null)
    {
        var json = properties?.ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal);
        return ResolveOutputs(type, (IReadOnlyDictionary<string, JsonElement>?)json, loadedOutputs);
    }

    public static IReadOnlyList<string> GetOutputs(StoryNodeResource node, IEnumerable<StoryConnectionResource>? loadedConnections = null) => ResolveOutputs(node, loadedConnections);

    public static IReadOnlyList<string> ResolveOutputs(StoryNodeDefinition definition, StoryNodeResource node, IEnumerable<StoryConnectionResource>? loadedConnections = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(node);
        return ResolveOutputs(definition.CanonicalType, node.Properties, ConnectionsForNode(node.Id, loadedConnections));
    }

    public static bool IsOutputAllowed(StoryNodeResource node, string? output, IEnumerable<StoryConnectionResource>? loadedConnections = null)
    {
        if (string.IsNullOrWhiteSpace(output) || !TryGet(node.Type, out var definition) || definition.IsTerminal) return false;
        var loaded = ConnectionsForNode(node.Id, loadedConnections);
        return ResolveOutputs(node, loadedConnections).Contains(output.Trim(), StringComparer.Ordinal)
            || definition.CompatibilityOutputs.Contains(output.Trim(), StringComparer.Ordinal);
    }

    public static bool IsOutputAllowed(string? type, string? output, IEnumerable<string>? loadedOutputs = null)
    {
        if (string.IsNullOrWhiteSpace(output) || !TryGet(type, out var definition) || definition.IsTerminal) return false;
        var value = output.Trim();
        return ResolveOutputs(type, (IReadOnlyDictionary<string, JsonElement>?)null, loadedOutputs).Contains(value, StringComparer.Ordinal)
            || definition.CompatibilityOutputs.Contains(value, StringComparer.Ordinal);
    }

    private static IEnumerable<string> ConnectionsForNode(string nodeId, IEnumerable<StoryConnectionResource>? connections) =>
        (connections ?? []).Where(connection => connection is not null && string.Equals(connection.From, nodeId, StringComparison.Ordinal)).Select(connection => connection.Output);

    private static IReadOnlyList<string> ResolveSequence(IReadOnlyDictionary<string, JsonElement>? properties, IReadOnlyCollection<string> loaded)
    {
        var count = ReadStepCount(properties);
        var numeric = loaded.Where(output => int.TryParse(output, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0)
            .Select(output => (Output: output, Number: int.Parse(output, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture)))
            .ToList();
        if (count > 0)
            for (var i = 1; i <= count; i++)
                if (!numeric.Any(item => item.Number == i)) numeric.Add((i.ToString(System.Globalization.CultureInfo.InvariantCulture), i));
        if (numeric.Count == 0) numeric.Add(("1", 1));
        return numeric.OrderBy(item => item.Number).ThenBy(item => item.Output, StringComparer.Ordinal).Select(item => item.Output).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static int ReadStepCount(IReadOnlyDictionary<string, JsonElement>? properties)
    {
        if (properties is null) return 0;
        foreach (var key in new[] { "step_count", "stepCount", "count" })
            if (properties.TryGetValue(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return Math.Max(0, number);
                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)) return Math.Max(0, number);
            }
        if (!properties.TryGetValue("steps", out var steps)) return 0;
        if (steps.ValueKind == JsonValueKind.Array) return steps.GetArrayLength();
        if (steps.ValueKind == JsonValueKind.String)
            return steps.GetString()?.Split([',', '，', ';', '；', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length ?? 0;
        return 0;
    }

    private static IReadOnlyList<string> ParseExitNames(IReadOnlyDictionary<string, JsonElement>? properties)
    {
        if (properties is null || !properties.TryGetValue("exit_names", out var value)) return [];
        IEnumerable<string> names = value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty),
            JsonValueKind.String => (value.GetString() ?? string.Empty).Split([',', '，', ';', '；', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ => [],
        };
        return names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> MergeNamed(IEnumerable<string> configured, IEnumerable<string> loaded) =>
        configured.Where(output => !string.IsNullOrWhiteSpace(output)).Select(output => output.Trim())
            .Concat(loaded.Where(output => !string.IsNullOrWhiteSpace(output)))
            .Distinct(StringComparer.Ordinal).ToArray();

    private static IReadOnlyDictionary<string, StoryNodeDefinition> BuildTypeIndex(IEnumerable<StoryNodeDefinition> definitions)
    {
        var index = new Dictionary<string, StoryNodeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions)
        {
            AddType(index, definition.CanonicalType, definition);
            AddType(index, definition.PersistedType, definition);
            foreach (var alias in definition.CompatibilityAliases) AddType(index, alias, definition);
        }
        return new ReadOnlyDictionary<string, StoryNodeDefinition>(index);
    }

    private static void AddType(IDictionary<string, StoryNodeDefinition> index, string type, StoryNodeDefinition definition)
    {
        if (index.TryGetValue(type, out var existing) && !ReferenceEquals(existing, definition))
            throw new InvalidOperationException($"Duplicate Story node type alias '{type}'.");
        index[type] = definition;
    }

    private static IReadOnlyList<StoryNodeDefinition> BuildDefinitions()
    {
        static StoryPropertyDefinition Ref(string name, string displayName, params string[] aliases) => new(name, true, displayName, aliases, string.Empty);
        static StoryPropertyDefinition Required(string name, string displayName, string defaultValue, bool isCore = true, IEnumerable<string>? aliases = null) => new(name, true, displayName, aliases, defaultValue, isCore);
        static StoryPropertyDefinition Optional(string name, string displayName, string? defaultValue = null, IEnumerable<string>? aliases = null) => new(name, false, displayName, aliases, defaultValue);
        static StoryNodeDefinition Node(string canonical, string persisted, string display, string category, bool input, StoryNodeOutputStrategy outputs, bool terminal = false, IEnumerable<StoryPropertyDefinition>? properties = null, IEnumerable<string>? aliases = null, IEnumerable<string>? compatibilityOutputs = null) => new(canonical, persisted, display, category, input, outputs, terminal, properties, aliases, compatibilityOutputs);

        return
        [
            Node("StoryStart", "story_start", "故事开始", "触发", false, StoryNodeOutputStrategy.SingleNext, aliases: ["storystart", "start"]),
            Node("ActorInteract", "interact_actor", "角色交互", "触发", true, StoryNodeOutputStrategy.SingleNext, properties: [Ref("actor_id", "角色", "actor", "actorId")], aliases: ["actorinteract", "actor_interact", "interact"]),
            Node("EnterRegion", "enter_region", "进入区域", "触发", true, StoryNodeOutputStrategy.SingleNext,
                properties:
                [
                    Required("dimension", "维度", "0"), Required("x", "X", "0"), Required("y", "Y", "0"),
                    Required("z", "Z", "0"), Required("radius", "半径", "3")
                ], aliases: ["enterregion", "region", "region_enter"]),
            Node("PlayDialogue", "play_dialogue", "播放对话", "对话", true, StoryNodeOutputStrategy.LegacyPreserved, properties: [Ref("dialogue_id", "对话", "dialogue", "dialogueId")], aliases: ["playdialogue", "dialogue"]),
            Node("DialogueExitBranch", "dialogue_exit_branch", "按 Dialogue Exit 分支", "对话", true, StoryNodeOutputStrategy.DialogueExits, properties: [Optional("exit_names", "出口", defaultValue: "hand_over, conceal", aliases: ["exitNames"])], aliases: ["dialogueexitbranch", "dialogue_branch", "exit_branch"]),
            Node("StartQuest", "start_quest", "开始任务", "任务", true, StoryNodeOutputStrategy.SingleNext, properties: [Ref("quest_id", "任务", "quest", "questId")], aliases: ["startquest", "quest_start"]),
            Node("WaitQuestComplete", "quest_completed", "等待任务完成", "任务", true, StoryNodeOutputStrategy.SingleNext, properties: [Ref("quest_id", "任务", "quest", "questId")], aliases: ["waitquestcomplete", "wait_quest_complete", "wait_quest", "quest_wait", "quest_completed"], compatibilityOutputs: ["complete"]),
            Node("CompleteQuest", "complete_quest", "完成任务", "任务", true, StoryNodeOutputStrategy.SingleNext, properties: [Ref("quest_id", "任务", "quest", "questId")], aliases: ["completequest"]),
            Node("Branch", "branch", "条件分支", "条件", true, StoryNodeOutputStrategy.Boolean,
                properties: [Required("variable", "变量", ""), Required("operator", "比较运算符", "=="), Required("value", "比较值", "")], aliases: ["condition", "if"]),
            Node("QuestState", "quest_state", "任务状态", "条件", true, StoryNodeOutputStrategy.Boolean,
                properties: [Ref("quest_id", "任务", "quest", "questId"), Required("state", "状态", "NOT_STARTED")], aliases: ["queststate"]),
            Node("HasItem", "has_item", "是否持有物品", "条件", true, StoryNodeOutputStrategy.Boolean,
                properties: [Required("item", "物品", ""), Required("metadata", "Metadata", "0", false), Required("amount", "数量", "1")], aliases: ["hasitem"]),
            Node("VariableCompare", "variable_compare", "变量比较", "条件", true, StoryNodeOutputStrategy.Boolean,
                properties: [Required("variable", "变量", ""), Required("operator", "比较运算符", "=="), Required("value", "比较值", "")], aliases: ["variablecompare", "compare_variable"]),
            Node("Sequence", "sequence", "顺序执行", "流程控制", true, StoryNodeOutputStrategy.Sequence),
            Node("GiveItem", "give_item", "物品给予", "动作 / 奖励", true, StoryNodeOutputStrategy.SingleNext,
                properties: [Required("item", "物品", ""), Required("metadata", "Metadata", "0", false), Required("amount", "数量", "1")], aliases: ["giveitem"]),
            Node("GiveXp", "give_xp", "经验给予", "动作 / 奖励", true, StoryNodeOutputStrategy.SingleNext,
                properties: [Required("amount", "经验值", "1")], aliases: ["givexp", "give_experience"]),
            Node("SendMessage", "send_message", "消息发送", "动作 / 奖励", true, StoryNodeOutputStrategy.SingleNext,
                properties: [Required("message", "消息", "")], aliases: ["sendmessage"]),
            Node("SetVariable", "set_variable", "设置变量", "动作 / 奖励", true, StoryNodeOutputStrategy.SingleNext,
                properties: [Required("variable", "变量", ""), Required("value", "值", "")], aliases: ["setvariable"]),
            Node("EnterStory", "enter_story", "进入故事", "故事", true, StoryNodeOutputStrategy.Terminal, true, [Ref("target_story_id", "目标故事", "story_id", "story", "target")], aliases: ["enterstory", "enter"]),
            Node("EndStory", "end_story", "结束当前故事", "故事", true, StoryNodeOutputStrategy.Terminal, true, aliases: ["endstory", "story_end"]),
            Node("End", "end", "结束", "结束", true, StoryNodeOutputStrategy.Terminal, true, aliases: ["terminal"]),
        ];
    }
}
