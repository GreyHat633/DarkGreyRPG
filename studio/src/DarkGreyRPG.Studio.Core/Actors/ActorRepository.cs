using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
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
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return CanonicalResourceFileSystem.EnumerateJsonFiles(ActorsDirectory)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var resource = ReadResource(path);
                if (!seen.Add(resource.Id))
                    throw new ActorRepositoryException($"Actor ID '{resource.Id}' is present in multiple files, including '{path}'.");
                return new ActorResourceInfo(resource.Id, resource.DisplayName, path, [.. resource.Tags], resource.Type ?? ActorResource.LegacyResourceType, resource.DefaultPortraitRef, resource.PortraitVariants.ToArray());
            })
            .ToArray();
    }

    public ActorDocument LoadActor(string id)
    {
        ValidateExistingId(id);
        var path = FindActorPath(id);
        if (path is null)
        {
            throw new ActorNotFoundException(id);
        }

        return ActorDocument.FromResource(ReadResource(path), path);
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

    public ActorDocument CreateIndividual(string npcId, string displayName) => CreateTyped(npcId, displayName, individual: true);

    public ActorDocument CreateCollective(string groupId, string displayName) => CreateTyped(groupId, displayName, individual: false);

    public ActorDocument LoadIndividual(string npcId) => LoadTyped(npcId, individual: true);

    public ActorDocument LoadCollective(string groupId) => LoadTyped(groupId, individual: false);

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
        var sourceResource = source.ToResource();
        var duplicate = sourceResource.Type switch
        {
            IndividualActorResource.ResourceType => ActorDocument.CreateIndividual(duplicateId, source.DisplayName),
            CollectiveActorResource.ResourceType => ActorDocument.CreateCollective(duplicateId, source.DisplayName),
            _ => ActorDocument.CreateNew(duplicateId, source.DisplayName),
        };
        duplicate.Notes = source.Notes;
        duplicate.SetTags(source.Tags);
        duplicate.HomeStoryId = source.HomeStoryId;
        duplicate.DefaultPortraitRef = source.DefaultPortraitRef;
        duplicate.SetPortraitVariants(source.PortraitVariants);
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

        var sourcePath = FindActorPath(sourceId);
        if (sourcePath is null)
        {
            throw new ActorNotFoundException(sourceId);
        }

        if (ActorExists(targetId))
        {
            throw new ActorCollisionException(targetId);
        }

        var sourceResource = ReadResource(sourcePath);
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
        var path = FindActorPath(id);
        if (path is null)
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

    private bool ActorExists(string id) => FindActorPath(id) is not null;

    private ActorDocument CreateTyped(string id, string displayName, bool individual)
    {
        ThrowIfInvalidNewId(id);
        if (ActorExists(id)) throw new ActorCollisionException(id);
        return individual ? ActorDocument.CreateIndividual(id, displayName) : ActorDocument.CreateCollective(id, displayName);
    }

    private ActorDocument LoadTyped(string id, bool individual)
    {
        var document = LoadActor(id);
        var expected = individual ? IndividualActorResource.ResourceType : CollectiveActorResource.ResourceType;
        if (!string.Equals(document.ToResource().Type, expected, StringComparison.Ordinal))
        {
            throw new ActorValidationException([new("actor.type.mismatch", "Actor file contains the wrong resource type.", nameof(ActorResource.Type))]);
        }
        return document;
    }

    public string GetActorPath(string id)
    {
        ValidateExistingId(id);
        return FindActorPath(id) ?? CanonicalActorPath(id);
    }

    private string? FindActorPath(string id)
    {
        if (!DgrResourceId.IsFullId(id))
        {
            var legacyPath = Path.Combine(ActorsDirectory, id + ".json");
            return File.Exists(legacyPath) ? legacyPath : null;
        }
        return CanonicalResourceFileSystem.FindUniquePath(
            ActorsDirectory,
            id,
            path => ReadResource(path).Id,
            (logicalId, paths) => new ActorRepositoryException(
                $"Actor ID '{logicalId}' is present in multiple files: {string.Join(", ", paths)}."),
            exception => exception is ActorValidationException or ActorDataException);
    }

    private string CanonicalActorPath(string id)
        => Path.Combine(ActorsDirectory, DarkGreyRPG.Studio.Core.Identity.DgrResourceId.RelativeJsonPath(id));

    private static ActorResource ReadResource(string path)
    {
        try
        {
            var resource = ActorSerializer.Deserialize(File.ReadAllText(path));
            if (!DgrResourceId.IsFullId(resource.Id)
                && !string.Equals(Path.GetFileName(path), resource.Id + ".json", StringComparison.Ordinal))
                throw new ActorValidationException([new(
                    "actor.filename.mismatch",
                    $"Actor file name must match its ID: expected '{resource.Id}.json', got '{Path.GetFileName(path)}'.",
                    nameof(ActorResource.Id))]);
            return resource;
        }
        catch (ActorValidationException) { throw; }
        catch (ActorDataException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw new ActorDataException($"Could not read Actor file '{path}'.", exception); }
    }

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
