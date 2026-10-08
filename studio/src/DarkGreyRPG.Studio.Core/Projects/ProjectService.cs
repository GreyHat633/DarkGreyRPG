using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Projects;

public sealed class ProjectService
{
    private static readonly string[] ProjectDirectories = ["actors", "resources"];
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    private readonly IAtomicFileWriter _atomicFileWriter;
    private readonly Dictionary<string, ActorDocument> _openActorDocuments =
        new(StringComparer.Ordinal);

    public ProjectService(IAtomicFileWriter? atomicFileWriter = null)
    {
        _atomicFileWriter = atomicFileWriter ?? new AtomicFileWriter();
    }

    public ProjectSession? CurrentProject { get; private set; }

    public IReadOnlyCollection<ActorDocument> OpenActorDocuments => _openActorDocuments.Values;

    public ProjectSession CreateProject(string projectDirectory, string id, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ValidateProjectIdentity(id, displayName, ActorIdPolicy.NewResource);

        var root = Path.GetFullPath(projectDirectory);
        if (File.Exists(root))
        {
            throw new ProjectException($"Project path is a file: '{root}'.");
        }

        Directory.CreateDirectory(root);
        var projectPath = Path.Combine(root, "project.json");
        if (File.Exists(projectPath))
        {
            throw new ProjectException($"A project already exists at '{root}'.");
        }

        foreach (var directory in ProjectDirectories)
        {
            Directory.CreateDirectory(Path.Combine(root, directory));
        }

        var resource = new ProjectResource
        {
            Id = id,
            DisplayName = displayName.Trim(),
            ProjectOriginCode = Guid.NewGuid().ToString("N"),
        };
        WriteProjectFile(projectPath, resource);
        return SetCurrent(root, resource);
    }

    public ProjectSession OpenProject(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        var root = Path.GetFullPath(projectDirectory);
        if (!Directory.Exists(root))
        {
            throw new ProjectException($"Project directory does not exist: '{root}'.");
        }

        var projectPath = Path.Combine(root, "project.json");
        var resource = ReadProjectFile(projectPath);
        ValidateProjectIdentity(resource.Id, resource.DisplayName, ActorIdPolicy.ExistingResource);
        if (ProjectAuthorDataBoundary.FindRetiredDirectory(root) is { } retiredDirectory)
            throw new ProjectException($"旧资源目录 '{retiredDirectory}' 含有不受支持的数据；当前版本不会自动转换或覆盖。");

        var actorsDirectory = Path.Combine(root, "actors");
        if (!Directory.Exists(actorsDirectory))
        {
            throw new ProjectException($"Project actors directory does not exist: '{actorsDirectory}'.");
        }

        return SetCurrent(root, resource);
    }

    public void CloseProject(bool discardUnsavedChanges = false)
    {
        if (!discardUnsavedChanges && _openActorDocuments.Values.Any(document => document.IsDirty))
        {
            throw new ProjectException("The project has unsaved resource documents.");
        }

        _openActorDocuments.Clear();
        CurrentProject = null;
    }

    public ProjectSession ReloadProject(bool discardUnsavedChanges = false)
    {
        var current = RequireCurrentProject();
        var path = current.ProjectDirectory;
        CloseProject(discardUnsavedChanges);
        return OpenProject(path);
    }

