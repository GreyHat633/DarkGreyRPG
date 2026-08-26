using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Actors;

public sealed class ActorRepository
{
    private readonly IAtomicFileWriter _atomicFileWriter;

    public ActorRepository(string projectDirectory, IAtomicFileWriter? atomicFileWriter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        ActorsDirectory = Path.Combine(ProjectDirectory, "actors");
        _atomicFileWriter = atomicFileWriter ?? new AtomicFileWriter();
    }

    public string ProjectDirectory { get; }

    public string ActorsDirectory { get; }

    public IReadOnlyList<ActorResourceInfo> ListActors()
    {
        EnsureActorsDirectoryExists();
        return Directory
            .EnumerateFiles(ActorsDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var resource = ActorSerializer.Read(path);
                return new ActorResourceInfo(resource.Id, resource.DisplayName, path, [.. resource.Tags]);
            })
            .ToArray();
    }

    public ActorDocument LoadActor(string id)
    {
        ValidateExistingId(id);
        var path = GetActorPath(id);
        if (!File.Exists(path))
        {
            throw new ActorNotFoundException(id);
        }

        return ActorDocument.FromResource(ActorSerializer.Read(path), path);
    }

    public ActorDocument CreateActor() => CreateActor(GetAvailableId("new_actor"), "新角色");

    public ActorDocument CreateActor(string id, string displayName)
    {
        ThrowIfInvalidNewId(id);
        if (ActorExists(id))
        {
            throw new ActorCollisionException(id);
        }

        return ActorDocument.CreateNew(id, displayName);
    }

    public ActorDocument SaveActor(ActorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureActorsDirectoryExists(createIfMissing: true);

        var resource = document.ToResource();
        var idPolicy = document.IsNew ? ActorIdPolicy.NewResource : ActorIdPolicy.ExistingResource;
        var serialized = ActorSerializer.Serialize(resource, idPolicy);
        var targetPath = GetActorPath(resource.Id);

        if (document.IsNew && ActorExists(resource.Id))
        {
            throw new ActorCollisionException(resource.Id);
        }

        if (!document.IsNew)
        {
            if (string.IsNullOrWhiteSpace(document.SourcePath))
            {
                throw new ActorRepositoryException("Saved Actor document has no source path.");
            }

            var sourcePath = Path.GetFullPath(document.SourcePath);
            if (!string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new ActorRepositoryException(
                    "Changing a saved Actor ID requires the explicit RenameActor operation.");
            }
        }

        try
        {
            _atomicFileWriter.Write(
                targetPath,
                serialized,
                temporaryPath =>
                {
                    var staged = ActorSerializer.Deserialize(File.ReadAllText(temporaryPath));
                    if (!string.Equals(staged.Id, resource.Id, StringComparison.Ordinal))
                    {
                        throw new ActorRepositoryException("The staged Actor ID changed during serialization.");
                    }
                });
        }
        catch (ActorValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ActorRepositoryException($"Could not save Actor '{resource.Id}'.", exception);
        }

        document.MarkSaved(targetPath);
        return document;
    }

    public ActorDocument DuplicateActor(string sourceId)
    {
        var source = LoadActor(sourceId);
        var duplicateId = GetAvailableId(source.Id + "_copy");
        var duplicate = ActorDocument.CreateNew(duplicateId, source.DisplayName);
        duplicate.Notes = source.Notes;
        duplicate.SetTags(source.Tags);
        duplicate.HomeStoryId = source.HomeStoryId;
        return SaveActor(duplicate);
    }

    public ActorDocument RenameActor(string sourceId, string targetId)
    {
        ValidateExistingId(sourceId);
        ThrowIfInvalidNewId(targetId);

        if (string.Equals(sourceId, targetId, StringComparison.Ordinal))
        {
            return LoadActor(sourceId);
        }

        var sourcePath = GetActorPath(sourceId);
        if (!File.Exists(sourcePath))
        {
            throw new ActorNotFoundException(sourceId);
        }

        if (ActorExists(targetId))
        {
            throw new ActorCollisionException(targetId);
        }

        var sourceResource = ActorSerializer.Read(sourcePath);
        var renamedResource = sourceResource.WithId(targetId);
        var serialized = ActorSerializer.Serialize(renamedResource, ActorIdPolicy.NewResource);
        var targetPath = GetActorPath(targetId);
        var stagingPath = Path.Combine(ActorsDirectory, $".{targetId}.{Guid.NewGuid():N}.rename.tmp");
        var backupPath = Path.Combine(ActorsDirectory, $".{sourceId}.{Guid.NewGuid():N}.rename.bak");
        var sourceMoved = false;
        var targetCreated = false;

        try
        {
            _atomicFileWriter.Write(
                stagingPath,
                serialized,
                temporaryPath => ActorSerializer.Deserialize(File.ReadAllText(temporaryPath)));

            File.Move(sourcePath, backupPath);
            sourceMoved = true;
            File.Move(stagingPath, targetPath);
            targetCreated = true;
            File.Delete(backupPath);
            sourceMoved = false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Exception? rollbackException = null;
            try
            {
                if (targetCreated && File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                if (sourceMoved && File.Exists(backupPath) && !File.Exists(sourcePath))
                {
                    File.Move(backupPath, sourcePath);
                }
            }
            catch (Exception rollbackFailure) when (rollbackFailure is IOException or UnauthorizedAccessException)
            {
                rollbackException = rollbackFailure;
            }

            var cause = rollbackException is null
                ? exception
                : new AggregateException("Actor rename and rollback both failed.", exception, rollbackException);
            throw new ActorRepositoryException(
                $"Could not rename Actor '{sourceId}' to '{targetId}'. The original was retained when rollback succeeded.",
                cause);
        }
        finally
        {
            TryDelete(stagingPath);
        }

        return ActorDocument.FromResource(renamedResource, targetPath);
    }

    public void DeleteActor(string id)
    {
        ValidateExistingId(id);
        var path = GetActorPath(id);
        if (!File.Exists(path))
        {
            throw new ActorNotFoundException(id);
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ActorRepositoryException($"Could not delete Actor '{id}'.", exception);
        }
    }

    public string GetAvailableId(string baseId)
    {
        ThrowIfInvalidNewId(baseId);
        if (!ActorExists(baseId))
        {
            return baseId;
        }

        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{baseId}_{suffix}";
            if (!ActorExists(candidate))
            {
                return candidate;
            }
        }

        throw new ActorRepositoryException($"Could not allocate an available Actor ID based on '{baseId}'.");
    }

    private bool ActorExists(string id) => File.Exists(GetActorPath(id));

    private string GetActorPath(string id) => Path.GetFullPath(Path.Combine(ActorsDirectory, id + ".json"));

    private void EnsureActorsDirectoryExists(bool createIfMissing = false)
    {
        if (Directory.Exists(ActorsDirectory))
        {
            return;
        }

        if (createIfMissing)
        {
            Directory.CreateDirectory(ActorsDirectory);
            return;
        }

        throw new ActorRepositoryException($"Actors directory does not exist: '{ActorsDirectory}'.");
    }

    private static void ValidateExistingId(string id)
    {
        var issues = ActorValidator.ValidateId(id, ActorIdPolicy.ExistingResource);
        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            throw new ActorValidationException(issues);
        }
    }

    private static void ThrowIfInvalidNewId(string id)
    {
        var issues = ActorValidator.ValidateId(id, ActorIdPolicy.NewResource);
        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            throw new ActorValidationException(issues);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Recovery artifacts are intentionally retained when cleanup cannot complete.
        }
    }
}
