using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed record CanonicalStoryHomeEntry(
    string Id,
    string DisplayName,
    int OwnedActorCount,
    int ReferencedActorCount,
    int SessionCount,
    int TaskCount,
    int FlowNodeCount,
    bool IsComplete,
    bool IsValid,
    IReadOnlyList<string> Diagnostics);

/// <summary>Route-independent summary data for the selected Story on Project Home.</summary>
public sealed class StoryOverviewViewModel : ObservableObject
{
    public StoryOverviewViewModel(StoryResource story)
    {
        Story = story ?? throw new ArgumentNullException(nameof(story));
        Id = story.Id;
        DisplayName = string.IsNullOrWhiteSpace(story.DisplayName) ? story.Title : story.DisplayName;
        Description = story.Description;
        Tags = story.Tags;
        OwnedActorCount = story.OwnedResources.Actors.Count;
        ReferencedActorCount = story.ReferencedResources.Actors.Count;
        DialogueCount = story.OwnedResources.Dialogues.Count + story.ReferencedResources.Dialogues.Count;
        QuestCount = story.OwnedResources.Quests.Count + story.ReferencedResources.Quests.Count;
        FlowNodeCount = story.Nodes.Count;
        MembershipSummary =
            $"{OwnedActorCount} 个本故事角色 · {ReferencedActorCount} 个引用角色 · {DialogueCount} 个对话 · {QuestCount} 个任务";
    }

    public StoryOverviewViewModel(CanonicalStoryHomeEntry story)
    {
        ArgumentNullException.ThrowIfNull(story);
        Id = story.Id;
        DisplayName = string.IsNullOrWhiteSpace(story.DisplayName) ? story.Id : story.DisplayName;
        Description = story.IsValid && story.IsComplete
            ? "0.3.1.0 新格式故事"
            : string.Join(Environment.NewLine, story.Diagnostics);
        Tags = [];
        OwnedActorCount = story.OwnedActorCount;
        ReferencedActorCount = story.ReferencedActorCount;
        DialogueCount = story.SessionCount;
        QuestCount = story.TaskCount;
        FlowNodeCount = story.FlowNodeCount;
        MembershipSummary =
            $"{OwnedActorCount} 个本故事角色 · {ReferencedActorCount} 个引用角色 · {DialogueCount} 个会话 · {QuestCount} 个任务";
    }

    public StoryResource? Story { get; }
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public IReadOnlyList<string> Tags { get; }
    public int OwnedActorCount { get; }
    public int ReferencedActorCount { get; }
    public int DialogueCount { get; }
    public int QuestCount { get; }
    public int FlowNodeCount { get; }
    public string MembershipSummary { get; }
}

public sealed class StoryListItemViewModel
{
    public StoryListItemViewModel(StoryResource story)
    {
        Story = story ?? throw new ArgumentNullException(nameof(story));
        Overview = new StoryOverviewViewModel(Story);
        HasLegacyStory = true;
    }

    public StoryListItemViewModel(CanonicalStoryHomeEntry story, StoryResource? legacyStory = null)
    {
        CanonicalStory = story ?? throw new ArgumentNullException(nameof(story));
        Story = legacyStory;
        Overview = new StoryOverviewViewModel(story);
        HasLegacyStory = legacyStory is not null;
        HasCanonicalStory = true;
    }

    public StoryResource? Story { get; }
    public CanonicalStoryHomeEntry? CanonicalStory { get; }
    public StoryOverviewViewModel Overview { get; }
    public bool HasLegacyStory { get; }
    public bool HasCanonicalStory { get; }
    public bool IsCanonicalOnly => HasCanonicalStory && !HasLegacyStory;
    public bool CanDeleteLegacyStory => HasLegacyStory && !HasCanonicalStory;
    public string Id => Overview.Id;
    public string DisplayName => Overview.DisplayName;
    public string Description => Overview.Description;
    public IReadOnlyList<string> Tags => Overview.Tags;
    public string TagsText => HasCanonicalStory
        ? CanonicalStory!.IsValid && CanonicalStory.IsComplete
            ? "新格式"
            : "新格式 · 数据不完整"
        : string.Join(", ", Tags);
    public int ActorCount => Overview.OwnedActorCount + Overview.ReferencedActorCount;
    public int DialogueCount => Overview.DialogueCount;
    public int QuestCount => Overview.QuestCount;
    public string MembershipSummary => Overview.MembershipSummary;
    public int FlowNodeCount => Overview.FlowNodeCount;
}

public sealed class ProjectGraphNodeViewModel : ObservableObject
{
    private double _x;
    private double _y;
    private bool _isVisible = true;

    public ProjectGraphNodeViewModel(string id, string displayName, bool isHomeStory, bool isIsolated, bool hasWarning, string warningText)
    { Id = id; DisplayName = displayName; IsHomeStory = isHomeStory; IsIsolated = isIsolated; HasWarning = hasWarning; WarningText = warningText; }

    public string Id { get; }
    public string DisplayName { get; }
    public bool IsHomeStory { get; }
    public bool IsIsolated { get; }
    public bool HasWarning { get; }
    public string WarningText { get; }
    public double X { get => _x; private set => SetProperty(ref _x, value); }
    public double Y { get => _y; private set => SetProperty(ref _y, value); }
    public bool IsVisible { get => _isVisible; internal set => SetProperty(ref _isVisible, value); }
    internal void SetPosition(double x, double y) { X = x; Y = y; }
}

