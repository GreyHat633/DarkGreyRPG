using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Stories;

public sealed class StoryRepository
{
    private readonly IAtomicFileWriter _atomicFileWriter;

    public StoryRepository(string projectDirectory, IAtomicFileWriter? atomicFileWriter = null)
    {
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        StoriesDirectory = Path.Combine(ProjectDirectory, "stories");
        _atomicFileWriter = atomicFileWriter ?? new AtomicFileWriter();
    }

    public string ProjectDirectory { get; }
    public string StoriesDirectory { get; }

    public IReadOnlyList<StoryResource> ListStories()
    {
        if (!Directory.Exists(StoriesDirectory)) return [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return CanonicalResourceFileSystem.EnumerateJsonFiles(StoriesDirectory)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var story = ReadResource(path);
                if (!seen.Add(story.Id))
                    throw new StoryRepositoryException($"Story ID '{story.Id}' is present in multiple files, including '{path}'.");
                return story;
            })
            .ToArray()
            ;
    }

    public StoryResource LoadStory(string id)
    {
        var path = FindStoryPath(id);
        if (path is null) throw new StoryNotFoundException(id);
        return ReadResource(path);
    }

    public StoryDocument LoadStoryDocument(string id)
    {
        var path = FindStoryPath(id);
        if (path is null) throw new StoryNotFoundException(id);
        return StoryDocument.FromResource(ReadResource(path), path);
    }

    public StoryDocument LoadDocument(string id) => LoadStoryDocument(id);

    public void SaveStory(StoryResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        Directory.CreateDirectory(StoriesDirectory);
        var path = GetStoryPath(resource.Id);
        var json = StorySerializer.Serialize(resource);
        _atomicFileWriter.Write(path, json, temp => StorySerializer.Deserialize(File.ReadAllText(temp)));
    }

    public StoryDocument SaveStory(StoryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var resource = document.ToResource();
        var errors = StoryValidator.Validate(resource).Where(i => i.Severity == ValidationSeverity.Error).ToArray();
        if (errors.Length != 0) throw new StoryValidationException(errors);
        Directory.CreateDirectory(StoriesDirectory);
        var path = GetStoryPath(resource.Id);
        if (document.IsNew && File.Exists(path)) throw new StoryRepositoryException($"Story '{resource.Id}' already exists.");
        if (!document.IsNew && !string.Equals(Path.GetFullPath(document.SourcePath ?? string.Empty), path, StringComparison.OrdinalIgnoreCase))
            throw new StoryRepositoryException("Changing a saved Story ID requires an explicit rename operation.");
        try
        {
            _atomicFileWriter.Write(path, StorySerializer.Serialize(resource), temp => StorySerializer.Deserialize(File.ReadAllText(temp)));
        }
        catch (StoryDataException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw new StoryRepositoryException($"Could not save Story '{resource.Id}'.", exception); }
        document.MarkSaved(path);
        return document;
    }

    public StoryDocument SaveDocument(StoryDocument document) => SaveStory(document);

    public void DeleteStory(string id)
    {
        ValidateExistingId(id);
        var path = GetStoryPath(id);
        if (!File.Exists(path)) throw new StoryNotFoundException(id);

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new StoryRepositoryException($"Could not delete Story '{id}'.", exception);
        }
    }

    public StoryResource CreateStory(string id, string displayName)
    {
        ThrowIfInvalidNewId(id);
        var path = GetStoryPath(id);
        if (FindStoryPath(id) is not null) throw new StoryRepositoryException($"Story '{id}' already exists.");
        var story = new StoryResource
        {
            Id = id, DisplayName = displayName, Title = displayName, Entry = "end",
            FlowRef = id,
            Nodes = [new StoryNodeResource { Id = "end", Type = "END" }],
        };
        SaveStory(story);
        return story;
    }

    public string GetAvailableId(string baseId)
    {
        ThrowIfInvalidNewId(baseId);
        if (FindStoryPath(baseId) is null) return baseId;

        return StoryUid.Create(ListStories().Select(story => StoryUid.Parse(story.Id)).ToHashSet()).Value;
    }

    public string GetStoryPath(string id)
    {
        ValidateExistingId(id);
        return FindStoryPath(id) ?? Path.Combine(StoriesDirectory, StoryUid.Parse(id).Value + ".json");
    }

    private string? FindStoryPath(string id)
    {
        ValidateExistingId(id);
        return CanonicalResourceFileSystem.FindUniquePath(
            StoriesDirectory,
            id,
            path => ReadResource(path).Id,
            (logicalId, paths) => new StoryRepositoryException(
                $"Story ID '{logicalId}' is present in multiple files: {string.Join(", ", paths)}."),
            exception => exception is StoryDataException);
    }

    private static StoryResource ReadResource(string path)
    {
        try
        {
            var resource = StorySerializer.Deserialize(File.ReadAllText(path));
            if (!string.Equals(Path.GetFileName(path), resource.Id + ".json", StringComparison.Ordinal))
                throw new StoryDataException($"Story file name must match its ID '{resource.Id}'.");
            return resource;
        }
        catch (StoryDataException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw new StoryDataException($"Could not read Story file '{path}'.", exception); }
    }

    private static void ValidateExistingId(string id)
    {
        if (!StoryUid.IsValid(id)) throw new StoryRepositoryException($"Invalid Story UID '{id}'.");
    }

    private static void ThrowIfInvalidNewId(string id) => ValidateExistingId(id);

}

public sealed class StoryNotFoundException : Exception
{
    public StoryNotFoundException(string id) : base($"Story '{id}' was not found.") { }
}

public sealed class StoryRepositoryException : Exception
{
    public StoryRepositoryException(string message) : base(message) { }
    public StoryRepositoryException(string message, Exception innerException) : base(message, innerException) { }
}
