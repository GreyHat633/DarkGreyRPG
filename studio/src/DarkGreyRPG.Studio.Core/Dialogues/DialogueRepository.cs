using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Core.Dialogues;

public sealed class DialogueRepository
{
    private readonly IAtomicFileWriter _writer;
    public DialogueRepository(string projectDirectory, IAtomicFileWriter? writer = null) { ProjectDirectory = Path.GetFullPath(projectDirectory); DialoguesDirectory = Path.Combine(ProjectDirectory, "dialogues"); _writer = writer ?? new AtomicFileWriter(); }
    public string ProjectDirectory { get; } public string DialoguesDirectory { get; }
    public IReadOnlyList<DialogueResourceInfo> ListDialogues() { EnsureDirectory(); return Directory.EnumerateFiles(DialoguesDirectory, "*.json").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(path => { var r = DialogueSerializer.Read(path); return new DialogueResourceInfo(r.Id, r.DisplayName, path, [.. r.Speakers]); }).ToArray(); }
    public DialogueDocument LoadDialogue(string id) { ValidateId(id, false); var path = GetPath(id); if (!File.Exists(path)) throw new DialogueNotFoundException(id); return DialogueDocument.FromResource(DialogueSerializer.Read(path), path); }
    public DialogueDocument CreateDialogue() => CreateDialogue(GetAvailableId("new_dialogue"), "新对话");
    public DialogueDocument CreateDialogue(string id, string displayName) { ValidateId(id, true); if (File.Exists(GetPath(id))) throw new DialogueCollisionException(id); return DialogueDocument.CreateNew(id, displayName); }
    public DialogueDocument SaveDialogue(DialogueDocument document) { ArgumentNullException.ThrowIfNull(document); EnsureDirectory(); var resource = document.ToResource(); var json = DialogueSerializer.Serialize(resource, document.IsNew ? ActorIdPolicy.NewResource : ActorIdPolicy.ExistingResource); var path = GetPath(resource.Id); if (document.IsNew && File.Exists(path)) throw new DialogueCollisionException(resource.Id); if (!document.IsNew && !string.Equals(Path.GetFullPath(document.SourcePath ?? string.Empty), path, StringComparison.OrdinalIgnoreCase)) throw new DialogueRepositoryException("Changing a saved Dialogue ID requires an explicit rename operation."); try { _writer.Write(path, json, temp => DialogueSerializer.Deserialize(File.ReadAllText(temp))); } catch (DialogueException) { throw; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new DialogueRepositoryException($"Could not save Dialogue '{resource.Id}'.", ex); } document.MarkSaved(path); return document; }
    public void DeleteDialogue(string id) { ValidateId(id, false); var path = GetPath(id); if (!File.Exists(path)) throw new DialogueNotFoundException(id); try { File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new DialogueRepositoryException($"Could not delete Dialogue '{id}'.", ex); } }
    public string GetAvailableId(string baseId) { ValidateId(baseId, true); if (!File.Exists(GetPath(baseId))) return baseId; for (var n = 2; n < int.MaxValue; n++) { var c = $"{baseId}_{n}"; if (!File.Exists(GetPath(c))) return c; } throw new DialogueRepositoryException($"Could not allocate an available Dialogue ID based on '{baseId}'."); }
    private string GetPath(string id) => Path.Combine(DialoguesDirectory, id + ".json"); private void EnsureDirectory() { if (!Directory.Exists(DialoguesDirectory)) Directory.CreateDirectory(DialoguesDirectory); }
    private static void ValidateId(string id, bool isNew) { var errors = DialogueValidator.ValidateId(id, isNew ? ActorIdPolicy.NewResource : ActorIdPolicy.ExistingResource); if (errors.Any(e => e.Severity == DarkGreyRPG.Studio.Core.Validation.ValidationSeverity.Error)) throw new DialogueValidationException(errors); }
}
