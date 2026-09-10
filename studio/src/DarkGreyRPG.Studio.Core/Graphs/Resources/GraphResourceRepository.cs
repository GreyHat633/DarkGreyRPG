using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

public sealed record GraphResourceInfo(
    string Id,
    string DisplayName,
    GraphResourceKind ResourceKind,
    string SourcePath);

public class GraphResourceRepositoryException : Exception
{
    public GraphResourceRepositoryException(string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>
/// Atomic persistence for exactly one caller-selected canonical resource kind.
/// The caller supplies the directory so this layer does not freeze a project
/// folder layout before the Story-first workspace contract is complete.
/// </summary>
public sealed class GraphResourceRepository
{
    private static readonly Regex LegacyIdPattern = new(
        "^[a-z0-9][a-z0-9_-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private readonly IAtomicFileWriter _writer;
    private readonly object _writeGate = new();

    public GraphResourceRepository(
        string resourceDirectory,
        GraphResourceKind expectedKind,
        IAtomicFileWriter? writer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceDirectory);
        if (!GraphResourceScopeAdapter.TryGetScope(expectedKind, out _))
            throw Failure("graph.resource.repository.kind.unsupported",
                $"Unsupported repository resource kind '{expectedKind}'.");
        ResourceDirectory = Path.GetFullPath(resourceDirectory);
        ExpectedKind = expectedKind;
        _writer = writer ?? new AtomicFileWriter();
    }

    public string ResourceDirectory { get; }
    public GraphResourceKind ExpectedKind { get; }

    public IReadOnlyList<GraphResourceInfo> List()
    {
        if (!Directory.Exists(ResourceDirectory)) return [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return CanonicalResourceFileSystem.EnumerateJsonFiles(ResourceDirectory)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var envelope = ReadAndValidate(path);
                EnsureUnique(seen, envelope.Id, path);
                return new GraphResourceInfo(
                    envelope.Id,
                    envelope.DisplayName,
                    envelope.ResourceKind,
                    Path.GetFullPath(path));
            })
            .ToArray();
    }

    public GraphResourceEnvelope Load(string id)
    {
        ValidateId(id);
        var path = FindExistingPath(id);
        if (path is null)
            throw Failure("graph.resource.repository.not_found",
                $"Canonical {KindText()} resource '{id}' was not found.");
        return ReadAndValidate(path);
    }

    /// <summary>Creates a new file and fails closed if its stable ID exists.</summary>
    public GraphResourceEnvelope Create(GraphResourceEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateEnvelope(envelope);
        lock (_writeGate)
        {
            EnsureDirectory();
            if (FindExistingPath(envelope.Id) is not null)
                throw Failure("graph.resource.repository.collision",
                    $"Canonical {KindText()} resource '{envelope.Id}' already exists.");
            var path = CanonicalPath(envelope.Id);
            Write(path, envelope);
        }
        return Load(envelope.Id);
    }

    /// <summary>Atomically replaces an existing file without allowing ID rename.</summary>
    public GraphResourceEnvelope Replace(GraphResourceEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateEnvelope(envelope);
        lock (_writeGate)
        {
            var path = FindExistingPath(envelope.Id);
            if (path is null)
                throw Failure("graph.resource.repository.not_found",
                    $"Canonical {KindText()} resource '{envelope.Id}' was not found.");
            Write(path, envelope);
        }
        return Load(envelope.Id);
    }

    public void Delete(string id)
    {
        ValidateId(id);
        lock (_writeGate)
        {
            var path = FindExistingPath(id);
            if (path is null)
                throw Failure("graph.resource.repository.not_found",
                    $"Canonical {KindText()} resource '{id}' was not found.");
            try
            {
                File.Delete(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw Failure("graph.resource.repository.delete.failed",
                    $"Could not delete canonical {KindText()} resource '{id}'.", exception);
            }
        }
    }

    public string GetAvailableId(string baseId)
    {
        ValidateId(baseId);
        EnsureDirectory();
        if (FindExistingPath(baseId) is null) return baseId;
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{baseId}_{suffix}";
            if (FindExistingPath(candidate) is null) return candidate;
        }
        throw Failure("graph.resource.repository.id.unavailable",
            $"Could not allocate a canonical resource ID based on '{baseId}'.");
    }

    public string GetPath(string id)
    {
        ValidateId(id);
        return FindExistingPath(id) ?? CanonicalPath(id);
    }

    private GraphResourceEnvelope ReadAndValidate(string path)
    {
        try
        {
            var envelope = GraphResourceEnvelopeSerializer.Deserialize(File.ReadAllText(path));
            ValidateEnvelope(envelope);
            if (!DgrResourceId.IsFullId(envelope.Id)
                && !string.Equals(Path.GetFileName(path), envelope.Id + ".json", StringComparison.Ordinal))
                throw Failure("graph.resource.repository.filename.mismatch",
                    $"Canonical resource file name '{Path.GetFileNameWithoutExtension(path)}' does not match ID '{envelope.Id}'.");
            return envelope;
        }
        catch (GraphResourceRepositoryException)
        {
            throw;
        }
        catch (GraphResourceEnvelopeException exception)
        {
            throw Failure("graph.resource.repository.data.invalid",
                $"Canonical resource file '{path}' is invalid.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Failure("graph.resource.repository.read.failed",
                $"Could not read canonical resource file '{path}'.", exception);
        }
    }

    private void Write(string path, GraphResourceEnvelope envelope)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = GraphResourceEnvelopeSerializer.Serialize(envelope);
            _writer.Write(path, json, temporaryPath =>
            {
                var restored = GraphResourceEnvelopeSerializer.Deserialize(File.ReadAllText(temporaryPath));
                ValidateEnvelope(restored);
                if (!string.Equals(restored.Id, envelope.Id, StringComparison.Ordinal))
                    throw Failure("graph.resource.repository.staged_id.changed",
                        "The staged canonical resource ID changed during serialization.");
            });
        }
        catch (GraphResourceRepositoryException)
        {
            throw;
        }
        catch (GraphResourceEnvelopeException exception)
        {
            throw Failure("graph.resource.repository.data.invalid",
                $"Canonical {KindText()} resource '{envelope.Id}' is invalid.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Failure("graph.resource.repository.write.failed",
                $"Could not save canonical {KindText()} resource '{envelope.Id}'.", exception);
        }
    }

    private void ValidateEnvelope(GraphResourceEnvelope envelope)
    {
        ValidateId(envelope.Id);
        if (!GraphResourceScopeAdapter.TryGetScope(envelope.ResourceKind, out var actualScope))
            throw Failure("graph.resource.repository.kind.mismatch",
                $"Resource kind value '{envelope.ResourceKind}' is not supported by this repository.");
        if (envelope.ResourceKind != ExpectedKind)
            throw Failure("graph.resource.repository.kind.mismatch",
                $"Resource kind '{GraphResourceEnvelopeSerializer.FormatResourceKind(envelope.ResourceKind)}' does not match repository kind '{KindText()}'.");
        try
        {
            _ = GraphResourceScopeAdapter.Open(envelope, actualScope);
        }
        catch (GraphResourceEnvelopeException exception)
        {
            throw Failure("graph.resource.repository.data.invalid",
                $"Canonical {KindText()} resource '{envelope.Id}' is invalid.", exception);
        }
    }

    private string PathFor(string id)
    {
        ValidateId(id);
        return FindExistingPath(id) ?? CanonicalPath(id);
    }

    private static void ValidateId(string? id)
    {
        if (!DgrResourceId.IsFullId(id) && !LegacyIdPattern.IsMatch(id ?? string.Empty))
            throw Failure("graph.resource.repository.id.invalid",
                $"Canonical resource ID '{id}' must be a valid full DGR ID or a compatible legacy ID.");
    }

    private string? FindExistingPath(string id)
    {
        if (!DgrResourceId.IsFullId(id))
        {
            var legacyPath = Path.Combine(ResourceDirectory, id + ".json");
            return File.Exists(legacyPath) ? legacyPath : null;
        }
        var found = CanonicalResourceFileSystem.FindUniquePath(
            ResourceDirectory,
            id,
            path => ReadAndValidate(path).Id,
            (logicalId, paths) => Failure("graph.resource.repository.duplicate_id",
                $"Canonical {KindText()} resource ID '{logicalId}' is present in multiple files: {string.Join(", ", paths)}."),
            exception => exception is GraphResourceRepositoryException);
        if (found is null)
        {
            var canonical = CanonicalPath(id);
            if (File.Exists(canonical))
            {
                _ = ReadAndValidate(canonical);
                return canonical;
            }
        }
        return found;
    }

    private static void EnsureUnique(HashSet<string> seen, string id, string path)
    {
        if (!seen.Add(id))
            throw Failure("graph.resource.repository.duplicate_id",
                $"Canonical resource ID '{id}' is present in multiple files, including '{path}'.");
    }

    private string CanonicalPath(string id) => Path.Combine(ResourceDirectory, DgrResourceId.RelativeJsonPath(id));

    private void EnsureDirectory()
    {
        try
        {
            Directory.CreateDirectory(ResourceDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Failure("graph.resource.repository.directory.failed",
                $"Could not access canonical resource directory '{ResourceDirectory}'.", exception);
        }
    }

    private string KindText() => GraphResourceEnvelopeSerializer.FormatResourceKind(ExpectedKind);

    private static GraphResourceRepositoryException Failure(
        string code,
        string message,
        Exception? innerException = null)
        => new(code, message, innerException);
}

