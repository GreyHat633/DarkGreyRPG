using System.Collections.ObjectModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Canonical registry for Story Flow, Session, and Task node types.</summary>
public static class GraphNodeDefinitionRegistry
{
    private static readonly IReadOnlyList<GraphNodeDefinition> _definitions =
        new ReadOnlyCollection<GraphNodeDefinition>(BuildDefinitions());

    private static readonly IReadOnlyDictionary<(GraphScope Scope, string Type), GraphNodeDefinition> _byScopeType =
        new ReadOnlyDictionary<(GraphScope Scope, string Type), GraphNodeDefinition>(
            _definitions.ToDictionary(definition => (definition.Scope, definition.Type)));

    public static IReadOnlyList<GraphNodeDefinition> Definitions => _definitions;
    public static IReadOnlyList<GraphNodeDefinition> All => _definitions;

    public static IReadOnlyList<GraphNodeDefinition> ForScope(GraphScope scope)
        => _definitions.Where(definition => definition.Scope == scope).ToArray();

    public static IReadOnlyList<GraphNodeDefinition> GetForScope(GraphScope scope) => ForScope(scope);

    /// <summary>Returns authorable definitions in canonical registry order.</summary>
    public static IReadOnlyList<GraphNodeDefinition> ForAuthoringScope(GraphScope scope)
        => _definitions.Where(definition => definition.Scope == scope && !definition.CompatibilityOnly).ToArray();

    public static IReadOnlyList<GraphNodeDefinition> GetForAuthoringScope(GraphScope scope)
        => ForAuthoringScope(scope);

    public static bool TryGet(string? type, out GraphNodeDefinition definition)
    {
        // This overload is useful for callers which only need to discover a
        // canonical type; scoped validation always uses the scoped overload.
        definition = _definitions.FirstOrDefault(item => string.Equals(item.Type, type, StringComparison.Ordinal))!;
        return definition is not null;
    }

    public static bool TryGet(GraphScope scope, string? type, out GraphNodeDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(type) && _byScopeType.TryGetValue((scope, type), out var found))
        {
            definition = found;
            return true;
        }

