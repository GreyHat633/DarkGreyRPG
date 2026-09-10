using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Core.Identity;

public enum DgrResourceKind { Story, Actor, Item, ItemGroup, Session, Task }
public readonly record struct DgrResourceKey(DgrResourceKind Kind, string Id);

/// <summary>Exact typed resource renames. Node identities, ports and prose are never rename keys.</summary>
public sealed class ResourceRenameMap
{
    private readonly Dictionary<DgrResourceKey, string> _renames = new();
    public IReadOnlyDictionary<DgrResourceKey, string> Entries => new ReadOnlyDictionary<DgrResourceKey, string>(_renames);

    public void Add(DgrResourceKind kind, string oldId, string newId)
    {
        if (!DgrResourceId.IsCompatibleId(oldId) || !DgrResourceId.IsFullId(newId))
            throw new ArgumentException("A rename requires a compatible source and a full destination ID.");
        var key = new DgrResourceKey(kind, oldId);
        if (_renames.TryGetValue(key, out var previous) && previous != newId)
            throw new InvalidOperationException($"Resource '{kind}:{oldId}' has conflicting owners or destinations.");
        _renames[key] = newId;
    }

    public string Resolve(DgrResourceKind kind, string id) => _renames.GetValueOrDefault(new(kind, id), id);

    public void ValidateCollisions(IEnumerable<DgrResourceKey> existing)
    {
        var destinations = new HashSet<DgrResourceKey>();
        foreach (var resource in existing)
            if (!destinations.Add(new(resource.Kind, Resolve(resource.Kind, resource.Id))))
                throw new InvalidOperationException($"Namespace migration collides at {resource.Kind} '{Resolve(resource.Kind, resource.Id)}'.");
    }

    public GraphResourceEnvelope Rewrite(GraphResourceEnvelope source)
    {
        var graph = source.Graph ?? throw new InvalidOperationException("Resource graph is missing.");
        foreach (var node in graph.Nodes)
        {
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
            { SchemaVersion = source.SchemaVersion, Tags = source.Tags.ToArray() };
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
