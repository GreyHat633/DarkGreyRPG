using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Quests;

public sealed class QuestResource
{
    public const int LegacySchemaVersion = 1; public const int CurrentSchemaVersion = 2;
    [JsonPropertyName("schema_version")] [JsonPropertyOrder(0)] public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    [JsonPropertyName("id")] [JsonPropertyOrder(1)] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("title")] [JsonPropertyOrder(2)] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("display_name")] [JsonPropertyOrder(3)] public string DisplayName { get; init; } = string.Empty;
    [JsonPropertyName("description")] [JsonPropertyOrder(4)] public string Description { get; init; } = string.Empty;
    [JsonPropertyName("home_story_id")] [JsonPropertyOrder(5)] public string HomeStoryId { get; init; } = "uncategorized";
    [JsonPropertyName("objectives")] [JsonPropertyOrder(6)] public List<QuestObjectiveResource> Objectives { get; init; } = [];
    [JsonPropertyName("objective_groups")] [JsonPropertyOrder(7)] public List<ObjectiveGroupResource> ObjectiveGroups { get; init; } = [];
    [JsonPropertyName("metadata")] [JsonPropertyOrder(8)] public QuestMetadata Metadata { get; init; } = new();
    public QuestResource WithId(string id) => new() { SchemaVersion = SchemaVersion, Id = id, Title = Title, DisplayName = DisplayName, Description = Description, HomeStoryId = HomeStoryId, Objectives = Objectives.Select(x => x.Clone()).ToList(), ObjectiveGroups = ObjectiveGroups.Select(x => x.Clone()).ToList(), Metadata = Metadata.Clone() };
}
public sealed class QuestObjectiveResource
{
    [JsonPropertyName("id")] [JsonPropertyOrder(0)] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("type")] [JsonPropertyOrder(1)] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("description")] [JsonPropertyOrder(2)] public string Description { get; init; } = string.Empty;
    [JsonPropertyName("entity")] [JsonPropertyOrder(3)] public string? Entity { get; init; }
    [JsonPropertyName("item")] [JsonPropertyOrder(4)] public string? Item { get; init; }
    [JsonPropertyName("metadata")] [JsonPropertyOrder(5)] public int? ItemMetadata { get; init; }
    [JsonPropertyName("required")] [JsonPropertyOrder(6)] public int? Required { get; init; }
    [JsonPropertyName("dimension")] [JsonPropertyOrder(7)] public int? Dimension { get; init; }
    [JsonPropertyName("x")] [JsonPropertyOrder(8)] public double? X { get; init; }
    [JsonPropertyName("y")] [JsonPropertyOrder(9)] public double? Y { get; init; }
    [JsonPropertyName("z")] [JsonPropertyOrder(10)] public double? Z { get; init; }
    [JsonPropertyName("radius")] [JsonPropertyOrder(11)] public double? Radius { get; init; }
    [JsonPropertyName("actor_id")] [JsonPropertyOrder(12)] public string? ActorId { get; init; }
    [JsonIgnore] public int? Metadata { get => ItemMetadata; init => ItemMetadata = value; }
    [JsonIgnore] public string? ItemId { get => Item; init => Item = value; }
    [JsonIgnore] public int? RequiredAmount { get => Required; init => Required = value; }
    public QuestObjectiveResource Clone() => new() { Id = Id, Type = Type, Description = Description, Entity = Entity, Item = Item, ItemMetadata = ItemMetadata, Required = Required, Dimension = Dimension, X = X, Y = Y, Z = Z, Radius = Radius, ActorId = ActorId };
    public static QuestObjectiveResource Kill(string id, string description, string entity, int required) => new() { Id = id, Type = "kill_entity", Description = description, Entity = entity, Required = required };
    public static QuestObjectiveResource Collect(string id, string description, string item, int metadata, int required) => new() { Id = id, Type = "collect_item", Description = description, Item = item, ItemMetadata = metadata, Required = required };
    public static QuestObjectiveResource Interact(string id, string description, string actorId, int required = 1) => new() { Id = id, Type = "interact_actor", Description = description, ActorId = actorId, Required = required };
    public static QuestObjectiveResource Reach(string id, string description, int dimension, double x, double y, double z, double radius) => new() { Id = id, Type = "reach_location", Description = description, Dimension = dimension, X = x, Y = y, Z = z, Radius = radius };
}
public sealed class ObjectiveGroupResource
{
    [JsonPropertyName("id")] [JsonPropertyOrder(0)] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("mode")] [JsonPropertyOrder(1)] public string Mode { get; init; } = "ALL";
    [JsonPropertyName("objectives")] [JsonPropertyOrder(2)] public List<string> Objectives { get; init; } = [];
    [JsonIgnore] public List<string> ObjectiveIds { get => Objectives; init => Objectives = value ?? []; }
    public ObjectiveGroupResource Clone() => new() { Id = Id, Mode = Mode, Objectives = [.. Objectives] };
}
public sealed class QuestMetadata
{
    [JsonPropertyName("notes")] public string Notes { get; init; } = string.Empty;
    [JsonPropertyName("tags")] public List<string> Tags { get; init; } = [];
    public QuestMetadata Clone() => new() { Notes = Notes, Tags = [.. Tags] };
}
public sealed record QuestResourceInfo(string Id, string DisplayName, string Path, int ObjectiveCount);
public class QuestException : Exception { public QuestException(string message, Exception? inner = null) : base(message, inner) { } }
public sealed class QuestDataException : QuestException { public QuestDataException(string message, Exception? inner = null) : base(message, inner) { } }
public class QuestRepositoryException : QuestException { public QuestRepositoryException(string message, Exception? inner = null) : base(message, inner) { } }
public sealed class QuestNotFoundException : QuestRepositoryException { public QuestNotFoundException(string id) : base($"Quest '{id}' was not found.") { } }
public sealed class QuestCollisionException : QuestRepositoryException { public QuestCollisionException(string id) : base($"Quest '{id}' already exists.") { } }
public sealed class QuestValidationException : QuestException
{ public QuestValidationException(IEnumerable<DarkGreyRPG.Studio.Core.Validation.ValidationIssue> issues) : base(string.Join(" ", issues.Select(i => i.Message))) => Issues = issues.ToArray(); public IReadOnlyList<DarkGreyRPG.Studio.Core.Validation.ValidationIssue> Issues { get; } }
