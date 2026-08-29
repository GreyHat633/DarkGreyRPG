using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.IO;

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
    private static readonly Regex IdPattern = new(
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
        EnsureDirectory();
        return Directory.EnumerateFiles(ResourceDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var envelope = ReadAndValidate(path);
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
        var path = PathFor(id);
        if (!File.Exists(path))
            throw Failure("graph.resource.repository.not_found",
                $"Canonical {KindText()} resource '{id}' was not found.");
        return ReadAndValidate(path);
    }

    /// <summary>Creates a new file and fails closed if its stable ID exists.</summary>
    public GraphResourceEnvelope Create(GraphResourceEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateEnvelope(envelope);
        var path = PathFor(envelope.Id);
        lock (_writeGate)
        {
            EnsureDirectory();
            if (File.Exists(path))
                throw Failure("graph.resource.repository.collision",
                    $"Canonical {KindText()} resource '{envelope.Id}' already exists.");
            Write(path, envelope);
        }
        return Load(envelope.Id);
    }

    /// <summary>Atomically replaces an existing file without allowing ID rename.</summary>
    public GraphResourceEnvelope Replace(GraphResourceEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateEnvelope(envelope);
        var path = PathFor(envelope.Id);
        lock (_writeGate)
        {
            if (!File.Exists(path))
                throw Failure("graph.resource.repository.not_found",
                    $"Canonical {KindText()} resource '{envelope.Id}' was not found.");
            Write(path, envelope);
        }
        return Load(envelope.Id);
    }

    public void Delete(string id)
    {
        var path = PathFor(id);
        lock (_writeGate)
        {
            if (!File.Exists(path))
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
        if (!File.Exists(PathFor(baseId))) return baseId;
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{baseId}_{suffix}";
            if (!File.Exists(PathFor(candidate))) return candidate;
        }
        throw Failure("graph.resource.repository.id.unavailable",
            $"Could not allocate a canonical resource ID based on '{baseId}'.");
    }

    public string GetPath(string id) => PathFor(id);

    private GraphResourceEnvelope ReadAndValidate(string path)
    {
        try
        {
            var envelope = GraphResourceEnvelopeSerializer.Deserialize(File.ReadAllText(path));
            ValidateEnvelope(envelope);
            var fileId = Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(fileId, envelope.Id, StringComparison.Ordinal))
                throw Failure("graph.resource.repository.filename.mismatch",
                    $"Canonical resource file name '{fileId}' does not match ID '{envelope.Id}'.");
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
        return Path.Combine(ResourceDirectory, id + ".json");
    }

    private static void ValidateId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !IdPattern.IsMatch(id))
            throw Failure("graph.resource.repository.id.invalid",
                $"Canonical resource ID '{id}' must match [a-z0-9][a-z0-9_-]*.");
    }

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