/// <summary>A single EnterStory transition retained inside an aggregate project-graph edge.</summary>
public sealed record ProjectGraphTransitionViewModel(
    string NodeId,
    IReadOnlyList<string> IncomingBranchOutputs)
{
    public string EnterStoryNodeId => NodeId;
    public IReadOnlyList<string> IncomingOutputs => IncomingBranchOutputs;
    public IReadOnlyList<string> BranchReasons => IncomingBranchOutputs;
    public IReadOnlyList<string> IncomingBranchReasons => IncomingBranchOutputs;
    public string IncomingBranchOutput => string.Join(", ", IncomingBranchOutputs);
    public string BranchReason => IncomingBranchOutput;
    public string Tooltip => IncomingBranchOutputs.Count == 0
        ? $"来源 EnterStory：{NodeId}"
        : $"来源 EnterStory：{NodeId}（入线分支：{string.Join("、", IncomingBranchOutputs)}）";
}

/// <summary>One derived edge per source/target pair; the underlying EnterStory nodes remain available as details.</summary>
public sealed record ProjectGraphEdgeViewModel(string SourceStoryId, string TargetStoryId, string NodeId)
{
    public int Count { get; init; } = 1;
    public int TransitionCount => Count;
    public bool IsSelfLoop => string.Equals(SourceStoryId, TargetStoryId, StringComparison.Ordinal);
    public bool SelfLoop => IsSelfLoop;
    public IReadOnlyList<ProjectGraphTransitionViewModel> Transitions { get; init; } =
        Array.AsReadOnly(Array.Empty<ProjectGraphTransitionViewModel>());
    public IReadOnlyList<ProjectGraphTransitionViewModel> TransitionDetails => Transitions;
    public IReadOnlyList<string> EnterStoryNodeIds => Transitions.Select(transition => transition.NodeId).ToArray();
    public IReadOnlyList<string> NodeIds => EnterStoryNodeIds;
    public IReadOnlyList<string> IncomingBranchOutputs => Transitions
        .SelectMany(transition => transition.IncomingBranchOutputs)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(output => output, StringComparer.Ordinal)
        .ToArray();
    public IReadOnlyList<string> BranchReasons => IncomingBranchOutputs;
    public string Tooltip => Transitions.Count == 0
        ? $"{SourceStoryId} → {TargetStoryId}"
        : $"{SourceStoryId} → {TargetStoryId}（{string.Join("；", Transitions.Select(transition => transition.Tooltip))}）";
    public string TooltipText => Tooltip;

    public ProjectGraphEdgeViewModel(
        string sourceStoryId,
        string targetStoryId,
        IReadOnlyList<ProjectGraphTransitionViewModel> transitions)
        : this(sourceStoryId, targetStoryId, transitions.FirstOrDefault()?.NodeId ?? string.Empty)
    {
        Transitions = Array.AsReadOnly(transitions.ToArray());
        Count = transitions.Count;
    }
}

public sealed record ProjectGraphDiagnosticViewModel(
    string Code,
    string Message,
    string? StoryId)
{
    public string? NodeId { get; init; }
    public string? SourceStoryId => StoryId;
    public string? StoryNodeId => NodeId;

    public ProjectGraphDiagnosticViewModel(
        string code,
        string message,
        string? storyId,
        string? nodeId)
        : this(code, message, storyId)
    {
        NodeId = nodeId;
    }
}

/// <summary>Read-only project Story graph snapshot for the M3 Project Graph route.</summary>
public sealed partial class ProjectGraphViewModel : ObservableObject
{
    private sealed record BuildNode(string Id, string DisplayName, string BaseWarningText = "");
    private sealed record BuildTransition(
        string SourceStoryId,
        string TargetStoryId,
        ProjectGraphTransitionViewModel Transition);
    private sealed record BuildInput(
        IReadOnlyList<BuildNode> Nodes,
        IReadOnlyList<BuildTransition> Transitions,
        IReadOnlyList<ProjectGraphDiagnosticViewModel> Diagnostics);

    private readonly ProjectGraphLayoutStore? _layoutStore;
    private readonly CanonicalStoryLogicGraphRepository? _storyLogicRepository;
    private ProjectStoryLogicEndpointViewModel? _selectedLogicSource;
    private ProjectStoryLogicEndpointViewModel? _selectedLogicTarget;
    private string _logicEditorError = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedFilter = "全部";
    private double _zoom = 1;
    private double _panX;
    private double _panY;
    private string _persistenceWarning = string.Empty;
    private long _problemFocusSequence;
    private ProjectGraphFocusRequest? _problemFocusRequest;

    public ProjectGraphViewModel(
        IReadOnlyList<StoryResource> stories,
        string? homeStoryId = null,
        string? projectDirectory = null)
        : this(CreateLegacyInput(stories), homeStoryId, projectDirectory)
    {
    }

    public ProjectGraphViewModel(
        IReadOnlyList<StoryResource> legacyStories,
        CanonicalProjectStoryGraphSnapshot canonicalSnapshot,
        string? homeStoryId = null,
        string? projectDirectory = null)
        : this(CreateMergedInput(legacyStories, canonicalSnapshot), homeStoryId, projectDirectory)
    {
    }

