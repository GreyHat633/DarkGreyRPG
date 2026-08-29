using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

public sealed record CanonicalStoryMembershipInfo(string StoryId, string SourcePath);

public sealed class CanonicalStoryMembershipRepositoryException : Exception
{
    public CanonicalStoryMembershipRepositoryException(
        string code,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Atomic persistence in one caller-selected membership directory.</summary>
public sealed class CanonicalStoryMembershipRepository
{
    private static readonly Regex IdPattern = new(
        "^[a-z0-9][a-z0-9_-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly IAtomicFileWriter _writer;
    private readonly object _writeGate = new();

    public CanonicalStoryMembershipRepository(
        string membershipDirectory,
        IAtomicFileWriter? writer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(membershipDirectory);
        MembershipDirectory = Path.GetFullPath(membershipDirectory);
        _writer = writer ?? new AtomicFileWriter();
    }

    public string MembershipDirectory { get; }

    public IReadOnlyList<CanonicalStoryMembershipInfo> List()
    {
        EnsureDirectory();
        return Directory.EnumerateFiles(MembershipDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var manifest = Read(path);
                return new CanonicalStoryMembershipInfo(manifest.StoryId, Path.GetFullPath(path));
            })
            .ToArray();
    }

    public CanonicalStoryMembershipManifest Load(string storyId)
    {
        var path = PathFor(storyId);
        if (!File.Exists(path))
            throw Failure("story.membership.repository.not_found",
                $"Story membership '{storyId}' was not found.");
        return Read(path);
    }

    public CanonicalStoryMembershipManifest Create(CanonicalStoryMembershipManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Validate(manifest);
        var path = PathFor(manifest.StoryId);
        lock (_writeGate)
        {
            EnsureDirectory();
            if (File.Exists(path))
                throw Failure("story.membership.repository.collision",
                    $"Story membership '{manifest.StoryId}' already exists.");
            Write(path, manifest);
        }
        return Load(manifest.StoryId);
    }

    public CanonicalStoryMembershipManifest Replace(CanonicalStoryMembershipManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Validate(manifest);
        var path = PathFor(manifest.StoryId);
        lock (_writeGate)
        {
            if (!File.Exists(path))
                throw Failure("story.membership.repository.not_found",
                    $"Story membership '{manifest.StoryId}' was not found.");
            Write(path, manifest);
        }
        return Load(manifest.StoryId);
    }

    public void Delete(string storyId)
    {
        var path = PathFor(storyId);
        lock (_writeGate)
        {
            if (!File.Exists(path))
                throw Failure("story.membership.repository.not_found",
                    $"Story membership '{storyId}' was not found.");
            try { File.Delete(path); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw Failure("story.membership.repository.delete.failed",
                    $"Could not delete Story membership '{storyId}'.", exception);
            }
        }
    }

    public string GetPath(string storyId) => PathFor(storyId);

    private CanonicalStoryMembershipManifest Read(string path)
    {
        try
        {
            var manifest = CanonicalStoryMembershipSerializer.Deserialize(File.ReadAllText(path));
            var fileId = Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(fileId, manifest.StoryId, StringComparison.Ordinal))
                throw Failure("story.membership.repository.filename.mismatch",
                    $"Membership file name '{fileId}' does not match story_id '{manifest.StoryId}'.");
            return manifest;
        }
        catch (CanonicalStoryMembershipRepositoryException) { throw; }
        catch (CanonicalStoryMembershipException exception)
        {
            throw Failure("story.membership.repository.data.invalid",
                $"Story membership file '{path}' is invalid.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Failure("story.membership.repository.read.failed",
                $"Could not read Story membership file '{path}'.", exception);
        }
    }

    private void Write(string path, CanonicalStoryMembershipManifest manifest)
    {
        try
        {
            var json = CanonicalStoryMembershipSerializer.Serialize(manifest);
            _writer.Write(path, json, temporaryPath =>
            {
                var restored = CanonicalStoryMembershipSerializer.Deserialize(File.ReadAllText(temporaryPath));
                if (!string.Equals(restored.StoryId, manifest.StoryId, StringComparison.Ordinal))
                    throw Failure("story.membership.repository.staged_id.changed",
                        "The staged membership story_id changed during serialization.");
            });
        }
        catch (CanonicalStoryMembershipRepositoryException) { throw; }
        catch (CanonicalStoryMembershipException exception)
        {
            throw Failure("story.membership.repository.data.invalid",
                $"Story membership '{manifest.StoryId}' is invalid.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Failure("story.membership.repository.write.failed",
                $"Could not save Story membership '{manifest.StoryId}'.", exception);
        }
    }

    private static void Validate(CanonicalStoryMembershipManifest manifest)
    {
        try { CanonicalStoryMembershipSerializer.Validate(manifest); }
        catch (CanonicalStoryMembershipException exception)
        {
            throw Failure("story.membership.repository.data.invalid",
                $"Story membership '{manifest.StoryId}' is invalid.", exception);
        }
    }

    private string PathFor(string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId) || !IdPattern.IsMatch(storyId))
            throw Failure("story.membership.repository.story_id.invalid",
                $"Story ID '{storyId}' must match [a-z0-9][a-z0-9_-]*.");
        return Path.Combine(MembershipDirectory, storyId + ".json");
    }

    private void EnsureDirectory()
    {
        try { Directory.CreateDirectory(MembershipDirectory); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Failure("story.membership.repository.directory.failed",
                $"Could not access membership directory '{MembershipDirectory}'.", exception);
        }
    }

    private static CanonicalStoryMembershipRepositoryException Failure(
        string code,
        string message,
        Exception? innerException = null)
        => new(code, message, innerException);
}
