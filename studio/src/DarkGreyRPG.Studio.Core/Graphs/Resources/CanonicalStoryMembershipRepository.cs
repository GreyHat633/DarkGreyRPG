using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.Identity;
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
    private static readonly Regex LegacyIdPattern = new(
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
        if (!Directory.Exists(MembershipDirectory)) return [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return CanonicalResourceFileSystem.EnumerateJsonFiles(MembershipDirectory)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var manifest = Read(path);
                if (!seen.Add(manifest.StoryId))
                    throw Failure("story.membership.repository.duplicate_id",
                        $"Story membership ID '{manifest.StoryId}' is present in multiple files, including '{path}'.");
                return new CanonicalStoryMembershipInfo(manifest.StoryId, Path.GetFullPath(path));
            })
            .ToArray();
    }

    public CanonicalStoryMembershipManifest Load(string storyId)
    {
        ValidateId(storyId);
        var path = FindExistingPath(storyId);
        if (path is null)
            throw Failure("story.membership.repository.not_found",
                $"Story membership '{storyId}' was not found.");
        return Read(path);
    }

    public CanonicalStoryMembershipManifest Create(CanonicalStoryMembershipManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Validate(manifest);
        lock (_writeGate)
        {
            EnsureDirectory();
            if (FindExistingPath(manifest.StoryId) is not null)
                throw Failure("story.membership.repository.collision",
                    $"Story membership '{manifest.StoryId}' already exists.");
            var path = CanonicalPath(manifest.StoryId);
            Write(path, manifest);
        }
        return Load(manifest.StoryId);
    }

    public CanonicalStoryMembershipManifest Replace(CanonicalStoryMembershipManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Validate(manifest);
        lock (_writeGate)
        {
            var path = FindExistingPath(manifest.StoryId);
            if (path is null)
                throw Failure("story.membership.repository.not_found",
                    $"Story membership '{manifest.StoryId}' was not found.");
            Write(path, manifest);
        }
        return Load(manifest.StoryId);
    }

    public void Delete(string storyId)
    {
        ValidateId(storyId);
        lock (_writeGate)
        {
            var path = FindExistingPath(storyId);
            if (path is null)
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

    public string GetPath(string storyId)
    {
        ValidateId(storyId);
        return FindExistingPath(storyId) ?? CanonicalPath(storyId);
    }

    private CanonicalStoryMembershipManifest Read(string path)
    {
        try
        {
            var manifest = CanonicalStoryMembershipSerializer.Deserialize(File.ReadAllText(path));
            if (!DgrResourceId.IsFullId(manifest.StoryId)
                && !string.Equals(Path.GetFileName(path), manifest.StoryId + ".json", StringComparison.Ordinal))
                throw Failure("story.membership.repository.filename.mismatch",
                    $"Membership file name '{Path.GetFileNameWithoutExtension(path)}' does not match story_id '{manifest.StoryId}'.");
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
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
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
        ValidateId(storyId);
        return FindExistingPath(storyId) ?? CanonicalPath(storyId);
    }

    private string? FindExistingPath(string storyId)
    {
        if (!DgrResourceId.IsFullId(storyId))
        {
            var legacyPath = Path.Combine(MembershipDirectory, storyId + ".json");
            return File.Exists(legacyPath) ? legacyPath : null;
        }
        return CanonicalResourceFileSystem.FindUniquePath(
            MembershipDirectory,
            storyId,
            path => Read(path).StoryId,
            (logicalId, paths) => Failure("story.membership.repository.duplicate_id",
                $"Story membership ID '{logicalId}' is present in multiple files: {string.Join(", ", paths)}."),
            exception => exception is CanonicalStoryMembershipRepositoryException);
    }

    private string CanonicalPath(string storyId)
        => Path.Combine(MembershipDirectory, DgrResourceId.RelativeJsonPath(storyId));

    private static void ValidateId(string? storyId)
    {
        if (!DgrResourceId.IsFullId(storyId) && !LegacyIdPattern.IsMatch(storyId ?? string.Empty))
            throw Failure("story.membership.repository.story_id.invalid",
                $"Story ID '{storyId}' must be a valid full DGR ID or a compatible legacy ID.");
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
