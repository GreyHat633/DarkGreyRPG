using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Quests;

namespace DarkGreyRPG.Studio.Core.Projects;

public enum ProjectResourceType { Actor, Dialogue, Quest, Story }

public sealed record ResourceDescriptor(ProjectResourceType Type, string Id, string DisplayName, string Path);

/// <summary>Project-level resource index. Story membership is authoritative for references.</summary>
public sealed class ProjectResourceRegistry
{
    private readonly string _root;
    private readonly ActorRepository _actors;
    private readonly StoryRepository _stories;
    private readonly DialogueRepository _dialogues;
    private readonly QuestRepository _quests;

    public ProjectResourceRegistry(string projectDirectory, DarkGreyRPG.Studio.Core.IO.IAtomicFileWriter? writer = null)
    {
        _root = Path.GetFullPath(projectDirectory);
        _actors = new ActorRepository(_root, writer);
        _stories = new StoryRepository(_root, writer);
        _dialogues = new DialogueRepository(_root, writer);
        _quests = new QuestRepository(_root, writer);
    }

    public ActorRepository Actors => _actors;
    public StoryRepository Stories => _stories;
    public DialogueRepository Dialogues => _dialogues;
    public QuestRepository Quests => _quests;

    public object? GetById(ProjectResourceType type, string id) => type switch
    {
        ProjectResourceType.Actor => TryActor(id),
        ProjectResourceType.Story => TryStory(id),
        ProjectResourceType.Dialogue => TryDialogue(id),
        ProjectResourceType.Quest => TryQuest(id),
        _ => null,
    };
    public object? GetById(string type, string id) => GetById(ParseType(type), id);
    public object? GetById(string id) => Enum.GetValues<ProjectResourceType>().Select(type => GetById(type, id)).FirstOrDefault(value => value is not null);
    public bool Exists(ProjectResourceType type, string id) => GetById(type, id) is not null;
    public bool Exists(string type, string id) => Exists(ParseType(type), id);

    public IReadOnlyList<ResourceDescriptor> GetReferences(ProjectResourceType type, string id)
    {
        if (type == ProjectResourceType.Story)
        {
            // Inter-Story edges are derived later by StoryGraphAnalyzer, not membership lists.
            return [];
        }

        var references = new List<ResourceDescriptor>();
        var homeStoryId = GetHomeStory(type, id)?.Id;
        foreach (var story in _stories.ListStories())
        {
            var isExternalOwnership = Members(story.OwnedResources, type).Contains(id, StringComparer.Ordinal)
                && !string.Equals(story.Id, homeStoryId, StringComparison.Ordinal);
            var isReference = Members(story.ReferencedResources, type).Contains(id, StringComparer.Ordinal);
            if (isExternalOwnership || isReference)
            {
                references.Add(new(ProjectResourceType.Story, story.Id, story.DisplayName,
                    Path.Combine(_root, "stories", story.Id + ".json")));
            }
        }
        return references;
    }
    public IReadOnlyList<ResourceDescriptor> GetReferences(string type, string id) => GetReferences(ParseType(type), id);
    public IReadOnlyList<ResourceDescriptor> GetReferences(string id) => Enum.GetValues<ProjectResourceType>()
        .SelectMany(type => GetReferences(type, id)).Distinct().ToArray();

    public StoryResource? GetHomeStory(ProjectResourceType type, string id)
    {
        if (type == ProjectResourceType.Story) return TryStory(id);
        if (type == ProjectResourceType.Actor && TryActor(id) is ActorResource actor)
            return TryStory(actor.HomeStoryId ?? string.Empty);
        if (type == ProjectResourceType.Dialogue && TryDialogue(id) is DialogueResource dialogue)
            return TryStory(dialogue.HomeStoryId);
        if (type == ProjectResourceType.Quest && TryQuest(id) is QuestResource quest)
            return TryStory(quest.HomeStoryId);
        return _stories.ListStories().FirstOrDefault(s => Members(s.OwnedResources, type).Contains(id, StringComparer.Ordinal));
    }
    public StoryResource? GetHomeStory(string type, string id) => GetHomeStory(ParseType(type), id);
    public StoryResource? GetHomeStory(ActorResource actor) => GetHomeStory(ProjectResourceType.Actor, actor.Id);

    public object Create(ProjectResourceType type, string id, string displayName, string? homeStoryId = null) => type switch
    {
        ProjectResourceType.Actor => CreateActor(id, displayName, homeStoryId),
        ProjectResourceType.Story => _stories.CreateStory(id, displayName),
        ProjectResourceType.Dialogue => CreateDialogue(id, displayName, homeStoryId),
        ProjectResourceType.Quest => CreateQuest(id, displayName, homeStoryId),
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
    public object Create(string type, string id, string displayName, string? homeStoryId = null) => Create(ParseType(type), id, displayName, homeStoryId);

