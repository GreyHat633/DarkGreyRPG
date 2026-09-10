using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Identity;

/// <summary>Validates the detached final authoring project before a Namespace transaction can write.</summary>
public static class NamespaceProjectValidator
{
    public static void Validate(NamespaceProjectSnapshot project)
    {
        var resources = NamespaceMigrationPlanner.Inventory(project).ToArray();
        if (resources.Distinct().Count() != resources.Length || resources.Any(key => !DgrResourceId.IsFullId(key.Id)))
            throw new InvalidDataException("Final project has duplicate or invalid full resource IDs.");
        var inventory = resources.ToHashSet();
        var graphs = project.Graphs.ToDictionary(graph => new DgrResourceKey(ResourceRenameMap.Kind(graph.ResourceKind), graph.Id));
        foreach (var actor in project.Actors) _ = ActorSerializer.Serialize(actor, ActorIdPolicy.ExistingResource);
        foreach (var item in project.Items) _ = ItemSerializer.Serialize(item);
        foreach (var graph in project.Graphs)
        {
            _ = GraphResourceEnvelopeSerializer.Serialize(graph);
            var errors = GraphScopePolicy.Validate(graph.Graph!, GraphResourceScopeAdapter.GetScope(graph.ResourceKind), compatibilityMode: true)
                .Concat(GraphNodeShapeValidator.Validate(graph.Graph!, GraphResourceScopeAdapter.GetScope(graph.ResourceKind), compatibilityMode: true))
                .Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();
            if (errors.Length != 0) throw new InvalidDataException($"Graph '{graph.Id}' is invalid: {string.Join("; ", errors.Select(issue => issue.Message))}");
        }
        var memberships = project.Memberships.ToDictionary(member => member.StoryId, StringComparer.Ordinal);
        var storyIds = project.Graphs.Where(graph => graph.ResourceKind == GraphResourceKind.Story).Select(graph => graph.Id).ToHashSet(StringComparer.Ordinal);
        if (!storyIds.SetEquals(memberships.Keys)) throw new InvalidDataException("Story/Membership identities do not match.");
        foreach (var membership in project.Memberships)
        {
            _ = CanonicalStoryMembershipSerializer.Serialize(membership);
            foreach (var key in NamespaceMigrationPlanner.Members(membership.OwnedResources))
                if (!inventory.Contains(key)) throw new InvalidDataException($"Missing owned {key.Kind} '{key.Id}'.");
            var declared = NamespaceMigrationPlanner.Members(membership.OwnedResources)
                .Concat(NamespaceMigrationPlanner.Members(membership.ReferencedResources)).ToHashSet();
            foreach (var key in declared)
                if (!DgrResourceId.IsFullId(key.Id)) throw new InvalidDataException($"Invalid referenced full ID '{key.Id}'.");
            var graphKeys = declared.Where(key => key.Kind is DgrResourceKind.Session or DgrResourceKind.Task)
                .Append(new(DgrResourceKind.Story, membership.StoryId));
            foreach (var graphKey in graphKeys)
            {
                // Explicit external references may be unavailable in this authoring project.
                if (!graphs.TryGetValue(graphKey, out var graph)) continue;
                foreach (var node in graph.Graph!.Nodes)
                {
                    void Require(string field, DgrResourceKind kind, bool legacyTarget = false, bool allowItemGroup = false)
                    {
                        if (!node.Properties.TryGetValue(field, out var value) || value.ValueKind != JsonValueKind.String) return;
                        var id = value.GetString()!;
                        if (id.Length == 0) return; // An unfinished authoring selection remains editable.
                        if (declared.Contains(new(kind, id))) return;
                        if (allowItemGroup && declared.Contains(new(DgrResourceKind.ItemGroup, id))) return;
                        if (legacyTarget && (!id.Contains(':') || id.StartsWith("minecraft:", StringComparison.Ordinal))) return;
                        throw new InvalidDataException($"Story '{membership.StoryId}', node '{node.Id}' references undeclared {kind} '{id}'.");
                    }
                    if (graph.ResourceKind == GraphResourceKind.Story)
                    {
                        switch (node.Type)
                        {
                            case "session": Require("resource_id", DgrResourceKind.Session); break;
                            case "task": Require("resource_id", DgrResourceKind.Task); break;
                            case "interact_actor": Require("actor_id", DgrResourceKind.Actor); break;
                            case "action" when Text(node, "action_type") == "give_item": Require("item_id", DgrResourceKind.Item); break;
                            case "enter_story":
                                if (!storyIds.Contains(Text(node, "target_story_id") ?? "")) throw new InvalidDataException("Missing target Story.");
                                break;
                            case "start" when node.Properties.TryGetValue("triggers", out var triggers):
                                foreach (var trigger in triggers.EnumerateArray())
                                    if (trigger.GetProperty("trigger_type").GetString() == "interact_actor")
                                    {
                                        var actor = trigger.GetProperty("trigger_properties").GetProperty("actor_id").GetString()!;
                                        if (!declared.Contains(new(DgrResourceKind.Actor, actor))) throw new InvalidDataException($"Start references undeclared Actor '{actor}'.");
                                    }
                                break;
                        }
                    }
                    else if (graph.ResourceKind == GraphResourceKind.Session && node.Type == "line") Require("speaker_actor_id", DgrResourceKind.Actor);
                    else if (graph.ResourceKind == GraphResourceKind.Task && node.Type == "objective")
                    {
                        switch (Text(node, "objective_type"))
                        {
                            case "kill_entity": Require("entity", DgrResourceKind.Actor, legacyTarget: true); break;
                            case "collect_item": Require("item", DgrResourceKind.Item, legacyTarget: true, allowItemGroup: true); break;
                            case "interact_actor": Require("actor_id", DgrResourceKind.Actor); break;
                        }
                    }
                }
            }
        }
        foreach (var edge in project.Logic.Connections)
        {
            var source = graphs.GetValueOrDefault(new(DgrResourceKind.Story, edge.SourceStoryId));
            var target = graphs.GetValueOrDefault(new(DgrResourceKind.Story, edge.TargetStoryId));
            if (source is null || target is null
                || !source.Graph!.Nodes.Any(node => node.Type == "logic_output" && Text(node, "port_id") == edge.SourcePortId)
                || !target.Graph!.Nodes.Any(node => node.Type == "logic_input" && Text(node, "port_id") == edge.TargetPortId
                    || node.Type == "start" && node.Ports.Any(port => port.IsInput && port.Id == edge.TargetPortId)))
                throw new InvalidDataException("Invalid project Story Logic reference.");
        }
        if (project.Policy is null) throw new InvalidDataException("Namespace policy is missing.");
        foreach (var pair in project.Policy.StoryOverrides)
            if (!storyIds.Contains(pair.Key) || DgrResourceId.Namespace(pair.Key) != pair.Value)
                throw new InvalidDataException("Custom Story Namespace policy does not match its resource identity.");
        foreach (var story in storyIds.Where(id => !project.Policy.IsCustom(id)))
            if (DgrResourceId.Namespace(story) != project.Policy.GlobalNamespace)
                throw new InvalidDataException("Global Story Namespace differs from project policy.");
    }

    private static string? Text(GraphNode node, string field)
        => node.Properties.TryGetValue(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
