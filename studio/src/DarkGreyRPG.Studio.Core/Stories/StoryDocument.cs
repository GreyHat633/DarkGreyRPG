using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Stories;

/// <summary>Mutable, UI-independent editing facade for a Story Flow resource.</summary>
public sealed class StoryDocument : INotifyPropertyChanged
{
    private StoryResource _resource;
    private StoryResource? _saved;
    private bool _isNew;
    private string? _sourcePath;
    private IReadOnlyList<ValidationIssue> _issues = [];

    private StoryDocument(StoryResource resource, string? sourcePath, bool isNew)
    {
        _resource = Clone(resource);
        _sourcePath = sourcePath is null ? null : Path.GetFullPath(sourcePath);
        _isNew = isNew;
        _saved = isNew ? null : Clone(resource);
        Revalidate();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Id { get => _resource.Id; set => Change(nameof(Id), Clone(id: value)); }
    public string DisplayName { get => _resource.DisplayName; set => Change(nameof(DisplayName), Clone(displayName: value)); }
    public string Description { get => _resource.Description; set => Change(nameof(Description), Clone(description: value)); }
    public IList<string> Tags => _resource.Tags;
    public StoryEntryPresentation EntryPresentation { get => _resource.EntryPresentation; set => Change(nameof(EntryPresentation), Clone(entryPresentation: value)); }
    public StoryMembership OwnedResources { get => _resource.OwnedResources; set => Change(nameof(OwnedResources), Clone(ownedResources: value)); }
    public StoryMembership ReferencedResources { get => _resource.ReferencedResources; set => Change(nameof(ReferencedResources), Clone(referencedResources: value)); }
    public string FlowRef { get => _resource.FlowRef; set => Change(nameof(FlowRef), Clone(flowRef: value)); }
    public string Title { get => _resource.Title; set => Change(nameof(Title), Clone(title: value)); }
    public string Entry { get => _resource.Entry; set => Change(nameof(Entry), Clone(entry: value)); }
    public IList<StoryNodeResource> Nodes => _resource.Nodes;
    public IList<StoryConnectionResource> Connections => _resource.Connections;
    public StoryMetadata Metadata { get => _resource.Metadata; set => Change(nameof(Metadata), Clone(metadata: value)); }
    public string? SourcePath => _sourcePath;
    public bool IsNew => _isNew;
    public bool IsDirty => _isNew || !SavedContentEquals();
    public IReadOnlyList<ValidationIssue> ValidationIssues => _issues;
    public IReadOnlyList<ValidationIssue> ValidationErrors => _issues.Where(i => i.Severity == ValidationSeverity.Error).ToArray();

    public static StoryDocument CreateNew(string id, string title = "新剧情")
    {
        var start = new StoryNodeResource { Id = "start", Type = "story_start" };
        var end = new StoryNodeResource { Id = "end", Type = "end" };
        return new(new StoryResource { Id = id, DisplayName = title, Title = title, FlowRef = id, Entry = start.Id, Nodes = [start, end], Connections = [new StoryConnectionResource { From = start.Id, Output = "next", To = end.Id }] }, null, true);
    }

    public static StoryDocument FromResource(StoryResource resource, string? sourcePath = null) => new(resource, sourcePath, false);
    public StoryResource ToResource() => Clone(_resource);
    public StoryDocumentSnapshot CreateSnapshot() => new(ToResource());
    public StoryDocumentSnapshot CaptureSnapshot() => CreateSnapshot();
    public StoryResource CreateResourceSnapshot() => ToResource();
    public void RestoreSnapshot(StoryDocumentSnapshot snapshot) { ArgumentNullException.ThrowIfNull(snapshot); Replace(snapshot.ToResource()); }
    public void RestoreSnapshot(StoryResource resource) => Replace(resource);
    public void Replace(StoryResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _resource = Clone(resource);
        Changed(nameof(Nodes));
    }
    public void ReplaceWith(StoryResource resource) => Replace(resource);
    public void ReplaceNodes(IEnumerable<StoryNodeResource> nodes) { _resource = Clone(nodes: nodes); Changed(nameof(Nodes)); }
    public void ReplaceConnections(IEnumerable<StoryConnectionResource> connections) { _resource = Clone(connections: connections); Changed(nameof(Connections)); }

    public void AddNode(StoryNodeResource node) { ArgumentNullException.ThrowIfNull(node); Nodes.Add(Clone(node)); Changed(nameof(Nodes)); }
    public bool RemoveNode(string id)
    {
        var node = Nodes.FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.Ordinal));
        if (node is null) return false;
        Nodes.Remove(node);
        for (var i = Connections.Count - 1; i >= 0; i--)
            if (string.Equals(Connections[i].From, id, StringComparison.Ordinal) || string.Equals(Connections[i].To, id, StringComparison.Ordinal)) Connections.RemoveAt(i);
        Changed(nameof(Nodes));
        return true;
    }
    public bool MoveNode(string id, double x, double y)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == id);
        if (node is null) return false;
        ReplaceNode(node, Clone(node, new StoryNodePosition { X = x, Y = y }));
        return true;
    }
    public bool SetNodePosition(string id, double x, double y) => MoveNode(id, x, y);
    public bool SetNodeProperty(string nodeId, string property, JsonElement value)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node is null || string.IsNullOrWhiteSpace(property)) return false;
        var properties = new Dictionary<string, JsonElement>(node.Properties ?? [], StringComparer.Ordinal) { [property] = value.Clone() };
        ReplaceNode(node, Clone(node, node.Position, properties));
        return true;
    }
    public bool SetNodeProperty<T>(string nodeId, string property, T value) => SetNodeProperty(nodeId, property, JsonSerializer.SerializeToElement(value));
    public bool RemoveNodeProperty(string nodeId, string property)
    {
        var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node?.Properties is null || !node.Properties.Remove(property)) return false;
        ReplaceNode(node, Clone(node, node.Position, node.Properties));
        return true;
    }
    public bool AddConnection(StoryConnectionResource connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (Connections.Any(c => c.From == connection.From && c.Output == connection.Output)) return false;
        Connections.Add(Clone(connection)); Changed(nameof(Connections)); return true;
    }
    public bool AddConnection(string from, string output, string to) => AddConnection(new StoryConnectionResource { From = from, Output = output, To = to });
    public bool Connect(string from, string output, string to) => AddConnection(new StoryConnectionResource { From = from, Output = output, To = to });
    public bool RemoveConnection(StoryConnectionResource connection) => connection is not null && RemoveConnection(connection.From, connection.Output, connection.To);
    public bool RemoveConnection(string from, string output, string to)
    {
        var connection = Connections.FirstOrDefault(c => c.From == from && c.Output == output && c.To == to);
        if (connection is null) return false;
        Connections.Remove(connection); Changed(nameof(Connections)); return true;
    }
    public void SetEntry(string entry) { Entry = entry; }
    public void Revalidate()
    {
        _issues = StoryValidator.Validate(ToResource());
        PropertyChanged?.Invoke(this, new(nameof(ValidationIssues)));
        PropertyChanged?.Invoke(this, new(nameof(ValidationErrors)));
    }

    internal void MarkSaved(string path)
    {
        _sourcePath = Path.GetFullPath(path); _isNew = false; _resource = Clone(_resource); _saved = Clone(_resource); Revalidate();
        PropertyChanged?.Invoke(this, new(nameof(SourcePath))); PropertyChanged?.Invoke(this, new(nameof(IsNew))); PropertyChanged?.Invoke(this, new(nameof(IsDirty)));
    }

    private void ReplaceNode(StoryNodeResource oldNode, StoryNodeResource replacement)
    {
        var index = Nodes.IndexOf(oldNode); if (index < 0) return; Nodes[index] = replacement; Changed(nameof(Nodes));
    }
    private void Change(string propertyName, StoryResource replacement) { _resource = replacement; Changed(propertyName); }
    private void Changed(string propertyName)
    {
        PropertyChanged?.Invoke(this, new(propertyName)); Revalidate(); PropertyChanged?.Invoke(this, new(nameof(IsDirty)));
    }
    private bool SavedContentEquals()
    {
        try { return _saved is not null && string.Equals(StorySerializer.Serialize(_resource), StorySerializer.Serialize(_saved), StringComparison.Ordinal); }
        catch (StoryDataException) { return false; }
    }

    private StoryResource Clone(string? id = null, string? displayName = null, string? description = null, IEnumerable<string>? tags = null, StoryEntryPresentation? entryPresentation = null, StoryMembership? ownedResources = null, StoryMembership? referencedResources = null, string? flowRef = null, string? title = null, string? entry = null, IEnumerable<StoryNodeResource>? nodes = null, IEnumerable<StoryConnectionResource>? connections = null, StoryMetadata? metadata = null)
        => new() { SchemaVersion = _resource.SchemaVersion, Id = id ?? Id, DisplayName = displayName ?? DisplayName, Description = description ?? Description, Tags = [.. (tags ?? Tags)], EntryPresentation = Clone(entryPresentation ?? EntryPresentation), OwnedResources = Clone(ownedResources ?? OwnedResources), ReferencedResources = Clone(referencedResources ?? ReferencedResources), FlowRef = flowRef ?? FlowRef, Title = title ?? Title, Entry = entry ?? Entry, Nodes = [.. (nodes ?? Nodes).Select(Clone)], Connections = [.. (connections ?? Connections).Select(Clone)], Metadata = Clone(metadata ?? Metadata) };

    private static StoryResource Clone(StoryResource r) => new() { SchemaVersion = r.SchemaVersion, Id = r.Id, DisplayName = r.DisplayName, Description = r.Description, Tags = [.. (r.Tags ?? [])], EntryPresentation = Clone(r.EntryPresentation), OwnedResources = Clone(r.OwnedResources), ReferencedResources = Clone(r.ReferencedResources), FlowRef = r.FlowRef, Title = r.Title, Entry = r.Entry, Nodes = [.. (r.Nodes ?? []).Select(Clone)], Connections = [.. (r.Connections ?? []).Select(Clone)], Metadata = Clone(r.Metadata) };
    private static StoryNodeResource Clone(StoryNodeResource n) => Clone(n, n.Position, n.Properties);
    private static StoryNodeResource Clone(StoryNodeResource n, StoryNodePosition? position, IDictionary<string, JsonElement>? properties = null) => new() { Id = n.Id, Type = n.Type, Position = position is null ? new() : new StoryNodePosition { X = position.X, Y = position.Y }, Properties = new Dictionary<string, JsonElement>(properties ?? new Dictionary<string, JsonElement>(), StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value.Clone(), StringComparer.Ordinal) };
    private static StoryConnectionResource Clone(StoryConnectionResource c) => new() { From = c.From, Output = c.Output, To = c.To };
    private static StoryEntryPresentation Clone(StoryEntryPresentation? p) => p is null ? new() : new() { Mode = p.Mode, Eyebrow = p.Eyebrow, Title = p.Title, DurationSeconds = p.DurationSeconds };
    private static StoryMembership Clone(StoryMembership m) => new() { Actors = [.. (m?.Actors ?? [])], Dialogues = [.. (m?.Dialogues ?? [])], Quests = [.. (m?.Quests ?? [])] };
    private static StoryMetadata Clone(StoryMetadata? m) => m is null ? new() : new() { Notes = m.Notes, Tags = [.. (m.Tags ?? [])] };
}

public sealed class StoryDocumentSnapshot
{
    private readonly StoryResource _resource;
    internal StoryDocumentSnapshot(StoryResource resource) => _resource = resource;
    public StoryResource ToResource() => StoryDocument.FromResource(_resource).ToResource();
}
