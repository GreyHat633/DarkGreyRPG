using System.IO;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed partial class ProjectGraphViewModel
{
    public GraphEditorHostViewModel? CanonicalHost { get; private set; }
    public string ConnectionSummary => CanonicalHost is null ? "" : $"{CanonicalHost.Nodes.Count} 个故事 · {CanonicalHost.Connections.Count} 条连接";
    private bool _savingCanonical;
    private readonly HashSet<string> _referencedStoryIds = new(StringComparer.Ordinal);
    private readonly List<CanonicalStoryLogicConnection> _referencedEdges = [];
    public bool IsReferencedStory(string id) => _referencedStoryIds.Contains(id);

    private void InitializeCanonicalGraph(string? directory)
    {
        var stories = new Dictionary<string, GraphResourceEnvelope>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            var store = new CanonicalProjectGraphStore(directory);
            foreach (var item in new CanonicalStoryDiscoveryService(store).Discover().Items)
                if (item.Story is not null) stories[item.Id] = item.Story;
            foreach (var package in OfflineProviderCatalog.Load(directory).Providers)
            {
                foreach (var resource in package.Resources.Where(r => r.Kind == DgrResourceKind.Story))
                {
                    if (stories.ContainsKey(resource.Id)) { LogicEditorError = $"故事身份重复：{resource.Id}"; continue; }
                    stories[resource.Id] = resource.ReadGraphDefinition()!;
                    _referencedStoryIds.Add(resource.Id);
                }
                if (package.Manifest.RequiredResources.StoryLogicGraph is { } path)
                {
                    using var json = JsonDocument.Parse(package.GetEntry(path));
                    _referencedEdges.AddRange(CanonicalStoryLogicGraphRepository.Parse(json.RootElement).Connections);
                }
            }
        }
        var nodes = Nodes.Select(node => new GraphNode(node.Id, "story", node.DisplayName,
            stories.TryGetValue(node.Id, out var story) ? CanonicalStoryBoundaryProjection.Ports(story) : [])).ToList();
        nodes.AddRange(stories.Values.Where(story => !nodes.Any(node => node.Id == story.Id))
            .Select(story => new GraphNode(story.Id, "story", story.DisplayName + (_referencedStoryIds.Contains(story.Id) ? "（引用 · 只读）" : ""), CanonicalStoryBoundaryProjection.Ports(story))));
        var edges = LogicConnections.Select(edge => edge.Connection).Concat(_referencedEdges).Distinct().ToArray();
        var visibleEdges = edges.Where(edge => stories.ContainsKey(edge.SourceStoryId) && stories.ContainsKey(edge.TargetStoryId)).ToArray();
        if (visibleEdges.Length != edges.Length) LogicEditorError = "引用包存在缺少目标故事的连线，请引用对应故事包。";
        var graph = new GraphDocument(nodes, visibleEdges.Select(edge => new GraphConnection(edge.SourceStoryId, edge.SourcePortId,
                edge.TargetStoryId, edge.TargetPortId, edge.InterfaceKind == "Flow" ? GraphInterfaceKind.Flow : GraphInterfaceKind.Logic)));
        CanonicalHost = new GraphEditorHostViewModel(graph, GraphScope.Project)
        {
            ReadOnlySourcePolicy = IsReferencedCanonicalSource,
        };
        foreach (var node in Nodes) CanonicalHost.SetNodePosition(node.Id, node.X, node.Y);
        var layout = _layoutStore?.Load();
        foreach (var id in _referencedStoryIds)
            if (layout?.Nodes.TryGetValue(id, out var position) == true) CanonicalHost.SetNodePosition(id, position.X, position.Y);
        CanonicalHost.GraphChanged += (_, _) => SaveCanonicalConnections();
        CanonicalHost.LayoutChanged += (_, _) =>
        {
            foreach (var node in CanonicalHost.Nodes)
                Nodes.FirstOrDefault(n => n.Id == node.NodeId)?.SetPosition(node.X, node.Y);
            _layoutStore?.Save(CanonicalHost.Nodes.ToDictionary(node => node.NodeId, node => new ProjectGraphNodeLayout { X = node.X, Y = node.Y }));
        };
    }

    private bool IsReferencedCanonicalSource(string storyId)
    {
        if (!IsReferencedStory(storyId)) return false;
        LogicEditorError = "引用故事的输出连线由源故事包定义；请导入后编辑。可从本地故事接入引用故事输入。";
        return true;
    }

    private void SaveCanonicalConnections()
    {
        if (_savingCanonical || CanonicalHost is null || _storyLogicRepository is null) return;
        _savingCanonical = true;
        try
        {
            var edges = CanonicalHost.Graph.Connections.Select(edge =>
                new CanonicalStoryLogicConnection(edge.FromNodeId, edge.FromPortId, edge.ToNodeId, edge.ToPortId, edge.InterfaceKind.ToString())).ToArray();
            var visibleReferenceEdges = _referencedEdges.Where(edge => CanonicalHost.Nodes.Any(n => n.NodeId == edge.TargetStoryId));
            if (!edges.Where(edge => _referencedStoryIds.Contains(edge.SourceStoryId)).ToHashSet().SetEquals(visibleReferenceEdges))
                throw new CanonicalStoryLogicGraphRepositoryException("story.graph.reference.readonly", "引用故事的输出连线由源故事包定义；请导入后编辑。可从本地故事接入引用故事输入。");
            var saved = _storyLogicRepository.Save(edges.Where(edge => !_referencedStoryIds.Contains(edge.SourceStoryId)));
            ReplaceLogicConnections(saved.Connections);
            LogicEditorError = "";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CanonicalStoryLogicGraphRepositoryException)
        {
            LogicEditorError = exception.Message;
            // A failed atomic file write keeps the previous file and restores the visible graph.
            CanonicalHost.Undo();
        }
        finally { _savingCanonical = false; OnPropertyChanged(nameof(ConnectionSummary)); }
    }
}
