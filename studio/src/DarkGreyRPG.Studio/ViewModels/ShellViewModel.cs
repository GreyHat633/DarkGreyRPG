using DarkGreyRPG.Studio.Core.Identity;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Markup;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels.Graph;

using System.Text.Json;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly PortableProjectStore? _portableProjects;
    private readonly IProjectFolderPicker _projectFolderPicker;
    private readonly IActorWorkspaceDialogs _actorWorkspaceDialogs;
    private readonly IResourceWorkspaceDialogs _resourceWorkspaceDialogs;
    private readonly ICanonicalStoryResourceDialogs _canonicalStoryResourceDialogs;
    private readonly IItemWorkspaceDialogs _itemWorkspaceDialogs;
    private readonly IProjectWorkspaceDialogs _projectWorkspaceDialogs;
    private readonly ICrashLogService _crashLogService;
    private readonly IDgrsExportPathPicker _dgrsExportPathPicker;
    private readonly Func<string, CanonicalProjectGraphStore> _canonicalGraphStoreFactory;
    private CanonicalStoryWorkspaceViewModel? _canonicalStoryWorkspace;
    private CanonicalProjectGraphStore? _canonicalGraphStore;
    private CanonicalGraphResourceSaveCoordinator? _canonicalSaveCoordinator;
    private IReadOnlyList<CanonicalStoryDiscoveryIssue> _canonicalStoryDiscoveryIssues = [];
    private bool _isResourceBrowserVisible = true;
    private bool _isCanonicalStoryWorkspaceVisible;
    private string _projectDisplayName = "未打开项目";
    private string _projectDirectory = "请选择包含 project.json 与 actors 目录的项目文件夹。";
    private string _statusMessage = "就绪";
    private string _lastUiCommand = "(none)";

    public ShellViewModel(
        ProjectService projectService,
        IProjectFolderPicker projectFolderPicker,
        IActorWorkspaceDialogs? actorWorkspaceDialogs = null,
        IProjectWorkspaceDialogs? projectWorkspaceDialogs = null,
        IResourceWorkspaceDialogs? resourceWorkspaceDialogs = null,
        ICrashLogService? crashLogService = null,
        ICanonicalStoryResourceDialogs? canonicalStoryResourceDialogs = null,
        Func<string, CanonicalProjectGraphStore>? canonicalGraphStoreFactory = null,
        IItemWorkspaceDialogs? itemWorkspaceDialogs = null,
        IDgrsExportPathPicker? dgrsExportPathPicker = null,
        IOfflinePackageDialogs? offlinePackageDialogs = null,
        PortableProjectStore? portableProjects = null)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _portableProjects = portableProjects;
        _projectFolderPicker = projectFolderPicker ?? throw new ArgumentNullException(nameof(projectFolderPicker));
        _actorWorkspaceDialogs = actorWorkspaceDialogs ?? new NullActorWorkspaceDialogs();
        _projectWorkspaceDialogs = projectWorkspaceDialogs ?? new NullProjectWorkspaceDialogs();
        _resourceWorkspaceDialogs = resourceWorkspaceDialogs ?? new NullResourceWorkspaceDialogs();
        _canonicalStoryResourceDialogs = canonicalStoryResourceDialogs ?? new NullCanonicalStoryResourceDialogs();
        _itemWorkspaceDialogs = itemWorkspaceDialogs ?? new NullItemWorkspaceDialogs();
        _crashLogService = crashLogService ?? new CrashLogService();
        _dgrsExportPathPicker = dgrsExportPathPicker ?? new NullDgrsExportPathPicker();
        _offlinePackageDialogs = offlinePackageDialogs ?? NullOfflinePackageDialogs.Instance;
        ReferencePackageCommand = new RelayCommand(ReferencePackage, () => HasProject);
        ImportPackageCommand = new RelayCommand(ImportPackage, () => HasProject);
        AddExternalReferenceCommand = new RelayCommand(() => AddExternalReference(CanonicalStoryWorkspace?.StoryEditor.Id ?? ProjectHome.SelectedStory?.Id));
        _canonicalGraphStoreFactory = canonicalGraphStoreFactory
            ?? (projectDirectory => new CanonicalProjectGraphStore(projectDirectory));
        NewProjectCommand = new RelayCommand(NewProject);
        OpenProjectCommand = new RelayCommand(OpenProject);
        SaveCurrentResourceCommand = new RelayCommand(SaveAll, CanSaveAll);
        UndoCurrentCommand = new RelayCommand(UndoCurrent, () => CanonicalStoryWorkspace?.InspectorPortraitEditor?.CanUndo == true || LatestCanonicalUndoHost is not null || ActiveEditor?.UndoCommand.CanExecute(null) == true || CanUndoReference());
        RedoCurrentCommand = new RelayCommand(RedoCurrent, () => CanonicalStoryWorkspace?.InspectorPortraitEditor?.CanRedo == true || NextCanonicalRedoHost is not null || (ActiveEditor is not CanonicalGraphResourceEditorViewModel && ActiveEditor?.RedoCommand.CanExecute(null) == true) || CanRedoReference());
        SaveAllCommand = new RelayCommand(SaveAll, CanSaveAll);
        OpenProjectDirectoryCommand = new RelayCommand(OpenProjectDirectory, () => HasProject);
        ValidateProjectCommand = new RelayCommand(ValidateProject, () => HasProject);
        PrepareRuntimeReloadCommand = new RelayCommand(PrepareRuntimeReload, () => HasProject);
        ShowProjectSettingsCommand = new RelayCommand(ShowProjectSettings, () => HasProject);
        OpenSelectedStoryCommand = new RelayCommand(OpenSelectedStory, () => HasProject && ProjectHome.SelectedStory is not null);
        CreateStoryCommand = new RelayCommand(CreateStory, () => HasProject);
        DeleteSelectedStoryCommand = new RelayCommand(
            DeleteSelectedStory,
            () => HasProject && ProjectHome.SelectedStory is { } story
                && story.HasCanonicalStory);
        ShowProjectHomeCommand = new RelayCommand(ShowProjectHome, () => HasProject);
        ShowProjectGraphCommand = new RelayCommand(ShowProjectGraph, () => HasProject);
        CopyStoryContentCommand = new RelayCommand(CopyStoryContent,
            () => HasProject && ProjectHome.SelectedStory is { HasCanonicalStory: true });
        ExportSelectedStoryPackageCommand = new RelayCommand(
            ExportSelectedStoryPackage,
            () => HasProject && ProjectHome.SelectedStory is not null);
        ToggleResourceBrowserCommand = new RelayCommand(
            () => IsResourceBrowserVisible = !IsResourceBrowserVisible);
        ToggleBottomPanelCommand = new RelayCommand(
            BottomPanel.Toggle);
        ProjectHome.PropertyChanged += OnProjectHomePropertyChanged;
        ProjectHome.OpenStoryFlowRequested += ProjectHomeOnOpenStoryFlowRequested;
        ProjectHome.OpenStoryRequested += ProjectHomeOnOpenStoryRequested;
        Output.Append("DarkGrey RPG Studio 已启动。", source: "Studio");
    }

    public ObservableCollection<ActorResourceInfo> Actors { get; } = [];

    public ObservableCollection<RecentProjectItemViewModel> RecentProjects { get; } = [];

    public ProjectHomeViewModel ProjectHome { get; } = new();

    public NavigationViewModel Navigation { get; } = new();

    public BottomPanelViewModel BottomPanel { get; } = new();

    public OutputViewModel Output { get; } = new();

    public ProblemsViewModel Problems { get; } = new();

    public ToastViewModel Toast { get; } = new();

    public string LastUiCommand => _lastUiCommand;

    public IReadOnlyDictionary<string, string?> GetCrashLogDetails() => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["Current Project"] = HasProject ? ProjectDirectory : null,
        ["Current Story"] = CanonicalStoryWorkspace?.StoryEditor.Id ?? ProjectHome.SelectedStory?.Id,
        ["Current Route"] = IsCanonicalStoryWorkspaceVisible ? CanonicalStoryWorkspace?.ActiveEditor?.Id : ProjectHome.Route.ToString(),
        ["Last UI Command"] = LastUiCommand,
    };

    public void ReportUnhandledUiException(Exception exception, string context, bool alreadyLogged = false)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (!alreadyLogged)
        {
            LogCrash(exception, context);
        }

        ReportFailure("界面操作", exception, writeCrashLog: false);
    }

    public RelayCommand OpenProjectCommand { get; }

    public RelayCommand NewProjectCommand { get; }

    public RelayCommand SaveCurrentResourceCommand { get; }

    public RelayCommand UndoCurrentCommand { get; }

    public RelayCommand RedoCurrentCommand { get; }

    public RelayCommand SaveAllCommand { get; }

    public RelayCommand OpenProjectDirectoryCommand { get; }

    public RelayCommand ValidateProjectCommand { get; }

    public RelayCommand PrepareRuntimeReloadCommand { get; }

    public RelayCommand ShowProjectSettingsCommand { get; }

    public RelayCommand OpenSelectedStoryCommand { get; }

    public RelayCommand CreateStoryCommand { get; }

    public RelayCommand DeleteSelectedStoryCommand { get; }

    public RelayCommand ShowProjectHomeCommand { get; }

    public RelayCommand ShowProjectGraphCommand { get; }


    public RelayCommand ExportSelectedStoryPackageCommand { get; }


    public RelayCommand ToggleResourceBrowserCommand { get; }

    public RelayCommand ToggleBottomPanelCommand { get; }

    public bool HasProject => _projectService.CurrentProject is not null;

    public IReadOnlyList<string> RecentProjectDirectories =>
        RecentProjects.Select(project => project.ProjectDirectory).ToArray();

    public string ProjectDisplayName
    {
        get => _projectDisplayName;
        private set
        {
            if (SetProperty(ref _projectDisplayName, value))
                OnPropertyChanged(nameof(WindowTitle));
        }
    }

    public string WindowTitle => _projectService.CurrentProject is { } project
        ? $"{project.Project.DisplayName} — {StudioBuildInfo.ProductTitle}"
        : StudioBuildInfo.ProductTitle;

    private readonly CanonicalGraphClipboard _graphClipboard = new();

    public string ProjectDirectory
    {
        get => _projectDirectory;
        private set => SetProperty(ref _projectDirectory, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsResourceBrowserVisible
    {
        get => _isResourceBrowserVisible;
        private set
        {
            if (SetProperty(ref _isResourceBrowserVisible, value))
                OnPropertyChanged(nameof(EffectiveResourceBrowserVisible));
        }
    }

    public bool EffectiveResourceBrowserVisible => IsResourceBrowserVisible && !IsCanonicalStoryWorkspaceVisible;

    public bool TryClose()
    {
        if (!HasUnsavedDocuments()) return true;

        return _projectWorkspaceDialogs.ConfirmCloseWithUnsavedChanges() switch
        {
            UnsavedChangesChoice.Save => TrySaveAll(),
            UnsavedChangesChoice.Discard => true,
            _ => false,
        };
    }

    public bool RestoreLastProject(string? projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory))
        {
            return false;
        }

        return OpenProjectFromDirectory(projectDirectory, isRestore: true);
    }

    public void SetRecentProjects(IEnumerable<string>? projectDirectories)
    {
        RecentProjects.Clear();
        if (projectDirectories is null) return;

        foreach (var directory in projectDirectories)
        {
            if (!IsExistingProjectDirectory(directory)
                || RecentProjects.Any(project => PathsEqual(project.ProjectDirectory, directory))) continue;
            AddRecentProject(directory);
        }
    }

    public bool OpenRecentProject(string projectDirectory)
    {
        if (!IsExistingProjectDirectory(projectDirectory))
        {
            RemoveRecentProject(projectDirectory);
            ReportWarning($"最近项目已不存在：{projectDirectory}", "Project");
            return false;
        }

        if (HasUnsavedDocuments())
        {
            ReportWarning("当前项目有未保存的资源；请先保存后再打开其他项目。", "Project");
            return false;
        }

        return OpenProjectFromDirectory(projectDirectory, isRestore: false);
    }

    public void OpenProblem(ProblemItem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        var prefix = problem.Source?.StartsWith("export/story/", StringComparison.Ordinal) == true ? "export/story/" : "canonical/story/";
        if (problem.Source?.StartsWith(prefix, StringComparison.Ordinal) == true
            && problem.NodeId is { } nodeId && problem.GraphResourceId is { } resourceId)
        {
            var storyId = problem.Source[prefix.Length..];
            if (ProjectHome.Stories.FirstOrDefault(story => story.Id == storyId) is { } story) OpenStory(story);
            if (CanonicalStoryWorkspace?.StoryEditor.Id == storyId) CanonicalStoryWorkspace.RequestResourceNodeFocus(resourceId, nodeId, problem.Field);
            return;
        }
        if (TryOpenProjectGraphProblem(problem)) return;
        const string actorPrefix = "actor/";
        if (problem.Source?.StartsWith(actorPrefix, StringComparison.Ordinal) != true) return;
        var actorId = problem.Source[actorPrefix.Length..];
        if (!Actors.Any(actor => actor.Id == actorId)) { ReportWarning("问题引用的角色当前不在资源列表中。", "Problems"); return; }
        var owner = ResourceAddress.FromKey(actorId).StoryUid.Value;
        if (TryOpenCanonicalStory(owner) != CanonicalOpenResult.Opened) return;
        if (CanonicalStoryWorkspace!.Folders.Single(folder => folder.Kind == CanonicalStoryFolderKind.Actors).Items
            .OfType<CanonicalStoryActorItem>().FirstOrDefault(item => item.Id == actorId) is { } item)
            CanonicalStoryWorkspace.SelectTreeItem(item);
    }

    private bool TryOpenProjectGraphProblem(ProblemItem problem)
    {
        var parts = problem.Source?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: >= 1 } || !string.Equals(parts[0], "project-graph", StringComparison.Ordinal))
            return false;
        if (problem.Code is "project_graph.target.missing"
                or "project_graph.target.invalid"
                or "project_graph.enter_story.malformed"
                or "project_graph.enter_story.ambiguous"
            && parts.Length >= 3)
        {
            OpenStoryFlowNode(parts[1], parts[2], "target_story_id");
            return true;
        }

        ShowProjectGraph();
        if (parts.Length >= 2 && ProjectHome.IsGraphVisible && !ProjectHome.Graph.RequestProblemFocus(parts[1]))
            ReportWarning($"图谱问题引用的 Story '{parts[1]}' 当前不存在。", problem.Source);
        return true;
    }

    public CanonicalStoryWorkspaceViewModel? CanonicalStoryWorkspace => _canonicalStoryWorkspace;
    public bool HasCanonicalStoryWorkspace => CanonicalStoryWorkspace is not null;
    public bool IsCanonicalStoryWorkspaceVisible => _isCanonicalStoryWorkspaceVisible;

    public IWorkspaceEditorViewModel? ActiveEditor => IsCanonicalStoryWorkspaceVisible ? CanonicalStoryWorkspace?.ActiveEditor : null;

    private void OpenProject()
    {
        var selectedDirectory = _projectFolderPicker.PickProjectFolder();
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            return;
        }

        if (HasUnsavedDocuments())
        {
            ReportWarning("当前项目有未保存的资源；请先保存后再打开其他项目。", "Project");
            return;
        }

        OpenProjectFromDirectory(selectedDirectory, isRestore: false);
    }

    private bool OpenProjectFromDirectory(string projectDirectory, bool isRestore)
    {
        try
        {
            if (_portableProjects is not null)
            {
                projectDirectory = _portableProjects.PrepareOpen(projectDirectory, isRestore);
            }
            // Reject unsupported graph contracts before changing the active
            // workspace or running startup media cleanup. Never repair user files.
            var candidateStore = _canonicalGraphStoreFactory(projectDirectory);
            _ = candidateStore.Stories.List();
            _ = candidateStore.Sessions.List();
            _ = candidateStore.Tasks.List();
            _ = candidateStore.StoryLogicGraph.Load();
            var firstProject = _projectService.CurrentProject is null;
            var project = _projectService.OpenProject(projectDirectory);
            if (firstProject)
            {
                try
                {
                    if (System.Diagnostics.Process.GetProcessesByName("DarkGreyRPGStudio").Length <= 1)
                        DarkGreyRPG.Studio.Core.Media.ProjectMediaGarbageCollector.CollectAtStartup(project.ProjectDirectory);
                }
                catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException
                    or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or DarkGreyRPG.Studio.Core.Packaging.StoryPackageException
                    or DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceEnvelopeException)
                {
                    Output.Append("已保留媒体孤立文件：" + exception.Message, source: "Project/Media");
                }
            }
            SetCanonicalStoryWorkspace(null);
            _canonicalGraphStore = candidateStore;
            _canonicalSaveCoordinator = new CanonicalGraphResourceSaveCoordinator(_canonicalGraphStore);
            ProjectDisplayName = $"{project.Project.DisplayName} ({project.Project.Id})";
            ProjectDirectory = project.ProjectDirectory;
            _graphClipboard.SetProject(ProjectDirectory);
            RememberProject(project.ProjectDirectory);
            LoadActorList();
            LoadStoryList();
            RefreshHomeResourceFolders();
            ProjectHome.ShowHome();
            RefreshProblems();
            ReportSuccess(
                isRestore
                    ? $"已恢复上次项目，共 {Actors.Count} 个 Actor。"
                    : $"项目已打开，共 {Actors.Count} 个 Actor。",
                "Project");
            OnPropertyChanged(nameof(HasProject));
            RaiseWorkspaceCommandStates();
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException
                or ProjectException or ActorRepositoryException or ActorDataException or ActorValidationException
                or GraphResourceRepositoryException or GraphResourceEnvelopeException
                or CanonicalStoryLogicGraphRepositoryException)
        {
            var detail = exception.Message == exception.GetBaseException().Message
                ? exception.Message : exception.Message + "\n" + exception.GetBaseException().Message;
            if (isRestore)
            {
                ReportWarning($"无法恢复上次项目，文件保持原样：{detail}", "Project");
            }
            else
            {
                ReportFailure("打开项目", new ProjectException(detail + "\n项目文件保持原样。", exception));
            }

            return false;
        }
    }

    private void NewProject()
    {
        if (HasUnsavedDocuments())
        {
            ReportWarning("当前项目有未保存的资源；请先保存后再新建项目。", "Project");
            return;
        }

        var initialParent = _portableProjects?.Paths.Projects ?? (_projectService.CurrentProject is null
            ? null
            : Directory.GetParent(_projectService.CurrentProject.ProjectDirectory)?.FullName);
        var request = _projectWorkspaceDialogs.RequestCreate(initialParent);
        if (request is null)
        {
            return;
        }

        try
        {
            _portableProjects?.ValidateCreationDestination(request.ProjectDirectory);
            var project = _projectService.CreateProject(
                request.ProjectDirectory,
                request.Id,
                request.DisplayName);
            SetCanonicalStoryWorkspace(null);
            _canonicalGraphStore = _canonicalGraphStoreFactory(project.ProjectDirectory);
            _canonicalSaveCoordinator = new CanonicalGraphResourceSaveCoordinator(_canonicalGraphStore);
            ProjectDisplayName = $"{project.Project.DisplayName} ({project.Project.Id})";
            ProjectDirectory = project.ProjectDirectory;
            _graphClipboard.SetProject(ProjectDirectory);
            RememberProject(project.ProjectDirectory);
            LoadActorList();
            LoadStoryList();
            RefreshHomeResourceFolders();
            ProjectHome.ShowHome();
            OnPropertyChanged(nameof(HasProject));
            RaiseWorkspaceCommandStates();
            RefreshProblems();
            ReportSuccess("项目已创建，可开始新建故事。", "Project");
            try { _portableProjects?.RememberCreatedProject(project.ProjectDirectory); }
            catch (DarkGreyRPG.Studio.Settings.SettingsPersistenceException exception)
            {
                ReportWarning("项目已创建，但保存自选项目位置失败：" + exception.Message, "Settings");
            }
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("新建项目", exception);
        }
    }

    private void LoadActorList()
    {
        Actors.Clear();
        var project = _projectService.CurrentProject
            ?? throw new ProjectException("No project is open.");
        foreach (var actor in project.Actors.ListActors())
        {
            Actors.Add(actor);
        }


    }

    private void LoadStoryList()
    {
        var project = _projectService.CurrentProject
            ?? throw new ProjectException("No project is open.");
        var discovery = _canonicalGraphStore is null
            ? new CanonicalStoryDiscoverySnapshot([])
            : new CanonicalStoryDiscoveryService(_canonicalGraphStore).Discover();
        var canonicalProjectGraph = _canonicalGraphStore is null
            ? new CanonicalProjectStoryGraphSnapshot([], [], [])
            : new CanonicalProjectStoryGraphService(_canonicalGraphStore).Derive();
        _canonicalStoryDiscoveryIssues = discovery.Issues;
        ProjectHome.ReplaceDiscoveredStories(
            discovery.Items.Select(ToCanonicalStoryHomeEntry).ToArray(),
            projectDirectory: project.ProjectDirectory,
            canonicalGraph: canonicalProjectGraph);
        LoadReferencedPackages();
        RefreshHomeResourceFolders();
        UpdateProjectGraphProblems();
        UpdateCanonicalStoryDiscoveryProblems();
        OnPropertyChanged(nameof(ProjectHome));
        OpenSelectedStoryCommand.RaiseCanExecuteChanged();
        DeleteSelectedStoryCommand.RaiseCanExecuteChanged();
        CopyStoryContentCommand.RaiseCanExecuteChanged();
        ExportSelectedStoryPackageCommand.RaiseCanExecuteChanged();
    }

    private static CanonicalStoryHomeEntry ToCanonicalStoryHomeEntry(
        CanonicalStoryDiscoveryItem item)
    {
        var membership = item.Membership;
        var graph = item.Story?.Graph;
        return new CanonicalStoryHomeEntry(
            item.Id,
            item.DisplayName,
            membership?.OwnedResources.Actors.Count ?? 0,
            membership?.ReferencedResources.Actors.Count ?? 0,
            (membership?.OwnedResources.Sessions.Count ?? 0)
                + (membership?.ReferencedResources.Sessions.Count ?? 0),
            (membership?.OwnedResources.Tasks.Count ?? 0)
                + (membership?.ReferencedResources.Tasks.Count ?? 0),
            graph?.Nodes.Count ?? 0,
            item.IsComplete,
            item.IsValid,
            item.Issues.Select(issue => issue.Message).ToArray());
    }

    private void OpenSelectedStory()
    {
        if (ProjectHome.SelectedStory is not { } selected || !HasProject) return;
        if (TryOpenCanonicalStory(selected.Id) == CanonicalOpenResult.NotPresent)
        {
            ReportWarning($"故事 '{selected.DisplayName}' 缺少可打开的现行定义；文件保持原样。", $"canonical/story/{selected.Id}");
        }
    }

    private CanonicalOpenResult TryOpenCanonicalStory(string storyId)
    {
        var store = _canonicalGraphStore;
        if (store is null) return CanonicalOpenResult.NotPresent;
        try
        {
            var storyExists = File.Exists(store.Stories.GetPath(storyId));
            var membershipExists = File.Exists(store.Memberships.GetPath(storyId));
            if (!storyExists && !membershipExists) return CanonicalOpenResult.NotPresent;

            if (CanonicalStoryWorkspace?.StoryEditor.Id == storyId)
            {
                ShowRetainedCanonicalStoryWorkspace();
                return CanonicalOpenResult.Opened;
            }
            var workspace = _retainedStoryWorkspaces.Remove(storyId, out var retained) ? retained
                : new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load(storyId),
                    new CanonicalGraphLayoutStore(store.ProjectDirectory));
            if (retained is not null && !retained.HasDirtyEditors)
                workspace.ApplyResourceSnapshot(new CanonicalStoryWorkspaceLoader(store).Load(storyId),
                    workspace.SelectedFolderKind ?? CanonicalStoryFolderKind.Sessions);
            ConfigureCanonicalResourceActions(workspace);
            SetCanonicalStoryWorkspace(workspace);
            Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
            ReplaceCanonicalValidation(workspace);
            StatusMessage = $"已打开故事：{storyId}";
            Output.Append(StatusMessage, source: $"canonical/story/{storyId}");
            return CanonicalOpenResult.Opened;
        }
        catch (Exception exception) when (exception is GraphResourceRepositoryException
            or CanonicalStoryMembershipRepositoryException
            or ActorRepositoryException
            or ActorValidationException
            or ItemRepositoryException
            or ItemValidationException
            or ItemDataException)
        {
            ReportFailure(
                "打开故事",
                exception,
                sourceOverride: $"canonical/story/{storyId}");
            return CanonicalOpenResult.Failed;
        }
    }

    private void ConfigureCanonicalResourceActions(CanonicalStoryWorkspaceViewModel workspace)
    {
        workspace.MediaProjectDirectory = ProjectDirectory;
        var searchGate = new System.Threading.SemaphoreSlim(1, 1);
        string? diskSignature = null;
        IReadOnlyList<AuthoringSearchHit> diskDocuments = [];
        workspace.ProjectSearch = async query =>
        {
            var searchStore = _canonicalGraphStore;
            if (searchStore is null) return workspace.SearchLoaded(query);
            // Disk reads and parsing belong to a worker. Never touch retained UI models there.
            await searchGate.WaitAsync();
            IReadOnlyList<AuthoringSearchHit> disk;
            try
            {
                disk = await Task.Run(() =>
                {
                    var files = System.IO.Directory.EnumerateFiles(searchStore.ProjectDirectory, "*", System.IO.SearchOption.AllDirectories)
                        .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dgrs", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(path => path, StringComparer.Ordinal).Select(path => new System.IO.FileInfo(path));
                    var signature = string.Join("\n", files.Select(file => $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}"));
                    if (signature != diskSignature)
                    {
                        var documents = new List<AuthoringSearchHit>();
                        foreach (var story in searchStore.Stories.List())
                        {
                            using var unloaded = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(searchStore).Load(story.Id), new CanonicalGraphLayoutStore(searchStore.ProjectDirectory));
                            documents.AddRange(unloaded.SearchLoaded(""));
                        }
                        diskDocuments = documents;
                        diskSignature = signature;
                    }
                    return CanonicalStoryWorkspaceViewModel.FilterSearchDocuments(diskDocuments, query);
                });
            }
            finally { searchGate.Release(); }
            var open = _retainedStoryWorkspaces.Values.Concat(CanonicalStoryWorkspace is { } active ? [active] : []).Distinct().ToDictionary(item => item.StoryEditor.Id);
            var results = disk.Where(hit => !open.ContainsKey(hit.StoryId)).ToList();
            foreach (var current in open.Values) results.AddRange(current.SearchLoaded(query));
            return results;
        };
        workspace.ProjectSearchNavigate = hit =>
        {
            if (TryOpenCanonicalStory(hit.StoryId) == CanonicalOpenResult.Opened) CanonicalStoryWorkspace?.NavigateSearch(hit);
        };
        var usageGate = new System.Threading.SemaphoreSlim(1, 1);
        string? usageSignature = null;
        IReadOnlyList<ResourceUsage> diskUsages = [];
        workspace.ProjectUsage = async key =>
        {
            var store = _canonicalGraphStore;
            if (store is null) return workspace.ScanLoadedUsages().Where(u => u.Key == key).ToArray();
            await usageGate.WaitAsync();
            IReadOnlyList<ResourceUsage> disk;
            try
            {
                disk = await Task.Run(() =>
                {
                    var signature = string.Join("\n", System.IO.Directory.EnumerateFiles(store.ProjectDirectory, "*", System.IO.SearchOption.AllDirectories)
                        .Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".dgrs", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal)
                        .Select(p => new System.IO.FileInfo(p)).Select(f => $"{f.FullName}|{f.Length}|{f.LastWriteTimeUtc.Ticks}"));
                    if (signature != usageSignature)
                    {
                        var usages = new List<ResourceUsage>();
                        foreach (var story in store.Stories.List())
                        {
                            using var detached = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load(story.Id));
                            usages.AddRange(detached.ScanLoadedUsages());
                        }
                        diskUsages = usages; usageSignature = signature;
                    }
                    return diskUsages.Where(u => u.Key == key).ToArray();
                });
            }
            finally { usageGate.Release(); }
            if (!ReferenceEquals(store, _canonicalGraphStore)) return [];
            var open = _retainedStoryWorkspaces.Values.Concat(CanonicalStoryWorkspace is { } active ? [active] : []).Distinct().ToDictionary(w => w.StoryEditor.Id);
            return disk.Where(u => !open.ContainsKey(u.Location.StoryId)).Concat(open.Values.SelectMany(w => w.ScanLoadedUsages()).Where(u => u.Key == key)).ToArray();
        };
        _graphClipboard.SetProject(ProjectDirectory);
        workspace.Clipboard = _graphClipboard;
        workspace.OpenProjectWorkspaces = () => _retainedStoryWorkspaces.Values
            .Concat(CanonicalStoryWorkspace is { } active ? [active] : []).Distinct();
        workspace.PortraitEditorFactory = actor =>
        {
            var document = actor.Provider is { } provider
                ? ActorDocument.FromResource(provider.ReadActorDefinition()!, provider.SourcePath)
                : _projectService.OpenActor(actor.Id);
            var editor = new ActorEditorViewModel(document) { IsReadOnly = actor.IsReadOnly };
            if (actor.Provider is { } mediaProvider)
                editor.PortraitPreviewData = mediaRef => OfflineDgrsPackageReader.ReadContainer(mediaProvider.PackagePath)[0]
                    .Entries.TryGetValue("resources/" + mediaRef, out var data) ? data : null;
            editor.IsPortraitVariantReferenced = variant =>
            {
                var sessions = workspace.SessionEditors.Select(item => item.Document.Graph)
                    .Concat(_canonicalGraphStore?.Sessions.List().Select(info => _canonicalGraphStore.Sessions.Load(info.Id).Graph) ?? []);
                return sessions.Where(graph => graph is not null).SelectMany(graph => graph!.Nodes).Any(node => node.Type == "line"
                    && node.Properties.TryGetValue("speaker_actor_id", out var speaker) && speaker.ValueKind == JsonValueKind.String && speaker.GetString() == actor.Id
                    && node.Properties.TryGetValue("portrait_variant", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() == variant.Name);
            };
            editor.PropertyChanged += (_, _) => { ReplaceOpenActorValidation(); SaveCurrentResourceCommand.RaiseCanExecuteChanged(); UndoCurrentCommand.RaiseCanExecuteChanged(); RedoCurrentCommand.RaiseCanExecuteChanged(); };
            return editor;
        };
        if (_projectService.CurrentProject?.Project is { } project)
            workspace.ConfigureProjectBreadcrumb(project.Id, project.DisplayName, ShowProjectHome);
        workspace.ReadOnlyResourceRequested = item =>
        {
            if (item.Provider is { } provider)
                _offlinePackageDialogs.ShowReadOnlyResource(ToOfflineChoice(provider) with
                {
                    RelatedGraphs = OfflineProviderCatalog.Load(ProjectDirectory).Resources
                        .Where(resource => resource.PackageIdentity == provider.PackageIdentity)
                        .Select(ToOfflineChoice).Where(choice => choice.HasGraph).ToArray(),
                });
            else
                _offlinePackageDialogs.ShowReadOnlyResource(new OfflineResourceChoice(
                    item.ResourceKind.ToString(), item.Id, item.DisplayName,
                    "当前项目引用（只读）", item.Editor.CreatePersistenceSnapshot().ToJson()));
        };
        workspace.CreateResourceRequested = CreateCanonicalStoryResource;
        workspace.ReferenceResourceRequested = ReferenceCanonicalStoryResource;
        workspace.CreateActorRequested = CreateCanonicalStoryActor;
        workspace.EditActorPortraitRequested = actor =>
        {
            var project = _projectService.CurrentProject;
            if (project is null || actor.IsReadOnly) return;
            try
            {
                var document = project.Actors.LoadActor(actor.Id);
                if (!document.SupportsPortraits || !_actorWorkspaceDialogs.EditPortraits(document, project.ProjectDirectory)) return;
                project.Actors.SaveActor(document);
                LoadActorList();
                ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, CanonicalStoryFolderKind.Actors, actor.Id);
                ReportSuccess("角色头像与表情已保存。", $"actor/{actor.Id}");
            }
            catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
            {
                ReportFailure("保存角色头像", exception);
            }
        };
        workspace.ReferenceActorRequested = ReferenceCanonicalStoryActor;
        workspace.CreateItemRequested = CreateCanonicalStoryItem;
        workspace.ReferenceItemRequested = ReferenceCanonicalStoryItem;
        workspace.DeleteResourceRequested = DeleteCanonicalStoryResource;
        workspace.RenameResourceRequested = RenameCanonicalStoryResource;
        workspace.ResourceOrderChangeRequested = (kind, handles) => PersistCanonicalStoryResourceOrder(workspace, kind, handles);
        workspace.RefreshResourceCommandStates();
    }

    private bool PersistCanonicalStoryResourceOrder(
        CanonicalStoryWorkspaceViewModel workspace,
        CanonicalStoryFolderKind folderKind,
        IReadOnlyList<string> handles)
    {
        var store = _canonicalGraphStore;
        if (store is null) return false;
        try
        {
            var membership = store.Memberships.Load(workspace.StoryEditor.Id);
            var before = folderKind switch
            {
                CanonicalStoryFolderKind.Actors => membership.DisplayOrder.Actors.ToArray(),
                CanonicalStoryFolderKind.Items => membership.DisplayOrder.Items.ToArray(),
                CanonicalStoryFolderKind.Sessions => membership.DisplayOrder.Sessions.ToArray(),
                CanonicalStoryFolderKind.Tasks => membership.DisplayOrder.Tasks.ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(folderKind)),
            };
            var beforeVisible = workspace.ResourceOrderHandles(folderKind).ToArray();
            var after = handles.ToArray();
            if (beforeVisible.SequenceEqual(after)) return true;
            void Apply(IReadOnlyList<string> saved, IReadOnlyList<string> shown)
            {
                var current = store.Memberships.Load(workspace.StoryEditor.Id);
                var order = current.DisplayOrder;
                switch (folderKind)
                {
                    case CanonicalStoryFolderKind.Actors: order.Actors = [.. saved]; break;
                    case CanonicalStoryFolderKind.Items: order.Items = [.. saved]; break;
                    case CanonicalStoryFolderKind.Sessions: order.Sessions = [.. saved]; break;
                    case CanonicalStoryFolderKind.Tasks: order.Tasks = [.. saved]; break;
                    default: throw new ArgumentOutOfRangeException(nameof(folderKind));
                }
                current.SchemaVersion = CanonicalStoryMembershipManifest.CurrentSchemaVersion;
                current.DisplayOrder = order;
                store.Memberships.Replace(current);
                workspace.ApplyResourceOrder(folderKind, shown);
            }
            workspace.StoryEditor.Host.EditMetadata(() => Apply(before, beforeVisible), () => Apply(after, after));
            UndoCurrentCommand.RaiseCanExecuteChanged(); RedoCurrentCommand.RaiseCanExecuteChanged();
            return true;
        }
        catch (Exception exception) when (exception is CanonicalStoryMembershipException
            or CanonicalStoryMembershipRepositoryException or IOException or UnauthorizedAccessException)
        {
            ReportFailure("保存资源顺序", exception,
                sourceOverride: $"canonical/story/{workspace.StoryEditor.Id}/membership");
            return false;
        }
    }

    private void RenameCanonicalStoryResource(ICanonicalStoryTreeItem item)
    {
        var workspace = CanonicalStoryWorkspace;
        var store = _canonicalGraphStore;
        var project = _projectService.CurrentProject;
        if (workspace is null || store is null || project is null) return;
        try
        {
            switch (item)
            {
                case CanonicalStoryActorItem actor:
                {
                    var label = actor.Actor.Type == CollectiveActorResource.ResourceType ? "角色组" : "角色";
                    var rename = _actorWorkspaceDialogs.RequestResourceRename(label, actor.Id, actor.DisplayName, actor.Actor.Tags);
                    if (rename is null) return;
                    if (rename.Id != actor.Id)
                    {
                        RenameCanonicalResourceIdentity(Core.Identity.DgrResourceKind.Actor, actor.Id, rename);
                        return;
                    }
                    var next = rename.DisplayName;
                    if (next is null || (string.Equals(next.Trim(), actor.DisplayName, StringComparison.Ordinal) && (rename.Tags is null || rename.Tags.SequenceEqual(actor.Actor.Tags)))) return;
                    var resourcePath = project.Actors.GetActorPath(actor.Id);
                    var resourceBefore = File.ReadAllBytes(resourcePath);
                    var document = project.Actors.LoadActor(actor.Id);
                    document.DisplayName = next.Trim();
                    document.SetTags(rename.Tags ?? actor.Actor.Tags);
                    project.Actors.SaveActor(document);
                    LoadActorList();
                    ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, CanonicalStoryFolderKind.Actors, actor.Id);
                    RecordResourceNameChange(resourcePath, resourceBefore);
                    ReportSuccess($"{label} '{actor.Id}' 已编辑。", $"canonical/actor/{actor.Id}");
                    return;
                }
                case CanonicalStoryItemItem itemResource:
                {
                    var isIndividual = itemResource.Item is IndividualItemResource;
                    var label = isIndividual ? "物品" : "物品组";
                    var rename = _itemWorkspaceDialogs.RequestResourceRename(label, itemResource.Id, itemResource.DisplayName, itemResource.Tags);
                    if (rename is null) return;
                    if (rename.Id != itemResource.Id)
                    {
                        RenameCanonicalResourceIdentity(isIndividual ? Core.Identity.DgrResourceKind.Item : Core.Identity.DgrResourceKind.ItemGroup, itemResource.Id, rename);
                        return;
                    }
                    var next = rename.DisplayName;
                    if (next is null || (string.Equals(next.Trim(), itemResource.DisplayName, StringComparison.Ordinal) && (rename.Tags is null || rename.Tags.SequenceEqual(itemResource.Tags)))) return;
                    var repository = new ItemRepository(project.ProjectDirectory);
                    var resourcePath = isIndividual ? repository.GetItemPath(itemResource.Id) : repository.GetGroupPath(itemResource.Id);
                    var resourceBefore = File.ReadAllBytes(resourcePath);
                    if (isIndividual)
                    {
                        var original = repository.LoadItem(itemResource.Id);
                        repository.SaveItem(new IndividualItemResource
                        {
                            SchemaVersion = original.SchemaVersion,
                            ItemId = original.ItemId,
                            DisplayName = next.Trim(),
                            Tags = [.. rename.Tags ?? original.Tags],
                        });
                    }
                    else
                    {
                        var original = repository.LoadGroup(itemResource.Id);
                        repository.SaveGroup(new CollectiveItemResource
                        {
                            SchemaVersion = original.SchemaVersion,
                            GroupId = original.GroupId,
                            DisplayName = next.Trim(),
                            Tags = [.. rename.Tags ?? original.Tags],
                        });
                    }
                    ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, CanonicalStoryFolderKind.Items, itemResource.Id);
                    RecordResourceNameChange(resourcePath, resourceBefore);
                    ReportSuccess($"{label} '{itemResource.Id}' 已编辑。", $"canonical/item/{itemResource.Id}");
                    return;
                }
                case CanonicalStoryGraphItem graph:
                    RenameCanonicalGraphResource(workspace, store, graph);
                    return;
            }
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                "编辑资源",
                exception,
                sourceOverride: $"canonical/story/{workspace.StoryEditor.Id}/resource/{item.Id}");
        }
    }

    private void RenameCanonicalGraphResource(
        CanonicalStoryWorkspaceViewModel workspace,
        CanonicalProjectGraphStore store,
        CanonicalStoryGraphItem graph)
    {
        var label = CanonicalKindLabel(graph.ResourceKind);
        var rename = _canonicalStoryResourceDialogs.RequestResourceRename(label, graph.Id, graph.DisplayName, graph.Editor.Tags);
                    if (rename is null) return;
                    if (rename.Id != graph.Id)
                    {
                        RenameCanonicalResourceIdentity(graph.ResourceKind == GraphResourceKind.Session ? Core.Identity.DgrResourceKind.Session : Core.Identity.DgrResourceKind.Task, graph.Id, rename);
                        return;
                    }
                    var next = rename.DisplayName;
        if (next is null || (string.Equals(next.Trim(), graph.DisplayName, StringComparison.Ordinal) && (rename.Tags is null || rename.Tags.SequenceEqual(graph.Editor.Tags)))) return;
        var repository = CanonicalRepository(store, graph.ResourceKind);
        var original = repository.Load(graph.Id);
        var renamed = new GraphResourceEnvelope(original.ResourceKind, original.Id, next.Trim(), original.Graph!)
        {
            SchemaVersion = original.SchemaVersion,
            Tags = (rename.Tags ?? original.Tags).ToArray(),
        };

        var currentPlan = workspace.StoryEditor.Host.AnalyzeAggregateSynchronization(renamed);
        var diskStory = store.Stories.Load(workspace.StoryEditor.Id);
        var diskHost = new GraphEditorHostViewModel(diskStory.Graph!, GraphScope.StoryFlow);
        var diskPlan = diskHost.AnalyzeAggregateSynchronization(renamed);
        if (!currentPlan.IsSuccess || !diskPlan.IsSuccess)
        {
            ReportCanonicalAggregateSynchronizationFailure(
                graph.Editor,
                currentPlan.Issues.Concat(diskPlan.Issues).ToArray());
            return;
        }
        if (currentPlan.RequiresConfirmation || diskPlan.RequiresConfirmation)
            throw new InvalidOperationException("Display-name-only rename unexpectedly changes aggregate ports.");
        if (!diskHost.ApplyAggregateSynchronization(diskPlan))
            throw new InvalidOperationException("Could not synchronize the persisted Story aggregate display name.");
        diskStory.Graph = diskHost.Graph;

        repository.Replace(renamed);
        try
        {
            store.Stories.Replace(diskStory);
        }
        catch
        {
            repository.Replace(original);
            throw;
        }

        var storyWasDirty = workspace.StoryEditor.IsDirty;
        if (!workspace.ApplyAggregateSynchronization(currentPlan))
            throw new InvalidOperationException("The in-memory Story aggregate could not apply the persisted display name.");
        graph.Editor.ApplyPersistedDisplayName(next);
        graph.Editor.ApplyPersistedTags(renamed.Tags);
        RefreshHomeResourceFolders();
        if (!storyWasDirty) workspace.StoryEditor.MarkSaved();
        ReportSuccess(
            $"{label} '{graph.Id}' 已编辑，故事流程中的聚合显示已同步。",
            $"canonical/{graph.ResourceKind}/{graph.Id}");
    }

    private void CreateCanonicalStoryActor()
    {
        var workspace = CanonicalStoryWorkspace;
        var store = _canonicalGraphStore;
        var project = _projectService.CurrentProject;
        if (workspace is null || store is null || project is null) return;
        try
        {
            var kind = _actorWorkspaceDialogs.RequestCanonicalCreationKind(workspace.StoryEditor.DisplayName);
            if (kind is null) return;
            var suggestedId = project.Actors.GetAvailableId(
                AllocateResourceAddress(workspace.StoryEditor.Id, ResourceKind.Actor));
            var request = _actorWorkspaceDialogs.RequestCreateCanonical(kind.Value, suggestedId);
            if (request is null) return;
            if (request.Kind != kind.Value)
                throw new InvalidOperationException("Actor creation dialog returned a different identity kind.");

            var created = new CanonicalStoryActorLifecycleService(store, project.Actors)
                .CreateOwned(workspace.StoryEditor.Id, request.Kind, suggestedId, request.DisplayName, request.Tags);
            LoadActorList();
            ReloadCanonicalStoryWorkspace(
                workspace.StoryEditor.Id,
                CanonicalStoryFolderKind.Actors,
                created.Id);
            ReportSuccess(
                $"角色“{created.DisplayName}”已创建。",
                $"canonical/actor/{created.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                "创建角色",
                exception,
                sourceOverride: "canonical/actor");
        }
    }

    private void ReferenceCanonicalStoryActor()
    {
        if (_offlinePackageDialogs is not NullOfflinePackageDialogs)
        {
            PickOfflineResourceReference(CanonicalStoryWorkspace?.StoryEditor.Id, CanonicalStoryFolderKind.Actors);
            return;
        }
        var workspace = CanonicalStoryWorkspace;
        var store = _canonicalGraphStore;
        var project = _projectService.CurrentProject;
        if (workspace is null || store is null || project is null) return;
        try
        {
            var presentIds = workspace.Folders
                .Single(folder => folder.Kind == CanonicalStoryFolderKind.Actors)
                .Items.Select(item => item.Id)
                .ToHashSet(StringComparer.Ordinal);
            var candidates = project.Actors.ListActors()
                .Where(actor => !presentIds.Contains(actor.Id))
                .ToArray();
            var choice = _actorWorkspaceDialogs.PickActor(
                candidates,
                ActorPickerMode.Reference,
                workspace.StoryEditor.DisplayName);
            if (choice is null) return;
            if (!candidates.Any(candidate => string.Equals(candidate.Id, choice.Id, StringComparison.Ordinal)))
                throw new InvalidOperationException("Actor picker returned an item outside the offered scope.");

            new CanonicalStoryActorLifecycleService(store, project.Actors)
                .AddReference(workspace.StoryEditor.Id, choice.Id);
            ReloadCanonicalStoryWorkspace(
                workspace.StoryEditor.Id,
                CanonicalStoryFolderKind.Actors,
                choice.Id);
            ReportSuccess(
                $"已引用角色 '{choice.Id}'。",
                $"canonical/actor/{choice.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                "引用角色",
                exception,
                sourceOverride: "canonical/actor");
        }
    }

    private static string AllocateResourceAddress(string owner, ResourceKind kind)
        => ResourceAddress.Create(StoryUid.Parse(owner), kind, new HashSet<ResourceAddress>()).ToKey();

    private void CreateCanonicalStoryResource(GraphResourceKind resourceKind)
    {
        var workspace = CanonicalStoryWorkspace;
        var store = _canonicalGraphStore;
        if (workspace is null || store is null) return;
        try
        {
            var repository = CanonicalRepository(store, resourceKind);
            var suggestedId = repository.GetAvailableId(
                AllocateResourceAddress(workspace.StoryEditor.Id, resourceKind == GraphResourceKind.Session ? ResourceKind.Session : ResourceKind.Task));
            var request = _canonicalStoryResourceDialogs.RequestCreate(resourceKind, suggestedId);
            if (request is null) return;

            var created = new CanonicalStoryResourceLifecycleService(store).CreateOwned(
                workspace.StoryEditor.Id,
                resourceKind,
                suggestedId,
                request.DisplayName, request.Tags);
            ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, resourceKind, created.Id);
            ReportSuccess(
                $"{CanonicalKindLabel(resourceKind)}“{created.DisplayName}”已创建。",
                $"canonical/{resourceKind}/{created.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                $"创建{CanonicalKindLabel(resourceKind)}",
                exception,
                sourceOverride: $"canonical/{resourceKind}");
        }
    }

    private void ReferenceCanonicalStoryResource(GraphResourceKind resourceKind)
    {
        if (_offlinePackageDialogs is not NullOfflinePackageDialogs)
        {
            PickOfflineResourceReference(CanonicalStoryWorkspace?.StoryEditor.Id, FolderFor(resourceKind));
            return;
        }
        var workspace = CanonicalStoryWorkspace;
        var store = _canonicalGraphStore;
        if (workspace is null || store is null) return;
        try
        {
            var repository = CanonicalRepository(store, resourceKind);
            var presentIds = workspace.Folders
                .Single(folder => folder.Kind == FolderFor(resourceKind))
                .Items.Select(item => item.Id)
                .ToHashSet(StringComparer.Ordinal);
            var candidates = repository.List()
                .Where(resource => !presentIds.Contains(resource.Id))
                .ToArray();
            var choice = _canonicalStoryResourceDialogs.PickReference(
                resourceKind,
                candidates,
                workspace.StoryEditor.DisplayName);
            if (choice is null) return;
            if (choice.ResourceKind != resourceKind
                || !candidates.Any(candidate => candidate.ResourceKind == choice.ResourceKind
                    && string.Equals(candidate.Id, choice.Id, StringComparison.Ordinal)))
                throw new InvalidOperationException("Canonical resource picker returned an item outside the offered scope.");

            new CanonicalStoryResourceLifecycleService(store).AddReference(
                workspace.StoryEditor.Id,
                resourceKind,
                choice.Id);
            ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, resourceKind, choice.Id);
            ReportSuccess(
                $"已引用{CanonicalKindLabel(resourceKind)} '{choice.Id}'。",
                $"canonical/{resourceKind}/{choice.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                $"引用{CanonicalKindLabel(resourceKind)}",
                exception,
                sourceOverride: $"canonical/{resourceKind}");
        }
    }

    private void CreateCanonicalStoryItem()
    {
        var workspace = CanonicalStoryWorkspace;
        var store = _canonicalGraphStore;
        var project = _projectService.CurrentProject;
        if (workspace is null || store is null || project is null) return;
        try
        {
            var repository = new ItemRepository(project.ProjectDirectory);
            var mode = _itemWorkspaceDialogs.RequestCreationMode(workspace.StoryEditor.DisplayName);
            if (mode is null) return;
            if (!Enum.IsDefined(mode.Value))
                throw new InvalidOperationException("Item creation dialog returned an unsupported mode.");
            var kind = mode == ItemCreationMode.Individual
                ? CanonicalStoryItemKind.Individual
                : CanonicalStoryItemKind.Collective;
            var suggestedId = kind == CanonicalStoryItemKind.Individual
                ? repository.GetAvailableItemId(AllocateResourceAddress(workspace.StoryEditor.Id, ResourceKind.Item))
                : repository.GetAvailableGroupId(AllocateResourceAddress(workspace.StoryEditor.Id, ResourceKind.ItemGroup));
            var request = _itemWorkspaceDialogs.RequestCreate(kind, suggestedId);
            if (request is null) return;
            if (request.Kind != kind)
                throw new InvalidOperationException("Item creation dialog returned a different resource kind.");

            var created = new CanonicalStoryItemLifecycleService(store, repository).CreateOwned(
                workspace.StoryEditor.Id,
                kind,
                suggestedId,
                request.DisplayName,
                request.Tags);
            ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, CanonicalStoryFolderKind.Items, created.Id);
            ReportSuccess(
                $"{(kind == CanonicalStoryItemKind.Individual ? "物品" : "物品组")}“{created.DisplayName}”已创建。",
                $"canonical/{(kind == CanonicalStoryItemKind.Individual ? "item" : "item_group")}/{created.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure("创建物品", exception, sourceOverride: "canonical/item");
        }
    }

    private void ReferenceCanonicalStoryItem()
    {
        if (_offlinePackageDialogs is not NullOfflinePackageDialogs)
        {
            PickOfflineResourceReference(CanonicalStoryWorkspace?.StoryEditor.Id, CanonicalStoryFolderKind.Items);
            return;
        }
        var workspace = CanonicalStoryWorkspace;
        var store = _canonicalGraphStore;
        var project = _projectService.CurrentProject;
        if (workspace is null || store is null || project is null) return;
        try
        {
            var repository = new ItemRepository(project.ProjectDirectory);
            var present = workspace.Folders
                .Single(folder => folder.Kind == CanonicalStoryFolderKind.Items)
                .Items.OfType<CanonicalStoryItemItem>()
                .Select(item => (item.Type, item.Id))
                .ToHashSet();
            var candidates = repository.ListItems().Concat(repository.ListGroups())
                .Where(info => !present.Contains((info.Type, info.Id)))
                .ToArray();
            var choice = _itemWorkspaceDialogs.PickReference(candidates, workspace.StoryEditor.DisplayName);
            if (choice is null) return;
            if (!candidates.Any(candidate =>
                    string.Equals(candidate.Id, choice.Id, StringComparison.Ordinal)
                    && ((choice.Kind == CanonicalStoryItemKind.Individual && candidate.Type == IndividualItemResource.ResourceType)
                        || (choice.Kind == CanonicalStoryItemKind.Collective && candidate.Type == CollectiveItemResource.ResourceType))))
                throw new InvalidOperationException("Item picker returned an item outside the offered scope.");

            new CanonicalStoryItemLifecycleService(store, repository).AddReference(
                workspace.StoryEditor.Id, choice.Kind, choice.Id);
            ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, CanonicalStoryFolderKind.Items, choice.Id);
            ReportSuccess(
                $"已引用{(choice.Kind == CanonicalStoryItemKind.Individual ? "物品" : "物品组")} '{choice.Id}'。",
                $"canonical/{(choice.Kind == CanonicalStoryItemKind.Individual ? "item" : "item_group")}/{choice.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure("引用物品", exception, sourceOverride: "canonical/item");
        }
    }

    private void DeleteCanonicalStoryResource(ICanonicalStoryTreeItem item)
    {
        if (TryRemoveIndependentReference(item)) return;
        var workspace = CanonicalStoryWorkspace;
        var store = _canonicalGraphStore;
        if (workspace is null || store is null) return;
        if (item is CanonicalStoryActorItem
            || item is CanonicalStoryMissingItem { FolderKind: CanonicalStoryFolderKind.Actors })
        {
            DeleteCanonicalStoryActor(item, workspace, store);
            return;
        }
        if (TryDescribeCanonicalItem(item, store, out var itemChoice, out var itemIsReferenced, out var itemIsMissing))
        {
            DeleteCanonicalStoryItem(itemChoice, itemIsReferenced, itemIsMissing, workspace, store);
            return;
        }
        if (!TryDescribeCanonicalResource(item, store, out var choice, out var isReferenced, out var isMissing))
            return;

        var storyId = workspace.StoryEditor.Id;
        var storyDisplayName = workspace.StoryEditor.DisplayName;
        var service = new CanonicalStoryResourceLifecycleService(store);
        try
        {
            // Resource lifecycle cleanup is computed from the persisted Story.
            // Advance that baseline first so reloading the committed cleanup
            // cannot discard unsaved placements or resurrect stale ones later.
            if (workspace.StoryEditor.IsDirty)
            {
                var coordinator = _canonicalSaveCoordinator
                    ?? throw new InvalidOperationException("Canonical save coordinator is unavailable.");
                coordinator.Replace(workspace.StoryEditor);
            }
            if (isReferenced)
            {
                if (!_canonicalStoryResourceDialogs.ConfirmRemoveReference(choice, storyDisplayName)) return;
                service.RemoveReference(storyId, choice.ResourceKind, choice.Id);
                ReloadCanonicalStoryWorkspace(storyId, choice.ResourceKind);
                ReportSuccess(
                    $"已解除{CanonicalKindLabel(choice.ResourceKind)} '{choice.Id}' 的引用。",
                    $"canonical/{choice.ResourceKind}/{choice.Id}");
                return;
            }

            if (isMissing)
            {
                ReportWarning(
                    $"拥有的{CanonicalKindLabel(choice.ResourceKind)} '{choice.Id}' 文件缺失，无法执行安全删除。",
                    $"canonical/{choice.ResourceKind}/{choice.Id}");
                return;
            }

            var plan = service.GetDeletionPlan(storyId, choice.ResourceKind, choice.Id);
            if (!plan.CanDelete)
            {
                _canonicalStoryResourceDialogs.ShowDeleteBlocked(choice, plan.ReferencingStoryIds);
                ReportWarning(
                    $"{CanonicalKindLabel(choice.ResourceKind)} '{choice.Id}' 仍被其它故事引用，未删除。",
                    $"canonical/{choice.ResourceKind}/{choice.Id}");
                return;
            }
            if (!_canonicalStoryResourceDialogs.ConfirmDeleteOwned(choice)) return;

            service.DeleteOwned(storyId, choice.ResourceKind, choice.Id);
            ReloadCanonicalStoryWorkspace(storyId, choice.ResourceKind);
            ReportSuccess(
                $"{CanonicalKindLabel(choice.ResourceKind)} '{choice.Id}' 已删除。",
                $"canonical/{choice.ResourceKind}/{choice.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                $"更新{CanonicalKindLabel(choice.ResourceKind)}",
                exception,
                sourceOverride: $"canonical/{choice.ResourceKind}/{choice.Id}");
        }
    }

    private void DeleteCanonicalStoryItem(
        ItemWorkspaceChoice choice,
        bool isReferenced,
        bool isMissing,
        CanonicalStoryWorkspaceViewModel workspace,
        CanonicalProjectGraphStore store)
    {
        var project = _projectService.CurrentProject;
        if (project is null) return;
        var storyId = workspace.StoryEditor.Id;
        var repository = new ItemRepository(project.ProjectDirectory);
        var service = new CanonicalStoryItemLifecycleService(store, repository);
        var label = choice.Kind == CanonicalStoryItemKind.Individual ? "物品" : "物品组";
        try
        {
            if (isReferenced)
            {
                if (!_itemWorkspaceDialogs.ConfirmRemoveReference(choice, workspace.StoryEditor.DisplayName)) return;
                service.RemoveReference(storyId, choice.Kind, choice.Id);
                ReloadCanonicalStoryWorkspace(storyId, CanonicalStoryFolderKind.Items);
                ReportSuccess($"已解除{label} '{choice.Id}' 的引用；文件未删除。", $"canonical/item/{choice.Id}");
                return;
            }
            if (isMissing)
            {
                ReportWarning($"拥有的{label} '{choice.Id}' 文件缺失，无法执行安全删除。", $"canonical/item/{choice.Id}");
                return;
            }

            var plan = service.GetDeletionPlan(storyId, choice.Kind, choice.Id);
            if (!plan.CanDelete)
            {
                _itemWorkspaceDialogs.ShowDeleteBlocked(choice, plan.ReferencingStoryIds);
                ReportWarning($"{label} '{choice.Id}' 仍被其它故事使用，未删除。", $"canonical/item/{choice.Id}");
                return;
            }
            if (!_itemWorkspaceDialogs.ConfirmDeleteOwned(choice)) return;
            service.DeleteOwned(storyId, choice.Kind, choice.Id);
            ReloadCanonicalStoryWorkspace(storyId, CanonicalStoryFolderKind.Items);
            ReportSuccess($"{label} '{choice.Id}' 已删除。", $"canonical/item/{choice.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure($"更新{label}", exception, sourceOverride: $"canonical/item/{choice.Id}");
        }
    }

    private void DeleteCanonicalStoryActor(
        ICanonicalStoryTreeItem item,
        CanonicalStoryWorkspaceViewModel workspace,
        CanonicalProjectGraphStore store)
    {
        var project = _projectService.CurrentProject;
        if (project is null
            || !TryDescribeCanonicalActor(item, project.Actors, out var actor, out var isReferenced, out var isMissing))
            return;

        var storyId = workspace.StoryEditor.Id;
        var service = new CanonicalStoryActorLifecycleService(store, project.Actors);
        try
        {
            if (isReferenced)
            {
                if (!_actorWorkspaceDialogs.ConfirmRemoveReference(actor, workspace.StoryEditor.DisplayName)) return;
                service.RemoveReference(storyId, actor.Id);
                ReloadCanonicalStoryWorkspace(storyId, CanonicalStoryFolderKind.Actors);
                ReportSuccess(
                    $"已解除角色 '{actor.Id}' 的引用；角色文件未删除。",
                    $"canonical/actor/{actor.Id}");
                return;
            }

            if (isMissing)
            {
                ReportWarning(
                    $"拥有的角色 '{actor.Id}' 文件缺失，无法执行安全删除。",
                    $"canonical/actor/{actor.Id}");
                return;
            }

            var plan = service.GetDeletionPlan(storyId, actor.Id);
            if (!plan.CanDelete)
            {
                _actorWorkspaceDialogs.ShowReferences(
                    actor,
                    DescribeCanonicalActorBlockers(plan, store));
                ReportWarning(
                    $"角色 '{actor.Id}' 仍被其它故事占用/引用，未删除。",
                    $"canonical/actor/{actor.Id}");
                return;
            }
            if (!_actorWorkspaceDialogs.ConfirmDelete(actor)) return;

            _projectService.ReleaseOpenActor(actor.Id);
            service.DeleteOwned(storyId, actor.Id);
            LoadActorList();
            ReloadCanonicalStoryWorkspace(storyId, CanonicalStoryFolderKind.Actors);
            ReportSuccess(
                $"角色 '{actor.Id}' 已从角色目录删除。",
                $"canonical/actor/{actor.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                "更新角色",
                exception,
                sourceOverride: $"canonical/actor/{actor.Id}");
        }
    }

    private void ReloadCanonicalStoryWorkspace(
        string storyId,
        GraphResourceKind selectedKind,
        string? selectedResourceId = null)
        => ReloadCanonicalStoryWorkspace(storyId, FolderFor(selectedKind), selectedResourceId);

    private void ReloadCanonicalStoryWorkspace(
        string storyId,
        CanonicalStoryFolderKind selectedFolderKind,
        string? selectedResourceId = null)
    {
        var store = _canonicalGraphStore ?? throw new InvalidOperationException("Canonical graph store is unavailable.");
        var snapshot = new CanonicalStoryWorkspaceLoader(store).Load(storyId);
        var workspace = CanonicalStoryWorkspace
            ?? throw new InvalidOperationException("Canonical Story workspace is unavailable.");
        workspace.ApplyResourceSnapshot(snapshot, selectedFolderKind, selectedResourceId);
        RefreshHomeResourceFolders();
        ReplaceCanonicalValidation(workspace);
    }

    private static bool TryDescribeCanonicalResource(
        ICanonicalStoryTreeItem item,
        CanonicalProjectGraphStore store,
        out CanonicalGraphResourceChoice choice,
        out bool isReferenced,
        out bool isMissing)
    {
        GraphResourceKind kind;
        string id;
        string displayName;
        switch (item)
        {
            case CanonicalStoryGraphItem graph:
                kind = graph.ResourceKind;
                id = graph.Id;
                displayName = graph.DisplayName;
                isReferenced = graph.IsReferenced;
                isMissing = false;
                break;
            case CanonicalStoryMissingItem { FolderKind: CanonicalStoryFolderKind.Sessions or CanonicalStoryFolderKind.Tasks } missing:
                kind = missing.FolderKind == CanonicalStoryFolderKind.Sessions
                    ? GraphResourceKind.Session
                    : GraphResourceKind.Task;
                id = missing.Id;
                displayName = missing.DisplayName;
                isReferenced = missing.IsReferenced;
                isMissing = true;
                break;
            default:
                choice = null!;
                isReferenced = false;
                isMissing = false;
                return false;
        }

        choice = new CanonicalGraphResourceChoice(
            kind,
            id,
            displayName,
            CanonicalRepository(store, kind).GetPath(id));
        return true;
    }

    private static bool TryDescribeCanonicalItem(
        ICanonicalStoryTreeItem item,
        CanonicalProjectGraphStore store,
        out ItemWorkspaceChoice choice,
        out bool isReferenced,
        out bool isMissing)
    {
        CanonicalStoryItemKind kind;
        string id;
        string displayName;
        string sourcePath;
        IReadOnlyList<string> tags;
        switch (item)
        {
            case CanonicalStoryItemItem resolved:
                kind = resolved.Item is IndividualItemResource
                    ? CanonicalStoryItemKind.Individual
                    : CanonicalStoryItemKind.Collective;
                id = resolved.Id;
                displayName = resolved.DisplayName;
                sourcePath = resolved.Item is IndividualItemResource
                    ? new ItemRepository(store.ProjectDirectory).GetItemPath(id)
                    : new ItemRepository(store.ProjectDirectory).GetGroupPath(id);
                tags = resolved.Tags;
                isReferenced = resolved.IsReferenced;
                isMissing = false;
                break;
            case CanonicalStoryMissingItem { FolderKind: CanonicalStoryFolderKind.Items } missing:
                // A missing entry still has enough identity to show a safe
                // delete warning, but must never be handed to DeleteOwned.
                kind = string.Equals(missing.Issue.Field, "item_groups", StringComparison.Ordinal)
                    ? CanonicalStoryItemKind.Collective
                    : CanonicalStoryItemKind.Individual;
                id = missing.Id;
                displayName = missing.DisplayName;
                var missingRepository = new ItemRepository(store.ProjectDirectory);
                sourcePath = kind == CanonicalStoryItemKind.Individual
                    ? missingRepository.GetItemPath(id)
                    : missingRepository.GetGroupPath(id);
                tags = [];
                isReferenced = missing.IsReferenced;
                isMissing = true;
                break;
            default:
                choice = null!;
                isReferenced = false;
                isMissing = false;
                return false;
        }

        choice = new ItemWorkspaceChoice(kind, id, displayName, sourcePath, tags);
        return true;
    }

    private static bool TryDescribeCanonicalActor(
        ICanonicalStoryTreeItem item,
        ActorRepository repository,
        out ActorResourceInfo actor,
        out bool isReferenced,
        out bool isMissing)
    {
        switch (item)
        {
            case CanonicalStoryActorItem resolved:
                actor = resolved.Actor;
                isReferenced = resolved.IsReferenced;
                isMissing = false;
                return true;
            case CanonicalStoryMissingItem { FolderKind: CanonicalStoryFolderKind.Actors } missing:
                actor = new ActorResourceInfo(
                    missing.Id,
                    missing.DisplayName,
                    Path.Combine(repository.ActorsDirectory, missing.Id + ".json"),
                    []);
                isReferenced = missing.IsReferenced;
                isMissing = true;
                return true;
            default:
                actor = null!;
                isReferenced = false;
                isMissing = false;
                return false;
        }
    }

    private static IReadOnlyList<ResourceDescriptor> DescribeCanonicalActorBlockers(CanonicalStoryActorDeletionPlan plan, CanonicalProjectGraphStore store)
        => plan.Blockers.Select(blocker =>
        {
            string displayName;
            try { displayName = store.Stories.Load(blocker.StoryId).DisplayName; }
            catch (GraphResourceRepositoryException) { displayName = blocker.StoryId; }
            return new ResourceDescriptor(ProjectResourceType.Story, blocker.StoryId,
                $"[{(blocker.IsOwned ? "占用" : "引用")}] {displayName}", store.Memberships.GetPath(blocker.StoryId));
        }).ToArray();

    private static GraphResourceRepository CanonicalRepository(
        CanonicalProjectGraphStore store,
        GraphResourceKind resourceKind) => resourceKind switch
    {
        GraphResourceKind.Session => store.Sessions,
        GraphResourceKind.Task => store.Tasks,
        _ => throw new ArgumentOutOfRangeException(nameof(resourceKind)),
    };

    private static CanonicalStoryFolderKind FolderFor(GraphResourceKind resourceKind) => resourceKind switch
    {
        GraphResourceKind.Session => CanonicalStoryFolderKind.Sessions,
        GraphResourceKind.Task => CanonicalStoryFolderKind.Tasks,
        _ => throw new ArgumentOutOfRangeException(nameof(resourceKind)),
    };

    private static string CanonicalKindLabel(GraphResourceKind resourceKind) => resourceKind switch
    {
        GraphResourceKind.Story => "故事",
        GraphResourceKind.Session => "会话",
        GraphResourceKind.Task => "任务",
        _ => throw new ArgumentOutOfRangeException(nameof(resourceKind)),
    };

    private static bool IsCanonicalResourceLifecycleException(Exception exception)
        => exception is CanonicalStoryLifecycleException
            or CanonicalStoryResourceLifecycleException
            or CanonicalStoryActorLifecycleException
            or CanonicalStoryItemLifecycleException
            or ProjectException
            or GraphResourceRepositoryException
            or CanonicalStoryMembershipRepositoryException
            or ActorRepositoryException
            or ActorValidationException
            or ItemRepositoryException
            or ItemValidationException
            or ItemDataException
            or IOException
            or UnauthorizedAccessException
            or InvalidOperationException;

    private void SetCanonicalStoryWorkspace(CanonicalStoryWorkspaceViewModel? workspace, bool retainDrafts = false)
    {
        if (ReferenceEquals(_canonicalStoryWorkspace, workspace)) return;
        if (_canonicalStoryWorkspace is not null)
        {
            _canonicalStoryWorkspace.PropertyChanged -= OnCanonicalStoryWorkspacePropertyChanged;
            Problems.RemoveSourceTree($"canonical/story/{_canonicalStoryWorkspace.StoryEditor.Id}");
            if ((workspace is not null || retainDrafts) && _canonicalStoryWorkspace.HasDirtyEditors)
                _retainedStoryWorkspaces[_canonicalStoryWorkspace.StoryEditor.Id] = _canonicalStoryWorkspace;
            else _canonicalStoryWorkspace.Dispose();
        }
        if (workspace is null && !retainDrafts)
        {
            foreach (var retained in _retainedStoryWorkspaces.Values) retained.Dispose();
            _retainedStoryWorkspaces.Clear();
        }
        _canonicalStoryWorkspace = workspace;
        if (_canonicalStoryWorkspace is not null)
        {
            _canonicalStoryWorkspace.PropertyChanged -= OnCanonicalStoryWorkspacePropertyChanged;
            _canonicalStoryWorkspace.PropertyChanged += OnCanonicalStoryWorkspacePropertyChanged;
        }
        SetCanonicalStoryWorkspaceVisible(workspace is not null);
        OnPropertyChanged(nameof(CanonicalStoryWorkspace));
        OnPropertyChanged(nameof(HasCanonicalStoryWorkspace));
        OnPropertyChanged(nameof(EffectiveResourceBrowserVisible));
        RaiseCurrentEditorStates();
    }

    private void SetCanonicalStoryWorkspaceVisible(bool value)
    {
        if (_isCanonicalStoryWorkspaceVisible == value) return;
        _isCanonicalStoryWorkspaceVisible = value;
        OnPropertyChanged(nameof(IsCanonicalStoryWorkspaceVisible));
        OnPropertyChanged(nameof(EffectiveResourceBrowserVisible));
        RaiseCurrentEditorStates();
    }

    private void ShowRetainedCanonicalStoryWorkspace()
    {
        if (CanonicalStoryWorkspace is null) return;
        SetCanonicalStoryWorkspaceVisible(true);
        Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
        StatusMessage = $"已返回故事：{CanonicalStoryWorkspace.StoryEditor.Id}";
        Output.Append(StatusMessage, source: $"canonical/story/{CanonicalStoryWorkspace.StoryEditor.Id}");
    }

    private void OnCanonicalStoryWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(CanonicalStoryWorkspaceViewModel.ActiveEditor)
            or nameof(CanonicalStoryWorkspaceViewModel.HasDirtyEditors)
            or nameof(CanonicalStoryWorkspaceViewModel.InspectorPortraitEditor))
            RaiseCurrentEditorStates();
        if (args.PropertyName is nameof(CanonicalStoryWorkspaceViewModel.ActiveEditor)
            or nameof(CanonicalStoryWorkspaceViewModel.InspectorValidationText)
            or nameof(CanonicalStoryWorkspaceViewModel.ValidationIssues))
            RefreshProblems();
    }

    private void CreateStory() => CreateStory(null);

    private void CreateStory(System.Windows.Point? graphPosition)
    {
        var project = _projectService.CurrentProject;
        if (project is null || _canonicalGraphStore is null) return;

        try
        {
            var lifecycle = new CanonicalStoryLifecycleService(_canonicalGraphStore, project.Actors);
            var allocatedUid = lifecycle.AllocateStoryUid();
            var request = _resourceWorkspaceDialogs.RequestCreateStory(allocatedUid.Value);
            if (request is null) return;

            var story = lifecycle.Create(allocatedUid.Value, request.DisplayName);
            LoadStoryList();
            if (graphPosition is { } position)
                ProjectHome.Graph.CanonicalHost?.SetNodePosition(story.Id, position.X, position.Y);
            ProjectHome.SelectedStory = ProjectHome.Stories.Single(item => item.Id == story.Id);
            RefreshHomeResourceFolders();
            ProjectHome.ShowHome();
            StatusMessage = $"故事 '{story.Id}' 已创建。";
            Output.Append(StatusMessage, OutputKind.Success, $"canonical/story/{story.Id}");
            Toast.Show(StatusMessage, ToastKind.Success);
        }
        catch (Exception exception) when (
            IsWorkspaceException(exception)
            || IsCanonicalResourceLifecycleException(exception)
            || IsRecoverableUiException(exception))
        {
            ReportFailure("新建故事", exception);
        }
        finally
        {
            RaiseWorkspaceCommandStates();
        }
    }

    private void DeleteSelectedStory()
    {
        if (ProjectHome.SelectedStory is { } selected && HasProject) DeleteSelectedCanonicalStory(selected);
    }

    public void OpenStory(StoryListItemViewModel story)
    {
        ArgumentNullException.ThrowIfNull(story);
        ProjectHome.SelectedStory = story;
        OpenSelectedStoryCommand.Execute(null);
    }

    public void OpenStoryFlow(string storyId)
    {
        if (ProjectHome.Stories.FirstOrDefault(item => item.Id == storyId) is not { } story) return;
        OpenStory(story);
        if (CanonicalStoryWorkspace?.StoryEditor.Id != storyId) return;
        CanonicalStoryWorkspace.ReturnToStory();
        StatusMessage = $"已从故事图谱打开故事流程：{storyId}";
        Output.Append(StatusMessage, source: $"canonical/story/{storyId}");
    }

    public void OpenStoryFlowNode(string storyId, string nodeId, string? field = null)
    {
        OpenStoryFlow(storyId);
        if (CanonicalStoryWorkspace?.StoryEditor.Id == storyId && !CanonicalStoryWorkspace.RequestStoryNodeFocus(nodeId, field))
            ReportWarning($"故事流程 '{storyId}' 中不存在唯一节点 '{nodeId}'。", $"canonical/story/{storyId}/flow/{nodeId}");
    }

    public void FocusProjectGraphProblems()
    {
        UpdateProjectGraphProblems();
        BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Problems");
        BottomPanel.IsExpanded = true;
    }

    private void ShowProjectHome()
    {
        if (CanonicalStoryWorkspace is { } workspace)
        {
            ProjectHome.Graph.RefreshStoryBoundary(workspace.StoryEditor.CreatePersistenceSnapshot(),
                () => TrySaveCanonicalResource(workspace.StoryEditor, workspace));
            SetCanonicalStoryWorkspaceVisible(false);
            ProjectHome.SelectedStory = ProjectHome.Stories.FirstOrDefault(story => story.Id == workspace.StoryEditor.Id);
            RefreshHomeResourceFolders();
        }
        ProjectHome.ShowHome();
        Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
    }

    private void ShowProjectGraph()
    {
        SetCanonicalStoryWorkspace(null, retainDrafts: true);
        LoadStoryList();
        ProjectHome.ShowGraph();
        Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
    }

    private bool TrySaveCanonicalResource(CanonicalGraphResourceEditorViewModel? target = null, CanonicalStoryWorkspaceViewModel? owner = null)
    {
        var workspace = owner ?? CanonicalStoryWorkspace;
        var coordinator = _canonicalSaveCoordinator;
        if (workspace is null || coordinator is null) return false;
        var editor = target ?? workspace.ActiveEditor;
        if (!workspace.IsWritableEditor(editor)) return false;
        if (!editor.IsDirty) return true;
        CanonicalAggregateSynchronizationPlan? synchronization = null;
        var synchronizationCommitted = false;
        if (editor.IsGraphDirty
            && editor.ResourceKind is GraphResourceKind.Session or GraphResourceKind.Task)
        {
            synchronization = workspace.AnalyzeAggregateSynchronization(editor);
            if (!synchronization.IsSuccess)
            {
                ReportCanonicalAggregateSynchronizationFailure(editor, synchronization.Issues);
                return false;
            }

            var choice = new CanonicalGraphResourceChoice(
                editor.ResourceKind,
                editor.Id,
                editor.DisplayName);
            if (synchronization.RequiresConfirmation
                && !_canonicalStoryResourceDialogs.ConfirmAggregateInterfaceRemoval(
                    choice,
                    synchronization.ObsoleteReferences))
            {
                ReportWarning(
                    $"已取消保存{CanonicalKindLabel(editor.ResourceKind)} '{editor.Id}'；故事流程外部连线未更改。",
                    $"canonical/{editor.ResourceKind}/{editor.Id}");
                return false;
            }

            var undoCount = workspace.StoryEditor.Host.Session.UndoCount;
            if (!workspace.ApplyAggregateSynchronization(
                    synchronization,
                    confirmReferencedRemoval: synchronization.RequiresConfirmation))
            {
                ReportCanonicalAggregateSynchronizationFailure(
                    editor,
                    workspace.StoryEditor.Host.LastValidationIssues);
                return false;
            }
            synchronizationCommitted = workspace.StoryEditor.Host.Session.UndoCount == undoCount + 1;
        }
        try
        {
            coordinator.Replace(editor);
            var synchronizationSummary = synchronizationCommitted
                ? $"；Story Flow 中 {synchronization!.Placements.Count} 个聚合节点已同步"
                : string.Empty;
            ReportSuccess(
                $"{CanonicalKindLabel(editor.ResourceKind)} '{editor.Id}' 已保存到磁盘{synchronizationSummary}。",
                $"canonical/{editor.ResourceKind}/{editor.Id}");
            RaiseCurrentEditorStates();
            return true;
        }
        catch (Exception exception) when (exception is GraphResourceRepositoryException
            or IOException
            or UnauthorizedAccessException)
        {
            if (exception is GraphResourceRepositoryException
                && synchronizationCommitted
                && !workspace.RollbackLastStoryEdit())
            {
                ReportWarning(
                    $"{CanonicalKindLabel(editor.ResourceKind)} '{editor.Id}' 写入失败，且故事流程同步回滚失败；请勿继续保存并检查“问题”面板。",
                    $"canonical/{editor.ResourceKind}/{editor.Id}");
            }
            ReportFailure(
                "保存图资源或布局",
                exception,
                sourceOverride: $"canonical/{editor.ResourceKind}/{editor.Id}");
            RaiseCurrentEditorStates();
            return false;
        }
    }

    private void DeleteSelectedCanonicalStory(StoryListItemViewModel selected)
    {
        var project = _projectService.CurrentProject;
        if (project is null || _canonicalGraphStore is null) return;

        try
        {
            var service = new CanonicalStoryLifecycleService(
                _canonicalGraphStore,
                project.Actors);
            var plan = service.GetDeletionPlan(selected.Id);
            if (!plan.CanDelete)
            {
                ReportWarning(
                    $"无法删除故事 '{selected.Id}'：{string.Join("；", plan.Blockers.Select(blocker => blocker.Message))}",
                    $"canonical/story/{selected.Id}");
                return;
            }

            var resourcesToDelete = plan.ActorIds.Select(id => $"角色：{id}")
                .Concat(plan.ItemIds.Select(id => $"物品：{id}"))
                .Concat(plan.ItemGroupIds.Select(id => $"物品组：{id}"))
                .Concat(plan.SessionIds.Select(id => $"会话：{id}"))
                .Concat(plan.TaskIds.Select(id => $"任务：{id}"))
                .ToArray();
            if (!_projectWorkspaceDialogs.ConfirmDeleteCanonicalStory(
                    selected.Id,
                    selected.DisplayName,
                    resourcesToDelete))
                return;

            service.Delete(selected.Id);
            foreach (var actorId in plan.ActorIds)
                _projectService.ReleaseOpenActor(actorId, discardUnsavedChanges: true);
            if (_retainedStoryWorkspaces.Remove(selected.Id, out var deletedDraft))
                deletedDraft.Dispose();
            if (string.Equals(CanonicalStoryWorkspace?.StoryEditor.Id, selected.Id, StringComparison.Ordinal))
            {
                // Discard only the deleted story; other story drafts remain in the project.
                _canonicalStoryWorkspace!.PropertyChanged -= OnCanonicalStoryWorkspacePropertyChanged;
                _canonicalStoryWorkspace.Dispose();
                _canonicalStoryWorkspace = null;
                SetCanonicalStoryWorkspaceVisible(false);
                OnPropertyChanged(nameof(CanonicalStoryWorkspace));
                OnPropertyChanged(nameof(HasCanonicalStoryWorkspace));
                OnPropertyChanged(nameof(EffectiveResourceBrowserVisible));
                RaiseCurrentEditorStates();
            }
            LoadActorList();
            LoadStoryList();
            ProjectHome.SelectedStory = ProjectHome.Stories.FirstOrDefault(story => story.Id == selected.Id);
            RefreshHomeResourceFolders();
            ProjectHome.ShowHome();
            ReportSuccess(
                $"故事 '{selected.Id}' 已从项目中删除。",
                $"canonical/story/{selected.Id}");
        }
        catch (Exception exception) when (
            IsWorkspaceException(exception)
            || IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                "删除故事",
                exception,
                sourceOverride: $"canonical/story/{selected.Id}");
        }
        finally
        {
            RaiseWorkspaceCommandStates();
        }
    }

    private void ReportCanonicalAggregateSynchronizationFailure(
        CanonicalGraphResourceEditorViewModel editor,
        IReadOnlyList<ValidationIssue> issues)
    {
        var source = $"canonical/{editor.ResourceKind}/{editor.Id}";
        ReplaceValidationSource(source, issues);
        BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Problems");
        var details = issues.Count == 0
            ? "未知同步错误。"
            : string.Join("；", issues.Select(issue => issue.Message));
        ReportWarning($"保存前无法同步 Story Flow 聚合接口：{details}", source);
    }

    private ActorDocument SaveOpenActor(ActorDocument document)
    {
        var project = _projectService.CurrentProject
            ?? throw new ProjectException("No project is open.");
        if (_canonicalGraphStore is not null && !string.IsNullOrWhiteSpace(document.HomeStoryId))
        {
            var storyPath = _canonicalGraphStore.Stories.GetPath(document.HomeStoryId);
            var membershipPath = _canonicalGraphStore.Memberships.GetPath(document.HomeStoryId);
            if (File.Exists(storyPath) && File.Exists(membershipPath))
            {
                _ = _canonicalGraphStore.Stories.Load(document.HomeStoryId);
                var membership = _canonicalGraphStore.Memberships.Load(document.HomeStoryId);
                if (membership.OwnedResources.Actors.Contains(document.Id, StringComparer.Ordinal))
                    return project.Actors.SaveActor(document);
            }
        }
        return _projectService.SaveActor(document);
    }

    private void SaveAll()
        => TrySaveAll();

    private bool TrySaveAll()
    {
        foreach (var retained in _retainedStoryWorkspaces.Values.ToArray())
            if (!TrySaveAllCanonicalResources(retained)) return false;
        if (CanonicalStoryWorkspace is { } workspace && !TrySaveAllCanonicalResources(workspace)) return false;
        try
        {
            foreach (var actor in _projectService.OpenActorDocuments.Where(document => document.IsDirty).ToArray()) SaveOpenActor(actor);
            _projectService.SaveAll();
            ReportSuccess("所有未保存的资源已写入磁盘。", "Project");
            RaiseCurrentEditorStates();
            return !HasUnsavedDocuments();
        }
        catch (Exception exception) when (IsWorkspaceException(exception) || IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure("保存全部", exception);
            return false;
        }
    }

    private bool TrySaveAllCanonicalResources(CanonicalStoryWorkspaceViewModel workspace)
    {
        var dirtyEditors = workspace.SessionEditors
            .Concat(workspace.TaskEditors)
            .Where(editor => workspace.IsWritableEditor(editor) && editor.IsDirty)
            .ToArray();
        foreach (var editor in dirtyEditors)
        {
            if (!TrySaveCanonicalResource(editor, workspace)) return false;
        }
        if (workspace.StoryEditor.IsDirty
            && !TrySaveCanonicalResource(workspace.StoryEditor, workspace)) return false;

        ReportSuccess("Canonical Story 的所有未保存图资源与布局已写入磁盘。", "canonical/story");
        RaiseCurrentEditorStates();
        return true;
    }

    private bool CanSaveAll() =>
        HasProject && HasUnsavedDocuments();

    private bool HasUnsavedDocuments() => _projectService.OpenActorDocuments.Any(document => document.IsDirty)
        || CanonicalStoryWorkspace?.HasDirtyEditors == true || _retainedStoryWorkspaces.Values.Any(workspace => workspace.HasDirtyEditors);

    private void OpenProjectDirectory()
    {
        if (_projectService.CurrentProject is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_projectService.CurrentProject.ProjectDirectory}\"",
                UseShellExecute = true,
            });
            Output.Append("已在文件资源管理器中打开项目文件夹。", source: "Project");
        }
        catch (Exception exception) when (exception is SystemException)
        {
            ReportFailure("打开项目文件夹", exception);
        }
    }

    private void ExportSelectedStoryPackage()
    {
        var project = _projectService.CurrentProject;
        var story = ProjectHome.SelectedStory;
        if (project is null || story is null) return;

        var suggestedDirectory = _portableProjects?.Paths.Exports
            ?? Path.Combine(project.ProjectDirectory, "build", "story_packages");
        try
        {
            Problems.RemoveSourceTree("export/story");
            var groups = new DgrsGroupPackageExporter(project.ProjectDirectory).Groups();
            groups.ByStory.TryGetValue(story.Id, out var group);
            var output = group is null ? _dgrsExportPathPicker.PickExportPath(story.DisplayName, suggestedDirectory)
                : _dgrsExportPathPicker.PickGroupExportPath(group.DisplayName, suggestedDirectory);
            if (string.IsNullOrWhiteSpace(output)) return;
            if (HasUnsavedDocuments() && !TrySaveAll())
            {
                ReportWarning("导出已取消：存在无法保存的故事或资源，请根据“输出/问题”面板修正后重试。", $"story/{story.Id}");
                return;
            }
            var resultPath = group is null
                ? new DgrsStoryPackageExporter(project.ProjectDirectory).Build(story.Id, output, StudioBuildInfo.Version).PackagePath
                : new DgrsGroupPackageExporter(project.ProjectDirectory).Build(story.Id, output, StudioBuildInfo.Version).PackagePath;
            _lastUiCommand = nameof(ExportSelectedStoryPackage);
            OnPropertyChanged(nameof(LastUiCommand));
            ReportSuccess($"{(group is null ? "故事包" : "完整故事组")}已导出并验证：{resultPath}", $"story/{story.Id}");
        }
        catch (StoryPackageException exception) when (exception.GraphIssues.Count != 0)
        {
            ReportExportGraphIssues(exception.GraphIssues);
        }
        catch (Exception exception) when (IsWorkspaceException(exception) || exception is StoryPackageException)
        {
            ReportFailure("导出故事包", exception, story.Id);
        }
    }

    private void ReportExportGraphIssues(IReadOnlyList<StoryPackageGraphIssue> issues)
    {
        static string Describe(StoryPackageGraphIssue item)
        {
            var detail = item.Issue.Code switch
            {
                "graph.objective.description.invalid" => "目标说明必须是文字，可以留空。",
                "graph.objective.target.invalid" => item.Issue.Field switch
                {
                    "properties.entity" => "请选择有效的击杀目标；未配置的目标不能接入任务逻辑。",
                    "properties.actor_id" => "请选择有效的交互或提交角色；未配置的目标不能接入任务逻辑。",
                    "properties.item" => "请选择有效的目标物品；未配置的目标不能接入任务逻辑。",
                    _ => "请选择有效的目标对象。",
                },
                "graph.objective.required.invalid" => "目标数量必须为正整数。",
                _ => ValidationIssuePresentation.FormatCompact(item.Issue),
            };
            var kind = item.ResourceKind switch
            {
                GraphResourceKind.Task => "任务", GraphResourceKind.Session => "会话", _ => "故事",
            };
            var node = string.IsNullOrWhiteSpace(item.NodeDisplayName) ? string.Empty : $" → 节点“{item.NodeDisplayName}”";
            return $"故事“{item.StoryDisplayName}” → {kind}“{item.ResourceDisplayName}”{node}：{detail}";
        }

        foreach (var group in issues.GroupBy(issue => issue.StoryId))
            Problems.ReplaceForSource($"export/story/{group.Key}", group.Select(item => new ProblemItem(
                item.Issue.Severity, item.Issue.Code, Describe(item), item.Issue.Field,
                NodeId: item.Issue.NodeId, GraphResourceId: item.ResourceId)));
        BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Problems");
        var message = $"导出未完成，共 {issues.Count} 项内容需要修正。双击“问题”中的条目可定位。";
        StatusMessage = message;
        Output.Append(message + Environment.NewLine + string.Join(Environment.NewLine, issues.Select(Describe)), OutputKind.Error, "export/story");
        Toast.Show(message, ToastKind.Error);
    }

    private void RememberProject(string projectDirectory)
    {
        RemoveRecentProject(projectDirectory);
        RecentProjects.Insert(0, CreateRecentProject(projectDirectory));
        while (RecentProjects.Count > 10) RecentProjects.RemoveAt(RecentProjects.Count - 1);
    }

    private void AddRecentProject(string projectDirectory) =>
        RecentProjects.Add(CreateRecentProject(projectDirectory));

    private RecentProjectItemViewModel CreateRecentProject(string projectDirectory) =>
        new(projectDirectory, () => OpenRecentProject(projectDirectory));

    private void RemoveRecentProject(string projectDirectory)
    {
        var existing = RecentProjects.FirstOrDefault(project => PathsEqual(project.ProjectDirectory, projectDirectory));
        if (existing is not null) RecentProjects.Remove(existing);
    }

    private static bool IsExistingProjectDirectory(string? projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory)) return false;
        try
        {
            var fullPath = Path.GetFullPath(projectDirectory.Trim());
            return Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, "project.json"));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private void ValidateProject()
    {
        try
        {
            var issues = _projectService.ValidateProject();
            ReplaceValidationSource("project", issues);
            ReplaceOpenActorValidation();
            BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Problems");
            if (issues.Count == 0)
            {
                ReportSuccess("项目验证通过，未发现问题。", "Project");
            }
            else
            {
                ReportWarning($"项目验证完成：发现 {issues.Count} 个问题。", "Project");
            }
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("验证项目", exception);
        }
    }

    private void ShowProjectSettings()
    {
        Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Settings");
    }

    private void PrepareRuntimeReload()
    {
        SaveAll();
        var issues = _projectService.ValidateProject();
        ReplaceValidationSource("project", issues);
        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Problems");
            ReportWarning("项目仍有错误；修复后再执行 Minecraft 重载。", "Runtime");
            return;
        }

        BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Output");
        const string message = "Studio 文件已保存并通过验证。请在 Minecraft 中执行：/dgrpg reload";
        StatusMessage = message;
        Output.Append(message, OutputKind.Information, "Runtime");
        Toast.Show("保存完成；请在 Minecraft 执行 /dgrpg reload。", ToastKind.Success);
    }

    private static bool IsWorkspaceException(Exception exception) =>
        exception is ProjectException or ActorRepositoryException or ActorDataException or ActorValidationException
            or ItemRepositoryException or ItemDataException or ItemValidationException;

    internal static bool IsRecoverableUiException(Exception exception) =>
        exception is InvalidOperationException or ArgumentException or InvalidCastException or XamlParseException;

    internal static bool IsRecoverableGlobalUiException(Exception exception)
    {
        if (exception is XamlParseException)
        {
            return true;
        }

        if (exception is not (InvalidOperationException or ArgumentException or InvalidCastException))
        {
            return false;
        }

        // A global boundary may only recover failures whose stack identifies
        // WPF/UI plumbing. Generic InvalidOperationException instances can
        // indicate corrupted application state and must remain unhandled.
        var stack = exception.ToString();
        return stack.Contains("System.Windows", StringComparison.Ordinal)
            || stack.Contains("MS.Internal.Data", StringComparison.Ordinal);
    }

    private void OnProjectHomePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ProjectHomeViewModel.Graph))
        {
            ProjectHome.Graph.CreateStoryRequested = null;
            ProjectHome.Graph.DeleteStoryGroupsRequested = DeleteStoryGroups;
            ProjectHome.Graph.OutputEditorProvider = storyId =>
            {
                if (CanonicalStoryWorkspace?.StoryEditor.Id == storyId) return CanonicalStoryWorkspace.StoryEditor;
                if (!_retainedStoryWorkspaces.TryGetValue(storyId, out var workspace))
                {
                    if (_canonicalGraphStore is null) return null;
                    workspace = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(_canonicalGraphStore).Load(storyId),
                        new CanonicalGraphLayoutStore(_canonicalGraphStore.ProjectDirectory));
                    ConfigureCanonicalResourceActions(workspace);
                    workspace.PropertyChanged += OnCanonicalStoryWorkspacePropertyChanged;
                    _retainedStoryWorkspaces.Add(storyId, workspace);
                }
                return workspace.StoryEditor;
            };
            ProjectHome.Graph.SelectOutputs(ProjectHome.SelectedStory?.Id);
            if (_observedProjectGraphHost is not null) _observedProjectGraphHost.PropertyChanged -= OnProjectGraphHostPropertyChanged;
            _observedProjectGraphHost = ProjectHome.Graph.CanonicalHost;
            if (_observedProjectGraphHost is not null) _observedProjectGraphHost.PropertyChanged += OnProjectGraphHostPropertyChanged;
            OnProjectGraphHostPropertyChanged(null, new PropertyChangedEventArgs(null));
        }
        if (args.PropertyName == nameof(ProjectHomeViewModel.SelectedStory))
        {
            ProjectHome.Graph.SelectOutputs(ProjectHome.SelectedStory?.Id);
            SelectedReferencedPackage = null;
            RefreshHomeResourceFolders();
            OpenSelectedStoryCommand.RaiseCanExecuteChanged();
            DeleteSelectedStoryCommand.RaiseCanExecuteChanged();
            CopyStoryContentCommand.RaiseCanExecuteChanged();
        ExportSelectedStoryPackageCommand.RaiseCanExecuteChanged();
        }
    }

    private GraphEditorHostViewModel? _observedProjectGraphHost;
    private void OnProjectGraphHostPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        UndoCurrentCommand.RaiseCanExecuteChanged();
        RedoCurrentCommand.RaiseCanExecuteChanged();
    }

    private void ProjectHomeOnOpenStoryFlowRequested(object? sender, string storyId) => OpenStoryFlow(storyId);
    private void ProjectHomeOnOpenStoryRequested(object? sender, string storyId)
    {
        if (ProjectHome.Stories.FirstOrDefault(story => story.Id == storyId) is not { } story) return;
        ProjectHome.SelectedStory = story;
        ProjectHome.ShowHome();
        StatusMessage = $"已选择 Story：{story.Id}（概览）";
        Output.Append(StatusMessage, source: $"story/{story.Id}");
    }

    private void RaiseWorkspaceCommandStates()
    {
        SaveCurrentResourceCommand.RaiseCanExecuteChanged();
        UndoCurrentCommand.RaiseCanExecuteChanged();
        RedoCurrentCommand.RaiseCanExecuteChanged();
        SaveAllCommand.RaiseCanExecuteChanged();
        OpenProjectDirectoryCommand.RaiseCanExecuteChanged();
        ReferencePackageCommand.RaiseCanExecuteChanged();
        ImportPackageCommand.RaiseCanExecuteChanged();
        ValidateProjectCommand.RaiseCanExecuteChanged();
        PrepareRuntimeReloadCommand.RaiseCanExecuteChanged();
        ShowProjectSettingsCommand.RaiseCanExecuteChanged();
        OpenSelectedStoryCommand.RaiseCanExecuteChanged();
        CreateStoryCommand.RaiseCanExecuteChanged();
        DeleteSelectedStoryCommand.RaiseCanExecuteChanged();
        ShowProjectHomeCommand.RaiseCanExecuteChanged();
        ShowProjectGraphCommand.RaiseCanExecuteChanged();
        CopyStoryContentCommand.RaiseCanExecuteChanged();
        ExportSelectedStoryPackageCommand.RaiseCanExecuteChanged();
    }

    private void ReplaceOpenActorValidation()
    {
        Problems.RemoveSourceTree("actor");
        foreach (var document in _projectService.OpenActorDocuments)
            ReplaceValidationSource($"actor/{document.Id}", document.ValidationIssues);
    }

    private void RefreshProblems()
    {
        ReplaceOpenActorValidation();
        if (CanonicalStoryWorkspace is { } workspace) { ReplaceCanonicalValidation(workspace); return; }
        if (HasProject)
        {
            ReplaceValidationSource("project", _projectService.ValidateProject());
            UpdateProjectGraphProblems();
            UpdateCanonicalStoryDiscoveryProblems();
        }
        else Problems.ClearAll();
    }

    private void ReplaceCanonicalValidation(CanonicalStoryWorkspaceViewModel workspace)
    {
        var source = $"canonical/story/{workspace.StoryEditor.Id}";
        var structural = workspace.ValidationIssues.Select(issue => new ProblemItem(
            issue.Severity, issue.Code, issue.Message, issue.Field, source, issue.NodeId, workspace.StoryEditor.Id));
        var active = workspace.ActiveEditor.ValidationIssues.Select(issue => new ProblemItem(
            issue.Severity, issue.Code, issue.Message, issue.Field, source, issue.NodeId, workspace.ActiveEditor.Id));
        Problems.ReplaceForSource(source, structural.Concat(active));
    }

    private void ReplaceValidationSource(string source, IEnumerable<ValidationIssue> issues) =>
        Problems.ReplaceForSource(
            source,
            issues.Select(issue => new ProblemItem(issue.Severity, issue.Code, issue.Message, issue.Field, source)));

    private void UpdateProjectGraphProblems()
    {
        var problems = ProjectHome.Graph.Diagnostics.Select(issue => new ProblemItem(
                ProjectGraphViewModel.IsErrorDiagnostic(issue)
                    ? ValidationSeverity.Error
                    : ValidationSeverity.Warning,
                issue.Code,
                issue.Message,
                issue.NodeId is null ? null : "target_story_id",
                string.IsNullOrWhiteSpace(issue.StoryId)
                    ? "project-graph"
                    : issue.NodeId is null
                        ? $"project-graph/{issue.StoryId}"
                        : $"project-graph/{issue.StoryId}/{issue.NodeId}"))
            .ToList();
        if (!string.IsNullOrWhiteSpace(ProjectHome.Graph.PersistenceWarning))
            problems.Add(new ProblemItem(
                ValidationSeverity.Warning,
                "project_graph.layout.persistence",
                ProjectHome.Graph.PersistenceWarning,
                Source: "project-graph"));
        Problems.ReplaceForSourceTree("project-graph", problems);
    }

    private void UpdateCanonicalStoryDiscoveryProblems()
    {
        Problems.ReplaceForSourceTree(
            "canonical-discovery",
            _canonicalStoryDiscoveryIssues.Select(issue => new ProblemItem(
                ValidationSeverity.Error,
                issue.Code,
                issue.Message,
                Source: $"canonical-discovery/{issue.StoryId}")));
    }

    private void ReportSuccess(string message, string? source = null)
    {
        StatusMessage = message;
        Output.Append(message, OutputKind.Success, source);
        Toast.Show(message, ToastKind.Success);
    }

    private void ReportWarning(string message, string? source = null)
    {
        StatusMessage = message;
        Output.Append(message, OutputKind.Warning, source);
        Toast.Show(message, ToastKind.Warning);
    }

    private void ReportFailure(
        string action,
        Exception exception,
        string? actorId = null,
        bool writeCrashLog = true,
        string? sourceOverride = null)
    {
        if (writeCrashLog)
        {
            LogCrash(exception, action);
        }

        var message = $"{action}失败：{exception.Message}";
        var source = sourceOverride
            ?? (actorId is null ? exception.GetType().Name : $"actor/{actorId}");
        StatusMessage = message;
        Output.Append(message, OutputKind.Error, source);
        Toast.Show(message, ToastKind.Error);

        if (exception is ActorValidationException validationException)
        {
            ReplaceValidationSource(source, validationException.Issues);
            BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Problems");
        }
        else
        {
            Problems.ReplaceForSource(source,
            [
                new ProblemItem(
                    ValidationSeverity.Error,
                    "operation.failure",
                    message,
                    Source: source),
            ]);
        }
    }

    private void LogCrash(Exception exception, string context)
    {
        try
        {
            _crashLogService.Log(exception, context, GetCrashLogDetails());
        }
        catch (Exception)
        {
            // CrashLogService is defensive by contract; retain this guard for
            // injected test implementations and future service replacements.
        }
    }

    private void RaiseCurrentEditorStates()
    {
        OnPropertyChanged(nameof(ActiveEditor));
        SaveCurrentResourceCommand.RaiseCanExecuteChanged();
        UndoCurrentCommand.RaiseCanExecuteChanged();
        RedoCurrentCommand.RaiseCanExecuteChanged();
        RaiseWorkspaceCommandStates();
        RefreshProblems();
    }

    private enum CanonicalOpenResult
    {
        NotPresent,
        Opened,
        Failed,
    }

    private sealed class NullActorWorkspaceDialogs : IActorWorkspaceDialogs
    {
        public ActorCreationMode? RequestCreationMode(string storyDisplayName) => null;

        public ActorIdentityRequest? RequestCreate(string suggestedId) => null;

        public ActorIdentityRequest? RequestImportIdentity(ActorResourceInfo source, string suggestedId) => null;

        public ActorResourceInfo? PickActor(
            IReadOnlyList<ActorResourceInfo> candidates,
            ActorPickerMode mode,
            string storyDisplayName) => null;

        public string? RequestRename(ActorResourceInfo actor, string suggestedId) => null;

        public bool ConfirmDelete(ActorResourceInfo actor) => false;

        public bool ConfirmRemoveReference(ActorResourceInfo actor, string storyDisplayName) => false;

        public void ShowReferences(ActorResourceInfo actor, IReadOnlyList<ResourceDescriptor> references) { }

        public bool ConfirmSaveBeforeSwitch(ActorResourceInfo actor) => false;

        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ActorResourceInfo actor) =>
            UnsavedChangesChoice.Cancel;
    }

    private sealed class NullProjectWorkspaceDialogs : IProjectWorkspaceDialogs
    {
        public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null) => null;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() => UnsavedChangesChoice.Cancel;
        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete) => false;
    }

    private sealed class NullResourceWorkspaceDialogs : IResourceWorkspaceDialogs
    {
        public StoryCreationRequest? RequestCreateStory(string allocatedStoryUid) => null;
        public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates,
            ResourcePickerMode mode, string storyDisplayName) => null;
    }
}
