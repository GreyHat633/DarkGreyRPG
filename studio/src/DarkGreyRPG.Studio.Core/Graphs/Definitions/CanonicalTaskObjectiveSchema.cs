using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Frozen persisted contract for a canonical Task Objective node.</summary>
public static class CanonicalTaskObjectiveSchema
{
    public const string NodeType = "objective";
    public const string TypeProperty = "objective_type";
    public const string DescriptionProperty = "description";
    public const string RequiredProperty = "required";
    public const string EntityProperty = "entity";
    public const string ItemProperty = "item";
    public const string MetadataProperty = "metadata";
    public const string ActorIdProperty = "actor_id";
    /// <summary>Stable fixed output carrying the objective completion state.</summary>
    public const string CompletionPortId = "logic_status";
    public const string CompletionDisplayName = "完成";
    public const string ActivationPortPrefix = "logic_enable";

    public const string KillEntity = "kill_entity";
    public const string CollectItem = "collect_item";
    public const string InteractActor = "interact_actor";

    public static IReadOnlyList<string> ObjectiveTypes { get; } =
        [KillEntity, CollectItem, InteractActor];

    public static IReadOnlySet<string> CommonProperties { get; } =
        new HashSet<string>([TypeProperty, DescriptionProperty, RequiredProperty], StringComparer.Ordinal);

    public static IReadOnlySet<string> AllProperties { get; } =
        new HashSet<string>([TypeProperty, DescriptionProperty, RequiredProperty,
            EntityProperty, ItemProperty, MetadataProperty, ActorIdProperty], StringComparer.Ordinal);

    public static IReadOnlySet<string> PropertiesFor(string type) => type switch
    {
        KillEntity => new HashSet<string>([TypeProperty, DescriptionProperty, RequiredProperty, EntityProperty], StringComparer.Ordinal),
        CollectItem => new HashSet<string>([TypeProperty, DescriptionProperty, RequiredProperty, ItemProperty, MetadataProperty], StringComparer.Ordinal),
        InteractActor => new HashSet<string>([TypeProperty, DescriptionProperty, RequiredProperty, ActorIdProperty], StringComparer.Ordinal),
        _ => new HashSet<string>(StringComparer.Ordinal),
    };