    private ProjectGraphViewModel(BuildInput input, string? homeStoryId, string? projectDirectory)
    {
        _layoutStore = string.IsNullOrWhiteSpace(projectDirectory) ? null : new ProjectGraphLayoutStore(projectDirectory);
        if (!string.IsNullOrWhiteSpace(projectDirectory))
        {
            try
            {
                var store = new CanonicalProjectGraphStore(projectDirectory);
                _storyLogicRepository = store.StoryLogicGraph;
                LoadLogicEditor(store);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                or GraphResourceRepositoryException or CanonicalStoryLogicGraphRepositoryException)
            {
                LogicEditorError = exception.Message;
            }
        }
        var storyIds = input.Nodes.Select(story => story.Id).ToHashSet(StringComparer.Ordinal);
        var diagnostics = input.Diagnostics.ToList();
        var warnedStories = input.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.BaseWarningText))
            .Select(node => node.Id)
            .Concat(diagnostics.Where(issue => issue.StoryId is not null).Select(issue => issue.StoryId!))
            .ToHashSet(StringComparer.Ordinal);

        var edges = input.Transitions
            .GroupBy(item => (item.SourceStoryId, item.TargetStoryId))
            .OrderBy(group => group.Key.SourceStoryId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.TargetStoryId, StringComparer.Ordinal)
            .Select(group => new ProjectGraphEdgeViewModel(
                group.Key.SourceStoryId,
                group.Key.TargetStoryId,
                group.OrderBy(item => item.Transition.NodeId, StringComparer.Ordinal)
                    .ThenBy(item => item.Transition.IncomingBranchOutput, StringComparer.Ordinal)
                    .Select(item => item.Transition)
                    .ToArray()))
            .ToList();

        var connected = edges.SelectMany(edge => new[] { edge.SourceStoryId, edge.TargetStoryId }).ToHashSet(StringComparer.Ordinal);
        var adjacency = storyIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in edges)
            adjacency[edge.SourceStoryId].Add(edge.TargetStoryId);
        foreach (var targets in adjacency.Values)
        {
            targets.Sort(StringComparer.Ordinal);
            for (var index = targets.Count - 1; index > 0; index--)
                if (string.Equals(targets[index], targets[index - 1], StringComparison.Ordinal)) targets.RemoveAt(index);
        }

        foreach (var component in FindStronglyConnectedComponents(storyIds, adjacency)
                     .Where(component => component.Count > 1 || adjacency[component[0]].Contains(component[0], StringComparer.Ordinal)))
        {
            var members = string.Join("、", component);
            foreach (var storyId in component)
            {
                diagnostics.Add(new(
                    "project_graph.story.cycle",
                    $"Story '{storyId}' 位于循环路径（{members}）。",
                    storyId));
                warnedStories.Add(storyId);
            }
        }
        Edges = new ReadOnlyCollection<ProjectGraphEdgeViewModel>(edges);
        Diagnostics = new ReadOnlyCollection<ProjectGraphDiagnosticViewModel>(diagnostics);
        Nodes = new ReadOnlyCollection<ProjectGraphNodeViewModel>(input.Nodes
            .Select(story => new ProjectGraphNodeViewModel(
                story.Id,
                string.IsNullOrWhiteSpace(story.DisplayName) ? story.Id : story.DisplayName,
                string.Equals(story.Id, homeStoryId, StringComparison.Ordinal),
                !connected.Contains(story.Id),
                warnedStories.Contains(story.Id),
                string.Join(Environment.NewLine,
                    new[] { story.BaseWarningText }
                        .Where(message => !string.IsNullOrWhiteSpace(message))
                        .Concat(diagnostics.Where(issue => issue.StoryId == story.Id).Select(issue => issue.Message))
                        .Distinct(StringComparer.Ordinal))))
            .ToList());

        ApplyPositions(CreateAutomaticPositions());
        if (_layoutStore is not null)
        {
            var persisted = _layoutStore.Load();
            foreach (var node in Nodes)
                if (persisted.Nodes.TryGetValue(node.Id, out var position)) node.SetPosition(position.X, position.Y);
        }
        AutoLayoutCommand = new RelayCommand(AutoLayout, () => Nodes.Count > 0);
        AddLogicConnectionCommand = new RelayCommand(AddLogicConnection,
            () => _storyLogicRepository is not null && SelectedLogicSource is not null && SelectedLogicTarget is not null);
        InitializeCanonicalGraph(projectDirectory);
    }

    private static BuildInput CreateLegacyInput(IReadOnlyList<StoryResource> stories)
    {
        ArgumentNullException.ThrowIfNull(stories);
        return CreateLegacyInput(stories, stories.Select(story => story.Id).ToHashSet(StringComparer.Ordinal));
    }

    private static BuildInput CreateLegacyInput(
        IReadOnlyList<StoryResource> stories,
        IReadOnlySet<string> validTargetIds)
    {
        var transitions = new List<BuildTransition>();
        var diagnostics = new List<ProjectGraphDiagnosticViewModel>();
        foreach (var story in stories)
        {
            foreach (var node in story.Nodes.Where(IsEnterStoryNode))
            {
                var target = TryGetTarget(node);
                if (string.IsNullOrWhiteSpace(target) || !validTargetIds.Contains(target))
                {
                    var label = string.IsNullOrWhiteSpace(target) ? "<empty>" : target;
                    diagnostics.Add(new("project_graph.target.missing", $"{story.Id}.{node.Id} 指向不存在的 Story '{label}'。", story.Id, node.Id));
                    continue;
                }
                var incomingOutputs = story.Connections
                    .Where(connection => string.Equals(connection.To, node.Id, StringComparison.Ordinal))
                    .Select(connection => connection.Output)
                    .Where(output => !string.IsNullOrWhiteSpace(output))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(output => output, StringComparer.Ordinal)
                    .ToArray();
                transitions.Add(new(story.Id, target,
                    new ProjectGraphTransitionViewModel(node.Id, Array.AsReadOnly(incomingOutputs))));
            }
        }

        return new(
            stories.Select(story => new BuildNode(
                story.Id,
                string.IsNullOrWhiteSpace(story.DisplayName) ? story.Title : story.DisplayName)).ToArray(),
            transitions,
            diagnostics);
    }

    private static BuildInput CreateMergedInput(
        IReadOnlyList<StoryResource> legacyStories,
        CanonicalProjectStoryGraphSnapshot canonicalSnapshot)
    {
        ArgumentNullException.ThrowIfNull(legacyStories);
        ArgumentNullException.ThrowIfNull(canonicalSnapshot);
        var canonicalIds = canonicalSnapshot.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var legacyOnlyStories = legacyStories.Where(story => !canonicalIds.Contains(story.Id)).ToArray();
        var legacy = CreateLegacyInput(
            legacyOnlyStories,
            legacyOnlyStories.Select(story => story.Id).ToHashSet(StringComparer.Ordinal));
        var canonicalNodes = canonicalSnapshot.Nodes.Select(node => new BuildNode(
            node.Id,
            node.DisplayName,
            string.Join(Environment.NewLine, node.Issues
                .Where(issue => issue.Code is not "project_graph.story.isolated" and not "project_graph.story.cycle")
                .Select(issue => issue.Message)
                .Distinct(StringComparer.Ordinal))));
        var canonicalTransitions = canonicalSnapshot.Edges.SelectMany(edge => edge.Transitions.Select(transition =>
            new BuildTransition(
                edge.SourceStoryId,
                edge.TargetStoryId,
                new ProjectGraphTransitionViewModel(
                    transition.NodeId,
                    Array.AsReadOnly(transition.IncomingBranchOutputs.ToArray())))));
        var canonicalDiagnostics = canonicalSnapshot.Diagnostics
            .Where(issue => issue.Code.StartsWith("project_graph.", StringComparison.Ordinal)
                && issue.Code is not "project_graph.story.isolated" and not "project_graph.story.cycle")
            .Select(issue => new ProjectGraphDiagnosticViewModel(
                issue.Code,
                issue.Message,
                issue.StoryId,
                issue.NodeId));

        return new(
            canonicalNodes.Concat(legacy.Nodes).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray(),
            canonicalTransitions.Concat(legacy.Transitions).ToArray(),
            canonicalDiagnostics.Concat(legacy.Diagnostics).ToArray());
    }

    public IReadOnlyList<ProjectGraphNodeViewModel> Nodes { get; }
    public IReadOnlyList<ProjectGraphEdgeViewModel> Edges { get; }
    public IReadOnlyList<ProjectGraphDiagnosticViewModel> Diagnostics { get; }
    public ObservableCollection<ProjectStoryLogicConnectionViewModel> LogicConnections { get; } = [];
    public IReadOnlyList<ProjectStoryLogicEndpointViewModel> LogicSources { get; private set; } = [];
    public IReadOnlyList<ProjectStoryLogicEndpointViewModel> LogicTargets { get; private set; } = [];
    public IReadOnlyList<string> FilterOptions { get; } = ["全部", "已连接", "孤立", "有警告"];
    public RelayCommand AutoLayoutCommand { get; }
    public RelayCommand AddLogicConnectionCommand { get; }
    public event EventHandler<string>? OpenStoryRequested;
    public event EventHandler<string>? OpenStoryOverviewRequested;
    public bool IsEmpty => Nodes.Count == 0;
    public string Summary => $"{Nodes.Count} 个故事 · {Edges.Sum(edge => edge.Count)} 条转场 / {Edges.Count} 组关系 · {Diagnostics.Count} 个诊断";
    public int ErrorCount => Diagnostics.Count(IsErrorDiagnostic);
    public int WarningCount => Diagnostics.Count - ErrorCount + (string.IsNullOrWhiteSpace(PersistenceWarning) ? 0 : 1);
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value ?? string.Empty)) RefreshVisibility(); } }
    public string SelectedFilter { get => _selectedFilter; set { if (SetProperty(ref _selectedFilter, value ?? "全部")) RefreshVisibility(); } }
    public double Zoom { get => _zoom; set => SetProperty(ref _zoom, Math.Clamp(value, .25, 2.5)); }
    public double PanX { get => _panX; set => SetProperty(ref _panX, value); }
    public double PanY { get => _panY; set => SetProperty(ref _panY, value); }
    public ProjectGraphFocusRequest? ProblemFocusRequest
    {
        get => _problemFocusRequest;
        private set => SetProperty(ref _problemFocusRequest, value);
    }
    public string PersistenceWarning
    {
        get => _persistenceWarning;
        private set
        {
            if (SetProperty(ref _persistenceWarning, value)) OnPropertyChanged(nameof(WarningCount));
        }
    }

    public ProjectStoryLogicEndpointViewModel? SelectedLogicSource
    {
        get => _selectedLogicSource;
        set
        {
            if (SetProperty(ref _selectedLogicSource, value)) AddLogicConnectionCommand.RaiseCanExecuteChanged();
        }
    }

    public ProjectStoryLogicEndpointViewModel? SelectedLogicTarget
    {
        get => _selectedLogicTarget;
        set
        {
            if (SetProperty(ref _selectedLogicTarget, value)) AddLogicConnectionCommand.RaiseCanExecuteChanged();
        }
    }

    public string LogicEditorError
    {
        get => _logicEditorError;
        private set
        {
            if (SetProperty(ref _logicEditorError, value ?? string.Empty))
                OnPropertyChanged(nameof(HasLogicEditorError));
        }
    }

    public bool HasLogicEditorError => !string.IsNullOrWhiteSpace(LogicEditorError);

    private void LoadLogicEditor(CanonicalProjectGraphStore store)
    {
        var stories = store.Stories.List().Select(info => store.Stories.Load(info.Id)).ToArray();
        LogicSources = FindLogicEndpoints(stories, "logic_output");
        LogicTargets = FindLogicEndpoints(stories, "logic_input");
        ReplaceLogicConnections(store.StoryLogicGraph.Load().Connections);
    }

    private static IReadOnlyList<ProjectStoryLogicEndpointViewModel> FindLogicEndpoints(
        IEnumerable<GraphResourceEnvelope> stories,
        string nodeType)
    {
        return stories.SelectMany(story => (story.Graph?.Nodes ?? [])
                .Where(node => string.Equals(node.Type, nodeType, StringComparison.Ordinal))
                .Select(node =>
                {
                    node.Properties.TryGetValue("port_id", out var portValue);
                    var portId = portValue.ValueKind == JsonValueKind.String ? portValue.GetString() : null;
                    return string.IsNullOrWhiteSpace(portId)
                        ? null
                        : new ProjectStoryLogicEndpointViewModel(
                            story.Id,
                            portId!,
                            string.IsNullOrWhiteSpace(node.DisplayName) ? portId! : node.DisplayName);
                }))
            .Where(endpoint => endpoint is not null)
            .Cast<ProjectStoryLogicEndpointViewModel>()
            .OrderBy(endpoint => endpoint.StoryId, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.PortId, StringComparer.Ordinal)
            .ToArray();
    }

    private void AddLogicConnection()
    {
        if (_storyLogicRepository is null || SelectedLogicSource is null || SelectedLogicTarget is null) return;
        var connection = new CanonicalStoryLogicConnection(
            SelectedLogicSource.StoryId,
            SelectedLogicSource.PortId,
            SelectedLogicTarget.StoryId,
            SelectedLogicTarget.PortId);
        SaveLogicConnections(LogicConnections.Select(item => item.Connection).Append(connection));
    }

    private void RemoveLogicConnection(CanonicalStoryLogicConnection connection)
    {
        SaveLogicConnections(LogicConnections.Select(item => item.Connection).Where(item => item != connection));
    }

    private void SaveLogicConnections(IEnumerable<CanonicalStoryLogicConnection> connections)
    {
        if (_storyLogicRepository is null) return;
        try
        {
            var saved = _storyLogicRepository.Save(connections);
            ReplaceLogicConnections(saved.Connections);
            LogicEditorError = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or GraphResourceRepositoryException or CanonicalStoryLogicGraphRepositoryException)
        {
            LogicEditorError = exception.Message;
        }
    }

    private void ReplaceLogicConnections(IEnumerable<CanonicalStoryLogicConnection> connections)
    {
        LogicConnections.Clear();
        foreach (var connection in connections)
            LogicConnections.Add(new ProjectStoryLogicConnectionViewModel(connection, () => RemoveLogicConnection(connection)));
    }

    public void OpenStoryFlow(string storyId)
    {
        if (Nodes.Any(node => node.Id == storyId)) OpenStoryRequested?.Invoke(this, storyId);
    }

    public void OpenStoryOverview(string storyId)
    {
        if (Nodes.Any(node => node.Id == storyId)) OpenStoryOverviewRequested?.Invoke(this, storyId);
    }

    public bool RequestProblemFocus(string storyId)
    {
        if (!Nodes.Any(node => string.Equals(node.Id, storyId, StringComparison.Ordinal))) return false;
        SearchText = string.Empty;
        SelectedFilter = "全部";
        ProblemFocusRequest = new(storyId, ++_problemFocusSequence);
        return true;
    }

    public void MoveNode(string storyId, double x, double y)
    {
        var node = Nodes.FirstOrDefault(candidate => candidate.Id == storyId);
        if (node is null || !double.IsFinite(x) || !double.IsFinite(y)) return;
        node.SetPosition(x, y);
        SaveLayout();
    }

    public void AutoLayout()
    {
        ApplyPositions(CreateAutomaticPositions());
        SaveLayout();
    }

    private void RefreshVisibility()
    {
        var query = SearchText.Trim();
        var connected = Edges.SelectMany(edge => new[] { edge.SourceStoryId, edge.TargetStoryId }).ToHashSet(StringComparer.Ordinal);
        foreach (var node in Nodes)
        {
            var matchesSearch = query.Length == 0 || node.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || node.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase);
            var matchesFilter = SelectedFilter switch
            {
                "已连接" => connected.Contains(node.Id),
                "孤立" => node.IsIsolated,
                "有警告" => node.HasWarning,
                _ => true,
            };
            node.IsVisible = matchesSearch && matchesFilter;
        }
        OnPropertyChanged(nameof(Summary));
    }

    private Dictionary<string, ProjectGraphNodeLayout> CreateAutomaticPositions()
    {
        var nodeIds = Nodes.Select(node => node.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var adjacency = nodeIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var edge in Edges)
            adjacency[edge.SourceStoryId].Add(edge.TargetStoryId);
        foreach (var targets in adjacency.Values)
        {
            targets.Sort(StringComparer.Ordinal);
            for (var index = targets.Count - 1; index > 0; index--)
                if (string.Equals(targets[index], targets[index - 1], StringComparison.Ordinal)) targets.RemoveAt(index);
        }

        var components = FindStronglyConnectedComponents(nodeIds, adjacency);
        var componentByNode = components
            .SelectMany((component, index) => component.Select(id => (id, index)))
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
        var componentOutgoing = components.Select(_ => new HashSet<int>()).ToArray();
        foreach (var edge in Edges)
        {
            var source = componentByNode[edge.SourceStoryId];
            var target = componentByNode[edge.TargetStoryId];
            if (source != target) componentOutgoing[source].Add(target);
        }

        var componentIncoming = componentOutgoing.Select(_ => 0).ToArray();
        foreach (var targets in componentOutgoing)
            foreach (var target in targets) componentIncoming[target]++;
        var levelsByComponent = new int[components.Count];
        var ready = new SortedSet<int>(Comparer<int>.Create((left, right) =>
        {
            var comparison = string.Compare(components[left][0], components[right][0], StringComparison.Ordinal);
            return comparison != 0 ? comparison : left.CompareTo(right);
        }));
        for (var index = 0; index < componentIncoming.Length; index++)
            if (componentIncoming[index] == 0) ready.Add(index);
        var processed = 0;
        while (ready.Count > 0)
        {
            var source = ready.Min;
            ready.Remove(source);
            processed++;
            foreach (var target in componentOutgoing[source].OrderBy(index => components[index][0], StringComparer.Ordinal))
            {
                levelsByComponent[target] = Math.Max(levelsByComponent[target], levelsByComponent[source] + 1);
                if (--componentIncoming[target] == 0) ready.Add(target);
            }
        }
        // Condensation is a DAG, but retain a finite fallback if malformed input ever violates that invariant.
        if (processed != components.Count)
            for (var index = 0; index < components.Count; index++) levelsByComponent[index] = 0;
        var levels = nodeIds.ToDictionary(id => id, id => levelsByComponent[componentByNode[id]], StringComparer.Ordinal);
        var positions = new Dictionary<string, ProjectGraphNodeLayout>(StringComparer.Ordinal);

        if (components.All(component => component.Count == 1 && !adjacency[component[0]].Contains(component[0], StringComparer.Ordinal)))
        {
            foreach (var group in Nodes.GroupBy(node => levels[node.Id]).OrderBy(group => group.Key))
            {
                var index = 0;
                foreach (var node in group.OrderByDescending(node => node.IsHomeStory).ThenBy(node => node.DisplayName, StringComparer.CurrentCultureIgnoreCase).ThenBy(node => node.Id, StringComparer.Ordinal))
                    positions[node.Id] = new ProjectGraphNodeLayout { X = 80 + group.Key * 280, Y = 70 + index++ * 170 };
            }
            return positions;
        }

        var nodeById = Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        foreach (var group in Nodes.GroupBy(node => levels[node.Id]).OrderBy(group => group.Key))
        {
            var y = 70d;
            foreach (var componentIndex in group.Select(node => componentByNode[node.Id]).Distinct().OrderBy(index => components[index][0], StringComparer.Ordinal))
            {
                var component = components[componentIndex];
                var members = component
                    .Select(id => nodeById[id])
                    .OrderByDescending(node => node.IsHomeStory)
                    .ThenBy(node => node.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                    .ThenBy(node => node.Id, StringComparer.Ordinal)
                    .ToArray();
                var spacing = component.Count > 1 || adjacency[component[0]].Contains(component[0], StringComparer.Ordinal) ? 110d : 170d;
                foreach (var node in members)
                {
                    positions[node.Id] = new ProjectGraphNodeLayout { X = 80 + group.Key * 280, Y = y };
                    y += spacing;
                }
            }
        }
        return positions;
    }

    private static IReadOnlyList<IReadOnlyList<string>> FindStronglyConnectedComponents(
        IEnumerable<string> nodeIds,
        IReadOnlyDictionary<string, List<string>> adjacency)
    {
        var nextIndex = 0;
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var lowLinks = new Dictionary<string, int>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        var onStack = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<IReadOnlyList<string>>();

        void Visit(string nodeId)
        {
            indexes[nodeId] = nextIndex;
            lowLinks[nodeId] = nextIndex++;
            stack.Push(nodeId);
            onStack.Add(nodeId);
            foreach (var target in adjacency[nodeId])
            {
                if (!indexes.ContainsKey(target))
                {
                    Visit(target);
                    lowLinks[nodeId] = Math.Min(lowLinks[nodeId], lowLinks[target]);
                }
                else if (onStack.Contains(target))
                    lowLinks[nodeId] = Math.Min(lowLinks[nodeId], indexes[target]);
            }

            if (lowLinks[nodeId] != indexes[nodeId]) return;
            var component = new List<string>();
            string member;
            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            } while (!string.Equals(member, nodeId, StringComparison.Ordinal));
            component.Sort(StringComparer.Ordinal);
            components.Add(component);
        }

        foreach (var nodeId in nodeIds.OrderBy(id => id, StringComparer.Ordinal))
            if (!indexes.ContainsKey(nodeId)) Visit(nodeId);
        components.Sort((left, right) => string.Compare(left[0], right[0], StringComparison.Ordinal));
        return components;
    }

    private void ApplyPositions(IReadOnlyDictionary<string, ProjectGraphNodeLayout> positions)
    {
        foreach (var node in Nodes)
            if (positions.TryGetValue(node.Id, out var position)) node.SetPosition(position.X, position.Y);
    }

    private void SaveLayout()
    {
        if (_layoutStore is null) return;
        try
        {
            _layoutStore.Save(Nodes.ToDictionary(node => node.Id, node => new ProjectGraphNodeLayout { X = node.X, Y = node.Y }, StringComparer.Ordinal));
            PersistenceWarning = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            PersistenceWarning = $"图谱布局保存失败：{exception.Message}";
        }
    }

    private static bool IsEnterStoryNode(StoryNodeResource node) =>
        node.Type.Equals("EnterStory", StringComparison.OrdinalIgnoreCase) ||
        node.Type.Equals("ENTER_STORY", StringComparison.OrdinalIgnoreCase) ||
        node.Type.Equals("enter_story", StringComparison.OrdinalIgnoreCase);

    private static string? TryGetTarget(StoryNodeResource node)
    {
        foreach (var key in new[] { "story_id", "target_story_id", "target", "story" })
        {
            if (node.Properties.TryGetValue(key, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                value.GetString() is { } target)
            {
                return target;
            }
        }

        return null;
    }

    private static bool IsErrorDiagnostic(ProjectGraphDiagnosticViewModel issue) =>
        issue.Code is "project_graph.target.missing"
            or "project_graph.target.invalid"
            or "project_graph.enter_story.malformed"
            or "project_graph.enter_story.ambiguous";
}