    public IReadOnlyList<ValidationIssue> ValidateProject()
    {
        var current = RequireCurrentProject();
        var issues = new List<ValidationIssue>();

        try
        {
            ValidateProjectIdentity(
                current.Project.Id,
                current.Project.DisplayName,
                ActorIdPolicy.ExistingResource);
        }
        catch (ProjectException exception)
        {
            issues.Add(new("project.identity.invalid", exception.Message));
        }

        foreach (var directory in ProjectDirectories)
        {
            var path = Path.Combine(current.ProjectDirectory, directory);
            if (!Directory.Exists(path))
            {
                var severity = directory == "actors" ? ValidationSeverity.Error : ValidationSeverity.Warning;
                issues.Add(new(
                    $"project.directory.{directory}.missing",
                    $"Project directory '{directory}' is missing.",
                    Severity: severity));
            }
        }

        try
        {
            current.Actors.ListActors();
        }
        catch (Exception exception) when (exception is ActorRepositoryException or ActorDataException or ActorValidationException)
        {
            issues.Add(new("project.actors.invalid", exception.Message));
        }

        // Canonical membership is validated against native content plus the
        // project-local, read-only provider catalog. Provider diagnostics are
        // surfaced once, while each Story retains consumer/kind/full-ID detail
        // from the workspace loader.
        try
        {
            var providers = OfflineProviderCatalog.Load(current.ProjectDirectory);
            issues.AddRange(providers.Diagnostics.Select(diagnostic => new ValidationIssue(
                diagnostic.Code,
                diagnostic.Message,
                Field: "references",
                Severity: ValidationSeverity.Error,
                NodeId: diagnostic.PackagePath)));

            var canonical = new CanonicalProjectGraphStore(current.ProjectDirectory);
            foreach (var story in canonical.Stories.List())
            {
                try
                {
                    var snapshot = new CanonicalStoryWorkspaceLoader(
                        canonical,
                        providers: providers).Load(story.Id);
                    issues.AddRange(snapshot.ValidationIssues);
                }
                catch (Exception exception) when (exception is GraphResourceRepositoryException
                    or CanonicalStoryMembershipRepositoryException)
                {
                    var code = exception switch
                    {
                        GraphResourceRepositoryException graph => graph.Code,
                        CanonicalStoryMembershipRepositoryException membership => membership.Code,
                        _ => "project.canonical.invalid",
                    };
                    issues.Add(new(
                        code,
                        $"Canonical Story '{story.Id}' could not be validated: {exception.Message}",
                        Field: "canonical",
                        Severity: ValidationSeverity.Error,
                        NodeId: story.Id));
                }
            }
        }
        catch (Exception exception) when (exception is GraphResourceRepositoryException
            or CanonicalStoryMembershipRepositoryException
            or IOException
            or UnauthorizedAccessException)
        {
            var code = exception switch
            {
                GraphResourceRepositoryException graph => graph.Code,
                CanonicalStoryMembershipRepositoryException membership => membership.Code,
                _ => "project.canonical.invalid",
            };
            issues.Add(new(code, $"Canonical project content could not be validated: {exception.Message}", "canonical"));
        }

        return issues;
    }

    public ActorDocument OpenActor(string id)
    {
        if (_openActorDocuments.TryGetValue(id, out var existing))
        {
            return existing;
        }

        var document = RequireCurrentProject().Actors.LoadActor(id);
        return RegisterOpenDocument(document);
    }

    /// <summary>
    /// Releases a clean cached Actor document before an external lifecycle
    /// service deletes the shared Actor file. Dirty documents fail closed.
    /// </summary>
    public void ReleaseOpenActor(string id, bool discardUnsavedChanges = false)
    {
        var document = FindOpenDocument(id);
        if (!discardUnsavedChanges) EnsureCanDiscardOpenDocument(id, document, "release");
        if (document is not null) UnregisterOpenDocument(document);
    }

    public ActorDocument CreateActor()
    {
        var document = RequireCurrentProject().Actors.CreateActor();
        return RegisterOpenDocument(document);
    }

    public ActorDocument CreateActor(string id, string displayName)
    {
        var document = RequireCurrentProject().Actors.CreateActor(id, displayName);
        return RegisterOpenDocument(document);
    }

    /// <summary>Creates a new unsaved Actor whose ownership is the selected Story.</summary>
    public ActorDocument CreateActor(string id, string displayName, string homeStoryId)
    {
        var current = RequireCurrentProject();
        _ = new CanonicalProjectGraphStore(current.ProjectDirectory).Stories.Load(homeStoryId);
        if (ResourceAddress.FromKey(id).StoryUid.Value != homeStoryId)
            throw new ProjectException("Actor address must belong to the selected Story.");
        var document = current.Actors.CreateActor(id, displayName);
        document.HomeStoryId = homeStoryId;
        return RegisterOpenDocument(document);
    }

    /// <summary>Creates and persists a blank Actor in the selected Story.</summary>
    public ActorDocument CreateActorInStory(string storyId, string id, string displayName) =>
        SaveActor(CreateActor(id, displayName, storyId));

    /// <summary>Imports an Actor as a new independent file owned by the selected Story.</summary>
    public ActorDocument ImportActorAsNew(string sourceId, string newId, string storyId)
    {
        var current = RequireCurrentProject();
        var source = current.Actors.LoadActor(sourceId);
        var imported = CreateActor(newId, source.DisplayName, storyId);
        if (source.ToResource().Type == CollectiveActorResource.ResourceType)
        {
            UnregisterOpenDocument(imported);
            imported = RegisterOpenDocument(current.Actors.CreateCollective(newId, source.DisplayName));
        }
        imported.SetTags(source.Tags);
        imported.DefaultPortraitRef = source.DefaultPortraitRef;
        imported.SetPortraitVariants(source.PortraitVariants);
        try { return SaveActor(imported); }
        catch { UnregisterOpenDocument(imported); throw; }
    }