/// <summary>Safe recursive discovery for resource files; reparse points are never traversed.</summary>
internal static class CanonicalResourceFileSystem
{
    public static IReadOnlyList<string> EnumerateJsonFiles(string root)
    {
        if (!Directory.Exists(root)) return [];
        var pending = new Stack<string>([Path.GetFullPath(root)]);
        var files = new List<string>();
        while (pending.Count != 0)
        {
            var directory = pending.Pop();
            var directoryInfo = new DirectoryInfo(directory);
            if (directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            foreach (var file in directoryInfo.EnumerateFiles("*.json", SearchOption.TopDirectoryOnly))
            {
                if (!file.Attributes.HasFlag(FileAttributes.ReparsePoint)) files.Add(file.FullName);
            }
            foreach (var child in directoryInfo.EnumerateDirectories("*", SearchOption.TopDirectoryOnly)
                         .OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase).Reverse())
            {
                if (!child.Attributes.HasFlag(FileAttributes.ReparsePoint)) pending.Push(child.FullName);
            }
        }
        return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string? FindUniquePath<T>(
        string root,
        string id,
        Func<string, T> read,
        Func<T, string> logicalId,
        Func<string, IReadOnlyList<string>, Exception> duplicate,
        Func<Exception, bool>? ignoreReadException = null)
    {
        var matches = new List<string>();
        foreach (var path in EnumerateJsonFiles(root))
        {
            try
            {
                if (string.Equals(logicalId(read(path)), id, StringComparison.Ordinal)) matches.Add(path);
            }
            catch (Exception exception) when (ignoreReadException?.Invoke(exception) == true)
            {
            }
        }
        if (matches.Count > 1) throw duplicate(id, matches);
        return matches.Count == 0 ? null : matches[0];
    }

    public static string? FindUniquePath(
        string root,
        string id,
        Func<string, string> readId,
        Func<string, IReadOnlyList<string>, Exception> duplicate,
        Func<Exception, bool>? ignoreReadException = null)
        => FindUniquePath(root, id, readId, value => value, duplicate, ignoreReadException);
}
