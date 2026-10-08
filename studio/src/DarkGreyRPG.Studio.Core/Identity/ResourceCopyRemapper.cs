using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Core.Identity;

/// <summary>Typed identity substitution for detached copies. Persisted source identities never change.</summary>
public sealed class ResourceCopyRemapper
{
    private readonly Dictionary<DgrResourceKey, string> _renames = new();
    private Action<DgrResourceKey>? _referenceVisitor;
    public IReadOnlyDictionary<DgrResourceKey, string> Entries => new ReadOnlyDictionary<DgrResourceKey, string>(_renames);

    public void Add(DgrResourceKind kind, string oldId, string newId)
    {
        if (!CurrentProjectInventory.IsValid(new(kind, oldId)) || !CurrentProjectInventory.IsValid(new(kind, newId)))
            throw new ArgumentException("Copy mapping requires matching current typed identities.");
        var key = new DgrResourceKey(kind, oldId);
        if (_renames.TryGetValue(key, out var previous) && previous != newId)
            throw new InvalidOperationException($"Resource '{kind}:{oldId}' has conflicting owners or destinations.");
        _renames[key] = newId;
    }

    public string Resolve(DgrResourceKind kind, string id)
    {
        var key = new DgrResourceKey(kind, id);
        if (CurrentProjectInventory.IsValid(key)) _referenceVisitor?.Invoke(key);
        return _renames.GetValueOrDefault(key, id);
    }

    /// <summary>Uses the same typed traversal as copying; prose is never interpreted as an ID.</summary>
    public static IReadOnlyCollection<DgrResourceKey> References(GraphResourceEnvelope source)
    {
        var references = new HashSet<DgrResourceKey>();
        var visitor = new ResourceCopyRemapper { _referenceVisitor = key => references.Add(key) };
        visitor.Rewrite(GraphResourceEnvelope.FromJson(source.ToJson()));
        references.Remove(new(Kind(source.ResourceKind), source.Id));
        return references;
    }

    public void ValidateCollisions(IEnumerable<DgrResourceKey> existing)
    {
        var destinations = new HashSet<DgrResourceKey>();
        foreach (var resource in existing)
            if (!destinations.Add(new(resource.Kind, Resolve(resource.Kind, resource.Id))))
                throw new InvalidOperationException($"Copy collides at {resource.Kind} '{Resolve(resource.Kind, resource.Id)}'.");
    }

    public GraphResourceEnvelope Rewrite(GraphResourceEnvelope source)
    {
        var graph = source.Graph ?? throw new InvalidOperationException("Resource graph is missing.");
        // Validate declared references before any copy/substitution, including direct in-memory callers.
        GraphResourceAddressCodec.Transform(source.SnapshotGraph()!, source.ResourceKind, writing: true);
        if (source.ResourceKind == GraphResourceKind.Session)
            foreach (var node in graph.Nodes.Where(node => node.Type == "choice"))
            {
                var issues = Graphs.Definitions.SessionChoiceSchema.Validate(node);
                if (issues.Count != 0) throw new InvalidOperationException(issues[0].Code + ": " + issues[0].Message);
            }
        foreach (var node in graph.Nodes)
        {
            foreach (var key in node.Properties.Keys.ToArray())
                node.Properties[key] = Graphs.Definitions.DynamicContentText.Rewrite(node.Properties[key], ResolveDynamicItem, id => Resolve(DgrResourceKind.Actor, id));
            if (source.ResourceKind == GraphResourceKind.Session && node.Type == "choice"
                && node.Properties.TryGetValue("options", out var options))
                foreach (var option in options.EnumerateArray())
                {
                    var text = option.GetProperty("display_text").GetString()!;
                    foreach (var port in node.Ports)
                    {
                        if (port.Id == option.GetProperty("flow_port_id").GetString()) port.DisplayName = text;
                        else if (port.IsInput && option.TryGetProperty("condition_port_id", out var condition) && port.Id == condition.GetString()) port.DisplayName = "条件 · " + text;
                    }
                }
            CanonicalTaskRewardReferences.Rewrite(node, source.ResourceKind, Resolve);
            if (source.ResourceKind == GraphResourceKind.Story)
            {
                switch (node.Type)
                {
                    case "session": RewriteProperty(node, "resource_id", DgrResourceKind.Session); break;
                    case "task": RewriteProperty(node, "resource_id", DgrResourceKind.Task); break;
                    case "enter_story": RewriteProperty(node, "target_story_id", DgrResourceKind.Story); break;
                    case "interact_actor": RewriteProperty(node, "actor_id", DgrResourceKind.Actor); break;
                    case "action" when StringProperty(node, "action_type") == "give_item":
                        RewriteProperty(node, "item_id", DgrResourceKind.Item); break;
                    case "start": RewriteStart(node); break;
                }
            }
            else if (source.ResourceKind == GraphResourceKind.Session && node.Type == "line")
                RewriteProperty(node, "speaker_actor_id", DgrResourceKind.Actor);
            else if (source.ResourceKind == GraphResourceKind.Task && node.Type == "objective")
            {
                switch (StringProperty(node, "objective_type"))
                {
                    case "kill_entity": RewriteProperty(node, "entity", DgrResourceKind.Actor); break;
                    case "interact_actor": RewriteProperty(node, "actor_id", DgrResourceKind.Actor); break;
                    case "submit_item": RewriteProperty(node, "actor_id", DgrResourceKind.Actor); goto case "collect_item";
                    case "collect_item":
                        var id = StringProperty(node, "item");
                        if (id is null) break;
                        var item = Resolve(DgrResourceKind.Item, id);
                        var group = Resolve(DgrResourceKind.ItemGroup, id);
                        if (item != id && group != id && item != group)
                            throw new InvalidOperationException($"Collect target '{id}' has ambiguous Item/Group renames.");
                        node.Properties["item"] = JsonSerializer.SerializeToElement(item != id ? item : group);
                        break;
                }
            }
        }
        return new(source.ResourceKind, Resolve(Kind(source.ResourceKind), source.Id), source.DisplayName, graph)
            { SchemaVersion = source.SchemaVersion, Tags = source.Tags.ToArray(), TaskMetadata = source.TaskMetadata is null ? null
                : new CanonicalTaskMetadata(Graphs.Definitions.DynamicContentText.Rewrite(JsonSerializer.SerializeToElement(source.TaskMetadata.Description), ResolveDynamicItem, id => Resolve(DgrResourceKind.Actor, id)).GetString()!) };
    }

