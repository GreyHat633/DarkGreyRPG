using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class StoryListItemViewModel
{
    public StoryListItemViewModel(StoryResource story)
    {
        Story = story ?? throw new ArgumentNullException(nameof(story));
    }

    public StoryResource Story { get; }
    public string Id => Story.Id;
    public string DisplayName => string.IsNullOrWhiteSpace(Story.DisplayName) ? Story.Title : Story.DisplayName;
    public string Description => Story.Description;
    public IReadOnlyList<string> Tags => Story.Tags;
    public string TagsText => string.Join(", ", Story.Tags);
    public int ActorCount => Story.OwnedResources.Actors.Count + Story.ReferencedResources.Actors.Count;
    public int DialogueCount => Story.OwnedResources.Dialogues.Count + Story.ReferencedResources.Dialogues.Count;
    public int QuestCount => Story.OwnedResources.Quests.Count + Story.ReferencedResources.Quests.Count;
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

public sealed record ProjectGraphEdgeViewModel(string SourceStoryId, string TargetStoryId, string NodeId);
public sealed record ProjectGraphDiagnosticViewModel(string Code, string Message, string? StoryId);

/// <summary>Read-only project Story graph snapshot for the M3 Project Graph route.</summary>
public sealed class ProjectGraphViewModel : ObservableObject
{
    private readonly ProjectGraphLayoutStore? _layoutStore;
    private string _searchText = string.Empty;
    private string _selectedFilter = "全部";
    private double _zoom = 1;
    private double _panX;
    private double _panY;
    private string _persistenceWarning = string.Empty;

    public ProjectGraphViewModel(IReadOnlyList<StoryResource> stories, string? homeStoryId = null, string? projectDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(stories);
        _layoutStore = string.IsNullOrWhiteSpace(projectDirectory) ? null : new ProjectGraphLayoutStore(projectDirectory);
        var storyIds = stories.Select(story => story.Id).ToHashSet(StringComparer.Ordinal);
        var edges = new List<ProjectGraphEdgeViewModel>();
        var diagnostics = new List<ProjectGraphDiagnosticViewModel>();
        var warnedStories = new HashSet<string>(StringComparer.Ordinal);
        foreach (var story in stories)
        {
            foreach (var node in story.Nodes.Where(IsEnterStoryNode))
            {
                var target = TryGetTarget(node);
                if (string.IsNullOrWhiteSpace(target) || !storyIds.Contains(target))
                {
                    var label = string.IsNullOrWhiteSpace(target) ? "<empty>" : target;
                    diagnostics.Add(new("project_graph.target.missing", $"{story.Id}.{node.Id} 指向不存在的 Story '{label}'。", story.Id));
                    warnedStories.Add(story.Id);
                    continue;
                }
                edges.Add(new(story.Id, target, node.Id));
            }
        }

        var connected = edges.SelectMany(edge => new[] { edge.SourceStoryId, edge.TargetStoryId }).ToHashSet(StringComparer.Ordinal);
        foreach (var story in stories.Where(story => !connected.Contains(story.Id)))
        {
            diagnostics.Add(new("project_graph.story.isolated", $"Story '{story.Id}' 未连接到任何 EnterStory 转场。", story.Id));
            warnedStories.Add(story.Id);
        }
        Edges = new ReadOnlyCollection<ProjectGraphEdgeViewModel>(edges);
        Diagnostics = new ReadOnlyCollection<ProjectGraphDiagnosticViewModel>(diagnostics);
        Nodes = new ReadOnlyCollection<ProjectGraphNodeViewModel>(stories
            .Select(story => new ProjectGraphNodeViewModel(
                story.Id,
                string.IsNullOrWhiteSpace(story.DisplayName) ? story.Title : story.DisplayName,
                string.Equals(story.Id, homeStoryId, StringComparison.Ordinal),
                !connected.Contains(story.Id),
                warnedStories.Contains(story.Id),
                string.Join(Environment.NewLine, diagnostics.Where(issue => issue.StoryId == story.Id).Select(issue => issue.Message))))
            .ToList());

        ApplyPositions(CreateAutomaticPositions());
        if (_layoutStore is not null)
        {
            var persisted = _layoutStore.Load();
            foreach (var node in Nodes)
                if (persisted.Nodes.TryGetValue(node.Id, out var position)) node.SetPosition(position.X, position.Y);
        }
        AutoLayoutCommand = new RelayCommand(AutoLayout, () => Nodes.Count > 0);
    }

    public IReadOnlyList<ProjectGraphNodeViewModel> Nodes { get; }
    public IReadOnlyList<ProjectGraphEdgeViewModel> Edges { get; }
    public IReadOnlyList<ProjectGraphDiagnosticViewModel> Diagnostics { get; }
    public IReadOnlyList<string> FilterOptions { get; } = ["全部", "已连接", "孤立", "有警告"];
    public RelayCommand AutoLayoutCommand { get; }
    public event EventHandler<string>? OpenStoryRequested;
    public bool IsEmpty => Nodes.Count == 0;
    public string Summary => $"{Nodes.Count} 个剧情 · {Edges.Count} 条转场 · {Diagnostics.Count} 个诊断";
    public string SearchText { get => _searchText; set { if (SetProperty(ref _searchText, value ?? string.Empty)) RefreshVisibility(); } }
    public string SelectedFilter { get => _selectedFilter; set { if (SetProperty(ref _selectedFilter, value ?? "全部")) RefreshVisibility(); } }
    public double Zoom { get => _zoom; set => SetProperty(ref _zoom, Math.Clamp(value, .25, 2.5)); }
    public double PanX { get => _panX; set => SetProperty(ref _panX, value); }
    public double PanY { get => _panY; set => SetProperty(ref _panY, value); }
    public string PersistenceWarning { get => _persistenceWarning; private set => SetProperty(ref _persistenceWarning, value); }

    public void OpenStoryFlow(string storyId)
    {
        if (Nodes.Any(node => node.Id == storyId)) OpenStoryRequested?.Invoke(this, storyId);
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
        var incoming = Nodes.ToDictionary(node => node.Id, _ => 0, StringComparer.Ordinal);
        foreach (var edge in Edges) incoming[edge.TargetStoryId]++;
        var levels = new Dictionary<string, int>(StringComparer.Ordinal);
        var queue = new Queue<string>(Nodes.Where(node => node.IsHomeStory || incoming[node.Id] == 0).Select(node => node.Id));
        foreach (var id in queue) levels[id] = 0;
        while (queue.TryDequeue(out var source))
        {
            foreach (var edge in Edges.Where(edge => edge.SourceStoryId == source))
            {
                var next = levels[source] + 1;
                if (levels.TryGetValue(edge.TargetStoryId, out var existing) && existing >= next) continue;
                levels[edge.TargetStoryId] = next;
                queue.Enqueue(edge.TargetStoryId);
            }
        }
        foreach (var node in Nodes) levels.TryAdd(node.Id, 0);
        var positions = new Dictionary<string, ProjectGraphNodeLayout>(StringComparer.Ordinal);
        foreach (var group in Nodes.GroupBy(node => levels[node.Id]).OrderBy(group => group.Key))
        {
            var index = 0;
            foreach (var node in group.OrderByDescending(node => node.IsHomeStory).ThenBy(node => node.DisplayName, StringComparer.CurrentCultureIgnoreCase))
                positions[node.Id] = new ProjectGraphNodeLayout { X = 80 + group.Key * 280, Y = 70 + index++ * 170 };
        }
        return positions;
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
}

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
    private ProjectHomeRoute _route = ProjectHomeRoute.Home;
    private ProjectGraphViewModel _graph = new([], null);

    public ProjectHomeViewModel() => _graph.OpenStoryRequested += GraphOnOpenStoryRequested;
    public event EventHandler<string>? OpenStoryFlowRequested;

    public ObservableCollection<StoryListItemViewModel> Stories { get; } = [];
    public ObservableCollection<StoryListItemViewModel> FilteredStories { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty)) RefreshFilter();
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

    public bool IsGraphVisible => Route == ProjectHomeRoute.Graph;
    public bool IsHomeVisible => Route == ProjectHomeRoute.Home;
    public bool HasStories => Stories.Count > 0;
    public string EmptyStateTitle => "还没有剧情";
    public string EmptyStateDescription => "剧情是 DarkGrey RPG 中的主要创作单元。";
    public string CurrentRoute => Route.ToString();
    public ProjectGraphViewModel Graph
    {
        get => _graph;
        private set
        {
            if (ReferenceEquals(_graph, value)) return;
            _graph.OpenStoryRequested -= GraphOnOpenStoryRequested;
            if (!SetProperty(ref _graph, value)) return;
            _graph.OpenStoryRequested += GraphOnOpenStoryRequested;
        }
    }

    public void ReplaceStories(IReadOnlyList<StoryResource> stories, string? homeStoryId = null, string? projectDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(stories);
        Stories.Clear();
        foreach (var story in stories.OrderBy(
                     story => string.IsNullOrWhiteSpace(story.DisplayName) ? story.Title : story.DisplayName,
                     StringComparer.CurrentCultureIgnoreCase))
            Stories.Add(new StoryListItemViewModel(story));
        OnPropertyChanged(nameof(HasStories));
        RefreshFilter();
        SelectedStory = null;
        Graph = new ProjectGraphViewModel(stories, homeStoryId, projectDirectory);
        ShowHome();
    }

    public void ShowHome() => Route = ProjectHomeRoute.Home;
    public void ShowGraph() => Route = ProjectHomeRoute.Graph;

    private void GraphOnOpenStoryRequested(object? sender, string storyId) => OpenStoryFlowRequested?.Invoke(this, storyId);

    private void RefreshFilter()
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
    }
}