    public object ImportAsNew(ProjectResourceType type, string sourceId, string newId, string? homeStoryId = null) => type switch
    {
        ProjectResourceType.Actor => ImportActorAsNew(sourceId, newId, homeStoryId),
        ProjectResourceType.Dialogue => ImportDialogueAsNew(sourceId, newId, homeStoryId),
        ProjectResourceType.Quest => ImportQuestAsNew(sourceId, newId, homeStoryId),
        _ => throw new ProjectException($"Import is not supported for {type}.")
    };
    public object ImportAsNew(string type, string sourceId, string newId, string? homeStoryId = null) => ImportAsNew(ParseType(type), sourceId, newId, homeStoryId);

    public ActorResource ImportAsNew(string sourceId, string newId, string? homeStoryId = null) => (ActorResource)ImportAsNew(ProjectResourceType.Actor, sourceId, newId, homeStoryId);
    private ActorResource ImportActorAsNew(string sourceId, string newId, string? homeStoryId)
    {
        var source = _actors.LoadActor(sourceId);
        var selectedStoryId = homeStoryId ?? source.HomeStoryId ?? "uncategorized";
        var selectedStory = TryStory(selectedStoryId)
            ?? throw new ProjectException($"Story '{selectedStoryId}' does not exist.");
        if (selectedStory.ReferencedResources.Actors.Contains(newId, StringComparer.Ordinal))
            throw new ProjectException($"Actor '{newId}' is already referenced by Story '{selectedStory.Id}'.");
        var document = ActorDocument.CreateNew(newId, source.DisplayName);
        document.Notes = source.Notes;
        document.SetTags(source.Tags);
        document.HomeStoryId = selectedStory.Id;
        _actors.SaveActor(document);
        // AddReference validates the Actor file and selected Story before updating membership.
        AddReference(document.HomeStoryId, ProjectResourceType.Actor, newId, true);
        return document.ToResource();
    }

    private DialogueResource ImportDialogueAsNew(string sourceId, string newId, string? homeStoryId)
    {
        var source = _dialogues.LoadDialogue(sourceId); var story = RequireStory(homeStoryId ?? source.HomeStoryId);
        var doc = _dialogues.CreateDialogue(newId, source.DisplayName); var sourceResource = source.ToResource();
        doc.Title = sourceResource.Title; doc.DisplayName = sourceResource.DisplayName; doc.HomeStoryId = story.Id; doc.Entry = sourceResource.Entry;
        doc.Speakers.Clear(); foreach (var s in sourceResource.Speakers) doc.Speakers.Add(s); doc.Nodes.Clear(); foreach (var n in sourceResource.Nodes) doc.Nodes.Add(n.Clone()); doc.Metadata = sourceResource.Metadata.Clone();
        _dialogues.SaveDialogue(doc); AddReference(story.Id, ProjectResourceType.Dialogue, newId, true); return doc.ToResource();
    }
    private QuestResource ImportQuestAsNew(string sourceId, string newId, string? homeStoryId)
    {
        var source = _quests.LoadQuest(sourceId); var story = RequireStory(homeStoryId ?? source.HomeStoryId); var doc = _quests.CreateQuest(newId, source.DisplayName); var r = source.ToResource();
        doc.Title = r.Title; doc.DisplayName = r.DisplayName; doc.Description = r.Description; doc.HomeStoryId = story.Id; doc.ReplaceObjectives(r.Objectives); doc.ReplaceGroups(r.ObjectiveGroups); doc.Metadata = r.Metadata.Clone(); _quests.SaveQuest(doc); AddReference(story.Id, ProjectResourceType.Quest, newId, true); return doc.ToResource();
    }

