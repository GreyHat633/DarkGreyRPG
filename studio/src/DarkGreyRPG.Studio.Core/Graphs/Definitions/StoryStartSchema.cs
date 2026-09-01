using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Canonical authoring contract for the Story Flow Start node.</summary>
public static class StoryStartSchema
{
    public const string TriggersProperty = "triggers";
    public const string RepeatPolicyProperty = "repeat_policy";
    public const string Once = "once";
    public const string Repeatable = "repeatable";
    public const string Daily = "daily";
    public const string Cooldown = "cooldown";

    // These persisted names match the canonical Story trigger vocabulary.
    public const string ActorInteraction = "interact_actor";
    public const string RegionEntry = "enter_region";
    public const string Logic = "logic";
    public const string EnterStory = "enter_story";
    /// <summary>Optional per-trigger Logic condition port identity.</summary>
    public const string LogicPortIdProperty = "logic_port_id";
    public const string ConditionPortIdProperty = LogicPortIdProperty;
    public const string ActorIdProperty = "actor_id";
    public const string DimensionProperty = "dimension";
    public const string XProperty = "x";
    public const string YProperty = "y";
    public const string ZProperty = "z";
    public const string RadiusProperty = "radius";

    public static IReadOnlyList<string> SupportedTriggerTypes { get; } =
        [ActorInteraction, RegionEntry, Logic];

    /// <summary>Legacy trigger types accepted only when loading old data.</summary>
    public static IReadOnlyList<string> LegacyTriggerTypes { get; } = [EnterStory];

    public static IReadOnlyList<string> SupportedRepeatPolicies { get; } = [Once, Repeatable];