    public void AddActorReference(string storyId, string actorId) =>
        CurrentActorLifecycle().AddReference(storyId, actorId);

    public void RemoveActorReference(string storyId, string actorId) =>
        CurrentActorLifecycle().RemoveReference(storyId, actorId);

    public IReadOnlyList<ResourceDescriptor> GetActorReferences(string actorId)
    {
        var current = RequireCurrentProject();
        var store = new CanonicalProjectGraphStore(current.ProjectDirectory);
        var owner = current.Actors.LoadActor(actorId).HomeStoryId;
        return CurrentActorLifecycle().EnumerateBlockers(owner, actorId).Select(reference =>
            new ResourceDescriptor(ProjectResourceType.Story, reference.StoryId,
                store.Stories.Load(reference.StoryId).DisplayName, store.Stories.GetPath(reference.StoryId))).ToArray();
    }

    public bool CanDeleteActor(string actorId) =>
        GetActorReferences(actorId).Count == 0;

    private CanonicalStoryActorLifecycleService CurrentActorLifecycle()
    {
        var current = RequireCurrentProject();
        return new(new CanonicalProjectGraphStore(current.ProjectDirectory, _atomicFileWriter), current.Actors);
    }

    public ActorDocument DuplicateActor(string sourceId)
    {
        var current = RequireCurrentProject();
        var source = current.Actors.LoadActor(sourceId);
        var address = ResourceAddress.FromKey(source.Id);
        var duplicateId = ResourceAddress.Create(address.StoryUid, ResourceKind.Actor,
            current.Actors.ListActors().Select(actor => ResourceAddress.FromKey(actor.Id)).ToHashSet()).ToKey();
        var document = source.ToResource().Type == IndividualActorResource.ResourceType
            ? current.Actors.CreateIndividual(duplicateId, source.DisplayName)
            : current.Actors.CreateCollective(duplicateId, source.DisplayName);
        document.SetTags(source.Tags);
        document.DefaultPortraitRef = source.DefaultPortraitRef;
        document.SetPortraitVariants(source.PortraitVariants);
        RegisterOpenDocument(document);
        try { return SaveActor(document); }
        catch { UnregisterOpenDocument(document); throw; }
    }

    public void DeleteActor(string id)
    {
        var current = RequireCurrentProject();
        var document = FindOpenDocument(id);
        EnsureCanDiscardOpenDocument(id, document, "delete");

        var actor = current.Actors.LoadActor(id);
        CurrentActorLifecycle().DeleteOwned(actor.HomeStoryId, id);
        if (document is not null)
        {
            UnregisterOpenDocument(document);
        }
    }

    public ActorDocument SaveActor(ActorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!IsOpenDocument(document))
        {
            throw new ProjectException("The Actor document is not open in the current project.");
        }