    /// <summary>Creates the deterministic valid default used by new Objective nodes.</summary>
    public static void InitializeDefault(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!string.Equals(node.Type, NodeType, StringComparison.Ordinal))
            throw new ArgumentException("The node must be a Task Objective.", nameof(node));
        InitializeType(node, KillEntity);
    }

    /// <summary>Replaces the type payload while retaining common fields.</summary>
    public static bool TryInitializeType(GraphNode node, string? type, out IReadOnlyList<ValidationIssue> issues)
        => TryInitializeType(node, type, actorId: null, out issues);

    /// <summary>Initializes a new type; Interact requires an explicit actor ID.</summary>
    public static bool TryInitializeType(GraphNode node, string? type, string? actorId,
        out IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(node);
        issues = ValidateType(type, node!.Id);
        if (issues.Count != 0) return false;
        if (type == InteractActor && string.IsNullOrWhiteSpace(actorId))
        {
            issues = [Issue("graph.objective.actor.required", "An explicit actor ID is required when changing to interact_actor.", $"properties.{ActorIdProperty}", node!.Id)];
            return false;
        }
        InitializeType(node!, type!, actorId);
        issues = [];
        return true;
    }

    public static IReadOnlyList<ValidationIssue> Validate(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var issues = new List<ValidationIssue>();
        var properties = node.Properties ?? [];
        var type = ReadString(properties, TypeProperty);
        issues.AddRange(ValidateType(type, node.Id));
        if (issues.Count != 0) return issues;

        var expected = PropertiesFor(type!);
        foreach (var key in properties.Keys)
            if (!expected.Contains(key))
                issues.Add(Issue("graph.objective.property.unsupported", $"Objective property '{key}' is not valid for type '{type}'.", $"properties.{key}", node.Id));
        foreach (var key in expected)
            if (!properties.ContainsKey(key))
                issues.Add(Issue("graph.objective.property.missing", $"Objective property '{key}' is required for type '{type}'.", $"properties.{key}", node.Id));

        if (!properties.TryGetValue(DescriptionProperty, out var description)
            || description.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(description.GetString()))
            issues.Add(Issue("graph.objective.description.invalid", "Objective description must be a nonblank string.", $"properties.{DescriptionProperty}", node.Id));

        if (!properties.TryGetValue(RequiredProperty, out var required)
            || required.ValueKind != JsonValueKind.Number
            || !required.TryGetInt32(out var count) || count <= 0)
            issues.Add(Issue("graph.objective.required.invalid", "Objective required must be a positive integer.", $"properties.{RequiredProperty}", node.Id));

        switch (type)
        {
            case KillEntity:
                ValidateString(properties, EntityProperty, issues, node.Id);
                break;
            case CollectItem:
                ValidateString(properties, ItemProperty, issues, node.Id);
                if (!properties.TryGetValue(MetadataProperty, out var metadata)
                    || metadata.ValueKind != JsonValueKind.Object)
                    issues.Add(Issue("graph.objective.metadata.invalid", "Collect objective metadata must be a JSON object.", $"properties.{MetadataProperty}", node.Id));
                else
                    foreach (var item in metadata.EnumerateObject())
                        if (item.Value.ValueKind != JsonValueKind.String)
                            issues.Add(Issue("graph.objective.metadata.value.invalid", "Collect objective metadata values must be strings.", $"properties.{MetadataProperty}.{item.Name}", node.Id));
                break;
            case InteractActor:
                ValidateString(properties, ActorIdProperty, issues, node.Id);
                break;
        }
        return issues;
    }

    public static bool IsValid(GraphNode node) => Validate(node).Count == 0;

    private static void InitializeType(GraphNode node, string type, string? actorId = null)
    {
        var properties = node.Properties ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var description = ReadString(properties, DescriptionProperty);
        var required = properties.TryGetValue(RequiredProperty, out var count)
            && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var parsed) && parsed > 0 ? parsed : 1;
        properties.Clear();
        properties[TypeProperty] = JsonSerializer.SerializeToElement(type);
        properties[DescriptionProperty] = JsonSerializer.SerializeToElement(
            string.IsNullOrWhiteSpace(description) ? DefaultDescription(type) : description);
        properties[RequiredProperty] = JsonSerializer.SerializeToElement(required);
        switch (type)
        {
            case KillEntity:
                properties[EntityProperty] = JsonSerializer.SerializeToElement("minecraft:slime");
                break;
            case CollectItem:
                properties[ItemProperty] = JsonSerializer.SerializeToElement("minecraft:stone");
                properties[MetadataProperty] = JsonSerializer.SerializeToElement(new Dictionary<string, string>());
                break;
            case InteractActor:
                if (string.IsNullOrWhiteSpace(actorId))
                    throw new ArgumentException("An explicit actor ID is required for interact_actor.", nameof(actorId));
                properties[ActorIdProperty] = JsonSerializer.SerializeToElement(actorId);
                break;
        }
    }

    private static IReadOnlyList<ValidationIssue> ValidateType(string? type, string? nodeId)
        => ObjectiveTypes.Contains(type ?? string.Empty, StringComparer.Ordinal)
            ? []
            : [Issue("graph.objective.type.invalid", "Objective type must be kill_entity, collect_item, or interact_actor.", $"properties.{TypeProperty}", nodeId)];

    private static void ValidateString(IReadOnlyDictionary<string, JsonElement> properties, string name,
        ICollection<ValidationIssue> issues, string? nodeId)
    {
        if (!properties.TryGetValue(name, out var value)
            || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            issues.Add(Issue("graph.objective.target.invalid", $"Objective '{name}' must be a nonblank string.", $"properties.{name}", nodeId));
    }

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement> properties, string name)
        => properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static string DefaultDescription(string type) => type switch
    {
        CollectItem => "收集史莱姆凝胶",
        InteractActor => "向角色复命",
        _ => "消灭史莱姆",
    };

    private static ValidationIssue Issue(string code, string message, string field, string? nodeId)
        => new(code, message, field, NodeId: string.IsNullOrWhiteSpace(nodeId) ? null : nodeId);
}

/// <summary>Short compatibility alias for callers using the Task terminology.</summary>
public static class TaskObjectiveSchema
{
    public static IReadOnlyList<string> ObjectiveTypes => CanonicalTaskObjectiveSchema.ObjectiveTypes;
    public const string CompletionPortId = CanonicalTaskObjectiveSchema.CompletionPortId;
    public const string CompletionDisplayName = CanonicalTaskObjectiveSchema.CompletionDisplayName;
    public static IReadOnlyList<ValidationIssue> Validate(GraphNode node) => CanonicalTaskObjectiveSchema.Validate(node);
    public static bool IsValid(GraphNode node) => CanonicalTaskObjectiveSchema.IsValid(node);
    public static void InitializeDefault(GraphNode node) => CanonicalTaskObjectiveSchema.InitializeDefault(node);
}