    public void AddReference(string storyId, ProjectResourceType type, string resourceId, bool owned = false)
    {
        EnsureMembershipType(type);
        var story = RequireStory(storyId);
        EnsureResourceExists(type, resourceId);
        if (owned && type == ProjectResourceType.Actor)
        {
            var actor = _actors.LoadActor(resourceId);
            if (!string.Equals(actor.HomeStoryId, story.Id, StringComparison.Ordinal))
                throw new ProjectException($"Actor '{resourceId}' is owned by Story '{actor.HomeStoryId}'.");
        }
        if (owned && type is ProjectResourceType.Dialogue or ProjectResourceType.Quest)
        {
            var homeStory = GetHomeStory(type, resourceId);
            if (homeStory is not null && !string.Equals(homeStory.Id, story.Id, StringComparison.Ordinal)) throw new ProjectException($"Resource '{resourceId}' is owned by Story '{homeStory.Id}'.");
        }
        var ownedMembership = story.OwnedResources.Clone();
        var referencedMembership = story.ReferencedResources.Clone();
        var ownedValues = Members(ownedMembership, type);
        var referencedValues = Members(referencedMembership, type);
        if (owned)
        {
            if (referencedValues.Contains(resourceId, StringComparer.Ordinal))
                throw new ProjectException($"Resource '{resourceId}' is already referenced by Story '{story.Id}'.");
            if (ownedValues.Contains(resourceId, StringComparer.Ordinal)) return;
            ownedValues.Add(resourceId);
        }
        else
        {
            if (ownedValues.Contains(resourceId, StringComparer.Ordinal))
                throw new ProjectException($"Resource '{resourceId}' is owned by Story '{story.Id}' and cannot also be referenced there.");
            if (referencedValues.Contains(resourceId, StringComparer.Ordinal)) return;
            referencedValues.Add(resourceId);
        }
        _stories.SaveStory(CopyStory(story, Normalize(ownedMembership), Normalize(referencedMembership)));
    }
    public void AddReference(string storyId, string type, string resourceId, bool owned = false) => AddReference(storyId, ParseType(type), resourceId, owned);

    public void RemoveReference(string storyId, ProjectResourceType type, string resourceId)
    {
        EnsureMembershipType(type);
        var story = RequireStory(storyId);
        var referenced = story.ReferencedResources.Clone();
        var changed = Members(referenced, type).RemoveAll(x => x == resourceId) > 0;
        if (changed) _stories.SaveStory(CopyStory(story, story.OwnedResources.Clone(), Normalize(referenced)));
    }
    public void RemoveReference(string storyId, string type, string resourceId) => RemoveReference(storyId, ParseType(type), resourceId);

    public void RemoveOwnership(string storyId, ProjectResourceType type, string resourceId)
    {
        EnsureMembershipType(type);
        var story = RequireStory(storyId);
        var owned = story.OwnedResources.Clone();
        var changed = Members(owned, type).RemoveAll(x => x == resourceId) > 0;
        if (changed) _stories.SaveStory(CopyStory(story, Normalize(owned), story.ReferencedResources.Clone()));
    }
    public void RemoveOwnership(string storyId, string type, string resourceId) => RemoveOwnership(storyId, ParseType(type), resourceId);
    public bool CanDelete(ProjectResourceType type, string id) => GetReferences(type, id).Count == 0;
    public bool CanDelete(string type, string id) => CanDelete(ParseType(type), id);

    public void ReplaceResourceId(ProjectResourceType type, string sourceId, string targetId)
    {
        EnsureMembershipType(type);
        foreach (var story in _stories.ListStories())
        {
            var owned = story.OwnedResources.Clone();
            var referenced = story.ReferencedResources.Clone();
            var changed = Replace(Members(owned, type), sourceId, targetId)
                | Replace(Members(referenced, type), sourceId, targetId);
            if (changed)
            {
                _stories.SaveStory(CopyStory(story, Normalize(owned), Normalize(referenced)));
            }
        }
    }

    private ActorResource CreateActor(string id, string displayName, string? homeStoryId)
    {
        var selectedStoryId = homeStoryId ?? "uncategorized";
        var selectedStory = TryStory(selectedStoryId)
            ?? throw new ProjectException($"Story '{selectedStoryId}' does not exist.");
        if (selectedStory.ReferencedResources.Actors.Contains(id, StringComparer.Ordinal))
            throw new ProjectException($"Actor '{id}' is already referenced by Story '{selectedStory.Id}'.");
        var document = _actors.CreateActor(id, displayName);
        document.HomeStoryId = selectedStory.Id;
        _actors.SaveActor(document);
        EnsureStory(document.HomeStoryId);
        AddReference(document.HomeStoryId, ProjectResourceType.Actor, id, true);
        return document.ToResource();
    }
    private DialogueResource CreateDialogue(string id, string displayName, string? homeStoryId)
    { var story = RequireStory(homeStoryId ?? "uncategorized"); var doc = _dialogues.CreateDialogue(id, displayName); doc.HomeStoryId = story.Id; _dialogues.SaveDialogue(doc); AddReference(story.Id, ProjectResourceType.Dialogue, id, true); return doc.ToResource(); }
    private QuestResource CreateQuest(string id, string displayName, string? homeStoryId)
    { var story = RequireStory(homeStoryId ?? "uncategorized"); var doc = _quests.CreateQuest(id, displayName); doc.HomeStoryId = story.Id; _quests.SaveQuest(doc); AddReference(story.Id, ProjectResourceType.Quest, id, true); return doc.ToResource(); }
    private ActorResource? TryActor(string id) { try { return _actors.LoadActor(id).ToResource(); } catch (ActorNotFoundException) { return null; } }
    private StoryResource? TryStory(string id) { try { return _stories.LoadStory(id); } catch (StoryNotFoundException) { return null; } }
    private DialogueResource? TryDialogue(string id) { try { return _dialogues.LoadDialogue(id).ToResource(); } catch (DialogueNotFoundException) { return null; } }
    private QuestResource? TryQuest(string id) { try { return _quests.LoadQuest(id).ToResource(); } catch (QuestNotFoundException) { return null; } }

