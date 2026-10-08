using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>
/// Session Choice contract. New options expose only their stable Flow output.
/// Each option may expose a condition Logic input; output Logic ports are retired.
/// </summary>
public static class SessionChoiceSchema
{
    public const string OptionsProperty = "options";

    public static void InitializeWithoutConditions(GraphNode node, string optionId, string flowPortId)
    {
        InitializeDefault(node, optionId, flowPortId);
        node.Ports.RemoveAll(p => p.IsInput && p.InterfaceKind == GraphInterfaceKind.Logic);
        node.Properties[OptionsProperty] = JsonSerializer.SerializeToElement(new[] { new { option_id = optionId, display_text = "选项 1", flow_port_id = flowPortId } });
    }

    public static void InitializeDefault(GraphNode node, string optionId, string flowPortId, string? conditionPortId = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!string.Equals(node.Type, "choice", StringComparison.Ordinal))
            throw new ArgumentException("Session Choice initialization requires a choice node.", nameof(node));
        if (string.IsNullOrWhiteSpace(optionId))
            throw new ArgumentException("Choice option_id is required.", nameof(optionId));
        if (string.IsNullOrWhiteSpace(flowPortId))
            throw new ArgumentException("Choice flow_port_id is required.", nameof(flowPortId));
        if (string.Equals(optionId, flowPortId, StringComparison.Ordinal))
            throw new ArgumentException("Choice option_id and flow_port_id must be distinct.", nameof(flowPortId));

