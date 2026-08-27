using System.ComponentModel;
using System.Runtime.CompilerServices;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Dialogues;

public sealed class DialogueDocument : INotifyPropertyChanged
{
    private DialogueResource _resource; private DialogueResource? _saved; private bool _isNew; private string? _sourcePath; private IReadOnlyList<ValidationIssue> _issues = [];
    private DialogueDocument(DialogueResource resource, string? sourcePath, bool isNew) { _resource = resource; _sourcePath = sourcePath; _isNew = isNew; HasEverBeenSaved = !isNew; _saved = isNew ? null : resource.WithId(resource.Id); Revalidate(); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Id { get => _resource.Id; set { _resource = Copy(id: value); Changed(nameof(Id)); } }
    public string Title { get => _resource.Title; set { _resource = Copy(title: value); Changed(nameof(Title)); } }
    public string DisplayName { get => _resource.DisplayName; set { _resource = Copy(displayName: value); Changed(nameof(DisplayName)); } }
    public string HomeStoryId { get => _resource.HomeStoryId; set { _resource = Copy(homeStoryId: value); Changed(nameof(HomeStoryId)); } }
    public IList<string> Speakers => _resource.Speakers;
    public string Entry { get => _resource.Entry; set { _resource = Copy(entry: value); Changed(nameof(Entry)); } }
    public IList<DialogueNodeResource> Nodes => _resource.Nodes;
    public DialogueMetadata Metadata { get => _resource.Metadata; set { _resource = Copy(metadata: value); Changed(nameof(Metadata)); } }
    public string? SourcePath => _sourcePath; public bool IsNew => _isNew; public bool IsNewDraft => _isNew; public string? DraftOwnerStoryId { get; private set; } public string? SourceTemplateId { get; set; } public bool HasEverBeenSaved { get; private set; } public bool IsDirty => _isNew || !SavedContentEquals(); public IReadOnlyList<ValidationIssue> ValidationIssues => _issues; public IReadOnlyList<ValidationIssue> ValidationErrors => _issues.Where(i => i.Severity == ValidationSeverity.Error).ToArray();
    public static DialogueDocument CreateNew(string id, string title = "新对话") => new(new DialogueResource { Id = id, Title = title, DisplayName = title, HomeStoryId = "uncategorized", Entry = "end", Nodes = [DialogueNodeResource.End("end", "complete")] }, null, true);
    public static DialogueDocument FromResource(DialogueResource resource, string sourcePath) => new(resource, Path.GetFullPath(sourcePath), false);
    public void SetSpeakers(IEnumerable<string> speakers) { _resource = Copy(); _resource.Speakers.Clear(); _resource.Speakers.AddRange(speakers); Changed(nameof(Speakers)); }
    public void ReplaceNodes(IEnumerable<DialogueNodeResource> nodes) { _resource = Copy(); _resource.Nodes.Clear(); _resource.Nodes.AddRange(nodes.Select(n => n.Clone())); Changed(nameof(Nodes)); }
    public void SetMetadata(DialogueMetadata metadata) { _resource = Copy(metadata: metadata); Changed(nameof(Metadata)); }
    public DialogueResource ToResource() => new() { SchemaVersion = 2, Id = Id, Title = Title, DisplayName = DisplayName, HomeStoryId = HomeStoryId, Speakers = [.. Speakers], Entry = Entry, Nodes = Nodes.Select(n => n.Clone()).ToList(), Metadata = Metadata.Clone() };
    public void AddNode(DialogueNodeResource node) { Nodes.Add(node); Changed(nameof(Nodes)); } public bool RemoveNode(string id) { var n = Nodes.FirstOrDefault(x => x.Id == id); if (n is null) return false; Nodes.Remove(n); Changed(nameof(Nodes)); return true; } public void Revalidate() { _issues = DialogueValidator.Validate(ToResource(), _isNew ? Actors.ActorIdPolicy.NewResource : Actors.ActorIdPolicy.ExistingResource); PropertyChanged?.Invoke(this, new(nameof(ValidationIssues))); PropertyChanged?.Invoke(this, new(nameof(ValidationErrors))); }
    internal void SetDraftOwnerStoryId(string storyId) { DraftOwnerStoryId = storyId; PropertyChanged?.Invoke(this, new(nameof(DraftOwnerStoryId))); }
    internal void MarkSaved(string path) { _sourcePath = Path.GetFullPath(path); _isNew = false; HasEverBeenSaved = true; _resource = ToResource(); _saved = _resource.WithId(_resource.Id); Revalidate(); PropertyChanged?.Invoke(this, new(nameof(SourcePath))); PropertyChanged?.Invoke(this, new(nameof(IsNew))); PropertyChanged?.Invoke(this, new(nameof(IsNewDraft))); PropertyChanged?.Invoke(this, new(nameof(HasEverBeenSaved))); PropertyChanged?.Invoke(this, new(nameof(IsDirty))); }
    internal void MarkDraft() { _sourcePath = null; _isNew = true; HasEverBeenSaved = false; _saved = null; Revalidate(); PropertyChanged?.Invoke(this, new(nameof(SourcePath))); PropertyChanged?.Invoke(this, new(nameof(IsNew))); PropertyChanged?.Invoke(this, new(nameof(IsNewDraft))); PropertyChanged?.Invoke(this, new(nameof(HasEverBeenSaved))); PropertyChanged?.Invoke(this, new(nameof(IsDirty))); }
    private DialogueResource Copy(string? id = null, string? title = null, string? displayName = null, string? homeStoryId = null, string? entry = null, DialogueMetadata? metadata = null) => new() { SchemaVersion = _resource.SchemaVersion, Id = id ?? Id, Title = title ?? Title, DisplayName = displayName ?? DisplayName, HomeStoryId = homeStoryId ?? HomeStoryId, Speakers = [.. Speakers], Entry = entry ?? Entry, Nodes = Nodes.Select(n => n.Clone()).ToList(), Metadata = metadata?.Clone() ?? Metadata.Clone() };
    private void Changed(string name) { PropertyChanged?.Invoke(this, new(name)); Revalidate(); PropertyChanged?.Invoke(this, new(nameof(IsDirty))); }
    private bool SavedContentEquals() { try { return _saved is not null && string.Equals(DialogueSerializer.Serialize(_resource, Actors.ActorIdPolicy.ExistingResource), DialogueSerializer.Serialize(_saved, Actors.ActorIdPolicy.ExistingResource), StringComparison.Ordinal); } catch (DialogueException) { return false; } }
}