public sealed record ProjectStoryLogicEndpointViewModel(string StoryId, string PortId, string DisplayName)
{
    public string Label => $"{StoryId} · {DisplayName} ({PortId})";
}

public sealed class ProjectStoryLogicConnectionViewModel
{
    public ProjectStoryLogicConnectionViewModel(CanonicalStoryLogicConnection connection, Action remove)
    {
        Connection = connection;
        RemoveCommand = new RelayCommand(remove);
    }

    public CanonicalStoryLogicConnection Connection { get; }
    public string Label => $"{Connection.SourceStoryId}.{Connection.SourcePortId}  →  {Connection.TargetStoryId}.{Connection.TargetPortId}";
    public RelayCommand RemoveCommand { get; }
}

public sealed record ProjectGraphFocusRequest(string StoryId, long Sequence);

public enum ProjectHomeRoute
{
    Home,
    Graph,
}

/// <summary>Project Home state: searchable Story list plus the read-only Project Graph route.</summary>
public sealed class ProjectHomeViewModel : ObservableObject
{
    private string _searchText = string.Empty;
    private StoryListItemViewModel? _selectedStory;
    private string? _selectionBeforeSearchId;
    private ProjectHomeRoute _route = ProjectHomeRoute.Home;
    private ProjectGraphViewModel _graph = new([], homeStoryId: null);

