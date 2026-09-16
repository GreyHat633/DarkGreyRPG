using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using System.IO;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed class CanonicalGraphClipboard
{
    private string? _project;
    public GraphClipboardSnapshot? Nodes { get; private set; }
    public GraphClipboardSnapshot? Parameters { get; private set; }
    public IReadOnlyDictionary<string, GraphEditorNodePosition> Layout { get; private set; } = new Dictionary<string, GraphEditorNodePosition>();
    public sealed record Resource(GraphResourceEnvelope Envelope, IReadOnlyDictionary<string, GraphEditorNodePosition> Layout,
        IReadOnlyDictionary<string, byte[]?>? ScreenMetadata = null);
    public IReadOnlyDictionary<string, byte[]?> NodeScreenMetadata { get; private set; } = new Dictionary<string, byte[]?>();
    public IReadOnlyDictionary<string, byte[]?> ParameterScreenMetadata { get; private set; } = new Dictionary<string, byte[]?>();
    private static IReadOnlyDictionary<string, byte[]?> ReadScreenMetadata(GraphEditorHostViewModel host, IEnumerable<GraphNode> nodes, string? root)
        => nodes.Where(n => n.Type == "screen").ToDictionary(n => n.Id, n =>
        {
            var path = root is null ? null : ScreenLayerEditorState.MetadataPath(root, host.AuthoringResourceKey, n.Id);
            return path is not null && File.Exists(path) ? File.ReadAllBytes(path) : null;
        });
    public IReadOnlyDictionary<(GraphResourceKind, string), Resource> NodeResources { get; private set; } = new Dictionary<(GraphResourceKind, string), Resource>();
    public IReadOnlyDictionary<(GraphResourceKind, string), Resource> ParameterResources { get; private set; } = new Dictionary<(GraphResourceKind, string), Resource>();
    public void CaptureResources(CanonicalStoryWorkspaceViewModel workspace, bool parameters)
    {
        var snapshot = parameters ? Parameters : Nodes;
        var screenMetadata = ReadScreenMetadata(workspace.ActiveGraphHost, snapshot?.Read().Nodes ?? [], workspace.MediaProjectDirectory);
        var result = new Dictionary<(GraphResourceKind, string), Resource>();
        foreach (var node in snapshot?.Read().Nodes ?? [])
        {
            if (node.Type is not ("session" or "task")) continue;
            var id = node.Properties["resource_id"].GetString()!;
            var kind = node.Type == "session" ? GraphResourceKind.Session : GraphResourceKind.Task;
            var editor = workspace.Editors.Single(e => e.Id == id && e.ResourceKind == kind);
            result[(kind, id)] = new(editor.CreateSnapshot(), editor.CreateLayoutSnapshot(), ReadScreenMetadata(editor.Host, editor.Host.Graph.Nodes, workspace.MediaProjectDirectory));
        }
        if (parameters) { ParameterResources = result; ParameterScreenMetadata = screenMetadata; }
        else { NodeResources = result; NodeScreenMetadata = screenMetadata; }
    }
    public int PasteCount { get; set; }
    public void CopyFrom(CanonicalStoryWorkspaceViewModel workspace, IEnumerable<string> ids, bool parameters)
    {
        var draft = new CanonicalGraphClipboard();
        if (parameters) draft.CopyParameters(workspace.ActiveGraphHost, ids.Single());
        else draft.CopyNodes(workspace.ActiveGraphHost, ids);
        draft.CaptureResources(workspace, parameters);
        if (parameters) { Parameters = draft.Parameters; ParameterResources = draft.ParameterResources; ParameterScreenMetadata = draft.ParameterScreenMetadata; }
        else { Nodes = draft.Nodes; NodeResources = draft.NodeResources; NodeScreenMetadata = draft.NodeScreenMetadata; Layout = draft.Layout; PasteCount = 0; }
    }
    public void SetProject(string? project)
    {
        if (string.Equals(_project, project, StringComparison.OrdinalIgnoreCase)) return;
        _project = project; NodeResources = new Dictionary<(GraphResourceKind, string), Resource>(); ParameterResources = new Dictionary<(GraphResourceKind, string), Resource>(); Nodes = null; Parameters = null; Layout = new Dictionary<string, GraphEditorNodePosition>(); PasteCount = 0;
        NodeScreenMetadata = new Dictionary<string, byte[]?>(); ParameterScreenMetadata = new Dictionary<string, byte[]?>();
    }
    public void CopyNodes(GraphEditorHostViewModel host, IEnumerable<string> ids)
    {
        var selected = ids.ToHashSet(StringComparer.Ordinal);
        Nodes = new(host.Scope, host.Graph, selected);
        Layout = host.Nodes.Where(n => selected.Contains(n.NodeId)).ToDictionary(n => n.NodeId, n => n.Position);
        PasteCount = 0;
    }
    public void CopyParameters(GraphEditorHostViewModel host, string id) => Parameters = new(host.Scope, host.Graph, [id]);
}
