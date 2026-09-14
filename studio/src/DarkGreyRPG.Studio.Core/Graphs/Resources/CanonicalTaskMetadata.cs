using System.Text.Json;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Task-only author text, independent of individual Objective descriptions.</summary>
public sealed record CanonicalTaskMetadata
{
    public CanonicalTaskMetadata(string description)
        => Description = description ?? throw new ArgumentNullException(nameof(description));

    public string Description { get; }

    internal static CanonicalTaskMetadata? Read(JsonElement root, GraphResourceKind kind)
    {
        if (!root.TryGetProperty("task_metadata", out var value)) return null;
        if (kind != GraphResourceKind.Task)
            throw new GraphResourceEnvelopeException("graph.resource.task_metadata.scope", "任务说明只能用于 Task 资源。");
        if (value.ValueKind != JsonValueKind.Object
            || value.EnumerateObject().Count() != 1
            || !value.TryGetProperty("description", out var description)
            || description.ValueKind != JsonValueKind.String)
            throw new GraphResourceEnvelopeException("graph.resource.task_metadata.invalid", "任务说明必须是 description 文本字段。");
        return new CanonicalTaskMetadata(description.GetString()!);
    }
}