    private string ResolveDynamicItem(string id)
    {
        var item = Resolve(DgrResourceKind.Item, id); var group = Resolve(DgrResourceKind.ItemGroup, id);
        if (item != id && group != id && item != group) throw new InvalidOperationException($"Dynamic item '{id}' has ambiguous renames.");
        return item != id ? item : group;
    }

    public CanonicalStoryMembershipManifest Rewrite(CanonicalStoryMembershipManifest source)
    {
        CanonicalStoryMembershipSet RewriteSet(CanonicalStoryMembershipSet set) => new()
        {
            Actors = set.Actors.Select(id => Resolve(DgrResourceKind.Actor, id)).ToList(),
            Items = set.Items.Select(id => Resolve(DgrResourceKind.Item, id)).ToList(),
            ItemGroups = set.ItemGroups.Select(id => Resolve(DgrResourceKind.ItemGroup, id)).ToList(),
            Sessions = set.Sessions.Select(id => Resolve(DgrResourceKind.Session, id)).ToList(),
            Tasks = set.Tasks.Select(id => Resolve(DgrResourceKind.Task, id)).ToList(),
        };
        var order = source.DisplayOrder;
        return new(Resolve(DgrResourceKind.Story, source.StoryId), RewriteSet(source.OwnedResources), RewriteSet(source.ReferencedResources))
        {
            SchemaVersion = source.SchemaVersion,
            DisplayOrder = new()
            {
                Actors = order.Actors.Select(id => Resolve(DgrResourceKind.Actor, id)).ToList(),
                Items = order.Items.Select(handle => handle.StartsWith("item_group:", StringComparison.Ordinal)
                    ? "item_group:" + Resolve(DgrResourceKind.ItemGroup, handle[11..])
                    : handle.StartsWith("item:", StringComparison.Ordinal)
                        ? "item:" + Resolve(DgrResourceKind.Item, handle[5..]) : handle).ToList(),
                Sessions = order.Sessions.Select(id => Resolve(DgrResourceKind.Session, id)).ToList(),
                Tasks = order.Tasks.Select(id => Resolve(DgrResourceKind.Task, id)).ToList(),
            },
        };
    }

    public ActorResource Rewrite(ActorResource source)
    {
        var result = source.WithId(Resolve(DgrResourceKind.Actor, source.Id));
        if (result.HomeStoryId is not null) result.HomeStoryId = Resolve(DgrResourceKind.Story, result.HomeStoryId);
        return result;
    }

    public ItemResource Rewrite(ItemResource source) => source switch
    {
        IndividualItemResource item => new IndividualItemResource
        {
            SchemaVersion = item.SchemaVersion, ItemId = Resolve(DgrResourceKind.Item, item.Id),
            DisplayName = item.DisplayName, Tags = [.. item.Tags],
        },
        CollectiveItemResource group => new CollectiveItemResource
        {
            SchemaVersion = group.SchemaVersion, GroupId = Resolve(DgrResourceKind.ItemGroup, group.Id),
            DisplayName = group.DisplayName, Tags = [.. group.Tags],
        },
        _ => throw new InvalidOperationException("Unsupported Item resource kind."),
    };

    public CanonicalStoryLogicGraph Rewrite(CanonicalStoryLogicGraph source) => new(source.SchemaVersion,
        source.Connections.Select(edge => edge with
        {
            SourceStoryId = Resolve(DgrResourceKind.Story, edge.SourceStoryId),
            TargetStoryId = Resolve(DgrResourceKind.Story, edge.TargetStoryId),
        }).ToArray());

    public static DgrResourceKind Kind(GraphResourceKind kind) => kind switch
    {
        GraphResourceKind.Story => DgrResourceKind.Story,
        GraphResourceKind.Session => DgrResourceKind.Session,
        GraphResourceKind.Task => DgrResourceKind.Task,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private void RewriteStart(GraphNode node)
    {
        if (!node.Properties.TryGetValue("triggers", out var value)) return;
        var triggers = JsonNode.Parse(value.GetRawText())?.AsArray() ?? throw new InvalidOperationException("Start triggers missing.");
        foreach (var trigger in triggers)
            if (trigger?["trigger_type"]?.GetValue<string>() == "interact_actor"
                && trigger["trigger_properties"] is JsonObject properties
                && properties["actor_id"] is JsonValue actor)
                properties["actor_id"] = Resolve(DgrResourceKind.Actor, actor.GetValue<string>());
        node.Properties["triggers"] = JsonSerializer.SerializeToElement(triggers);
    }

    private void RewriteProperty(GraphNode node, string name, DgrResourceKind kind)
    {
        var id = StringProperty(node, name);
        if (id is not null) node.Properties[name] = JsonSerializer.SerializeToElement(Resolve(kind, id));
    }

    private static string? StringProperty(GraphNode node, string name)
        => node.Properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