        EnsureRegistryIdAvailable(document.Id, document);
        var current = RequireCurrentProject();
        var store = new CanonicalProjectGraphStore(current.ProjectDirectory);
        _ = store.Stories.Load(document.HomeStoryId);
        var membership = store.Memberships.Load(document.HomeStoryId);
        if (membership.ReferencedResources.Actors.Contains(document.Id, StringComparer.Ordinal))
            throw new ProjectException($"Actor '{document.Id}' is already referenced by Story '{document.HomeStoryId}'.");
        var actorPath = current.Actors.GetActorPath(document.Id);
        if (!document.IsNew && !string.Equals(Path.GetFullPath(document.SourcePath!),
                Path.GetFullPath(actorPath), StringComparison.OrdinalIgnoreCase))
            throw new ActorRepositoryException("Persisted Actor identity is immutable; edit its display name instead.");
        if (document.IsNew && File.Exists(actorPath)) throw new ActorCollisionException(document.Id);
        var actorJson = ActorSerializer.Serialize(document.ToResource(), ActorIdPolicy.ExistingResource);
        var owned = membership.OwnedResources;
        if (!owned.Actors.Contains(document.Id, StringComparer.Ordinal)) owned.Actors.Add(document.Id);
        var updated = new CanonicalStoryMembershipManifest(membership.StoryId, owned, membership.ReferencedResources)
        { DisplayOrder = membership.DisplayOrder };
        var membershipJson = CanonicalStoryMembershipSerializer.Serialize(updated);
        var membershipPath = store.Memberships.GetPath(document.HomeStoryId);
        new ProjectFileTransaction(writeFile: (path, bytes) =>
            _atomicFileWriter.Write(path, Encoding.UTF8.GetString(bytes))).Apply(current.ProjectDirectory,
        [
            new(Path.GetRelativePath(current.ProjectDirectory, actorPath),
                File.Exists(actorPath) ? File.ReadAllBytes(actorPath) : null, Encoding.UTF8.GetBytes(actorJson)),
            new(Path.GetRelativePath(current.ProjectDirectory, membershipPath),
                File.ReadAllBytes(membershipPath), Encoding.UTF8.GetBytes(membershipJson)),
        ], () => { _ = ActorSerializer.Deserialize(actorJson); _ = CanonicalStoryMembershipSerializer.Deserialize(membershipJson); });
        document.MarkSaved(actorPath);
        return document;
    }

    public void SaveAll()
    {
        foreach (var document in _openActorDocuments.Values.Where(document => document.IsDirty).ToArray())
        {
            SaveActor(document);
        }
    }

    private ActorDocument RegisterOpenDocument(ActorDocument document)
    {
        if (_openActorDocuments.TryGetValue(document.Id, out var existing) && !ReferenceEquals(existing, document))
        {
            throw new ProjectException($"Actor '{document.Id}' already has an open document.");
        }

        _openActorDocuments[document.Id] = document;
        return document;
    }

    private ActorDocument? FindOpenDocument(string id) =>
        _openActorDocuments.TryGetValue(id, out var document) ? document : null;

    private bool IsOpenDocument(ActorDocument document) =>
        _openActorDocuments.Values.Any(openDocument => ReferenceEquals(openDocument, document));

    private void EnsureRegistryIdAvailable(string id, ActorDocument? except = null)
    {
        if (_openActorDocuments.TryGetValue(id, out var existing) && !ReferenceEquals(existing, except))
        {
            throw new ProjectException($"Actor '{id}' already has an open document.");
        }
    }

    private static void EnsureCanDiscardOpenDocument(
        string id,
        ActorDocument? document,
        string operation)
    {
        if (document?.IsDirty == true)
        {
            throw new ProjectException(
                $"Cannot {operation} Actor '{id}' because its open document has unsaved changes. Save it first.");
        }
    }

    private void UnregisterOpenDocument(ActorDocument document)
    {
        foreach (var key in _openActorDocuments
                     .Where(pair => ReferenceEquals(pair.Value, document))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _openActorDocuments.Remove(key);
        }
    }

    private ProjectSession SetCurrent(string root, ProjectResource resource)
    {
        _openActorDocuments.Clear();
        CurrentProject = new ProjectSession(root, resource, new ActorRepository(root, _atomicFileWriter));
        return CurrentProject;
    }

    private ProjectSession RequireCurrentProject() =>
        CurrentProject ?? throw new ProjectException("No project is open.");

    private void WriteProjectFile(string path, ProjectResource resource)
    {
        var json = JsonSerializer.Serialize(resource, JsonOptions).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        _atomicFileWriter.Write(path, json, temporaryPath => ReadProjectFile(temporaryPath));
    }

    private static ProjectResource ReadProjectFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count()
                || !root.TryGetProperty("schema_version", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var version)
                || version != ProjectResource.CurrentSchemaVersion
                || !root.TryGetProperty("identity_format", out var identity)
                || identity.ValueKind != JsonValueKind.String || identity.GetString() != ProjectResource.CurrentIdentityFormat)
                throw new JsonException("Project requires schema 3 and identity_format story-uid-v1; legacy projects are not migrated.");
            var resource = JsonSerializer.Deserialize<ProjectResource>(text, JsonOptions)
                ?? throw new JsonException("Project JSON root cannot be null.");
            return resource;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new ProjectException($"Could not read project file '{path}'.", exception);
        }
    }

    private static void ValidateProjectIdentity(string id, string displayName, ActorIdPolicy idPolicy)
    {
        if (!ProjectIdentity.IsValid(id))
        {
            throw new ProjectException("项目 ID 必须以英文字母或数字开头，只能包含英文字母、数字、下划线、点或连字符；大小写敏感。");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ProjectException("Project display_name is required.");
        }
    }
}
