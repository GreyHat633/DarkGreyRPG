using System.Text.Json;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.ViewModels.Graph;
namespace DarkGreyRPG.Studio.ViewModels;

public sealed class OfflineReadOnlyResourceViewModel : ObservableObject, IDisposable
{
    private readonly OfflineResourceChoice _root;
    private readonly Stack<OfflineResourceChoice> _history = new();
    public OfflineReadOnlyResourceViewModel(OfflineResourceChoice choice)
    {
        _root = choice ?? throw new ArgumentNullException(nameof(choice));
        Choice = choice;
        BackCommand = new RelayCommand(GoBack, () => _history.Count > 0);
        Load(choice);
    }
    public OfflineResourceChoice Choice { get; private set; }
    public CanonicalGraphResourceEditorViewModel? GraphPreview { get; private set; }
    public GraphEditorHostViewModel? PreviewHost { get; private set; }
    public bool HasGraph => PreviewHost is not null;
    public int InitialTabIndex => HasGraph ? 0 : 1;
    public string Title => HasGraph ? $"[引用] {Choice.DisplayName} · 流程图（只读）" : $"[引用] {Choice.DisplayName}";
    public string ReadOnlySummary => "引用资源为只读，请在来源故事中编辑，或导入为本地资源。";
    public string DefinitionText => Choice.DefinitionJson;
    public RelayCommand BackCommand { get; }
    public bool TryOpenSubgraph(GraphEditorNodeViewModel node)
    {
        var kind = node.Type switch { "story" => "Story", "session" => "Session", "task" => "Task", _ => null };
        if (kind is null) return false;
        var id = kind == "Story" ? node.NodeId : node.Properties.TryGetValue("resource_id", out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (id is null) return false;
        var candidates = _root.RelatedGraphs.Where(resource => resource.Kind == kind && resource.Id == id).ToArray();
        if (candidates.Length != 1) return false;
        _history.Push(Choice);
        Load(candidates[0]);
        return true;
    }
    private void GoBack()
    {
        if (_history.TryPop(out var choice)) Load(choice);
    }
    private void Load(OfflineResourceChoice choice)
    {
        GraphPreview?.Dispose();
        Choice = choice;
        GraphPreview = choice.HasGraph && choice.Kind != "StoryGroup" ? new(GraphResourceEnvelopeSerializer.Deserialize(choice.DefinitionJson)) : null;
        PreviewHost = GraphPreview?.Host;
        if (choice.ContainerGraph is { } container)
        {
            var stories = choice.RelatedGraphs.Where(resource => resource.Kind == "Story")
                .Select(resource => GraphResourceEnvelopeSerializer.Deserialize(resource.DefinitionJson)).ToArray();
            var nodes = stories.Select(story => new GraphNode(story.Id, "story", story.DisplayName, CanonicalStoryBoundaryProjection.Ports(story)));
            var edges = container.Connections.Select(edge => new GraphConnection(edge.SourceStoryId, edge.SourcePortId,
                edge.TargetStoryId, edge.TargetPortId, edge.InterfaceKind == "Flow" ? GraphInterfaceKind.Flow : GraphInterfaceKind.Logic));
            PreviewHost = new GraphEditorHostViewModel(new GraphDocument(nodes, edges), GraphScope.Project);
            var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(stories.Length)));
            for (var index = 0; index < stories.Length; index++) PreviewHost.SetNodePosition(stories[index].Id, index % columns * 360, index / columns * 220);
        }
        OnPropertyChanged(nameof(Choice));
        OnPropertyChanged(nameof(GraphPreview));
        OnPropertyChanged(nameof(PreviewHost));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasGraph));
        BackCommand.RaiseCanExecuteChanged();
    }
    public void Dispose() => GraphPreview?.Dispose();
}
