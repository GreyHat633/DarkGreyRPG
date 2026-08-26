using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Projects;

public sealed class ProjectService
{
    private static readonly string[] ProjectDirectories = ["actors", "dialogues", "quests", "stories", "resources"];
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
    private readonly Dictionary<string, DialogueDocument> _openDialogueDocuments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, QuestDocument> _openQuestDocuments = new(StringComparer.Ordinal);

    public ProjectService(IAtomicFileWriter? atomicFileWriter = null)
    {
        _atomicFileWriter = atomicFileWriter ?? new AtomicFileWriter();
    }

    public ProjectSession? CurrentProject { get; private set; }

    public IReadOnlyCollection<ActorDocument> OpenActorDocuments => _openActorDocuments.Values;
    public IReadOnlyCollection<DialogueDocument> OpenDialogueDocuments => _openDialogueDocuments.Values;
    public IReadOnlyCollection<QuestDocument> OpenQuestDocuments => _openQuestDocuments.Values;

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
        };
        WriteProjectFile(projectPath, resource);
        new StoryRepository(root, _atomicFileWriter).SaveStory(StoryResource.CreateUncategorized());
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
        ProjectMigration.MigrateIfRequired(root, _atomicFileWriter);
        resource = ReadProjectFile(projectPath);
        ValidateProjectIdentity(resource.Id, resource.DisplayName, ActorIdPolicy.ExistingResource);

        var actorsDirectory = Path.Combine(root, "actors");
        if (!Directory.Exists(actorsDirectory))
        {
            throw new ProjectException($"Project actors directory does not exist: '{actorsDirectory}'.");
        }

        return SetCurrent(root, resource);
    }

    public void CloseProject(bool discardUnsavedChanges = false)
    {
        if (!discardUnsavedChanges && (_openActorDocuments.Values.Any(document => document.IsDirty)
            || _openDialogueDocuments.Values.Any(document => document.IsDirty)
            || _openQuestDocuments.Values.Any(document => document.IsDirty)))
        {
            throw new ProjectException("The project has unsaved resource documents.");
        }

        _openActorDocuments.Clear();
        _openDialogueDocuments.Clear();
        _openQuestDocuments.Clear();
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
        _ = current.Stories.LoadStory(homeStoryId); // validate before creating the document
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
        var imported = current.Registry.ImportAsNew(sourceId, newId, storyId);
        return RegisterOpenDocument(current.Actors.LoadActor(imported.Id));
    }

    public void AddActorReference(string storyId, string actorId) =>
        RequireCurrentProject().Registry.AddReference(storyId, ProjectResourceType.Actor, actorId);

    public void RemoveActorReference(string storyId, string actorId) =>
        RequireCurrentProject().Registry.RemoveReference(storyId, ProjectResourceType.Actor, actorId);

    public IReadOnlyList<ResourceDescriptor> GetActorReferences(string actorId) =>
        RequireCurrentProject().Registry.GetReferences(ProjectResourceType.Actor, actorId);

    public bool CanDeleteActor(string actorId) =>
        RequireCurrentProject().Registry.CanDelete(ProjectResourceType.Actor, actorId);

    public ActorDocument DuplicateActor(string sourceId)
    {
        var current = RequireCurrentProject();
        var source = current.Actors.LoadActor(sourceId);
        var duplicateId = current.Actors.GetAvailableId(source.Id + "_copy");
        EnsureRegistryIdAvailable(duplicateId);

        var document = current.Actors.DuplicateActor(sourceId);
        current.Registry.AddReference(document.HomeStoryId, ProjectResourceType.Actor, document.Id, owned: true);
        return RegisterOpenDocument(document);
    }

    public ActorDocument RenameActor(string sourceId, string targetId)
    {
        var current = RequireCurrentProject();
        var sourceDocument = FindOpenDocument(sourceId);
        EnsureCanDiscardOpenDocument(sourceId, sourceDocument, "rename");
        if (!string.Equals(sourceId, targetId, StringComparison.Ordinal))
        {
            EnsureRegistryIdAvailable(targetId);
        }

        var document = current.Actors.RenameActor(sourceId, targetId);
        current.Registry.ReplaceResourceId(ProjectResourceType.Actor, sourceId, targetId);
        if (sourceDocument is not null)
        {
            UnregisterOpenDocument(sourceDocument);
        }

        return RegisterOpenDocument(document);
    }

    public void DeleteActor(string id)
    {
        var current = RequireCurrentProject();
        var document = FindOpenDocument(id);
        EnsureCanDiscardOpenDocument(id, document, "delete");

        var actor = current.Actors.LoadActor(id);
        var references = current.Registry.GetReferences(ProjectResourceType.Actor, id);
        if (references.Count > 0)
        {
            throw new ProjectException(
                $"Cannot delete Actor '{id}' because it is still referenced by: {string.Join(", ", references.Select(reference => reference.Id))}.");
        }

        current.Actors.DeleteActor(id);
        current.Registry.RemoveOwnership(actor.HomeStoryId, ProjectResourceType.Actor, id);
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
        string? previousHomeStoryId = null;
        if (!document.IsNew)
        {
            previousHomeStoryId = current.Actors.LoadActor(document.Id).HomeStoryId;
        }

        // A changed home Story is part of the Actor mutation; reject it before writing the Actor file.
        var targetStory = current.Stories.LoadStory(document.HomeStoryId);
        if (targetStory.ReferencedResources.Actors.Contains(document.Id, StringComparer.Ordinal))
        {
            throw new ProjectException(
                $"Actor '{document.Id}' is already referenced by Story '{targetStory.Id}' and cannot be owned there.");
        }
        var saved = current.Actors.SaveActor(document);
        if (!string.IsNullOrWhiteSpace(previousHomeStoryId)
            && !string.Equals(previousHomeStoryId, saved.HomeStoryId, StringComparison.Ordinal))
        {
            current.Registry.RemoveOwnership(previousHomeStoryId, ProjectResourceType.Actor, saved.Id);
        }
        current.Registry.AddReference(saved.HomeStoryId, ProjectResourceType.Actor, saved.Id, owned: true);
        UnregisterOpenDocument(document);
        return RegisterOpenDocument(saved);
    }

    public DialogueDocument OpenDialogue(string id)
    { if (_openDialogueDocuments.TryGetValue(id, out var existing)) return existing; return RegisterOpenDialogue(RequireCurrentProject().Dialogues.LoadDialogue(id)); }
    public DialogueDocument CreateDialogue(string id, string displayName) => RegisterOpenDialogue(RequireCurrentProject().Dialogues.CreateDialogue(id, displayName));
    public DialogueDocument CreateDialogueInStory(string storyId, string id, string displayName)
    { var current = RequireCurrentProject(); _ = current.Stories.LoadStory(storyId); var doc = current.Dialogues.CreateDialogue(id, displayName); doc.HomeStoryId = storyId; current.Dialogues.SaveDialogue(doc); current.Registry.AddReference(storyId, ProjectResourceType.Dialogue, id, owned: true); return RegisterOpenDialogue(doc); }
    public DialogueDocument ImportDialogueAsNew(string sourceId, string newId, string storyId)
    { var current = RequireCurrentProject(); var imported = current.Registry.ImportAsNew(ProjectResourceType.Dialogue, sourceId, newId, storyId); return RegisterOpenDialogue(current.Dialogues.LoadDialogue(((DialogueResource)imported).Id)); }
    public DialogueDocument SaveDialogue(DialogueDocument document)
    { ArgumentNullException.ThrowIfNull(document); if (!_openDialogueDocuments.Values.Contains(document)) throw new ProjectException("The Dialogue document is not open in the current project."); var current = RequireCurrentProject(); _ = current.Stories.LoadStory(document.HomeStoryId); var old = document.IsNew ? null : current.Dialogues.LoadDialogue(document.Id).HomeStoryId; var saved = current.Dialogues.SaveDialogue(document); if (!string.IsNullOrWhiteSpace(old) && old != saved.HomeStoryId) current.Registry.RemoveOwnership(old, ProjectResourceType.Dialogue, saved.Id); current.Registry.AddReference(saved.HomeStoryId, ProjectResourceType.Dialogue, saved.Id, owned: true); return RegisterOpenDialogue(saved); }
    public void AddDialogueReference(string storyId, string dialogueId) => RequireCurrentProject().Registry.AddReference(storyId, ProjectResourceType.Dialogue, dialogueId);
    public void RemoveDialogueReference(string storyId, string dialogueId) => RequireCurrentProject().Registry.RemoveReference(storyId, ProjectResourceType.Dialogue, dialogueId);
    public IReadOnlyList<ResourceDescriptor> GetDialogueReferences(string id) => RequireCurrentProject().Registry.GetReferences(ProjectResourceType.Dialogue, id);
    public bool CanDeleteDialogue(string id) => RequireCurrentProject().Registry.CanDelete(ProjectResourceType.Dialogue, id);
    public void DeleteDialogue(string id) { var c = RequireCurrentProject(); EnsureClean(_openDialogueDocuments.TryGetValue(id, out var d) ? d : null, id, "delete"); if (!c.Registry.CanDelete(ProjectResourceType.Dialogue, id)) throw new ProjectException($"Cannot delete Dialogue '{id}' because it is still referenced."); var r = c.Dialogues.LoadDialogue(id).ToResource(); c.Dialogues.DeleteDialogue(id); c.Registry.RemoveOwnership(r.HomeStoryId, ProjectResourceType.Dialogue, id); if (d is not null) _openDialogueDocuments.Remove(id); }

    public QuestDocument OpenQuest(string id)
    { if (_openQuestDocuments.TryGetValue(id, out var existing)) return existing; return RegisterOpenQuest(RequireCurrentProject().Quests.LoadQuest(id)); }
    public QuestDocument CreateQuest(string id, string displayName) => RegisterOpenQuest(RequireCurrentProject().Quests.CreateQuest(id, displayName));
    public QuestDocument CreateQuestInStory(string storyId, string id, string displayName)
    { var current = RequireCurrentProject(); _ = current.Stories.LoadStory(storyId); var doc = current.Quests.CreateQuest(id, displayName); doc.HomeStoryId = storyId; current.Quests.SaveQuest(doc); current.Registry.AddReference(storyId, ProjectResourceType.Quest, id, owned: true); return RegisterOpenQuest(doc); }
    public QuestDocument ImportQuestAsNew(string sourceId, string newId, string storyId)
    { var c = RequireCurrentProject(); var imported = c.Registry.ImportAsNew(ProjectResourceType.Quest, sourceId, newId, storyId); return RegisterOpenQuest(c.Quests.LoadQuest(((QuestResource)imported).Id)); }
    public QuestDocument SaveQuest(QuestDocument document)
    { ArgumentNullException.ThrowIfNull(document); if (!_openQuestDocuments.Values.Contains(document)) throw new ProjectException("The Quest document is not open in the current project."); var c = RequireCurrentProject(); _ = c.Stories.LoadStory(document.HomeStoryId); var old = document.IsNew ? null : c.Quests.LoadQuest(document.Id).HomeStoryId; var saved = c.Quests.SaveQuest(document); if (!string.IsNullOrWhiteSpace(old) && old != saved.HomeStoryId) c.Registry.RemoveOwnership(old, ProjectResourceType.Quest, saved.Id); c.Registry.AddReference(saved.HomeStoryId, ProjectResourceType.Quest, saved.Id, owned: true); return RegisterOpenQuest(saved); }
    public void AddQuestReference(string storyId, string questId) => RequireCurrentProject().Registry.AddReference(storyId, ProjectResourceType.Quest, questId);
    public void RemoveQuestReference(string storyId, string questId) => RequireCurrentProject().Registry.RemoveReference(storyId, ProjectResourceType.Quest, questId);
    public IReadOnlyList<ResourceDescriptor> GetQuestReferences(string id) => RequireCurrentProject().Registry.GetReferences(ProjectResourceType.Quest, id);
    public bool CanDeleteQuest(string id) => RequireCurrentProject().Registry.CanDelete(ProjectResourceType.Quest, id);
    public void DeleteQuest(string id) { var c = RequireCurrentProject(); EnsureClean(_openQuestDocuments.TryGetValue(id, out var d) ? d : null, id, "delete"); if (!c.Registry.CanDelete(ProjectResourceType.Quest, id)) throw new ProjectException($"Cannot delete Quest '{id}' because it is still referenced."); var r = c.Quests.LoadQuest(id).ToResource(); c.Quests.DeleteQuest(id); c.Registry.RemoveOwnership(r.HomeStoryId, ProjectResourceType.Quest, id); if (d is not null) _openQuestDocuments.Remove(id); }

    public void AddReference(string storyId, ProjectResourceType type, string resourceId) => RequireCurrentProject().Registry.AddReference(storyId, type, resourceId);
    public void RemoveReference(string storyId, ProjectResourceType type, string resourceId) => RequireCurrentProject().Registry.RemoveReference(storyId, type, resourceId);
    public IReadOnlyList<ResourceDescriptor> GetReferences(ProjectResourceType type, string resourceId) => RequireCurrentProject().Registry.GetReferences(type, resourceId);
    public bool CanDelete(ProjectResourceType type, string resourceId) => RequireCurrentProject().Registry.CanDelete(type, resourceId);

    public void SaveAll()
    {
        foreach (var document in _openActorDocuments.Values.Where(document => document.IsDirty).ToArray())
        {
            SaveActor(document);
        }
        foreach (var document in _openDialogueDocuments.Values.Where(document => document.IsDirty).ToArray()) SaveDialogue(document);
        foreach (var document in _openQuestDocuments.Values.Where(document => document.IsDirty).ToArray()) SaveQuest(document);
    }

    private DialogueDocument RegisterOpenDialogue(DialogueDocument document) { if (_openDialogueDocuments.TryGetValue(document.Id, out var existing) && !ReferenceEquals(existing, document)) throw new ProjectException($"Dialogue '{document.Id}' already has an open document."); _openDialogueDocuments[document.Id] = document; return document; }
    private QuestDocument RegisterOpenQuest(QuestDocument document) { if (_openQuestDocuments.TryGetValue(document.Id, out var existing) && !ReferenceEquals(existing, document)) throw new ProjectException($"Quest '{document.Id}' already has an open document."); _openQuestDocuments[document.Id] = document; return document; }
    private static void EnsureClean<T>(T? document, string id, string operation) where T : class { var dirty = document switch { DialogueDocument d => d.IsDirty, QuestDocument q => q.IsDirty, _ => false }; if (dirty) throw new ProjectException($"Cannot {operation} resource '{id}' because its open document has unsaved changes. Save it first."); }

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
        var registry = new ProjectResourceRegistry(root, _atomicFileWriter);
        CurrentProject = new ProjectSession(
            root,
            resource,
            registry.Actors,
            registry.Stories,
            registry.Dialogues,
            registry.Quests,
            registry);
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
            var resource = JsonSerializer.Deserialize<ProjectResource>(File.ReadAllText(path), JsonOptions)
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
        var idIssues = ActorValidator.ValidateId(id, idPolicy);
        var errors = idIssues.Where(issue => issue.Severity == ValidationSeverity.Error).ToArray();
        if (errors.Length > 0)
        {
            throw new ProjectException(string.Join(" ", errors.Select(issue => issue.Message)));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ProjectException("Project display_name is required.");
        }
    }
}
