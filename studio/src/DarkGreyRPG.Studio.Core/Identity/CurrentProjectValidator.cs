using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Identity;

/// <summary>Validates the detached final authoring project before a content transaction can write.</summary>
public static class CurrentProjectValidator
{
    public static void Validate(CurrentProjectSnapshot project)
    {
        var resources = CurrentProjectInventory.Resources(project).ToArray();
        if (resources.Distinct().Count() != resources.Length || resources.Any(key => !CurrentProjectInventory.IsValid(key)))
            throw new InvalidDataException("Final project has duplicate or invalid full resource IDs.");
        var inventory = resources.ToHashSet();
        var graphs = project.Graphs.ToDictionary(graph => new DgrResourceKey(CurrentProjectInventory.Kind(graph.ResourceKind), graph.Id));
        foreach (var actor in project.Actors) _ = ActorSerializer.Serialize(actor, ActorIdPolicy.ExistingResource);
        foreach (var item in project.Items) _ = ItemSerializer.Serialize(item);
        foreach (var graph in project.Graphs)
        {
            _ = GraphResourceEnvelopeSerializer.Serialize(graph);
            var errors = GraphScopePolicy.Validate(graph.Graph!, GraphResourceScopeAdapter.GetScope(graph.ResourceKind), compatibilityMode: true)
                .Concat(GraphNodeShapeValidator.Validate(graph.Graph!, GraphResourceScopeAdapter.GetScope(graph.ResourceKind), compatibilityMode: true))
                .Where(issue => issue.Severity == ValidationSeverity.Error)
                .Where(issue => graph.ResourceKind != GraphResourceKind.Task
                    || graph.Graph!.Nodes.FirstOrDefault(node => node.Id == issue.NodeId) is not { } node
                    || !CanonicalTaskObjectiveSchema.IsDormantUnselectedTarget(graph.Graph!, node)
                    || CanonicalTaskObjectiveSchema.AllowDraftIssues(node, [issue]).Count != 0).ToArray();
            if (errors.Length != 0) throw new InvalidDataException($"Graph '{graph.Id}' is invalid: {string.Join("; ", errors.Select(issue => issue.Message))}");
        }
        var memberships = project.Memberships.ToDictionary(member => member.StoryId, StringComparer.Ordinal);
        var storyIds = project.Graphs.Where(graph => graph.ResourceKind == GraphResourceKind.Story).Select(graph => graph.Id).ToHashSet(StringComparer.Ordinal);
        if (!storyIds.SetEquals(memberships.Keys)) throw new InvalidDataException("Story/Membership identities do not match.");
        foreach (var membership in project.Memberships)
        {
            _ = CanonicalStoryMembershipSerializer.Serialize(membership);
            foreach (var key in CurrentProjectInventory.Members(membership.OwnedResources))
                if (!inventory.Contains(key)) throw new InvalidDataException($"Missing owned {key.Kind} '{key.Id}'.");
            var declared = CurrentProjectInventory.Members(membership.OwnedResources)
                .Concat(CurrentProjectInventory.Members(membership.ReferencedResources)).ToHashSet();
            foreach (var key in declared)
                if (!CurrentProjectInventory.IsValid(key)) throw new InvalidDataException($"Invalid referenced full ID '{key.Id}'.");
            var graphKeys = declared.Where(key => key.Kind is DgrResourceKind.Session or DgrResourceKind.Task)
                .Append(new(DgrResourceKind.Story, membership.StoryId));
            foreach (var graphKey in graphKeys)
            {
                // Explicit external references may be unavailable in this authoring project.
                if (!graphs.TryGetValue(graphKey, out var graph)) continue;
                var dynamicFields = graph.Graph!.Nodes.SelectMany(node => node.Properties.Values).ToList();
                if (graph.TaskMetadata is { } taskMetadata) dynamicFields.Add(JsonSerializer.SerializeToElement(taskMetadata.Description));
                foreach (var id in dynamicFields.SelectMany(DynamicContentText.ActorReferences))
                    if (!declared.Contains(new(DgrResourceKind.Actor, id))) throw new InvalidDataException($"动态内容引用未声明角色 '{id}'。");
                if (graph.TaskMetadata is { } metadata)
                    foreach (var id in DynamicContentText.ItemReferences(metadata.Description))
                        if (!declared.Contains(new(DgrResourceKind.Item, id)) && !declared.Contains(new(DgrResourceKind.ItemGroup, id)))
                            throw new InvalidDataException($"任务说明动态内容引用未声明物品 '{id}'。");
                foreach (var node in graph.Graph!.Nodes)
                {
                    CanonicalTaskRewardReferences.Validate(node, graph.ResourceKind, declared);
                    foreach (var id in node.Properties.Values.SelectMany(Graphs.Definitions.DynamicContentText.ItemReferences))
                        if (!declared.Contains(new(DgrResourceKind.Item, id)) && !declared.Contains(new(DgrResourceKind.ItemGroup, id)))
                            throw new InvalidDataException($"动态内容引用未声明物品 '{id}'。");
                    void Require(string field, DgrResourceKind kind, bool allowItemGroup = false)
                    {
                        if (!node.Properties.TryGetValue(field, out var value) || value.ValueKind != JsonValueKind.String) return;
                        var id = value.GetString()!;
                        if (id.Length == 0) return; // An unfinished authoring selection remains editable.
                        if (declared.Contains(new(kind, id))) return;
                        if (allowItemGroup && declared.Contains(new(DgrResourceKind.ItemGroup, id))) return;
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
                            case "kill_entity": Require("entity", DgrResourceKind.Actor); break;
                            case "submit_item": Require("actor_id", DgrResourceKind.Actor); goto case "collect_item";
                            case "collect_item": Require("item", DgrResourceKind.Item, allowItemGroup: true); break;
                            case "interact_actor": Require("actor_id", DgrResourceKind.Actor); break;
                        }
                    }
                }
            }
        }
        CanonicalStoryLogicGraphRepository.ValidateDetached(project.Logic,
            project.Graphs.Where(graph => graph.ResourceKind == GraphResourceKind.Story)
                .ToDictionary(graph => graph.Id, StringComparer.Ordinal));

    }

    private static string? Text(GraphNode node, string field)
        => node.Properties.TryGetValue(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
