using System.IO;
using System.Text.Json;
using System.Text;
using DarkGreyRPG.Studio.Core.IO;
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
    public event EventHandler<string?>? StorySelected;
    public void SelectStory(string? storyId)
    {
        SelectOutputs(storyId);
        StorySelected?.Invoke(this, storyId);
    }
    public Func<string, CanonicalGraphResourceEditorViewModel?>? OutputEditorProvider { get; set; }
    private CanonicalGraphResourceEditorViewModel? _outputSource;
    public PublicOutputsViewModel? SelectedOutputs { get; private set; }
    public void SelectOutputs(string? storyId)
    {
        SelectedOutputs?.Dispose();
        if (_outputSource is not null) _outputSource.Host.GraphChanged -= OnOutputSourceChanged;
        _outputSource = null;
        try
        {
            if (storyId is not null && !IsReferencedStory(storyId)) _outputSource = OutputEditorProvider?.Invoke(storyId);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException
            or GraphResourceRepositoryException or CanonicalStoryMembershipRepositoryException or GraphResourceEnvelopeException)
        {
            LogicEditorError = "无法读取故事输出端口：" + exception.Message;
        }
        SelectedOutputs = _outputSource is null ? null : new PublicOutputsViewModel(_outputSource.Host, false, CanonicalHost);
        if (_outputSource is not null) _outputSource.Host.GraphChanged += OnOutputSourceChanged;
        OnPropertyChanged(nameof(SelectedOutputs));
    }
    private void OnOutputSourceChanged(object? sender, EventArgs args)
    {
        if (_outputSource is not null) RefreshStoryBoundary(_outputSource.CreatePersistenceSnapshot());
    }

    public GraphEditorHostViewModel? CanonicalHost { get; private set; }
    public Action<double, double>? CreateStoryRequested { get; set; }
    public Func<IReadOnlyCollection<string>, bool>? DeleteStoryGroupsRequested { get; set; }
    public string ConnectionSummary => CanonicalHost is null ? "" : $"{CanonicalHost.Nodes.Count} 个故事 · {CanonicalHost.Connections.Count} 条连接";
    private bool _savingCanonical;
    private Func<bool>? _saveBoundarySource;
    private StoryGroupNameStore? _groupNameStore;
    private StoryGraphPresentationStore? _presentationStore;
    public StoryGraphPresentation Presentation { get; private set; } = StoryGraphPresentation.Empty;
    public void MoveNavigationEntry(string key, string target, bool afterTarget = false)
    {
        if (CanonicalHost is null || key == target) return;
        var order = Presentation.NavigationOrder.Concat(StoryGroups.Groups.Select(g => g.Key))
            .Concat(StoryGroups.Singles).Distinct(StringComparer.Ordinal).ToList();
        if (!order.Remove(key) || !order.Contains(target)) return;
        order.Insert(order.IndexOf(target) + (afterTarget ? 1 : 0), key);
        if (order.SequenceEqual(Presentation.NavigationOrder)) return;
        var before = Presentation;
        var after = before with { NavigationOrder = order.ToArray() };
        CanonicalHost.EditMetadata(() => SavePresentation(before), () => SavePresentation(after));
    }
    private void SavePresentation(StoryGraphPresentation state)
    {
        _presentationStore?.Save(state);
        Presentation = state;
        OnPropertyChanged(nameof(Presentation));
    }
    private string? _groupProjectDirectory;
    public StoryGroupCatalog StoryGroups { get; private set; } = StoryGroupCatalog.Empty;

    public void RenameStoryGroup(string key, string name)
    {
        if (CanonicalHost is null) return;
        var before = StoryGroups;
        var after = before.Rename(key, name);
        CanonicalHost.EditMetadata(() => SaveGroupNames(before), () => SaveGroupNames(after));
    }

    private void SaveGroupNames(StoryGroupCatalog snapshot)
    {
        snapshot = DeriveStoryGroups(snapshot);
        _groupNameStore?.Save(snapshot);
        StoryGroups = snapshot;
        SyncStoryGroupFrames();
        OnPropertyChanged(nameof(StoryGroups));
    }

    private void SyncStoryGroupFrames()
    {
        if (CanonicalHost is null) return;
        var old = CanonicalHost.Frames.ToDictionary(frame => frame.Id, StringComparer.Ordinal);
        CanonicalHost.RestoreFrames(StoryGroups.Groups.Select(group => new GraphCommentFrame(
            group.Key, group.DisplayName, 0, 0, 80, 50, group.Members.ToArray())
        {
            Color = old.GetValueOrDefault(group.Key)?.Color
                ?? Presentation.Groups.GetValueOrDefault(group.Key)?.Color ?? "#5CA6CC",
            Collapsed = old.GetValueOrDefault(group.Key)?.Collapsed
                ?? Presentation.Groups.GetValueOrDefault(group.Key)?.Collapsed ?? false
        }));
    }

    private StoryGroupCatalog DeriveStoryGroups(StoryGroupCatalog? previous = null) => StoryGroupCatalog.Derive(
        CanonicalHost!.Graph.Nodes.Select(node => node.Id),
        new CanonicalStoryLogicGraph(2, CanonicalHost.Graph.Connections.Select(edge =>
            new CanonicalStoryLogicConnection(edge.FromNodeId, edge.FromPortId, edge.ToNodeId, edge.ToPortId, edge.InterfaceKind.ToString())).ToArray()), previous ?? StoryGroups);

    public void RefreshStoryBoundary(GraphResourceEnvelope story, Func<bool>? saveBoundarySource = null)
    {
        if (CanonicalHost?.Graph.Nodes.FirstOrDefault(node => node.Id == story.Id) is not { } node) return;
        node.Ports = CanonicalStoryBoundaryProjection.Ports(story).ToList();
        node.DisplayName = story.DisplayName;
        bool Valid(GraphConnection edge) =>
            (edge.FromNodeId != story.Id || node.Ports.Any(port => !port.IsInput && port.Id == edge.FromPortId && port.InterfaceKind == edge.InterfaceKind))
            && (edge.ToNodeId != story.Id || node.Ports.Any(port => port.IsInput && port.Id == edge.ToPortId && port.InterfaceKind == edge.InterfaceKind));
        CanonicalHost.Graph.Connections.RemoveAll(edge => !Valid(edge));
        // Restore still-persisted edges when an internal boundary removal was undone.
        if (_storyLogicRepository is not null)
            foreach (var edge in _storyLogicRepository.Load().Connections.Where(edge => edge.SourceStoryId == story.Id || edge.TargetStoryId == story.Id))
            {
                var connection = new GraphConnection(edge.SourceStoryId, edge.SourcePortId, edge.TargetStoryId, edge.TargetPortId,
                    edge.InterfaceKind == "Flow" ? GraphInterfaceKind.Flow : GraphInterfaceKind.Logic);
                if (Valid(connection) && !CanonicalHost.Graph.Connections.Contains(connection)) CanonicalHost.Graph.Connections.Add(connection);
            }
        _saveBoundarySource = saveBoundarySource;
        CanonicalHost.RefreshProjectedStoryBoundary(story.Id);
        OnPropertyChanged(nameof(ConnectionSummary));
    }
    private readonly HashSet<string> _referencedStoryIds = new(StringComparer.Ordinal);
    private readonly List<CanonicalStoryLogicConnection> _referencedEdges = [];
    private readonly HashSet<string> _knownReferencedStoryIds = new(StringComparer.Ordinal);
    private GraphResourceEnvelope[] _referencedDefinitions = [];
    public bool IsReferencedStory(string id) => _referencedStoryIds.Contains(id);

    public void RefreshReferencedStories()
    {
        if (CanonicalHost is null || string.IsNullOrWhiteSpace(_groupProjectDirectory)) return;
        var catalog = OfflineProviderCatalog.Load(_groupProjectDirectory);
        if (catalog.Diagnostics.Count != 0) throw new InvalidOperationException(string.Join("; ", catalog.Diagnostics.Select(issue => issue.Message)));
        var providers = catalog.Providers;
        var resources = providers.SelectMany(package => package.Resources)
            .Where(resource => resource.Kind == DgrResourceKind.Story).ToArray();
        var nextIds = resources.Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
        var nativeIds = CanonicalHost.Graph.Nodes.Select(node => node.Id)
            .Where(id => !_knownReferencedStoryIds.Contains(id) && !_referencedStoryIds.Contains(id)).ToHashSet(StringComparer.Ordinal);
        if (nextIds.Overlaps(nativeIds)) throw new InvalidOperationException("引用故事身份与本地故事重复。");
        var nextEdges = new List<CanonicalStoryLogicConnection>();
        foreach (var package in providers)
        {
            if (package.Manifest.RequiredResources.StoryLogicGraph is { } path)
            {
                using var json = JsonDocument.Parse(package.GetEntry(path));
                nextEdges.AddRange(CanonicalStoryLogicGraphRepository.Parse(json.RootElement).Connections);
            }
            nextEdges.AddRange(package.ContainerConnections.Connections);
        }
        _knownReferencedStoryIds.UnionWith(_referencedStoryIds);
        _knownReferencedStoryIds.UnionWith(nextIds);
        _referencedStoryIds.Clear(); _referencedStoryIds.UnionWith(nextIds);
        _referencedEdges.Clear(); _referencedEdges.AddRange(nextEdges.Distinct());
        _referencedDefinitions = resources.Select(resource => resource.ReadGraphDefinition()!).ToArray();
        ReconcileReferencedProjection(_referencedDefinitions);
        // Reference removal leaves unresolved native edges on disk. Restore them when
        // the provider returns, without replaying or replacing graph edit history.
        foreach (var edge in ReadPersistedNativeEdges().Where(edge => nextIds.Contains(edge.TargetStoryId)
                     && nativeIds.Contains(edge.SourceStoryId)))
        {
            var connection = new GraphConnection(edge.SourceStoryId, edge.SourcePortId, edge.TargetStoryId, edge.TargetPortId,
                edge.InterfaceKind == "Flow" ? GraphInterfaceKind.Flow : GraphInterfaceKind.Logic);
            if (!CanonicalHost.Graph.Connections.Contains(connection)) CanonicalHost.Graph.Connections.Add(connection);
        }
        CanonicalHost.Refresh();
        StoryGroups = DeriveStoryGroups();
        SyncStoryGroupFrames();
        OnPropertyChanged(nameof(StoryGroups));
        OnPropertyChanged(nameof(ConnectionSummary));
    }

    private IReadOnlyList<CanonicalStoryLogicConnection> ReadPersistedNativeEdges()
    {
        if (_storyLogicRepository is null || !File.Exists(_storyLogicRepository.Path)) return [];
        using var json = JsonDocument.Parse(File.ReadAllText(_storyLogicRepository.Path));
        return CanonicalStoryLogicGraphRepository.Parse(json.RootElement).Connections;
    }

    private void ReconcileReferencedProjection(IReadOnlyList<GraphResourceEnvelope> stories)
    {
        var graph = CanonicalHost!.Graph;
        graph.Nodes.RemoveAll(node => _knownReferencedStoryIds.Contains(node.Id));
        graph.Nodes.AddRange(stories.Select(story => new GraphNode(story.Id, "story", story.DisplayName + "（引用 · 只读）",
            CanonicalStoryBoundaryProjection.Ports(story))));
        var ids = graph.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        graph.Connections.RemoveAll(edge => _knownReferencedStoryIds.Contains(edge.FromNodeId)
            || !ids.Contains(edge.FromNodeId) || !ids.Contains(edge.ToNodeId));
        graph.Connections.AddRange(_referencedEdges.Where(edge => ids.Contains(edge.SourceStoryId) && ids.Contains(edge.TargetStoryId))
            .Select(edge => new GraphConnection(edge.SourceStoryId, edge.SourcePortId, edge.TargetStoryId, edge.TargetPortId,
                edge.InterfaceKind == "Flow" ? GraphInterfaceKind.Flow : GraphInterfaceKind.Logic)));
    }

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
                _referencedEdges.AddRange(package.ContainerConnections.Connections.Where(edge => !_referencedEdges.Contains(edge)));
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
        _knownReferencedStoryIds.UnionWith(_referencedStoryIds);
        _referencedDefinitions = stories.Values.Where(story => _referencedStoryIds.Contains(story.Id)).ToArray();
        _groupNameStore = string.IsNullOrWhiteSpace(directory) ? null : new StoryGroupNameStore(directory);
        _presentationStore = string.IsNullOrWhiteSpace(directory) ? null : new StoryGraphPresentationStore(directory);
        Presentation = _presentationStore?.Load() ?? StoryGraphPresentation.Empty;
        _groupProjectDirectory = directory;
        try { StoryGroups = _groupNameStore?.Load() ?? StoryGroupCatalog.Empty; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { LogicEditorError = exception.Message; }
        StoryGroups = DeriveStoryGroups();
        CanonicalHost.CaptureGraphMetadata = () => StoryGroups;
        CanonicalHost.RestoreGraphMetadata = snapshot =>
        {
            if (snapshot is StoryGroupCatalog groups)
            {
                StoryGroups = DeriveStoryGroups(groups);
                SyncStoryGroupFrames();
        OnPropertyChanged(nameof(StoryGroups));
            }
        };
        foreach (var node in Nodes) CanonicalHost.SetNodePosition(node.Id, node.X, node.Y);
        var layout = _layoutStore?.Load();
        foreach (var id in _referencedStoryIds)
            if (layout?.Nodes.TryGetValue(id, out var position) == true) CanonicalHost.SetNodePosition(id, position.X, position.Y);
        CanonicalHost.CommitGraphChange = SaveCanonicalConnections;
        CanonicalHost.GraphChanged += (_, _) => OnPropertyChanged(nameof(ConnectionSummary));
        var frameStore = string.IsNullOrWhiteSpace(directory) ? null : new CanonicalGraphLayoutStore(directory);
        // Project frames are derived components, never restored manual groups.
        CanonicalHost.RestoreFrames(StoryGroups.Groups.Select(group => new GraphCommentFrame(
            group.Key, group.DisplayName, 0, 0, 80, 50, group.Members.ToArray())
            { Color = Presentation.Groups.GetValueOrDefault(group.Key)?.Color ?? "#5CA6CC",
                Collapsed = Presentation.Groups.GetValueOrDefault(group.Key)?.Collapsed ?? false }));
        CanonicalHost.LayoutChanged += (_, _) =>
        {
            foreach (var node in CanonicalHost.Nodes)
                Nodes.FirstOrDefault(n => n.Id == node.NodeId)?.SetPosition(node.X, node.Y);
            _layoutStore?.Save(CanonicalHost.Nodes.ToDictionary(node => node.NodeId, node => new ProjectGraphNodeLayout { X = node.X, Y = node.Y }));
            var appearance = CanonicalHost.Frames.ToDictionary(frame => frame.Id,
                frame => new StoryGroupAppearance(frame.Color, frame.Collapsed));
            SavePresentation(Presentation with { Groups = appearance });
        };
    }

    private bool IsReferencedCanonicalSource(string storyId)
    {
        if (!IsReferencedStory(storyId)) return false;
        LogicEditorError = "引用故事的输出连线由源故事包定义；请导入后编辑。可从本地故事接入引用故事输入。";
        return true;
    }

    private bool SaveCanonicalConnections()
    {
        if (_savingCanonical || CanonicalHost is null || _storyLogicRepository is null) return true;
        _savingCanonical = true;
        try
        {
            // History snapshots may predate a provider change. Reapply current immutable providers.
            ReconcileReferencedProjection(_referencedDefinitions);
            if (ReadPersistedNativeEdges().Any(edge => _knownReferencedStoryIds.Contains(edge.TargetStoryId)
                    && !_referencedStoryIds.Contains(edge.TargetStoryId)))
                throw new CanonicalStoryLogicGraphRepositoryException("story.graph.reference.unresolved",
                    "已移除引用的故事仍有本地连线；请先恢复引用或处理这些连线，现有连线文件保持不变。");
            // Connecting to an unsaved internal entry must persist its declaration before the edge.
            if (_saveBoundarySource?.Invoke() == false)
            {
                LogicEditorError = "故事修改未能保存，连线未保存。";
                return false;
            }
            var edges = CanonicalHost.Graph.Connections.Select(edge =>
                new CanonicalStoryLogicConnection(edge.FromNodeId, edge.FromPortId, edge.ToNodeId, edge.ToPortId, edge.InterfaceKind.ToString())).ToArray();
            var visibleReferenceEdges = _referencedEdges.Where(edge => CanonicalHost.Nodes.Any(n => n.NodeId == edge.TargetStoryId));
            if (!edges.Where(edge => _referencedStoryIds.Contains(edge.SourceStoryId)).ToHashSet().SetEquals(visibleReferenceEdges))
                throw new CanonicalStoryLogicGraphRepositoryException("story.graph.reference.readonly", "引用故事的输出连线由源故事包定义；请导入后编辑。可从本地故事接入引用故事输入。");
            var groups = DeriveStoryGroups();
            var groupChanges = _groupNameStore is null ? null : new[] { new ProjectFileChange(
                Path.GetRelativePath(_groupProjectDirectory!, _groupNameStore.Path),
                File.Exists(_groupNameStore.Path) ? File.ReadAllBytes(_groupNameStore.Path) : null,
                Encoding.UTF8.GetBytes(StoryGroupNameStore.Serialize(groups))) };
            var saved = _storyLogicRepository.Save(edges.Where(edge => !_referencedStoryIds.Contains(edge.SourceStoryId)), groupChanges);
            ReplaceLogicConnections(saved.Connections);
            StoryGroups = groups;
            SyncStoryGroupFrames();
        OnPropertyChanged(nameof(StoryGroups));
            LogicEditorError = "";
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CanonicalStoryLogicGraphRepositoryException)
        {
            LogicEditorError = exception.Message;
            // The host restores this pending edit before adding it to history.
            return false;
        }
        finally { _savingCanonical = false; OnPropertyChanged(nameof(ConnectionSummary)); }
    }
}