    private StoryResource RequireStory(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return TryStory(id) ?? throw new ProjectException($"Story '{id}' does not exist.");
    }

    private void EnsureResourceExists(ProjectResourceType type, string id)
    {
        if (!Exists(type, id)) throw new ProjectException($"{type} '{id}' does not exist.");
    }

    private ResourceDescriptor? GetDescriptor(ProjectResourceType type, string id)
    {
        var path = Path.Combine(_root, DirectoryName(type), id + ".json");
        if (!File.Exists(path)) return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var name = root.TryGetProperty("display_name", out var display) ? display.GetString() : null;
            name ??= root.TryGetProperty("title", out var title) ? title.GetString() : null;
            return new(type, id, name ?? id, path);
        }
        catch (JsonException) { return null; }
    }
    private ResourceDescriptor CreateDescriptor(ProjectResourceType type, string id, string displayName)
    {
        var path = Path.Combine(_root, DirectoryName(type), id + ".json");
        if (File.Exists(path)) throw new ProjectException($"{type} '{id}' already exists.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"{{\n  \"schema_version\": 1,\n  \"id\": {JsonSerializer.Serialize(id)},\n  \"title\": {JsonSerializer.Serialize(displayName)}\n}}\n");
        return new(type, id, displayName, path);
    }
    private void EnsureStory(string id)
    {
        if (TryStory(id) is null && string.Equals(id, "uncategorized", StringComparison.Ordinal))
            _stories.SaveStory(StoryResource.CreateUncategorized());
    }
    private static string DirectoryName(ProjectResourceType type) => type switch
    {
        ProjectResourceType.Actor => "actors", ProjectResourceType.Dialogue => "dialogues", ProjectResourceType.Quest => "quests", ProjectResourceType.Story => "stories", _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };
    private static ProjectResourceType ParseType(string type) => Enum.TryParse<ProjectResourceType>(type, true, out var value) ? value : throw new ArgumentException($"Unknown resource type '{type}'.", nameof(type));
    private static void EnsureMembershipType(ProjectResourceType type)
    {
        if (type == ProjectResourceType.Story)
            throw new ArgumentException("Story-to-Story references are derived from Flow nodes, not membership lists.", nameof(type));
    }
    private static List<string> Members(StoryMembership membership, ProjectResourceType type) => type switch
    {
        ProjectResourceType.Actor => membership.Actors,
        ProjectResourceType.Dialogue => membership.Dialogues,
        ProjectResourceType.Quest => membership.Quests,
        _ => throw new ArgumentException($"Story membership does not contain resources of type '{type}'.", nameof(type)),
    };
    private static bool Replace(List<string> values, string sourceId, string targetId)
    {
        var changed = values.RemoveAll(value => string.Equals(value, sourceId, StringComparison.Ordinal)) > 0;
        if (changed && !values.Contains(targetId, StringComparer.Ordinal)) values.Add(targetId);
        return changed;
    }
    private static StoryMembership Normalize(StoryMembership value) => new()
    {
        Actors = value.Actors.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
        Dialogues = value.Dialogues.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
        Quests = value.Quests.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
    };
    private static StoryResource CopyStory(StoryResource s, StoryMembership owned, StoryMembership referenced) => new()
    {
        SchemaVersion = StoryResource.CurrentSchemaVersion, Id = s.Id, DisplayName = s.DisplayName, Description = s.Description,
        Tags = [.. s.Tags], EntryPresentation = s.EntryPresentation, OwnedResources = owned, ReferencedResources = referenced,
        FlowRef = s.FlowRef, Title = s.Title, Entry = s.Entry, Nodes = [.. s.Nodes], Connections = [.. s.Connections],
        Metadata = new StoryMetadata { Notes = s.Metadata.Notes, Tags = [.. s.Metadata.Tags] },
    };
}