    public ProjectHomeViewModel()
    {
        _graph.OpenStoryRequested += GraphOnOpenStoryRequested;
        _graph.OpenStoryOverviewRequested += GraphOnOpenStoryOverviewRequested;
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty, () => IsSearchActive);
    }
    public event EventHandler<string>? OpenStoryFlowRequested;
    public event EventHandler<string>? OpenStoryRequested;

    public ObservableCollection<StoryListItemViewModel> Stories { get; } = [];
    public ObservableCollection<StoryListItemViewModel> FilteredStories { get; } = [];
    public RelayCommand ClearSearchCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_searchText, next, StringComparison.Ordinal)) return;
            var wasActive = IsSearchActive;
            var isActive = !string.IsNullOrWhiteSpace(next);
            if (!wasActive && isActive) _selectionBeforeSearchId = SelectedStory?.Id;
            if (SetProperty(ref _searchText, next))
            {
                RefreshFilter(wasActive && !isActive);
                ClearSearchCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public StoryListItemViewModel? SelectedStory
    {
        get => _selectedStory;
        set => SetProperty(ref _selectedStory, value);
    }

    public ProjectHomeRoute Route
    {
        get => _route;
        private set
        {
            if (!SetProperty(ref _route, value)) return;
            OnPropertyChanged(nameof(IsGraphVisible));
            OnPropertyChanged(nameof(IsHomeVisible));
            OnPropertyChanged(nameof(CurrentRoute));
        }
    }

    // 0.3.1.3 uses one persistent three-column Project Home. Legacy route
    // commands remain callable, but no longer swap the graph and list pages.
    public bool IsGraphVisible => Route == ProjectHomeRoute.Graph;
    public bool IsHomeVisible => true;
    public bool HasStories => Stories.Count > 0;
    public bool HasFilteredStories => FilteredStories.Count > 0;
    public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);
    public bool IsSearchNoResults => HasStories && IsSearchActive && !HasFilteredStories;
    public bool IsEmptyProject => !HasStories;
    public string EmptyStateTitle => "当前项目还没有故事";
    public string EmptyStateDescription => "故事是 DarkGrey RPG 中的主要创作单元。";
    public string SearchNoResultsTitle => "没有匹配当前搜索条件的故事";
    public string SearchNoResultsDescription => "清空搜索后可查看项目中的全部故事。";
    public string CurrentRoute => Route.ToString();
    public ProjectGraphViewModel Graph
    {
        get => _graph;
        private set
        {
            if (ReferenceEquals(_graph, value)) return;
            _graph.OpenStoryRequested -= GraphOnOpenStoryRequested;
            _graph.OpenStoryOverviewRequested -= GraphOnOpenStoryOverviewRequested;
            if (!SetProperty(ref _graph, value)) return;
            _graph.OpenStoryRequested += GraphOnOpenStoryRequested;
            _graph.OpenStoryOverviewRequested += GraphOnOpenStoryOverviewRequested;
        }
    }

    public void ReplaceStories(IReadOnlyList<StoryResource> stories, string? homeStoryId = null, string? projectDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(stories);
        ReplaceStoryItems(
            stories.Select(story => new StoryListItemViewModel(story)).ToArray(),
            stories,
            homeStoryId,
            projectDirectory);
    }

    public void ReplaceDiscoveredStories(
        IReadOnlyList<StoryResource> legacyStories,
        IReadOnlyList<CanonicalStoryHomeEntry> canonicalStories,
        string? homeStoryId = null,
        string? projectDirectory = null,
        CanonicalProjectStoryGraphSnapshot? canonicalGraph = null)
    {
        ArgumentNullException.ThrowIfNull(legacyStories);
        ArgumentNullException.ThrowIfNull(canonicalStories);
        var legacyById = legacyStories.ToDictionary(story => story.Id, StringComparer.Ordinal);
        var canonicalById = canonicalStories.ToDictionary(story => story.Id, StringComparer.Ordinal);
        var items = canonicalStories
            .Select(story => new StoryListItemViewModel(
                story,
                legacyById.GetValueOrDefault(story.Id)))
            .Concat(legacyStories
                .Where(story => !canonicalById.ContainsKey(story.Id))
                .Select(story => new StoryListItemViewModel(story)))
            .ToArray();
        ReplaceStoryItems(items, legacyStories, homeStoryId, projectDirectory, canonicalGraph);
    }

    private void ReplaceStoryItems(
        IReadOnlyList<StoryListItemViewModel> items,
        IReadOnlyList<StoryResource> legacyStories,
        string? homeStoryId,
        string? projectDirectory,
        CanonicalProjectStoryGraphSnapshot? canonicalGraph = null)
    {
        var previousSelectedId = SelectedStory?.Id ?? _selectionBeforeSearchId;
        var previousSelectedIndex = SelectedStory is null
            ? -1
            : Stories.IndexOf(SelectedStory);

        Stories.Clear();
        foreach (var story in items.OrderBy(story => story.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            Stories.Add(story);
        OnPropertyChanged(nameof(HasStories));
        OnPropertyChanged(nameof(IsEmptyProject));
        RefreshFilter();
        ReconcileSelection(previousSelectedId, previousSelectedIndex);
        Graph = canonicalGraph is null
            ? new ProjectGraphViewModel(legacyStories, homeStoryId, projectDirectory)
            : new ProjectGraphViewModel(legacyStories, canonicalGraph, homeStoryId, projectDirectory);
        ShowHome();
    }

    public void ShowHome() => Route = ProjectHomeRoute.Home;
    public void ShowGraph() => Route = ProjectHomeRoute.Graph;

    private void GraphOnOpenStoryRequested(object? sender, string storyId) => OpenStoryFlowRequested?.Invoke(this, storyId);
    private void GraphOnOpenStoryOverviewRequested(object? sender, string storyId) => OpenStoryRequested?.Invoke(this, storyId);

    private void RefreshFilter(bool restoringSearchSelection = false)
    {
        var query = SearchText.Trim();
        FilteredStories.Clear();
        foreach (var story in Stories.Where(item =>
                     query.Length == 0 ||
                     item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     item.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                     item.Tags.Any(tag => tag.Contains(query, StringComparison.CurrentCultureIgnoreCase))))
        {
            FilteredStories.Add(story);
        }

        OnPropertyChanged(nameof(HasFilteredStories));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(IsSearchNoResults));
        ClearSearchCommand.RaiseCanExecuteChanged();

        if (restoringSearchSelection)
        {
            var restored = _selectionBeforeSearchId is null
                ? null
                : Stories.FirstOrDefault(item => item.Id == _selectionBeforeSearchId);
            SelectedStory = restored ?? FilteredStories.FirstOrDefault();
            _selectionBeforeSearchId = null;
            return;
        }

        if (!IsSearchActive) return;
        if (SelectedStory is not null && FilteredStories.Any(item => item.Id == SelectedStory.Id)) return;
        SelectedStory = FilteredStories.FirstOrDefault();
    }

    private void ReconcileSelection(string? previousSelectedId, int previousSelectedIndex)
    {
        if (Stories.Count == 0)
        {
            SelectedStory = null;
            return;
        }

        if (IsSearchActive)
        {
            SelectedStory = previousSelectedId is null
                ? FilteredStories.FirstOrDefault()
                : FilteredStories.FirstOrDefault(item => item.Id == previousSelectedId)
                    ?? FilteredStories.FirstOrDefault();
            return;
        }

        SelectedStory = previousSelectedId is not null
            ? Stories.FirstOrDefault(item => item.Id == previousSelectedId)
            : null;
        if (SelectedStory is not null) return;

        var fallbackIndex = previousSelectedIndex < 0
            ? 0
            : Math.Clamp(previousSelectedIndex, 0, Stories.Count - 1);
        SelectedStory = Stories[fallbackIndex];
    }
}