        definition = null!;
        return false;
    }

    public static GraphNodeDefinition? Get(string? type) => TryGet(type, out var definition) ? definition : null;
    public static GraphNodeDefinition? Get(GraphScope scope, string? type)
        => TryGet(scope, type, out var definition) ? definition : null;
    public static GraphNodeDefinition? Find(string? type) => Get(type);

    private static List<GraphNodeDefinition> BuildDefinitions()
    {
        static string LocalizePortDisplayName(string displayName) => displayName switch
        {
            "Flow In" => "流程输入",
            "Flow Out" => "流程输出",
            "Logic In" => "逻辑输入",
            "Logic Out" => "逻辑输出",
            "True" => "是",
            "False" => "否",
            _ => displayName,
        };
        static GraphPortDefinition In(string id, string displayName, GraphInterfaceKind kind, int order)
            => new(id, LocalizePortDisplayName(displayName), GraphPortDirection.Input, kind, order);
        static GraphPortDefinition Out(string id, string displayName, GraphInterfaceKind kind, int order)
            => new(id, LocalizePortDisplayName(displayName), GraphPortDirection.Output, kind, order);
        static JsonElement Json(string text)
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        static GraphPropertyDefinition StringProperty(string name)
            => new(name, JsonValueKind.String, true, Json("\"\""));
        static GraphPropertyDefinition ArrayProperty(string name)
            => new(name, JsonValueKind.Array, true, Json("[]"));
        static GraphPropertyDefinition NumberProperty(string name, int defaultValue, bool required = true)
            => new(name, JsonValueKind.Number, required, Json(defaultValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        static GraphPropertyDefinition ObjectProperty(string name)
            => new(name, JsonValueKind.Object, false);
        static GraphNodeDefinition Node(
            string type,
            GraphScope scope,
            string displayName,
            string category,
            bool required = false,
            bool unique = false,
            bool compatibilityOnly = false,
            IEnumerable<GraphInterfaceKind>? kinds = null,
            IEnumerable<GraphPortDefinition>? ports = null,
            IEnumerable<GraphPropertyDefinition>? properties = null)
            => new(type, scope, displayName, category, kinds, required, unique, required || unique, compatibilityOnly,
                fixedPorts: ports, propertyDefinitions: properties);

        var all = new[] { GraphInterfaceKind.Flow, GraphInterfaceKind.Logic };
        var flowOnly = new[] { GraphInterfaceKind.Flow };
        var logicOnly = new[] { GraphInterfaceKind.Logic };
        return
        [
            Node("start", GraphScope.StoryFlow, "开始", "流程", required: true, unique: true, kinds: all,
                properties: [
                    new GraphPropertyDefinition(StoryStartSchema.TriggersProperty, JsonValueKind.Array),
                    new GraphPropertyDefinition(StoryStartSchema.RepeatPolicyProperty, JsonValueKind.String),
                ]),
            Node("terminate", GraphScope.StoryFlow, "终止", "流程", kinds: flowOnly,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0)]),
            Node("session", GraphScope.StoryFlow, "会话", "聚合", kinds: all,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0)],
                properties: [StringProperty("resource_id")]),
            Node("task", GraphScope.StoryFlow, "任务", "聚合", kinds: all,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0)],
                properties: [StringProperty("resource_id")]),
            Node("condition", GraphScope.StoryFlow, "条件", "逻辑", kinds: all,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0), In("logic_in", "Logic In", GraphInterfaceKind.Logic, 1), Out("flow_true", "True", GraphInterfaceKind.Flow, 2), Out("flow_false", "False", GraphInterfaceKind.Flow, 3)]),
            Node("and", GraphScope.StoryFlow, "与", "逻辑", kinds: logicOnly,
                ports: [Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 0)]),
            Node("or", GraphScope.StoryFlow, "或", "逻辑", kinds: logicOnly,
                ports: [Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 0)]),
            Node("not", GraphScope.StoryFlow, "非", "逻辑", kinds: logicOnly,
                ports: [In("logic_in", "Logic In", GraphInterfaceKind.Logic, 0), Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 1)]),
            Node("action", GraphScope.StoryFlow, "动作", "动作", kinds: flowOnly,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0), Out("flow_out", "Flow Out", GraphInterfaceKind.Flow, 1)],
                properties:
                [
                    new GraphPropertyDefinition(CanonicalStoryActionSchema.TypeProperty, JsonValueKind.String, true,
                        Json("\"send_message\"")),
                    new GraphPropertyDefinition(CanonicalStoryActionSchema.ItemProperty, JsonValueKind.String),
                    new GraphPropertyDefinition(CanonicalStoryActionSchema.AmountProperty, JsonValueKind.Number),
                    new GraphPropertyDefinition(CanonicalStoryActionSchema.MessageProperty, JsonValueKind.String),
                ]),
            // Story event waits are ordinary mid-flow nodes.  Their trigger
            // payload is deliberately kept on the node rather than folded
            // into Story Start metadata; Start triggers have a separate
            // schema and lifecycle.
            Node("interact_actor", GraphScope.StoryFlow, "角色交互", "触发", kinds: flowOnly,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0), Out("flow_out", "Flow Out", GraphInterfaceKind.Flow, 1)],
                properties: [StringProperty(StoryStartSchema.ActorIdProperty)]),
            Node("enter_region", GraphScope.StoryFlow, "进入区域", "触发", kinds: flowOnly,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0), Out("flow_out", "Flow Out", GraphInterfaceKind.Flow, 1)],
                properties:
                [
                    NumberProperty(StoryStartSchema.DimensionProperty, 0),
                    NumberProperty(StoryStartSchema.XProperty, 0),
                    NumberProperty(StoryStartSchema.YProperty, 0),
                    NumberProperty(StoryStartSchema.ZProperty, 0),
                    NumberProperty(StoryStartSchema.RadiusProperty, 3),
                ]),
            Node("enter_story", GraphScope.StoryFlow, "进入故事", "故事", kinds: flowOnly,
                compatibilityOnly: true, ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0)],
                properties: [StringProperty("target_story_id")]),
            Node("logic_input", GraphScope.StoryFlow, "逻辑输入", "逻辑", kinds: logicOnly,
                ports: [Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 0)],
                properties: [StringProperty("port_id"), StringProperty("display_name")]),
            Node("logic_output", GraphScope.StoryFlow, "逻辑输出", "逻辑", kinds: logicOnly,
                ports: [In("logic_in", "Logic In", GraphInterfaceKind.Logic, 0)],
                properties: [StringProperty("port_id"), StringProperty("display_name")]),

            Node("start", GraphScope.Session, "起始", "会话", required: true, unique: true, kinds: flowOnly,
                ports: [Out("flow_out", "流程输出", GraphInterfaceKind.Flow, 0)]),
            Node("line", GraphScope.Session, "台词", "会话", kinds: flowOnly,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0), Out("flow_out", "Flow Out", GraphInterfaceKind.Flow, 1)],
                properties: [StringProperty("speaker_actor_id"), StringProperty("text")]),
            Node("choice", GraphScope.Session, "选择", "会话", kinds: all,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0)],
                properties: [StringProperty("prompt"), ArrayProperty("options")]),
            Node("narration", GraphScope.Session, "旁白", "会话", kinds: flowOnly,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0), Out("flow_out", "Flow Out", GraphInterfaceKind.Flow, 1)],
                properties: [StringProperty("text")]),
            Node("condition", GraphScope.Session, "条件", "逻辑", kinds: all,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0), In("logic_in", "Logic In", GraphInterfaceKind.Logic, 1), Out("flow_true", "True", GraphInterfaceKind.Flow, 2), Out("flow_false", "False", GraphInterfaceKind.Flow, 3)]),
            Node("and", GraphScope.Session, "与", "逻辑", kinds: logicOnly,
                ports: [Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 0)]),
            Node("or", GraphScope.Session, "或", "逻辑", kinds: logicOnly,
                ports: [Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 0)]),
            Node("not", GraphScope.Session, "非", "逻辑", kinds: logicOnly,
                ports: [In("logic_in", "Logic In", GraphInterfaceKind.Logic, 0), Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 1)]),
            Node("logic_output", GraphScope.Session, "逻辑输出", "逻辑", kinds: [GraphInterfaceKind.Logic],
                ports: [In("logic_in", "Logic In", GraphInterfaceKind.Logic, 0)],
                properties: [StringProperty("port_id"), StringProperty("display_name")]),
            Node("logic_input", GraphScope.Session, "逻辑输入", "逻辑", kinds: [GraphInterfaceKind.Logic],
                ports: [Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 0)],
                properties: [StringProperty("port_id"), StringProperty("display_name")]),
            Node("end", GraphScope.Session, "结束", "结束", kinds: flowOnly,
                ports: [In("flow_in", "Flow In", GraphInterfaceKind.Flow, 0)],
                properties: [StringProperty("port_id"), StringProperty("display_name")]),
            Node("legacy_jump", GraphScope.Session, "旧 Jump", "兼容", compatibilityOnly: true, kinds: all),

            Node("activate", GraphScope.Task, "激活", "兼容", compatibilityOnly: true, kinds: logicOnly,
                ports: [Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 0)]),
            Node("objective", GraphScope.Task, "目标", "任务", kinds: logicOnly,
                ports: [Out(CanonicalTaskObjectiveSchema.CompletionPortId, CanonicalTaskObjectiveSchema.CompletionDisplayName, GraphInterfaceKind.Logic, 0)],
                properties: [
                    new GraphPropertyDefinition(CanonicalTaskObjectiveSchema.TypeProperty, JsonValueKind.String, true, Json("\"kill_entity\"")),
                    new GraphPropertyDefinition(CanonicalTaskObjectiveSchema.DescriptionProperty, JsonValueKind.String, true, Json("\"消灭史莱姆\"")),
                    NumberProperty(CanonicalTaskObjectiveSchema.RequiredProperty, 10),
                    new GraphPropertyDefinition(CanonicalTaskObjectiveSchema.EntityProperty, JsonValueKind.String),
                    new GraphPropertyDefinition(CanonicalTaskObjectiveSchema.ItemProperty, JsonValueKind.String),
                    ObjectProperty(CanonicalTaskObjectiveSchema.MetadataProperty),
                    new GraphPropertyDefinition(CanonicalTaskObjectiveSchema.ActorIdProperty, JsonValueKind.String)]),
            Node("and", GraphScope.Task, "与", "逻辑", kinds: logicOnly,
                ports: [Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 0)]),
            Node("or", GraphScope.Task, "或", "逻辑", kinds: logicOnly,
                ports: [Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 0)]),
            Node("not", GraphScope.Task, "非", "逻辑", kinds: logicOnly,
                ports: [In("logic_in", "Logic In", GraphInterfaceKind.Logic, 0), Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 1)]),
            Node("logic_output", GraphScope.Task, "逻辑输出", "逻辑", kinds: [GraphInterfaceKind.Logic],
                ports: [In("logic_in", "Logic In", GraphInterfaceKind.Logic, 0)],
                properties: [StringProperty("port_id"), StringProperty("display_name")]),
            Node("logic_input", GraphScope.Task, "逻辑输入", "逻辑", kinds: [GraphInterfaceKind.Logic],
                ports: [Out("logic_out", "Logic Out", GraphInterfaceKind.Logic, 0)],
                properties: [StringProperty("port_id"), StringProperty("display_name"),
                    new GraphPropertyDefinition("source", JsonValueKind.String)]),
            Node("settle", GraphScope.Task, "结算", "任务", required: true, unique: true, kinds: logicOnly),
        ];
    }
}

/// <summary>Short facade for code that calls the canonical registry GraphNodeRegistry.</summary>
public static class GraphNodeRegistry
{
    public static IReadOnlyList<GraphNodeDefinition> Definitions => GraphNodeDefinitionRegistry.Definitions;
    public static IReadOnlyList<GraphNodeDefinition> ForScope(GraphScope scope) => GraphNodeDefinitionRegistry.ForScope(scope);
    public static IReadOnlyList<GraphNodeDefinition> ForAuthoringScope(GraphScope scope) => GraphNodeDefinitionRegistry.ForAuthoringScope(scope);
    public static bool TryGet(string? type, out GraphNodeDefinition definition) => GraphNodeDefinitionRegistry.TryGet(type, out definition);
    public static GraphNodeDefinition? Get(string? type) => GraphNodeDefinitionRegistry.Get(type);
    public static bool TryGet(GraphScope scope, string? type, out GraphNodeDefinition definition) => GraphNodeDefinitionRegistry.TryGet(scope, type, out definition);
    public static GraphNodeDefinition? Get(GraphScope scope, string? type) => GraphNodeDefinitionRegistry.Get(scope, type);
}
