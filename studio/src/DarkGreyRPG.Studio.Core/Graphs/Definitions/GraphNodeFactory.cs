using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>
/// Creates the fixed portion of a canonical node from the scoped definition
/// registry.  Dynamic-slot initialization remains owned by
/// <see cref="Editing.GraphDynamicPortPolicy"/>.
/// </summary>
public static class GraphNodeFactory
{
    public static GraphNode Create(
        GraphScope scope,
        string type,
        string id,
        string? displayName = null,
        bool compatibilityMode = false)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A node ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("A node type is required.", nameof(type));

        if (!GraphNodeDefinitionRegistry.TryGet(scope, type, out var definition))
        {
            if (GraphNodeDefinitionRegistry.TryGet(type, out var known))
                throw new InvalidOperationException(
                    $"Node type '{type}' belongs to scope '{known.Scope}', not '{scope}'.");
            throw new KeyNotFoundException($"Node type '{type}' is not registered for scope '{scope}'.");
        }

        if (definition.CompatibilityOnly && !compatibilityMode)
            throw new InvalidOperationException(
                $"Node type '{type}' is compatibility-only and requires explicit compatibility creation.");

        var ports = definition.FixedPorts.Select(port => port.CreatePort()).ToArray();
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in definition.PropertyDefinitions)
        {
            if (property.DefaultValue is { } defaultValue)
                properties[property.Name] = defaultValue.Clone();
        }

        // GraphNode clones both collections at its boundary.  The explicit
        // materialization above additionally ensures each call owns fresh
        // values even before the graph-core constructor runs.
        var authoringName = string.IsNullOrWhiteSpace(displayName)
            ? definition.DisplayName
            : displayName.Trim();
        var node = new GraphNode(id, type, authoringName, ports, properties);
        if (scope == GraphScope.Task && string.Equals(type, CanonicalTaskObjectiveSchema.NodeType, StringComparison.Ordinal))
            CanonicalTaskObjectiveSchema.InitializeDefault(node);
        if (scope == GraphScope.StoryFlow && string.Equals(type, CanonicalStoryActionSchema.NodeType, StringComparison.Ordinal))
            CanonicalStoryActionSchema.InitializeDefault(node);
        if (scope == GraphScope.Task && type == "reward")
            node.Properties["entries"] = JsonSerializer.SerializeToElement(new[] { new { type = "item", item = "", amount = 1 } });
        return node;
    }

    public static GraphNode CreateNode(
        GraphScope scope,
        string type,
        string id,
        string? displayName = null,
        bool compatibilityMode = false)
        => Create(scope, type, id, displayName, compatibilityMode);

    /// <summary>Creates the canonical Story Start with its minimum trigger.</summary>
    public static GraphNode CreateStoryStart(string id, string? displayName = null, string? triggerPortId = null)
    {
        var node = Create(GraphScope.StoryFlow, "start", id, displayName);
        StoryStartSchema.InitializeDefault(node, string.IsNullOrWhiteSpace(triggerPortId)
            ? $"story_trigger_{Guid.NewGuid():N}" : triggerPortId);
        return node;
    }

    public static GraphNode Create(
        string id,
        GraphScope scope,
        string type,
        string? displayName = null,
        bool compatibilityMode = false)
        => Create(scope, type, id, displayName, compatibilityMode);

    public static GraphNode CreateNode(
        string id,
        GraphScope scope,
        string type,
        string? displayName = null,
        bool compatibilityMode = false)
        => Create(scope, type, id, displayName, compatibilityMode);

    public static bool TryCreate(
        GraphScope scope,
        string type,
        string id,
        out GraphNode? node,
        string? displayName = null,
        bool compatibilityMode = false)
    {
        try
        {
            node = Create(scope, type, id, displayName, compatibilityMode);
            return true;
        }
        catch (ArgumentException)
        {
            node = null;
            return false;
        }
        catch (InvalidOperationException)
        {
            node = null;
            return false;
        }
        catch (KeyNotFoundException)
        {
            node = null;
            return false;
        }
    }
}
