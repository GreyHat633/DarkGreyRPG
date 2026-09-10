using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Core.Projects;

internal static class ProjectMigration
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public static void MigrateIfRequired(string root, IAtomicFileWriter writer)
    {
        var projectPath = Path.Combine(root, "project.json");
        var project = ReadProject(projectPath);
        var actorPaths = EnumerateResourcePaths(root, "actors");
        var dialoguePaths = EnumerateResourcePaths(root, "dialogues");
        var questPaths = EnumerateResourcePaths(root, "quests");
        var storyPaths = EnumerateResourcePaths(root, "stories");

        (string Path, ActorResource Resource)[] actors;
        (string Path, DialogueResource Resource)[] dialogues;
        (string Path, QuestResource Resource)[] quests;
        (string Path, StoryResource Resource)[] stories;
        try
        {
            actors = actorPaths.Select(path => (path, ActorSerializer.Read(path))).ToArray();
            dialogues = dialoguePaths.Select(path => (path, DialogueSerializer.Read(path))).ToArray();
            quests = questPaths.Select(path => (path, QuestSerializer.Read(path))).ToArray();
            stories = storyPaths.Select(path => (path, StorySerializer.Read(path))).ToArray();
        }
        catch (Exception exception)
        {
            TryAppendLog(root, $"{DateTime.UtcNow:O} migration 2.0-to-2.1 could not read resources; no files changed: {exception.Message}");
            throw new ProjectException("Project migration could not read a project resource; no files were changed.", exception);
        }

        var storyById = stories.ToDictionary(story => story.Resource.Id, story => story.Resource, StringComparer.Ordinal);
        var needsUncategorized = project.SchemaVersion == ProjectResource.LegacySchemaVersion
            || actors.Any(actor => string.IsNullOrWhiteSpace(actor.Resource.HomeStoryId)
                || string.Equals(actor.Resource.HomeStoryId, "uncategorized", StringComparison.Ordinal))
            || dialogues.Any(dialogue => string.IsNullOrWhiteSpace(dialogue.Resource.HomeStoryId)
                || string.Equals(dialogue.Resource.HomeStoryId, "uncategorized", StringComparison.Ordinal))
            || quests.Any(quest => string.IsNullOrWhiteSpace(quest.Resource.HomeStoryId)
                || string.Equals(quest.Resource.HomeStoryId, "uncategorized", StringComparison.Ordinal));
        if (needsUncategorized && !storyById.ContainsKey("uncategorized"))
        {
            var path = Path.Combine(root, "stories", "uncategorized.json");
            var resource = StoryResource.CreateUncategorized();
            stories = stories.Append((path, resource)).ToArray();
            storyById.Add(resource.Id, resource);
        }

        var repairedStories = stories
            .Select(story => (story.Path, Resource: RepairStoryMembership(story.Resource, actors, dialogues, quests, storyById)))
            .ToArray();
        var membershipNeedsRepair = repairedStories.Select((story, index) =>
            !StoryEquivalent(story.Resource, stories[index].Resource)).Any(changed => changed);
        var needs = project.SchemaVersion == ProjectResource.LegacySchemaVersion
            || actors.Any(actor => actor.Resource.SchemaVersion == ActorResource.LegacySchemaVersion)
            || dialogues.Any(dialogue => dialogue.Resource.SchemaVersion == DialogueResource.LegacySchemaVersion)
            || quests.Any(quest => quest.Resource.SchemaVersion == QuestResource.LegacySchemaVersion)
            || stories.Any(story => story.Resource.SchemaVersion == StoryResource.LegacySchemaVersion)
            || membershipNeedsRepair;
        if (!needs) return;

        var snapshot = CaptureSnapshot(new[] { projectPath }
            .Concat(actorPaths).Concat(dialoguePaths).Concat(questPaths).Concat(storyPaths)
            .Concat(repairedStories.Select(story => story.Path))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        var backupPath = Path.Combine(root, ".migration-backups", DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'"));
        try
        {
            WriteBackup(backupPath, root, snapshot);
            Directory.CreateDirectory(Path.Combine(root, "stories"));

            foreach (var actor in actors.Where(actor => actor.Resource.SchemaVersion == ActorResource.LegacySchemaVersion))
            {
                var upgraded = new ActorResource
                {
                    SchemaVersion = ActorResource.CurrentSchemaVersion,
                    Id = actor.Resource.Id,
                    DisplayName = actor.Resource.DisplayName,
                    Notes = actor.Resource.Notes,
                    Tags = [.. actor.Resource.Tags],
                    HomeStoryId = actor.Resource.HomeStoryId ?? "uncategorized",
                };
                writer.Write(actor.Path, ActorSerializer.Serialize(upgraded, ActorIdPolicy.ExistingResource),
                    temporaryPath => ActorSerializer.Deserialize(File.ReadAllText(temporaryPath)));
            }

            foreach (var dialogue in dialogues.Where(dialogue => dialogue.Resource.SchemaVersion == DialogueResource.LegacySchemaVersion))
            {
                writer.Write(dialogue.Path, DialogueSerializer.Serialize(dialogue.Resource),
                    temporaryPath => DialogueSerializer.Deserialize(File.ReadAllText(temporaryPath)));
            }

            foreach (var quest in quests.Where(quest => quest.Resource.SchemaVersion == QuestResource.LegacySchemaVersion))
            {
                writer.Write(quest.Path, QuestSerializer.Serialize(quest.Resource),
                    temporaryPath => QuestSerializer.Deserialize(File.ReadAllText(temporaryPath)));
            }

            foreach (var story in repairedStories.Where((story, index) =>
                !File.Exists(story.Path)
                ||
                stories[index].Resource.SchemaVersion == StoryResource.LegacySchemaVersion
                || !StoryEquivalent(story.Resource, stories[index].Resource)))
            {
                writer.Write(story.Path, StorySerializer.Serialize(story.Resource),
                    temporaryPath => StorySerializer.Deserialize(File.ReadAllText(temporaryPath)));
            }

            var upgradedProject = new ProjectResource { SchemaVersion = ProjectResource.CurrentSchemaVersion, Id = project.Id, DisplayName = project.DisplayName, ProjectOriginCode = project.ProjectOriginCode };
            writer.Write(projectPath, SerializeProject(upgradedProject), temporaryPath => ReadProject(temporaryPath));
            TryAppendLog(root, $"{DateTime.UtcNow:O} migration 2.0-to-2.1 succeeded; actors={actors.Length}; dialogues={dialogues.Length}; quests={quests.Length}; backup={backupPath}");
        }
        catch (Exception exception)
        {
            Exception failure = exception;
            try
            {
                Restore(snapshot);
            }
            catch (Exception rollbackException)
            {
                failure = new AggregateException("Migration and rollback both failed.", exception, rollbackException);
            }
            TryAppendLog(root, $"{DateTime.UtcNow:O} migration 2.0-to-2.1 failed and rollback was attempted: {failure.Message}");
            throw new ProjectException("Project migration failed; rollback was attempted for all original project files.", failure);
        }
    }

    private static string[] EnumerateResourcePaths(string root, string directory)
    {
        var path = Path.Combine(root, directory);
        return Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
    }

    private static StoryResource RepairStoryMembership(
        StoryResource story,
        IEnumerable<(string Path, ActorResource Resource)> actors,
        IEnumerable<(string Path, DialogueResource Resource)> dialogues,
        IEnumerable<(string Path, QuestResource Resource)> quests,
        IReadOnlyDictionary<string, StoryResource> stories)
    {
        var owned = story.OwnedResources.Clone();
        var referenced = story.ReferencedResources.Clone();
        RepairMembership(owned.Actors, referenced.Actors,
            actors.Select(actor => (actor.Resource.Id, actor.Resource.HomeStoryId ?? "uncategorized")), story.Id, stories);
        RepairMembership(owned.Dialogues, referenced.Dialogues,
            dialogues.Select(dialogue => (dialogue.Resource.Id, dialogue.Resource.HomeStoryId)), story.Id, stories);
        RepairMembership(owned.Quests, referenced.Quests,
            quests.Select(quest => (quest.Resource.Id, quest.Resource.HomeStoryId)), story.Id, stories);

        return new StoryResource
        {
            SchemaVersion = StoryResource.CurrentSchemaVersion,
            Id = story.Id,
            DisplayName = string.IsNullOrWhiteSpace(story.DisplayName) ? "未分类" : story.DisplayName,
            Description = story.Description,
            Tags = [.. story.Tags],
            EntryPresentation = story.EntryPresentation,
            OwnedResources = new StoryMembership
            {
                Actors = Normalize(owned.Actors),
                Dialogues = Normalize(owned.Dialogues),
                Quests = Normalize(owned.Quests),
            },
            ReferencedResources = new StoryMembership
            {
                Actors = Normalize(referenced.Actors),
                Dialogues = Normalize(referenced.Dialogues),
                Quests = Normalize(referenced.Quests),
            },
            FlowRef = string.IsNullOrWhiteSpace(story.FlowRef) ? story.Id : story.FlowRef,
            Title = string.IsNullOrWhiteSpace(story.Title) ? story.DisplayName : story.Title,
            Entry = story.Entry,
            Nodes = [.. story.Nodes],
            Connections = [.. story.Connections],
            Metadata = new StoryMetadata { Notes = story.Metadata.Notes, Tags = [.. story.Metadata.Tags] },
        };
    }

    private static void RepairMembership(
        List<string> owned,
        List<string> referenced,
        IEnumerable<(string Id, string HomeStoryId)> resources,
        string storyId,
        IReadOnlyDictionary<string, StoryResource> stories)
    {
        foreach (var resource in resources)
        {
            owned.RemoveAll(id => string.Equals(id, resource.Id, StringComparison.Ordinal));
            if (stories.ContainsKey(resource.HomeStoryId)
                && string.Equals(resource.HomeStoryId, storyId, StringComparison.Ordinal))
            {
                referenced.RemoveAll(id => string.Equals(id, resource.Id, StringComparison.Ordinal));
                if (!owned.Contains(resource.Id, StringComparer.Ordinal)) owned.Add(resource.Id);
            }
        }
    }

    private static List<string> Normalize(IEnumerable<string> values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    private static bool StoryEquivalent(StoryResource left, StoryResource right) =>
        string.Equals(StorySerializer.Serialize(left), StorySerializer.Serialize(right), StringComparison.Ordinal);

    private static ProjectResource ReadProject(string path)
    {
        var project = JsonSerializer.Deserialize<ProjectResource>(File.ReadAllText(path), Options)
            ?? throw new JsonException("Project JSON root cannot be null.");
        if (project.SchemaVersion is not (ProjectResource.LegacySchemaVersion or ProjectResource.CurrentSchemaVersion))
            throw new JsonException($"Unsupported project schema_version {project.SchemaVersion}.");
        return project;
    }

    private static string SerializeProject(ProjectResource project) => JsonSerializer.Serialize(project, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

    private sealed record FileSnapshot(string Path, byte[]? Contents);

    private static IReadOnlyList<FileSnapshot> CaptureSnapshot(IReadOnlyList<string> paths) =>
        paths.Select(path => new FileSnapshot(path, File.Exists(path) ? File.ReadAllBytes(path) : null)).ToArray();

    private static void WriteBackup(string backupPath, string root, IReadOnlyList<FileSnapshot> files)
    {
        foreach (var file in files.Where(f => f.Contents is not null))
        {
            var target = Path.Combine(backupPath, Path.GetRelativePath(root, file.Path));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllBytes(target, file.Contents!);
        }
    }

    private static void Restore(IReadOnlyList<FileSnapshot> files)
    {
        foreach (var file in files)
        {
            if (file.Contents is null)
            {
                if (File.Exists(file.Path)) File.Delete(file.Path);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file.Path)!);
                File.WriteAllBytes(file.Path, file.Contents);
            }
        }
    }

    private static void TryAppendLog(string root, string line)
    {
        try
        {
            File.AppendAllText(Path.Combine(root, "migration.log"), line + Environment.NewLine);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Logging must never hide the migration or rollback result.
        }
    }
}
