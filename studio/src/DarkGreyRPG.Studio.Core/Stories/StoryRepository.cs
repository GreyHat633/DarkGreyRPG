using DarkGreyRPG.Studio.Core.Actors;
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

    public IReadOnlyList<StoryResource> ListStories() => Directory.Exists(StoriesDirectory)
        ? Directory.EnumerateFiles(StoriesDirectory, "*.json")
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(StorySerializer.Read)
            .ToArray()
        : [];

    public StoryResource LoadStory(string id)
    {
        var path = GetStoryPath(id);
        if (!File.Exists(path)) throw new StoryNotFoundException(id);
        return StorySerializer.Read(path);
    }

    public StoryDocument LoadStoryDocument(string id)
    {
        var path = GetStoryPath(id);
        if (!File.Exists(path)) throw new StoryNotFoundException(id);
        return StoryDocument.FromResource(StorySerializer.Read(path), path);
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

    public StoryResource CreateStory(string id, string displayName)
    {
        var normalized = ActorValidator.NormalizeId(id);
        if (!string.Equals(normalized, id, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(id))
            throw new StoryRepositoryException($"Invalid Story ID '{id}'.");
        var path = GetStoryPath(id);
        if (File.Exists(path)) throw new StoryRepositoryException($"Story '{id}' already exists.");
        var story = new StoryResource
        {
            Id = id, DisplayName = displayName, Title = displayName, Entry = "end",
            FlowRef = id,
            Nodes = [new StoryNodeResource { Id = "end", Type = "END" }],
        };
        SaveStory(story);
        return story;
    }

    private string GetStoryPath(string id) => Path.Combine(StoriesDirectory, id + ".json");
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
