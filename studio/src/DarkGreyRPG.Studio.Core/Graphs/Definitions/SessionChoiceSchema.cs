using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>
/// Session Choice contract. New options expose only their stable Flow output.
/// A persisted Logic output whose ID equals <c>option_id</c> is accepted only as
/// a lossless 0.3.1.4 compatibility port; it is not created for new authoring.
/// </summary>
public static class SessionChoiceSchema
{
    public const string PromptProperty = "prompt";
    public const string OptionsProperty = "options";

    public static void InitializeDefault(GraphNode node, string optionId, string flowPortId)
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
        node.Properties[PromptProperty] = JsonSerializer.SerializeToElement(string.Empty);
        node.Properties[OptionsProperty] = JsonSerializer.SerializeToElement(new[]
        {
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["option_id"] = optionId,
                ["display_text"] = displayText,
                ["flow_port_id"] = flowPortId,
            },
        });
        node.Ports.Add(new(flowPortId, displayText, false, GraphInterfaceKind.Flow, 0));
    }

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
            if (names.Length != 3
                || !names.Contains("option_id", StringComparer.Ordinal)
                || !names.Contains("display_text", StringComparer.Ordinal)
                || !names.Contains("flow_port_id", StringComparer.Ordinal))
            {
                issues.Add(Issue("graph.session.choice.option.fields",
                    "Session Choice options require exactly option_id, display_text, and flow_port_id.", field, node.Id));
                index++;
                continue;
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
        ValidatePorts(options.Select(option => (option.FlowPortId, option.DisplayText)).ToArray(), flowOutputs,
            GraphInterfaceKind.Flow, issues, node.Id);
        ValidateLegacyLogicPorts(options, logicOutputs, issues, node.Id);
        return issues;
    }

    private static void ValidateLegacyLogicPorts(
        IReadOnlyList<(string OptionId, string DisplayText, string FlowPortId)> options,
        IReadOnlyList<GraphPort> actual,
        List<ValidationIssue> issues,
        string nodeId)
    {
        var expected = options.ToDictionary(option => option.OptionId, StringComparer.Ordinal);
        foreach (var port in actual)
        {
            if (!expected.TryGetValue(port.Id, out var option))
            {
                issues.Add(Issue("graph.session.choice.legacy_logic.unmapped",
                    $"Legacy Session Choice Logic output '{port.Id}' does not map to an option_id.",
                    $"ports[{port.Id}]", nodeId));
                continue;
            }

            var duplicates = actual.Count(candidate => string.Equals(candidate.Id, port.Id, StringComparison.Ordinal));
            if (duplicates != 1)
            {
                issues.Add(Issue("graph.session.choice.legacy_logic.duplicate",
                    $"Legacy Session Choice Logic output '{port.Id}' must occur at most once.",
                    $"ports[{port.Id}]", nodeId));
                continue;
            }

            var expectedOrder = options.ToList().FindIndex(candidate =>
                string.Equals(candidate.OptionId, port.Id, StringComparison.Ordinal));
            if (!string.Equals(port.DisplayName, LogicDisplayName(option.DisplayText), StringComparison.Ordinal)
                || port.Order != expectedOrder)
                issues.Add(Issue("graph.session.choice.legacy_logic.presentation",
                    $"Legacy Session Choice Logic output '{port.Id}' label/order is out of sync with its option.",
                    $"ports[{port.Id}]", nodeId));
        }
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

    private static string LogicDisplayName(string displayText) => $"已选择：{displayText}";

    private static ValidationIssue Issue(string code, string message, string field, string nodeId)
        => new(code, message, field, NodeId: string.IsNullOrWhiteSpace(nodeId) ? null : nodeId);
}
