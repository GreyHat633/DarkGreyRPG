using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.IO;
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
        var actorPaths = Directory.Exists(Path.Combine(root, "actors"))
            ? Directory.EnumerateFiles(Path.Combine(root, "actors"), "*.json").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        (string Path, ActorResource Resource)[] actors;
        try
        {
            actors = actorPaths.Select(path => (Path: path, Resource: ActorSerializer.Read(path))).ToArray();
        }
        catch (Exception exception)
        {
            TryAppendLog(root, $"{DateTime.UtcNow:O} migration 2.0-to-2.1 could not read actors; no files changed: {exception.Message}");
            throw new ProjectException("Project migration could not read an Actor; no files were changed.", exception);
        }
        var storyPath = Path.Combine(root, "stories", "uncategorized.json");
        StoryResource? existingStory = null;
        if (File.Exists(storyPath))
        {
            try
            {
                existingStory = StorySerializer.Read(storyPath);
            }
            catch (Exception exception)
            {
                TryAppendLog(root, $"{DateTime.UtcNow:O} migration 2.0-to-2.1 could not read uncategorized Story; no files changed: {exception.Message}");
                throw new ProjectException("Project migration could not read the uncategorized Story; no files were changed.", exception);
            }
        }

        var uncategorizedActorIds = actors
            .Where(actor => actor.Resource.SchemaVersion == ActorResource.LegacySchemaVersion
                || string.Equals(actor.Resource.HomeStoryId, "uncategorized", StringComparison.Ordinal))
            .Select(actor => actor.Resource.Id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var membershipNeedsRepair = existingStory is null
            || uncategorizedActorIds.Any(id => !existingStory.OwnedResources.Actors.Contains(id, StringComparer.Ordinal));
        var needs = project.SchemaVersion == ProjectResource.LegacySchemaVersion
            || actors.Any(actor => actor.Resource.SchemaVersion == ActorResource.LegacySchemaVersion)
            || membershipNeedsRepair;
        if (!needs) return;

        var snapshot = CaptureSnapshot(root, projectPath, actorPaths, storyPath);
        var backupPath = Path.Combine(root, ".migration-backups", DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'"));
        try
        {
            WriteBackup(backupPath, root, snapshot);
            Directory.CreateDirectory(Path.Combine(root, "stories"));
            var story = MergeUncategorizedMembership(
                existingStory ?? StoryResource.CreateUncategorized(),
                uncategorizedActorIds);

            foreach (var actor in actors)
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

            writer.Write(storyPath, StorySerializer.Serialize(story), temporaryPath => StorySerializer.Deserialize(File.ReadAllText(temporaryPath)));
            var upgradedProject = new ProjectResource { SchemaVersion = ProjectResource.CurrentSchemaVersion, Id = project.Id, DisplayName = project.DisplayName };
            writer.Write(projectPath, SerializeProject(upgradedProject), temporaryPath => ReadProject(temporaryPath));
            TryAppendLog(root, $"{DateTime.UtcNow:O} migration 2.0-to-2.1 succeeded; actors={actors.Length}; backup={backupPath}");
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

    private static StoryResource MergeUncategorizedMembership(StoryResource story, IEnumerable<string> actorIds)
    {
        var owned = new StoryMembership
        {
            Actors = story.OwnedResources.Actors
                .Concat(actorIds)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList(),
            Dialogues = story.OwnedResources.Dialogues
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList(),
            Quests = story.OwnedResources.Quests
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToList(),
        };

        return new StoryResource
        {
            SchemaVersion = StoryResource.CurrentSchemaVersion,
            Id = story.Id,
            DisplayName = string.IsNullOrWhiteSpace(story.DisplayName) ? "未分类" : story.DisplayName,
            Description = story.Description,
            Tags = [.. story.Tags],
            EntryPresentation = story.EntryPresentation,
            OwnedResources = owned,
            ReferencedResources = story.ReferencedResources.Clone(),
            FlowRef = string.IsNullOrWhiteSpace(story.FlowRef) ? story.Id : story.FlowRef,
            Title = string.IsNullOrWhiteSpace(story.Title) ? story.DisplayName : story.Title,
            Entry = story.Entry,
            Nodes = [.. story.Nodes],
            Connections = [.. story.Connections],
            Metadata = new StoryMetadata { Notes = story.Metadata.Notes, Tags = [.. story.Metadata.Tags] },
        };
    }

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
    private static IReadOnlyList<FileSnapshot> CaptureSnapshot(string root, string projectPath, IReadOnlyList<string> actors, string storyPath) =>
        new[] { projectPath }.Concat(actors).Concat(new[] { storyPath }).Select(path => new FileSnapshot(path, File.Exists(path) ? File.ReadAllBytes(path) : null)).ToArray();
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