        const string displayText = "选项 1";
        node.Properties[OptionsProperty] = JsonSerializer.SerializeToElement(new[]
        {
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["option_id"] = optionId,
                ["display_text"] = displayText,
                ["flow_port_id"] = flowPortId,
                ["condition_port_id"] = conditionPortId ?? Guid.NewGuid().ToString("N"),
                ["unavailable_behavior"] = "hide",
                ["unavailable_hint"] = "",
                ["condition_enabled"] = false,
            },
        });
        node.Ports.Add(new(flowPortId, displayText, false, GraphInterfaceKind.Flow, 0));
        var condition = node.Properties[OptionsProperty][0].GetProperty("condition_port_id").GetString()!;
        node.Ports.Add(new(condition, $"条件 · {displayText}", true, GraphInterfaceKind.Logic, 0));
    }

    public static bool ConditionEnabled(JsonElement option, GraphDocument graph, string nodeId)
        => option.TryGetProperty("condition_enabled", out var enabled) ? enabled.ValueKind == JsonValueKind.True
            : option.TryGetProperty("condition_port_id", out var port) && graph.Connections.Any(c => c.ToNodeId == nodeId && c.ToPortId == port.GetString());

    public static IReadOnlyList<ValidationIssue> Validate(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var issues = new List<ValidationIssue>();
        if (!string.Equals(node.Type, "choice", StringComparison.Ordinal)) return issues;

        if (!node.Properties.TryGetValue(OptionsProperty, out var optionsElement)
            || optionsElement.ValueKind != JsonValueKind.Array)
            return issues;

        var optionIds = new HashSet<string>(StringComparer.Ordinal);
        var flowPortIds = new HashSet<string>(StringComparer.Ordinal);
        var options = new List<(string OptionId, string DisplayText, string FlowPortId)>();
        var index = 0;
        foreach (var element in optionsElement.EnumerateArray())
        {
            var field = $"properties.options[{index}]";
            if (element.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue("graph.session.choice.option.object_required",
                    "Each Session Choice option must be an object.", field, node.Id));
                index++;
                continue;
            }

            var names = element.EnumerateObject().Select(property => property.Name).ToArray();
            if ((names.Length != 3 && names.Length != 6 && names.Length != 7)
                || !names.Contains("option_id", StringComparer.Ordinal)
                || !names.Contains("display_text", StringComparer.Ordinal)
                || !names.Contains("flow_port_id", StringComparer.Ordinal))
            {
                issues.Add(Issue("graph.session.choice.option.fields",
                    "Session Choice options require exactly option_id, display_text, and flow_port_id.", field, node.Id));
                index++;
                continue;
            }

            if (names.Length >= 6)
            {
                if (names.Length == 7 && (!element.TryGetProperty("condition_enabled", out var enabled) || enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False)))
                    issues.Add(Issue("graph.session.choice.condition.enabled", "前置条件启用状态必须为布尔值。", field, node.Id));
                var condition = ReadString(element, "condition_port_id");
                var mode = ReadString(element, "unavailable_behavior");
                if (condition is null || mode is not ("hide" or "disable")
                    || !element.TryGetProperty("unavailable_hint", out var hint) || hint.ValueKind != JsonValueKind.String
                    || hint.GetString()!.StartsWith(DynamicContentText.Prefix, StringComparison.Ordinal))
                    issues.Add(Issue("graph.session.choice.condition.fields", "Choice condition requires a stable input, hide/disable behavior, and a literal hint.", field, node.Id));
                var input = node.Ports.Where(port => port.Id == condition).ToArray();
                if (input.Length != 1 || !input[0].IsInput || input[0].InterfaceKind != GraphInterfaceKind.Logic
                    || input[0].Order != index || input[0].DisplayName != $"条件 · {ReadString(element, "display_text")}")
                    issues.Add(Issue("graph.session.choice.condition.mapping", "Choice condition input mapping is invalid.", field, node.Id));
            }

            var optionId = ReadString(element, "option_id");
            var displayText = ReadString(element, "display_text");
            var flowPortId = ReadString(element, "flow_port_id");
            if (optionId is null || displayText is null || flowPortId is null)
            {
                issues.Add(Issue("graph.session.choice.option.value",
                    "Session Choice option fields must be non-blank strings.", field, node.Id));
                index++;
                continue;
            }
            if (!optionIds.Add(optionId))
                issues.Add(Issue("graph.session.choice.option_id.duplicate",
                    $"Session Choice option_id '{optionId}' is duplicated.", field, node.Id));
            if (!flowPortIds.Add(flowPortId))
                issues.Add(Issue("graph.session.choice.flow_port_id.duplicate",
                    $"Session Choice flow_port_id '{flowPortId}' is duplicated.", field, node.Id));
            if (string.Equals(optionId, flowPortId, StringComparison.Ordinal))
                issues.Add(Issue("graph.session.choice.port_id.collision",
                    "Session Choice option_id and flow_port_id must be distinct.", field, node.Id));
            options.Add((optionId, displayText, flowPortId));
            index++;
        }

        if (options.Count == 0)
            issues.Add(Issue("graph.session.choice.options.minimum",
                "Session Choice requires at least one option.", "properties.options", node.Id));

        var ports = (node.Ports ?? []).Where(port => port is not null).ToArray();
        var flowOutputs = ports.Where(port => port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Flow).ToArray();
        var logicOutputs = ports.Where(port => port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Logic).ToArray();
        var expectedConditions = optionsElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object && e.TryGetProperty("condition_port_id", out _)).Select(e => ReadString(e, "condition_port_id")).ToArray();
        if (expectedConditions.Distinct().Count() != expectedConditions.Length || ports.Count(p => p.IsInput && p.InterfaceKind == GraphInterfaceKind.Logic) != expectedConditions.Length)
            issues.Add(Issue("graph.session.choice.condition.count", "Choice condition inputs must map one-to-one.", "ports", node.Id));
        if (expectedConditions.Any(id => id is not null && (optionIds.Contains(id) || flowPortIds.Contains(id))))
            issues.Add(Issue("graph.session.choice.port_id.collision", "Choice condition identity must differ from option and Flow identities.", "ports", node.Id));
        ValidatePorts(options.Select(option => (option.FlowPortId, option.DisplayText)).ToArray(), flowOutputs,
            GraphInterfaceKind.Flow, issues, node.Id);
        foreach (var port in logicOutputs)
            issues.Add(Issue("graph.session.choice.output_logic.retired",
                $"Session Choice Logic output '{port.Id}' is retired. Use its Flow result.",
                $"ports[{port.Id}]", node.Id));
        return issues;
    }

    private static void ValidatePorts(
        IReadOnlyList<(string Id, string DisplayName)> expected,
        IReadOnlyList<GraphPort> actual,
        GraphInterfaceKind kind,
        List<ValidationIssue> issues,
        string nodeId)
    {
        if (actual.Count != expected.Count)
            issues.Add(Issue("graph.session.choice.port.count",
                $"Session Choice requires exactly one {kind} output per option.", "ports", nodeId));
        for (var index = 0; index < expected.Count; index++)
        {
            var item = expected[index];
            var matches = actual.Where(port => string.Equals(port.Id, item.Id, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
            {
                issues.Add(Issue("graph.session.choice.port.mapping",
                    $"Session Choice option port '{item.Id}' must occur exactly once as a {kind} output.",
                    $"ports[{item.Id}]", nodeId));
                continue;
            }
            if (!string.Equals(matches[0].DisplayName, item.DisplayName, StringComparison.Ordinal)
                || matches[0].Order != index)
                issues.Add(Issue("graph.session.choice.port.presentation",
                    $"Session Choice option port '{item.Id}' label/order is out of sync with options.",
                    $"ports[{item.Id}]", nodeId));
        }
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var result = value.GetString();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static ValidationIssue Issue(string code, string message, string field, string nodeId)
        => new(code, message, field, NodeId: string.IsNullOrWhiteSpace(nodeId) ? null : nodeId);
}