    /// <summary>Returns a valid payload for a newly selected trigger type.</summary>
    public static IReadOnlyDictionary<string, JsonElement> DefaultTriggerProperties(
        string triggerType, string? actorId = null)
        => triggerType switch
        {
            ActorInteraction when !string.IsNullOrWhiteSpace(actorId) =>
                new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    [ActorIdProperty] = JsonSerializer.SerializeToElement(actorId.Trim()),
                },
            RegionEntry => new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                [DimensionProperty] = JsonSerializer.SerializeToElement(0),
                [XProperty] = JsonSerializer.SerializeToElement(0d),
                [YProperty] = JsonSerializer.SerializeToElement(0d),
                [ZProperty] = JsonSerializer.SerializeToElement(0d),
                [RadiusProperty] = JsonSerializer.SerializeToElement(3d),
            },
            _ => new Dictionary<string, JsonElement>(StringComparer.Ordinal),
        };

    public static IReadOnlyDictionary<string, JsonElement> GetDefaultTriggerProperties(
        string triggerType, string? actorId = null)
        => DefaultTriggerProperties(triggerType, actorId);

    /// <summary>Creates the minimum valid Start shape with one stable trigger.</summary>
    public static void InitializeDefault(GraphNode node, string portId,
        string triggerType = RegionEntry, string? actorId = null,
        string? logicPortId = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!string.Equals(node.Type, "start", StringComparison.Ordinal))
            throw new ArgumentException("Story Start initialization requires a start node.", nameof(node));
        if (string.IsNullOrWhiteSpace(portId))
            throw new ArgumentException("Story Start trigger port_id is required.", nameof(portId));

        if (!SupportedTriggerTypes.Contains(triggerType, StringComparer.Ordinal))
            throw new ArgumentException($"Unsupported Story Start trigger type '{triggerType}'.", nameof(triggerType));
        if (triggerType == ActorInteraction && string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("An actor ID is required for interact_actor.", nameof(actorId));
        if (logicPortId is not null && string.IsNullOrWhiteSpace(logicPortId))
            throw new ArgumentException("A Logic condition port ID cannot be blank.", nameof(logicPortId));
        if (triggerType == Logic && string.IsNullOrWhiteSpace(logicPortId))
            throw new ArgumentException("A Logic trigger requires a Logic condition port ID.", nameof(logicPortId));

        var properties = DefaultTriggerProperties(triggerType, actorId);
        node.Properties[RepeatPolicyProperty] = JsonSerializer.SerializeToElement(Once);
        var trigger = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["port_id"] = portId,
            ["display_name"] = "启动条件 1",
            ["trigger_type"] = triggerType,
            ["trigger_properties"] = properties,
            ["order"] = 0,
        };
        if (logicPortId is not null)
            trigger[LogicPortIdProperty] = logicPortId;
        node.Properties[TriggersProperty] = JsonSerializer.SerializeToElement(new[] { trigger });
        node.Ports.Add(new GraphPort(portId, "启动条件 1", false, GraphInterfaceKind.Flow, 0));
        if (logicPortId is not null)
            node.Ports.Add(new GraphPort(logicPortId, "条件", true, GraphInterfaceKind.Logic, 0));
    }

    /// <summary>Validates trigger metadata and its one-to-one Flow projection.</summary>
    public static IReadOnlyList<ValidationIssue> Validate(GraphNode node, bool compatibilityMode = false)
    {
        ArgumentNullException.ThrowIfNull(node);
        var issues = new List<ValidationIssue>();
        if (!string.Equals(node.Type, "start", StringComparison.Ordinal)) return issues;

        if (node.Properties.TryGetValue(RepeatPolicyProperty, out var policy))
        {
            if (policy.ValueKind != JsonValueKind.String)
                issues.Add(Issue("graph.story.start.repeat_policy.kind", "repeat_policy must be a JSON string.", $"properties.{RepeatPolicyProperty}", node.Id));
            else if (!SupportedRepeatPolicies.Contains(policy.GetString() ?? string.Empty, StringComparer.Ordinal))
                issues.Add(Issue("graph.story.start.repeat_policy.unsupported", "Only once and repeatable repeat policies are enabled.", $"properties.{RepeatPolicyProperty}", node.Id));
        }

        if (!node.Properties.TryGetValue(TriggersProperty, out var triggers))
        {
            issues.Add(Issue("graph.story.start.triggers.required", "Story Start requires trigger metadata.", $"properties.{TriggersProperty}", node.Id));
            return issues;
        }
        if (triggers.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue("graph.story.start.triggers.kind", "Story Start triggers must be a JSON array.", $"properties.{TriggersProperty}", node.Id));
            return issues;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var orders = new HashSet<int>();
        var expectedPorts = new List<(string Id, string Name, int Order, string? LogicPortId)>();
        var index = 0;
        foreach (var item in triggers.EnumerateArray())
        {
            var field = $"properties.{TriggersProperty}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue("graph.story.start.trigger.object_required", "Each Story Start trigger must be an object.", field, node.Id));
                index++;
                continue;
            }

            var names = item.EnumerateObject().Select(property => property.Name).ToArray();
            var required = new[] { "port_id", "display_name", "trigger_type", "trigger_properties", "order" };
            var allowedNames = required.Append(LogicPortIdProperty).ToArray();
            if (names.Any(name => !allowedNames.Contains(name, StringComparer.Ordinal))
                || required.Any(name => !names.Contains(name, StringComparer.Ordinal)))
            {
                issues.Add(Issue("graph.story.start.trigger.fields", "Story Start triggers require exactly port_id, display_name, trigger_type, trigger_properties, and order.", field, node.Id));
                index++;
                continue;
            }

            var portId = ReadString(item, "port_id");
            var displayName = ReadString(item, "display_name");
            var triggerType = ReadString(item, "trigger_type");
            if (portId is null || displayName is null || triggerType is null)
                issues.Add(Issue("graph.story.start.trigger.value", "Story Start trigger identity, display name, and type must be nonblank strings.", field, node.Id));
            if (portId is not null && !ids.Add(portId))
                issues.Add(Issue("graph.story.start.trigger.port_id.duplicate", $"Story Start trigger port_id '{portId}' is duplicated.", field, node.Id));
            if (triggerType is not null && !SupportedTriggerTypes.Contains(triggerType, StringComparer.Ordinal)
                && !(compatibilityMode && LegacyTriggerTypes.Contains(triggerType, StringComparer.Ordinal)))
                issues.Add(Issue("graph.story.start.trigger.type.unsupported", $"Unsupported Story Start trigger type '{triggerType}'.", field, node.Id));

            var logicPortId = ReadString(item, LogicPortIdProperty);
            if (item.TryGetProperty(LogicPortIdProperty, out var logicPortElement)
                && logicPortElement.ValueKind != JsonValueKind.Null && logicPortId is null)
                issues.Add(Issue("graph.story.start.trigger.logic_port_id.invalid", "logic_port_id must be a nonblank string or null.", field, node.Id));
            if (logicPortId is not null && !ids.Add(logicPortId))
                issues.Add(Issue("graph.story.start.trigger.port_id.duplicate", $"Story Start trigger logic_port_id '{logicPortId}' is duplicated.", field, node.Id));
            if (triggerType == Logic && logicPortId is null)
                issues.Add(Issue("graph.story.start.trigger.logic_port_id.required", "Logic Story Start trigger requires logic_port_id.", field, node.Id));

            var properties = item.GetProperty("trigger_properties");
            if (triggerType is not null && (SupportedTriggerTypes.Contains(triggerType, StringComparer.Ordinal)
                || (compatibilityMode && LegacyTriggerTypes.Contains(triggerType, StringComparer.Ordinal))))
                ValidateTriggerProperties(triggerType, properties, $"{field}.trigger_properties", node.Id, issues);

            var orderElement = item.GetProperty("order");
            if (orderElement.ValueKind != JsonValueKind.Number || !orderElement.TryGetInt32(out var order) || order < 0)
                issues.Add(Issue("graph.story.start.trigger.order.invalid", "Story Start trigger order must be a nonnegative integer.", $"{field}.order", node.Id));
            else if (!orders.Add(order))
                issues.Add(Issue("graph.story.start.trigger.order.duplicate", "Story Start trigger order must be unique.", $"{field}.order", node.Id));
            else if (portId is not null && displayName is not null)
                expectedPorts.Add((portId, displayName, order, logicPortId));
            index++;
        }

        if (expectedPorts.Count == 0)
            issues.Add(Issue("graph.story.start.triggers.minimum", "Story Start requires at least one trigger.", $"properties.{TriggersProperty}", node.Id));

        var ordered = expectedPorts.OrderBy(item => item.Order).ToArray();
        if (ordered.Select((item, index) => item.Order == index).Any(valid => !valid))
            issues.Add(Issue("graph.story.start.trigger.order.contiguous", "Story Start trigger order must be contiguous from zero.", $"properties.{TriggersProperty}", node.Id));
        var ports = (node.Ports ?? []).Where(port => port is not null).ToArray();
        var flowOutputs = ports.Where(port => port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Flow).ToArray();
        if (flowOutputs.Length != ordered.Length)
            issues.Add(Issue("graph.story.start.trigger.port.count", "Story Start requires exactly one Flow output per trigger.", "ports", node.Id));
        foreach (var expected in ordered)
        {
            var matches = flowOutputs.Where(port => string.Equals(port.Id, expected.Id, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
            {
                issues.Add(Issue("graph.story.start.trigger.port.mapping", $"Trigger port '{expected.Id}' must occur exactly once as a Flow output.", $"ports[{expected.Id}]", node.Id));
                continue;
            }
            if (!string.Equals(matches[0].DisplayName, expected.Name, StringComparison.Ordinal)
                || matches[0].Order != expected.Order)
                issues.Add(Issue("graph.story.start.trigger.port.presentation", $"Trigger port '{expected.Id}' label/order is out of sync with metadata.", $"ports[{expected.Id}]", node.Id));
        }
        var expectedLogic = expectedPorts
            .Where(item => item.LogicPortId is not null)
            .Select(item => (Id: item.LogicPortId!, Name: $"条件：{item.Name}", Order: item.Order))
            .OrderBy(item => item.Order).ThenBy(item => item.Id, StringComparer.Ordinal).ToArray();
        var logicInputs = ports.Where(port => port.IsInput && port.InterfaceKind == GraphInterfaceKind.Logic).ToArray();
        if (logicInputs.Length != expectedLogic.Length)
            issues.Add(Issue("graph.story.start.trigger.logic_port.count", "Story Start requires exactly one Logic condition input per configured trigger condition.", "ports", node.Id));
        foreach (var expected in expectedLogic)
        {
            var matches = logicInputs.Where(port => string.Equals(port.Id, expected.Id, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
            {
                issues.Add(Issue("graph.story.start.trigger.logic_port.mapping", $"Trigger Logic port '{expected.Id}' must occur exactly once as a Logic input.", $"ports[{expected.Id}]", node.Id));
                continue;
            }
            if (matches[0].Order != expected.Order)
                issues.Add(Issue("graph.story.start.trigger.logic_port.presentation", $"Trigger Logic port '{expected.Id}' order is out of sync with metadata.", $"ports[{expected.Id}]", node.Id));
        }
        return issues;
    }

    public static bool IsValid(GraphNode node) => Validate(node).Count == 0;

    public static bool IsValid(GraphNode node, bool compatibilityMode)
        => Validate(node, compatibilityMode).Count == 0;

    public static IReadOnlyList<StoryStartTriggerSlot> ReadTriggers(GraphNode node)
    {
        if (!node.Properties.TryGetValue(TriggersProperty, out var value) || value.ValueKind != JsonValueKind.Array) return [];
        var result = new List<StoryStartTriggerSlot>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var portId = ReadString(item, "port_id");
            var displayName = ReadString(item, "display_name");
            var type = ReadString(item, "trigger_type");
            if (portId is null || displayName is null || type is null
                || !item.TryGetProperty("order", out var orderElement)
                || !orderElement.TryGetInt32(out var order)
                || !item.TryGetProperty("trigger_properties", out var properties)) continue;
            var logicPortId = ReadString(item, LogicPortIdProperty);
            result.Add(new(portId, displayName, type, properties.Clone(), order, logicPortId));
        }
        return result.OrderBy(item => item.Order).ThenBy(item => item.PortId, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateTriggerProperties(string type, JsonElement properties, string field, string nodeId, List<ValidationIssue> issues)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Issue("graph.story.start.trigger.properties.object_required", "trigger_properties must be a JSON object.", field, nodeId));
            return;
        }
        var allowed = type switch
        {
            ActorInteraction => new[] { ActorIdProperty },
            RegionEntry => new[] { DimensionProperty, XProperty, YProperty, ZProperty, RadiusProperty },
            EnterStory => Array.Empty<string>(),
            Logic => Array.Empty<string>(),
            _ => Array.Empty<string>(),
        };
        var required = allowed;
        var propertyNames = properties.EnumerateObject().Select(property => property.Name).ToArray();
        if (propertyNames.Length != required.Length || required.Any(name => !propertyNames.Contains(name, StringComparer.Ordinal)))
        {
            issues.Add(Issue("graph.story.start.trigger.properties.required",
                $"Trigger type '{type}' requires exactly {string.Join(", ", required)} trigger propert{(required.Length == 1 ? "y" : "ies")}.",
                field, nodeId));
        }
        foreach (var property in properties.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                issues.Add(Issue("graph.story.start.trigger.properties.unsupported", $"Property '{property.Name}' is not supported for trigger type '{type}'.", field, nodeId));
                continue;
            }
            var valid = type == ActorInteraction
                ? property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString())
                : type == RegionEntry
                    ? property.Value.ValueKind == JsonValueKind.Number
                        && (property.Name != DimensionProperty || property.Value.TryGetInt32(out _))
                        && (property.Name != RadiusProperty || property.Value.GetDouble() > 0)
                    : true;
            if (!valid) issues.Add(Issue("graph.story.start.trigger.properties.value", $"Trigger property '{property.Name}' has an invalid value.", field, nodeId));
        }
    }

    private static string? ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString() : null;

    private static ValidationIssue Issue(string code, string message, string field, string nodeId)
        => new(code, message, field, NodeId: string.IsNullOrWhiteSpace(nodeId) ? null : nodeId);
}

public sealed record StoryStartTriggerSlot(
    string PortId,
    string DisplayName,
    string TriggerType,
    JsonElement TriggerProperties,
    int Order,
    string? LogicPortId = null)
{
    public string Id => PortId;
    public string Type => TriggerType;
    public string? ConditionPortId => LogicPortId;
}
