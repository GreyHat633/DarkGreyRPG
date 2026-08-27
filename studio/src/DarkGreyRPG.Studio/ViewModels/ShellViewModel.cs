using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Markup;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly IProjectFolderPicker _projectFolderPicker;
    private readonly IActorWorkspaceDialogs _actorWorkspaceDialogs;
    private readonly IResourceWorkspaceDialogs _resourceWorkspaceDialogs;
    private readonly IProjectWorkspaceDialogs _projectWorkspaceDialogs;
    private readonly IFlowWorkspaceDialogs _flowWorkspaceDialogs;
    private readonly ICrashLogService _crashLogService;
    private readonly HashSet<string> _ignoredFlowRecoveries = new(StringComparer.Ordinal);
    private StoryFlowRecoveryStore? _flowRecoveryStore;
    private ActorResourceInfo? _selectedActor;
    private StoryActorMembershipViewModel? _selectedStoryActor;
    private StoryActorsViewModel? _observedStoryActors;
    private StoryDialoguesViewModel? _observedStoryDialogues;
    private StoryQuestsViewModel? _observedStoryQuests;
    private ActorEditorViewModel? _currentActor;
    private DialogueEditorViewModel? _currentDialogue;
    private QuestEditorViewModel? _currentQuest;
    private StoryFlowEditorViewModel? _currentFlow;
    private StoryResourceMembershipViewModel? _selectedStoryResource;
    private readonly Dictionary<string, DialogueDocument> _dialogueDrafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, QuestDocument> _questDrafts = new(StringComparer.Ordinal);
    private string _searchText = string.Empty;
    private bool _isResourceBrowserVisible = true;
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
        IFlowWorkspaceDialogs? flowWorkspaceDialogs = null,
        ICrashLogService? crashLogService = null)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _projectFolderPicker = projectFolderPicker ?? throw new ArgumentNullException(nameof(projectFolderPicker));
        _actorWorkspaceDialogs = actorWorkspaceDialogs ?? new NullActorWorkspaceDialogs();
        _projectWorkspaceDialogs = projectWorkspaceDialogs ?? new NullProjectWorkspaceDialogs();
        _resourceWorkspaceDialogs = resourceWorkspaceDialogs ?? new NullResourceWorkspaceDialogs();
        _flowWorkspaceDialogs = flowWorkspaceDialogs ?? new NullFlowWorkspaceDialogs();
        _crashLogService = crashLogService ?? new CrashLogService();
        NewProjectCommand = new RelayCommand(NewProject);
        OpenProjectCommand = new RelayCommand(OpenProject);
        SaveActorCommand = new RelayCommand(SaveActor, () => CurrentActor?.CanSave == true);
        SaveCurrentResourceCommand = new RelayCommand(SaveCurrentResource, () => ActiveEditor?.CanSave == true);
        UndoCurrentCommand = new RelayCommand(() => ActiveEditor?.UndoCommand.Execute(null), () => ActiveEditor?.UndoCommand.CanExecute(null) == true);
        RedoCurrentCommand = new RelayCommand(() => ActiveEditor?.RedoCommand.Execute(null), () => ActiveEditor?.RedoCommand.CanExecute(null) == true);
        NewActorCommand = new RelayCommand(NewActor, CanCreateOrReferenceActor);
        ReferenceActorCommand = new RelayCommand(ReferenceActor, CanCreateOrReferenceActor);
        RemoveActorReferenceCommand = new RelayCommand(RemoveActorReference, CanRemoveActorReference);
        ViewActorReferencesCommand = new RelayCommand(ViewActorReferences, CanViewActorReferences);
        DuplicateActorCommand = new RelayCommand(DuplicateActor, CanMutateSelectedActor);
        RenameActorCommand = new RelayCommand(RenameActor, CanMutateSelectedActor);
        DeleteActorCommand = new RelayCommand(DeleteActor, CanDeleteSelectedActor);
        NewStoryResourceCommand = new RelayCommand(NewStoryResource, CanCreateOrReferenceStoryResource);
        ReferenceStoryResourceCommand = new RelayCommand(ReferenceStoryResource, CanCreateOrReferenceStoryResource);
        RemoveStoryResourceReferenceCommand = new RelayCommand(RemoveStoryResourceReference, CanRemoveStoryResourceReference);
        ViewStoryResourceReferencesCommand = new RelayCommand(ViewStoryResourceReferences, () => SelectedStoryResource?.Descriptor is not null);
        DeleteStoryResourceCommand = new RelayCommand(DeleteStoryResource, CanDeleteSelectedStoryResource);
        DeleteCurrentResourceCommand = new RelayCommand(DeleteCurrentResource, CanDeleteCurrentResource);
        DuplicateStoryResourceCommand = new RelayCommand(DuplicateStoryResource, CanCreateOrReferenceStoryResource);
        SaveAllCommand = new RelayCommand(SaveAll, CanSaveAll);
        OpenProjectDirectoryCommand = new RelayCommand(OpenProjectDirectory, () => HasProject);
        ValidateProjectCommand = new RelayCommand(ValidateProject, () => HasProject);
        PrepareRuntimeReloadCommand = new RelayCommand(PrepareRuntimeReload, () => HasProject);
        ShowProjectSettingsCommand = new RelayCommand(ShowProjectSettings, () => HasProject);
        OpenSelectedStoryCommand = new RelayCommand(OpenSelectedStory, () => HasProject && ProjectHome.SelectedStory is not null);
        CreateStoryCommand = new RelayCommand(CreateStory, () => HasProject);
        DeleteSelectedStoryCommand = new RelayCommand(DeleteSelectedStory, () => HasProject && ProjectHome.SelectedStory is not null);
        ShowProjectHomeCommand = new RelayCommand(ShowProjectHome, () => HasProject);
        ShowProjectGraphCommand = new RelayCommand(ShowProjectGraph, () => HasProject);
        ToggleResourceBrowserCommand = new RelayCommand(
            () => IsResourceBrowserVisible = !IsResourceBrowserVisible);
        ToggleBottomPanelCommand = new RelayCommand(
            BottomPanel.Toggle);
        StoryWorkspace.CanLeaveActorsRoute = TryLeaveActorEditor;
        StoryWorkspace.CanLeaveRoute = TryLeaveRoute;
        ProjectHome.PropertyChanged += OnProjectHomePropertyChanged;
        ProjectHome.OpenStoryFlowRequested += ProjectHomeOnOpenStoryFlowRequested;
        ProjectHome.OpenStoryRequested += ProjectHomeOnOpenStoryRequested;
        StoryWorkspace.PropertyChanged += OnStoryWorkspacePropertyChanged;
        Output.Append("DarkGrey RPG Studio 已启动。", source: "Studio");
    }

    public ObservableCollection<ActorResourceInfo> Actors { get; } = [];

    public ObservableCollection<ActorResourceInfo> FilteredActors { get; } = [];

    public ObservableCollection<RecentProjectItemViewModel> RecentProjects { get; } = [];

    public ProjectHomeViewModel ProjectHome { get; } = new();

    public StoryWorkspaceViewModel StoryWorkspace { get; } = new();

    public NavigationViewModel Navigation { get; } = new();

    public BottomPanelViewModel BottomPanel { get; } = new();

    public OutputViewModel Output { get; } = new();

    public ProblemsViewModel Problems { get; } = new();

    public ToastViewModel Toast { get; } = new();

    public string LastUiCommand => _lastUiCommand;

    public IReadOnlyDictionary<string, string?> GetCrashLogDetails() => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["Current Project"] = HasProject ? ProjectDirectory : null,
        ["Current Story"] = StoryWorkspace.HasStory ? StoryWorkspace.StoryId : null,
        ["Current Route"] = StoryWorkspace.HasStory ? StoryWorkspace.CurrentRoute : null,
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

    public RelayCommand SaveActorCommand { get; }

    public RelayCommand SaveCurrentResourceCommand { get; }

    public RelayCommand UndoCurrentCommand { get; }

    public RelayCommand RedoCurrentCommand { get; }

    public RelayCommand NewActorCommand { get; }

    public RelayCommand ReferenceActorCommand { get; }

    public RelayCommand RemoveActorReferenceCommand { get; }

    public RelayCommand ViewActorReferencesCommand { get; }

    public RelayCommand DuplicateActorCommand { get; }

    public RelayCommand RenameActorCommand { get; }

    public RelayCommand DeleteActorCommand { get; }

    public RelayCommand NewStoryResourceCommand { get; }

    public RelayCommand ReferenceStoryResourceCommand { get; }

    public RelayCommand RemoveStoryResourceReferenceCommand { get; }

    public RelayCommand ViewStoryResourceReferencesCommand { get; }

    public RelayCommand DeleteStoryResourceCommand { get; }

    public RelayCommand DeleteCurrentResourceCommand { get; }

    public RelayCommand DuplicateStoryResourceCommand { get; }

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

    public RelayCommand ToggleResourceBrowserCommand { get; }

    public RelayCommand ToggleBottomPanelCommand { get; }

    public bool HasProject => _projectService.CurrentProject is not null;

    public IReadOnlyList<string> RecentProjectDirectories =>
        RecentProjects.Select(project => project.ProjectDirectory).ToArray();

    public string ProjectDisplayName
    {
        get => _projectDisplayName;
        private set => SetProperty(ref _projectDisplayName, value);
    }

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

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                RefreshActorFilter();
            }
        }
    }

    public bool IsResourceBrowserVisible
    {
        get => _isResourceBrowserVisible;
        private set => SetProperty(ref _isResourceBrowserVisible, value);
    }

    public ActorResourceInfo? SelectedActor
    {
        get => _selectedActor;
        set
        {
            if (_selectedActor is not null &&
                CurrentActor?.Document.IsDirty == true &&
                !string.Equals(_selectedActor.Id, value?.Id, StringComparison.Ordinal))
            {
                if (!_actorWorkspaceDialogs.ConfirmSaveBeforeSwitch(_selectedActor) || !TrySaveCurrentActor())
                {
                    return;
                }
            }

            if (!SetProperty(ref _selectedActor, value))
            {
                return;
            }

            OpenSelectedActor();
            RaiseWorkspaceCommandStates();
        }
    }

    /// <summary>Story Actors page binding that preserves the existing Actor editor workflow.</summary>
    public StoryActorMembershipViewModel? SelectedStoryActor
    {
        get => _selectedStoryActor;
        set
        {
            var nextActorId = value?.Actor?.Id;
            if (CurrentActor?.Document.IsDirty == true &&
                !string.Equals(_selectedActor?.Id, nextActorId, StringComparison.Ordinal))
            {
                if (_selectedActor is null ||
                    !_actorWorkspaceDialogs.ConfirmSaveBeforeSwitch(_selectedActor) ||
                    !TrySaveCurrentActor())
                {
                    return;
                }
            }

            if (!SetProperty(ref _selectedStoryActor, value)) return;
            if (StoryWorkspace.Actors is not null)
                StoryWorkspace.Actors.SelectedMembership = value;
            if (value?.Actor is not null)
                SelectedActor = Actors.FirstOrDefault(actor => actor.Id == value.Actor.Id) ?? value.Actor;
            else if (value is not null)
                ReportWarning($"Story Actor '{value.Id}' 未解析，无法打开编辑器。", $"story/{StoryWorkspace.StoryId}/actor/{value.Id}");
            RaiseWorkspaceCommandStates();
        }
    }

    /// <summary>Selected Dialogue or Quest membership for the active Story route.</summary>
    public StoryResourceMembershipViewModel? SelectedStoryResource
    {
        get => _selectedStoryResource;
        set
        {
            var currentId = _selectedStoryResource?.Id;
            if (!string.Equals(currentId, value?.Id, StringComparison.Ordinal) && !TryLeaveCurrentStoryResourceEditor())
            {
                return;
            }

            if (!SetProperty(ref _selectedStoryResource, value)) return;
            if (StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Dialogues && StoryWorkspace.Dialogues is not null)
                StoryWorkspace.Dialogues.SelectedItem = value;
            if (StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Quests && StoryWorkspace.Quests is not null)
                StoryWorkspace.Quests.SelectedItem = value;
            OpenSelectedStoryResource();
            RaiseWorkspaceCommandStates();
        }
    }

    public bool TryClose()
    {
        if (CurrentFlow?.IsDirty == true && !TryResolveUnsavedFlow()) return false;

        if (CurrentActor?.Document.IsDirty == true && SelectedActor is not null)
        {
            return _actorWorkspaceDialogs.ConfirmCloseWithUnsavedChanges(SelectedActor) switch
            {
                UnsavedChangesChoice.Save => TrySaveCurrentActor(),
                UnsavedChangesChoice.Discard => true,
                _ => false,
            };
        }

        if (CurrentDialogue?.Document.IsNewDraft == true && SelectedStoryResource is { } draftMembership)
        {
            var draftResource = new ResourceDescriptor(ProjectResourceType.Dialogue, draftMembership.Id, draftMembership.DisplayName, string.Empty);
            return _resourceWorkspaceDialogs.ConfirmCloseWithUnsavedChanges(draftResource) switch
            {
                UnsavedChangesChoice.Save => TrySaveCurrentStoryResource(),
                UnsavedChangesChoice.Discard => DiscardCurrentDialogueDraft(),
                _ => false,
            };
        }

        if (CurrentQuest?.Document.IsNewDraft == true && SelectedStoryResource is { } questDraftMembership)
        {
            var draftResource = new ResourceDescriptor(ProjectResourceType.Quest, questDraftMembership.Id, questDraftMembership.DisplayName, string.Empty);
            return _resourceWorkspaceDialogs.ConfirmCloseWithUnsavedChanges(draftResource) switch
            {
                UnsavedChangesChoice.Save => TrySaveCurrentStoryResource(),
                UnsavedChangesChoice.Discard => DiscardCurrentQuestDraft(),
                _ => false,
            };
        }

        if (ActiveEditor?.IsDirty != true || SelectedStoryResource?.Descriptor is not { } resource)
        {
            return true;
        }

        return _resourceWorkspaceDialogs.ConfirmCloseWithUnsavedChanges(resource) switch
        {
            UnsavedChangesChoice.Save => TrySaveCurrentStoryResource(),
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

        if (!TryResolveStoryResourceDraftBeforeProjectSwitch() || HasUnsavedDocuments())
        {
            ReportWarning("当前项目有未保存的资源；请先保存后再打开其他项目。", "Project");
            return false;
        }

        return OpenProjectFromDirectory(projectDirectory, isRestore: false);
    }

    public void OpenProblem(ProblemItem problem)
    {
        ArgumentNullException.ThrowIfNull(problem);
        if (TryOpenProjectGraphProblem(problem)) return;
        if (TryOpenFlowProblem(problem)) return;

        const string actorPrefix = "actor/";
        if (problem.Source?.StartsWith(actorPrefix, StringComparison.Ordinal) != true)
        {
            return;
        }

        var id = problem.Source[actorPrefix.Length..];
        var actor = Actors.FirstOrDefault(candidate => candidate.Id == id);
        if (actor is null)
        {
            ReportWarning($"问题引用的 Actor '{id}' 当前不在资源列表中。", "Problems");
            return;
        }

        // Actor editing remains available from the Story workspace during M1,
        // while Story is the only content-oriented top-level navigation item.
        Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
        SelectedActor = actor;
    }

    private bool TryOpenProjectGraphProblem(ProblemItem problem)
    {
        var parts = problem.Source?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: >= 1 } || !string.Equals(parts[0], "project-graph", StringComparison.Ordinal))
            return false;
        if (problem.Code == "project_graph.target.missing" && parts.Length >= 3)
        {
            OpenStoryFlowNode(parts[1], parts[2], "target_story_id");
            return true;
        }

        ShowProjectGraph();
        if (parts.Length >= 2 && ProjectHome.IsGraphVisible && !ProjectHome.Graph.RequestProblemFocus(parts[1]))
            ReportWarning($"图谱问题引用的 Story '{parts[1]}' 当前不存在。", problem.Source);
        return true;
    }

    private bool TryOpenFlowProblem(ProblemItem problem)
    {
        var parts = problem.Source?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: >= 4 } ||
            !string.Equals(parts[0], "story", StringComparison.Ordinal) ||
            !string.Equals(parts[2], "flow", StringComparison.Ordinal))
            return false;

        var storyId = parts[1];
        var nodeId = parts[3];
        if (CurrentFlow?.Id == storyId && StoryWorkspace.StoryId == storyId)
        {
            Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
            StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Flow);
        }
        else
        {
            OpenStoryFlow(storyId);
        }
        if (CurrentFlow?.Id != storyId) return true;
        if (!CurrentFlow.RequestProblemFocus(nodeId, problem.Field))
            ReportWarning($"问题引用的 Flow 节点 '{nodeId}' 当前不存在。", problem.Source);
        else
            StatusMessage = $"已定位 Story Flow 问题：{storyId}/{nodeId}/{problem.Field ?? "node"}";
        return true;
    }

    public ActorEditorViewModel? CurrentActor
    {
        get => _currentActor;
        private set
        {
            if (_currentActor is not null)
            {
                _currentActor.PropertyChanged -= OnCurrentActorPropertyChanged;
            }

            if (!SetProperty(ref _currentActor, value))
            {
                return;
            }

            if (_currentActor is not null)
            {
                _currentActor.PropertyChanged += OnCurrentActorPropertyChanged;
            }

            RaiseCurrentEditorStates();
        }
    }

    public DialogueEditorViewModel? CurrentDialogue
    {
        get => _currentDialogue;
        private set
        {
            if (_currentDialogue is not null) _currentDialogue.PropertyChanged -= OnCurrentResourceEditorPropertyChanged;
            if (!SetProperty(ref _currentDialogue, value)) return;
            if (_currentDialogue is not null) _currentDialogue.PropertyChanged += OnCurrentResourceEditorPropertyChanged;
            RaiseCurrentEditorStates();
        }
    }

    public QuestEditorViewModel? CurrentQuest
    {
        get => _currentQuest;
        private set
        {
            if (_currentQuest is not null) _currentQuest.PropertyChanged -= OnCurrentResourceEditorPropertyChanged;
            if (!SetProperty(ref _currentQuest, value)) return;
            if (_currentQuest is not null) _currentQuest.PropertyChanged += OnCurrentResourceEditorPropertyChanged;
            RaiseCurrentEditorStates();
        }
    }

    public StoryFlowEditorViewModel? CurrentFlow
    {
        get => _currentFlow;
        private set
        {
            if (_currentFlow is not null)
            {
                _currentFlow.PropertyChanged -= OnCurrentResourceEditorPropertyChanged;
                _currentFlow.UndoCommand.CanExecuteChanged -= OnFlowHistoryCanExecuteChanged;
                _currentFlow.RedoCommand.CanExecuteChanged -= OnFlowHistoryCanExecuteChanged;
            }
            if (!SetProperty(ref _currentFlow, value)) return;
            if (_currentFlow is not null)
            {
                _currentFlow.PropertyChanged += OnCurrentResourceEditorPropertyChanged;
                _currentFlow.UndoCommand.CanExecuteChanged += OnFlowHistoryCanExecuteChanged;
                _currentFlow.RedoCommand.CanExecuteChanged += OnFlowHistoryCanExecuteChanged;
            }
            RaiseCurrentEditorStates();
        }
    }

    public IWorkspaceEditorViewModel? ActiveEditor => StoryWorkspace.CurrentRoute switch
    {
        StoryWorkspaceRoutes.Actors => CurrentActor,
        StoryWorkspaceRoutes.Dialogues => CurrentDialogue,
        StoryWorkspaceRoutes.Quests => CurrentQuest,
        StoryWorkspaceRoutes.Flow => CurrentFlow,
        _ => null,
    };

    private void OpenProject()
    {
        var selectedDirectory = _projectFolderPicker.PickProjectFolder();
        if (string.IsNullOrWhiteSpace(selectedDirectory))
        {
            return;
        }

        if (!TryResolveStoryResourceDraftBeforeProjectSwitch() || HasUnsavedDocuments())
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
            var project = _projectService.OpenProject(projectDirectory);
            _dialogueDrafts.Clear();
            _questDrafts.Clear();
            _flowRecoveryStore = new StoryFlowRecoveryStore(project.ProjectDirectory);
            _ignoredFlowRecoveries.Clear();
            ProjectDisplayName = $"{project.Project.DisplayName} ({project.Project.Id})";
            ProjectDirectory = project.ProjectDirectory;
            RememberProject(project.ProjectDirectory);
            LoadActorList();
            LoadStoryList();
            CurrentActor = null;
            CurrentDialogue = null;
            CurrentQuest = null;
            CurrentFlow = null;
            _selectedStoryResource = null;
            OnPropertyChanged(nameof(SelectedStoryResource));
            ProjectHome.ShowHome();
            StoryWorkspace.CloseStory();
            RefreshProblems();
            ReportSuccess(
                isRestore
                    ? $"已恢复上次项目，共 {Actors.Count} 个 Actor。"
                    : $"项目已打开，共 {Actors.Count} 个 Actor。",
                "Project");
            ReportPendingFlowRecoveries();
            OnPropertyChanged(nameof(HasProject));
            RaiseWorkspaceCommandStates();
            return true;
        }
        catch (Exception exception) when (
            exception is ProjectException or ActorRepositoryException or ActorDataException or ActorValidationException
                or StoryRepositoryException or StoryNotFoundException or StoryDataException)
        {
            if (isRestore)
            {
                ReportWarning($"无法恢复上次项目：{exception.Message}", "Project");
            }
            else
            {
                ReportFailure("打开项目", exception);
            }

            return false;
        }
    }

    private void NewProject()
    {
        if (!TryResolveStoryResourceDraftBeforeProjectSwitch() || HasUnsavedDocuments())
        {
            ReportWarning("当前项目有未保存的资源；请先保存后再新建项目。", "Project");
            return;
        }

        var initialParent = _projectService.CurrentProject is null
            ? null
            : Directory.GetParent(_projectService.CurrentProject.ProjectDirectory)?.FullName;
        var request = _projectWorkspaceDialogs.RequestCreate(initialParent);
        if (request is null)
        {
            return;
        }

        try
        {
            var project = _projectService.CreateProject(
                request.ProjectDirectory,
                request.Id,
                request.DisplayName);
            _dialogueDrafts.Clear();
            _questDrafts.Clear();
            _flowRecoveryStore = new StoryFlowRecoveryStore(project.ProjectDirectory);
            _ignoredFlowRecoveries.Clear();
            ProjectDisplayName = $"{project.Project.DisplayName} ({project.Project.Id})";
            ProjectDirectory = project.ProjectDirectory;
            RememberProject(project.ProjectDirectory);
            LoadActorList();
            LoadStoryList();
            CurrentActor = null;
            CurrentDialogue = null;
            CurrentQuest = null;
            CurrentFlow = null;
            _selectedStoryResource = null;
            OnPropertyChanged(nameof(SelectedStoryResource));
            ProjectHome.ShowHome();
            StoryWorkspace.CloseStory();
            OnPropertyChanged(nameof(HasProject));
            RaiseWorkspaceCommandStates();
            RefreshProblems();
            ReportSuccess("项目已创建，可开始新建剧情。", "Project");
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

        RefreshActorFilter();

        _selectedActor = null;
        OnPropertyChanged(nameof(SelectedActor));
    }

    private void LoadStoryList()
    {
        var project = _projectService.CurrentProject
            ?? throw new ProjectException("No project is open.");
        var stories = project.Stories.ListStories();
        ProjectHome.ReplaceStories(stories, projectDirectory: project.ProjectDirectory);
        UpdateProjectGraphProblems();
        OnPropertyChanged(nameof(ProjectHome));
        OpenSelectedStoryCommand.RaiseCanExecuteChanged();
    }

    private void OpenSelectedStory()
    {
        var selected = ProjectHome.SelectedStory;
        if (selected is null || !TryLeaveCurrentEditor()) return;
        var project = _projectService.CurrentProject;
        if (project is null) return;

        try
        {
            var story = project.Stories.LoadStory(selected.Id);
            var actors = project.Actors.ListActors();
            var descriptors = GetResourceDescriptors(project, story, _dialogueDrafts.Values.Where(draft => draft.DraftOwnerStoryId == story.Id));
            StoryWorkspace.OpenStory(
                story,
                actors,
                descriptors,
                GetActorHomeStoryNames(project, actors),
                GetResourceHomeStoryNames(project, descriptors, ProjectResourceType.Dialogue),
                GetResourceHomeStoryNames(project, descriptors, ProjectResourceType.Quest),
                _dialogueDrafts.Values.Where(draft => draft.DraftOwnerStoryId == story.Id).ToArray(),
                _questDrafts.Values.Where(draft => draft.DraftOwnerStoryId == story.Id).ToArray());
            ProjectHome.SelectedStory = ProjectHome.Stories.FirstOrDefault(item => item.Id == story.Id);
            _selectedStoryActor = null;
            OnPropertyChanged(nameof(SelectedStoryActor));
            _selectedActor = null;
            OnPropertyChanged(nameof(SelectedActor));
            CurrentActor = null;
            _selectedStoryResource = null;
            OnPropertyChanged(nameof(SelectedStoryResource));
            CurrentDialogue = null;
            CurrentQuest = null;
            CurrentFlow = CreateFlowEditor(story.Id);
            Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
            StatusMessage = $"已打开 Story：{story.Id}（角色）";
            Output.Append(StatusMessage, source: $"story/{story.Id}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("打开 Story", exception);
        }
    }

    private void CreateStory()
    {
        var project = _projectService.CurrentProject;
        if (project is null) return;

        try
        {
            var request = _resourceWorkspaceDialogs.RequestCreate(
                ProjectResourceType.Story,
                project.Stories.GetAvailableId("new_story"));
            if (request is null) return;

            var story = _projectService.CreateStory(request.Id, request.DisplayName);
            LoadStoryList();
            ProjectHome.SelectedStory = ProjectHome.Stories.Single(item => item.Id == story.Id);
            ProjectHome.ShowHome();
            StatusMessage = $"剧情 '{story.Id}' 已创建。";
            Output.Append(StatusMessage, OutputKind.Success, $"story/{story.Id}");
            Toast.Show(StatusMessage, ToastKind.Success);
        }
        catch (Exception exception) when (IsWorkspaceException(exception) || IsRecoverableUiException(exception))
        {
            ReportFailure("新建剧情", exception);
        }
        finally
        {
            RaiseWorkspaceCommandStates();
        }
    }

    private void DeleteSelectedStory()
    {
        var selected = ProjectHome.SelectedStory;
        if (selected is null || _projectService.CurrentProject is null) return;

        try
        {
            var plan = _projectService.GetStoryDeletionPlan(selected.Id);
            if (plan.Blockers.Count > 0)
            {
                ReportWarning(
                    $"无法删除剧情 '{selected.Id}'：{string.Join("；", plan.Blockers)}",
                    $"story/{selected.Id}");
                return;
            }
            var resourcesToDelete = plan.ActorIds.Select(id => $"角色：{id}")
                .Concat(plan.DialogueIds.Select(id => $"对话：{id}"))
                .Concat(plan.QuestIds.Select(id => $"任务：{id}"))
                .ToArray();
            if (!_projectWorkspaceDialogs.ConfirmDeleteStory(selected.Id, selected.DisplayName, resourcesToDelete)) return;

            _projectService.DeleteStory(selected.Id);
            _flowRecoveryStore?.Delete(selected.Id);
            LoadStoryList();
            ProjectHome.ShowHome();
            StatusMessage = $"剧情 '{selected.Id}' 已从项目中删除。";
            Output.Append(StatusMessage, OutputKind.Success, $"story/{selected.Id}");
            Toast.Show(StatusMessage, ToastKind.Success);
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("删除剧情", exception, selected.Id);
        }
    }

    public void OpenStory(StoryListItemViewModel story)
    {
        ArgumentNullException.ThrowIfNull(story);
        ProjectHome.SelectedStory = story;
        OpenSelectedStoryCommand.Execute(null);
    }

    public void OpenStoryFlow(string storyId)
    {
        var story = ProjectHome.Stories.FirstOrDefault(item => item.Id == storyId);
        if (story is null) return;
        OpenStory(story);
        if (!StoryWorkspace.HasStory || StoryWorkspace.StoryId != storyId) return;
        StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Flow);
        StatusMessage = $"已从剧情图谱打开 Story Flow：{storyId}";
        Output.Append(StatusMessage, source: $"story/{storyId}/flow");
    }

    public void OpenStoryFlowNode(string storyId, string nodeId, string? field = null)
    {
        if (CurrentFlow?.Id == storyId && StoryWorkspace.StoryId == storyId)
        {
            Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
            StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Flow);
        }
        else
        {
            OpenStoryFlow(storyId);
        }
        if (CurrentFlow?.Id != storyId) return;
        if (!CurrentFlow.RequestProblemFocus(nodeId, field))
            ReportWarning($"Story Flow '{storyId}' 中不存在节点 '{nodeId}'。", $"story/{storyId}/flow/{nodeId}");
    }

    public void FocusCurrentFlowProblems()
    {
        if (CurrentFlow is not null)
            ReplaceFlowValidationSource(CurrentFlow);
        BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Problems");
        BottomPanel.IsExpanded = true;
    }

    public void FocusProjectGraphProblems()
    {
        UpdateProjectGraphProblems();
        BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Problems");
        BottomPanel.IsExpanded = true;
    }

    private void ShowProjectHome()
    {
        if (!TryLeaveCurrentEditor()) return;
        var storyId = StoryWorkspace.StoryId;
        ClearAllEditorSelections();
        StoryWorkspace.CloseStory();
        LoadStoryList();
        ProjectHome.SelectedStory = ProjectHome.Stories.FirstOrDefault(story => story.Id == storyId);
        ProjectHome.ShowHome();
        Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
    }

    private void ShowProjectGraph()
    {
        if (!TryLeaveCurrentEditor()) return;
        ClearAllEditorSelections();
        StoryWorkspace.CloseStory();
        LoadStoryList();
        ProjectHome.ShowGraph();
        Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
    }

    private void ClearAllEditorSelections()
    {
        _selectedStoryActor = null;
        OnPropertyChanged(nameof(SelectedStoryActor));
        _selectedActor = null;
        OnPropertyChanged(nameof(SelectedActor));
        CurrentActor = null;
        _selectedStoryResource = null;
        OnPropertyChanged(nameof(SelectedStoryResource));
        CurrentDialogue = null;
        CurrentQuest = null;
        CurrentFlow = null;
    }

    private bool TryLeaveActorEditor()
    {
        if (CurrentActor?.Document.IsDirty != true || SelectedActor is null) return true;
        if (!_actorWorkspaceDialogs.ConfirmSaveBeforeSwitch(SelectedActor)) return false;
        return TrySaveCurrentActor();
    }

    private bool TryLeaveRoute(string route) => route switch
    {
        StoryWorkspaceRoutes.Actors => TryLeaveActorEditor(),
        StoryWorkspaceRoutes.Dialogues or StoryWorkspaceRoutes.Quests => TryLeaveCurrentStoryResourceEditor(),
        StoryWorkspaceRoutes.Flow => true,
        _ => true,
    };

    private bool TryLeaveCurrentEditor()
    {
        if (CurrentFlow?.IsDirty == true && !TryResolveUnsavedFlow()) return false;
        return TryLeaveRoute(StoryWorkspace.CurrentRoute);
    }

    private bool TryResolveStoryResourceDraftBeforeProjectSwitch() =>
        (CurrentDialogue?.Document.IsNewDraft != true && CurrentQuest?.Document.IsNewDraft != true)
        || TryLeaveCurrentStoryResourceEditor();

    private bool TryResolveUnsavedFlow()
    {
        if (CurrentFlow?.IsDirty != true) return true;
        return _flowWorkspaceDialogs.ConfirmCloseWithUnsavedChanges(CurrentFlow) switch
        {
            UnsavedChangesChoice.Save => TrySaveCurrentFlow(),
            UnsavedChangesChoice.Discard => DiscardCurrentFlowDraft(),
            _ => false,
        };
    }

    private bool DiscardCurrentFlowDraft()
    {
        if (CurrentFlow is null) return true;
        var storyId = CurrentFlow.Id;
        _flowRecoveryStore?.Delete(storyId);
        CurrentFlow = CreateFlowEditor(storyId);
        Problems.RemoveSourceTree($"story/{storyId}/flow");
        return true;
    }

    private bool TryLeaveCurrentStoryResourceEditor()
    {
        if (CurrentDialogue?.Document.IsNewDraft == true && SelectedStoryResource is { } draftMembership)
        {
            var draftResource = new ResourceDescriptor(ProjectResourceType.Dialogue, draftMembership.Id, draftMembership.DisplayName, string.Empty);
            return _resourceWorkspaceDialogs.ConfirmCloseWithUnsavedChanges(draftResource) switch
            {
                UnsavedChangesChoice.Save => TrySaveCurrentStoryResource(),
                UnsavedChangesChoice.Discard => DiscardCurrentDialogueDraft(),
                _ => false,
            };
        }

        if (CurrentQuest?.Document.IsNewDraft == true && SelectedStoryResource is { } questDraftMembership)
        {
            var draftResource = new ResourceDescriptor(ProjectResourceType.Quest, questDraftMembership.Id, questDraftMembership.DisplayName, string.Empty);
            return _resourceWorkspaceDialogs.ConfirmCloseWithUnsavedChanges(draftResource) switch
            {
                UnsavedChangesChoice.Save => TrySaveCurrentStoryResource(),
                UnsavedChangesChoice.Discard => DiscardCurrentQuestDraft(),
                _ => false,
            };
        }

        if (ActiveEditor?.IsDirty != true || SelectedStoryResource?.Descriptor is not { } resource) return true;
        if (!_resourceWorkspaceDialogs.ConfirmSaveBeforeSwitch(resource)) return false;
        return TrySaveCurrentStoryResource();
    }

    private bool DiscardCurrentDialogueDraft()
    {
        if (CurrentDialogue?.Document is not { IsNewDraft: true } document) return true;
        var id = document.Id;
        try
        {
            _projectService.DiscardDialogueDraft(document);
            _dialogueDrafts.Remove(id);
            StoryWorkspace.Dialogues?.RemoveDraft(id);
            CurrentDialogue = null;
            _selectedStoryResource = null;
            OnPropertyChanged(nameof(SelectedStoryResource));
            ReportSuccess($"Dialogue 草稿 '{id}' 已放弃。", $"dialogue/{id}");
            return true;
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportResourceFailure("放弃 Dialogue 草稿", exception, ProjectResourceType.Dialogue, id);
            return false;
        }
    }

    private bool DiscardCurrentQuestDraft()
    {
        if (CurrentQuest?.Document is not { IsNewDraft: true } document) return true;
        var id = document.Id;
        try
        {
            _projectService.DiscardQuestDraft(document);
            _questDrafts.Remove(id);
            StoryWorkspace.Quests?.RemoveDraft(id);
            CurrentQuest = null;
            _selectedStoryResource = null;
            OnPropertyChanged(nameof(SelectedStoryResource));
            ReportSuccess($"Quest 草稿 '{id}' 已放弃。", $"quest/{id}");
            return true;
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportResourceFailure("放弃 Quest 草稿", exception, ProjectResourceType.Quest, id);
            return false;
        }
    }

    private static IReadOnlyList<ResourceDescriptor> GetResourceDescriptors(ProjectSession project, StoryResource story, IEnumerable<DialogueDocument>? dialogueDrafts = null)
    {
        var dialogueIds = story.OwnedResources.Dialogues.Concat(story.ReferencedResources.Dialogues).ToHashSet(StringComparer.Ordinal);
        var questIds = story.OwnedResources.Quests.Concat(story.ReferencedResources.Quests).ToHashSet(StringComparer.Ordinal);
        var values = project.Dialogues.ListDialogues()
            .Where(resource => dialogueIds.Contains(resource.Id))
            .Select(resource => new ResourceDescriptor(ProjectResourceType.Dialogue, resource.Id, resource.DisplayName, resource.Path))
            .Concat(project.Quests.ListQuests()
                .Where(resource => questIds.Contains(resource.Id))
                .Select(resource => new ResourceDescriptor(ProjectResourceType.Quest, resource.Id, resource.DisplayName, resource.Path)))
            .ToList();
        return values;
    }

    private static IReadOnlyDictionary<string, string> GetActorHomeStoryNames(
        ProjectSession project,
        IReadOnlyList<ActorResourceInfo> actors)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var actor in actors)
        {
            var homeStory = project.Registry.GetHomeStory(ProjectResourceType.Actor, actor.Id);
            if (homeStory is null) continue;
            values[actor.Id] = string.IsNullOrWhiteSpace(homeStory.DisplayName)
                ? homeStory.Id
                : homeStory.DisplayName;
        }
        return values;
    }

    private static IReadOnlyDictionary<string, string> GetResourceHomeStoryNames(
        ProjectSession project,
        IReadOnlyList<ResourceDescriptor> descriptors,
        ProjectResourceType resourceType)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var descriptor in descriptors.Where(item => item.Type == resourceType))
        {
            var homeStory = project.Registry.GetHomeStory(descriptor.Type, descriptor.Id);
            if (homeStory is null) continue;
            values[descriptor.Id] = string.IsNullOrWhiteSpace(homeStory.DisplayName)
                ? homeStory.Id
                : homeStory.DisplayName;
        }
        return values;
    }

    private void RefreshActorFilter()
    {
        var query = SearchText.Trim();
        FilteredActors.Clear();
        foreach (var actor in Actors.Where(actor => MatchesSearch(actor, query)))
        {
            FilteredActors.Add(actor);
        }
    }

    private static bool MatchesSearch(ActorResourceInfo actor, string query) =>
        query.Length == 0 ||
        actor.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        actor.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
        actor.Tags.Any(tag => tag.Contains(query, StringComparison.CurrentCultureIgnoreCase));

    private void OpenSelectedActor()
    {
        if (SelectedActor is null)
        {
            CurrentActor = null;
            return;
        }

        try
        {
            CurrentActor = new ActorEditorViewModel(_projectService.OpenActor(SelectedActor.Id));
            StatusMessage = $"已加载 Actor：{SelectedActor.Id}";
            Output.Append(StatusMessage, source: $"actor/{SelectedActor.Id}");
        }
        catch (Exception exception) when (
            exception is ProjectException or ActorRepositoryException or ActorDataException or ActorValidationException)
        {
            CurrentActor = null;
            ReportFailure("加载 Actor", exception, SelectedActor.Id);
        }
    }

    private void OpenSelectedStoryResource()
    {
        var descriptor = SelectedStoryResource?.Descriptor;
        var project = _projectService.CurrentProject;
        if (project is null)
        {
            CurrentDialogue = null;
            CurrentQuest = null;
            return;
        }

        if (descriptor is null && SelectedStoryResource?.IsDraft != true)
        {
            CurrentDialogue = null;
            CurrentQuest = null;
            return;
        }

            var resourceType = descriptor?.Type ?? SelectedStoryResource?.ResourceType ?? ProjectResourceType.Dialogue;
        var resourceId = descriptor?.Id ?? SelectedStoryResource?.Id ?? string.Empty;
        try
        {
            var actorIds = project.Actors.ListActors().Select(actor => actor.Id).ToArray();
            if (SelectedStoryResource?.IsDraft == true && _dialogueDrafts.TryGetValue(SelectedStoryResource.Id, out var draft))
            {
                CurrentQuest = null;
                CurrentDialogue = new DialogueEditorViewModel(draft, actorIds);
                StatusMessage = $"已加载 Dialogue 草稿：{draft.Id}";
                Output.Append(StatusMessage, source: $"dialogue/{draft.Id}");
                return;
            }
            if (SelectedStoryResource?.IsDraft == true && _questDrafts.TryGetValue(SelectedStoryResource.Id, out var questDraft))
            {
                CurrentDialogue = null;
                CurrentQuest = new QuestEditorViewModel(questDraft, actorIds);
                StatusMessage = $"已加载 Quest 草稿：{questDraft.Id}";
                Output.Append(StatusMessage, source: $"quest/{questDraft.Id}");
                return;
            }
            if (descriptor is null)
            {
                CurrentDialogue = null;
                CurrentQuest = null;
                return;
            }
            if (descriptor.Type == ProjectResourceType.Dialogue)
            {
                CurrentQuest = null;
                CurrentDialogue = new DialogueEditorViewModel(_projectService.OpenDialogue(descriptor.Id), actorIds);
            }
            else if (descriptor.Type == ProjectResourceType.Quest)
            {
                CurrentDialogue = null;
                CurrentQuest = new QuestEditorViewModel(_projectService.OpenQuest(descriptor.Id), actorIds);
            }
            StatusMessage = $"已加载 {descriptor.Type}：{descriptor.Id}";
            Output.Append(StatusMessage, source: $"{descriptor.Type.ToString().ToLowerInvariant()}/{descriptor.Id}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            CurrentDialogue = null;
            CurrentQuest = null;
            ReportResourceFailure($"加载 {resourceType}", exception, resourceType, resourceId);
        }
    }

    private void SaveCurrentResource()
    {
        if (StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Actors)
        {
            SaveActor();
            return;
        }

        if (StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Flow)
        {
            if (!TrySaveCurrentFlow()) return;
            RefreshCurrentStory();
            return;
        }

        var selectedId = SelectedStoryResource?.Id;
        if (selectedId is null) return;
        if (!TrySaveCurrentStoryResource()) return;
        RefreshCurrentStory(selectedResourceId: selectedId);
    }

    private bool TrySaveCurrentStoryResource()
    {
        try
        {
            if (CurrentDialogue is not null)
            {
                var wasDraft = CurrentDialogue.Document.IsNewDraft;
                var dialogueId = CurrentDialogue.Id;
                _projectService.SaveDialogue(CurrentDialogue.Document);
                if (wasDraft)
                {
                    _dialogueDrafts.Remove(dialogueId);
                    if (_projectService.CurrentProject?.Dialogues.ListDialogues().FirstOrDefault(item => item.Id == dialogueId) is { } saved)
                    {
                        StoryWorkspace.Dialogues?.PromoteDraft(
                            dialogueId,
                            new ResourceDescriptor(ProjectResourceType.Dialogue, saved.Id, saved.DisplayName, saved.Path));
                    }
                }
                ReportSuccess($"Dialogue '{dialogueId}' 已保存到磁盘。", $"dialogue/{dialogueId}");
                return true;
            }
            if (CurrentQuest is not null)
            {
                var wasDraft = CurrentQuest.Document.IsNewDraft;
                var questId = CurrentQuest.Id;
                _projectService.SaveQuest(CurrentQuest.Document);
                if (wasDraft && _projectService.CurrentProject?.Quests.ListQuests().FirstOrDefault(item => item.Id == questId) is { } savedQuest)
                {
                    _questDrafts.Remove(questId);
                    StoryWorkspace.Quests?.PromoteDraft(
                        questId,
                        new ResourceDescriptor(ProjectResourceType.Quest, savedQuest.Id, savedQuest.DisplayName, savedQuest.Path));
                }
                ReportSuccess($"Quest '{questId}' 已保存到磁盘。", $"quest/{questId}");
                return true;
            }
            return true;
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            var type = CurrentDialogue is not null ? ProjectResourceType.Dialogue : ProjectResourceType.Quest;
            var id = CurrentDialogue?.Id ?? CurrentQuest?.Id;
            ReportResourceFailure($"保存 {type}", exception, type, id);
            return false;
        }
    }

    private StoryFlowEditorViewModel CreateFlowEditor(string storyId)
    {
        var project = _projectService.CurrentProject ?? throw new ProjectException("No project is open.");
        var story = project.Stories.LoadStory(storyId);
        var actorIds = project.Actors.ListActors().Select(actor => actor.Id).Distinct(StringComparer.Ordinal).ToArray();
        var dialogueIds = project.Dialogues.ListDialogues().Select(dialogue => dialogue.Id).Distinct(StringComparer.Ordinal).ToArray();
        var questIds = project.Quests.ListQuests().Select(quest => quest.Id).Distinct(StringComparer.Ordinal).ToArray();
        var storyActorIds = story.OwnedResources.Actors.Concat(story.ReferencedResources.Actors).Distinct(StringComparer.Ordinal).ToArray();
        var storyDialogueIds = story.OwnedResources.Dialogues.Concat(story.ReferencedResources.Dialogues).Distinct(StringComparer.Ordinal).ToArray();
        var storyQuestIds = story.OwnedResources.Quests.Concat(story.ReferencedResources.Quests).Distinct(StringComparer.Ordinal).ToArray();
        var storyIds = project.Stories.ListStories().Select(candidate => candidate.Id).ToArray();
        var document = project.Stories.LoadStoryDocument(storyId);
        var recovery = _ignoredFlowRecoveries.Contains(storyId) ? null : _flowRecoveryStore?.Load(storyId);
        if (recovery?.Resource is not null)
        {
            switch (_flowWorkspaceDialogs.ChooseRecovery(recovery))
            {
                case StoryFlowRecoveryChoice.Recover:
                    document.Replace(recovery.Resource);
                    ReportWarning($"已恢复 Story Flow '{storyId}' 的编辑器草稿；正式 Story JSON 尚未改变。", $"story/{storyId}/flow");
                    break;
                case StoryFlowRecoveryChoice.Delete:
                    _flowRecoveryStore?.Delete(storyId);
                    break;
                default:
                    _ignoredFlowRecoveries.Add(storyId);
                    break;
            }
        }
        return new StoryFlowEditorViewModel(
            document, actorIds, dialogueIds, questIds, storyIds,
            storyActorIds, storyDialogueIds, storyQuestIds);
    }

    private bool TrySaveCurrentFlow()
    {
        if (CurrentFlow?.IsDirty != true) return true;
        try
        {
            if (CurrentFlow.ValidationErrors.Count != 0)
            {
                ReplaceFlowValidationSource(CurrentFlow);
                BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Problems");
                BottomPanel.IsExpanded = true;
                ReportWarning($"Story Flow '{CurrentFlow.Id}' 仍有校验错误，尚未保存。", $"story/{CurrentFlow.Id}/flow");
                return false;
            }
            var project = _projectService.CurrentProject ?? throw new ProjectException("No project is open.");
            project.Stories.SaveStory(CurrentFlow.Document);
            _flowRecoveryStore?.Delete(CurrentFlow.Id);
            _ignoredFlowRecoveries.Remove(CurrentFlow.Id);
            Problems.RemoveSourceTree($"story/{CurrentFlow.Id}/flow");
            ReportSuccess($"Story Flow '{CurrentFlow.Id}' 已保存到磁盘。", $"story/{CurrentFlow.Id}/flow");
            return true;
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("保存 Story Flow", exception, CurrentFlow?.Id);
            return false;
        }
    }

    private void SaveActor()
    {
        if (CurrentActor is null)
        {
            return;
        }

        try
        {
            var document = _projectService.SaveActor(CurrentActor.Document);
            var selectedId = document.Id;
            RefreshCurrentStory(selectedId);
            ReportSuccess($"Actor '{selectedId}' 已保存到磁盘。", $"actor/{selectedId}");
        }
        catch (Exception exception) when (
            exception is ProjectException or ActorRepositoryException or ActorDataException or ActorValidationException)
        {
            ReportFailure("保存 Actor", exception, CurrentActor?.Id);
        }
    }

    private bool TrySaveCurrentActor()
    {
        if (CurrentActor is null)
        {
            return true;
        }

        try
        {
            _projectService.SaveActor(CurrentActor.Document);
            ReportSuccess($"Actor '{CurrentActor.Id}' 已保存到磁盘。", $"actor/{CurrentActor.Id}");
            return true;
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("保存 Actor", exception, CurrentActor?.Id);
            return false;
        }
    }

    private void SaveAll()
    {
        try
        {
            if (CurrentFlow?.IsDirty == true && !TrySaveCurrentFlow()) return;
            _projectService.SaveAll();
            ReportSuccess("所有未保存的资源已写入磁盘。", "Project");
            SaveActorCommand.RaiseCanExecuteChanged();
            SaveCurrentResourceCommand.RaiseCanExecuteChanged();
            RaiseWorkspaceCommandStates();
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("保存全部", exception);
        }
    }

    private bool CanSaveAll() =>
        HasProject && HasUnsavedDocuments();

    private bool HasUnsavedDocuments() =>
        _projectService.OpenActorDocuments.Any(document => document.IsDirty) ||
        _projectService.OpenDialogueDocuments.Any(document => document.IsDirty) ||
        _projectService.OpenQuestDocuments.Any(document => document.IsDirty) ||
        CurrentFlow?.IsDirty == true;

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

        BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Minecraft");
        const string message = "Studio 文件已保存并通过验证。请在 Minecraft 中执行：/dgrpg reload";
        StatusMessage = message;
        Output.Append(message, OutputKind.Information, "Runtime");
        Toast.Show("保存完成；请在 Minecraft 执行 /dgrpg reload。", ToastKind.Success);
    }

    private void NewActor()
    {
        var project = _projectService.CurrentProject;
        if (project is null || !StoryWorkspace.HasStory || !TryLeaveActorEditor())
        {
            return;
        }

        var mode = _actorWorkspaceDialogs.RequestCreationMode(StoryWorkspace.StoryDisplayName);
        if (mode is null)
        {
            return;
        }

        if (mode == ActorCreationMode.Blank)
        {
            var request = _actorWorkspaceDialogs.RequestCreate(project.Actors.GetAvailableId("new_actor"));
            if (request is null) return;
            ExecuteWorkspaceOperation(
                () => _projectService.CreateActorInStory(StoryWorkspace.StoryId, request.Id, request.DisplayName),
                document => $"Actor '{document.Id}' 已创建并归入“{StoryWorkspace.StoryDisplayName}”。");
            return;
        }

        var source = _actorWorkspaceDialogs.PickActor(Actors, ActorPickerMode.ImportAsNew, StoryWorkspace.StoryDisplayName);
        if (source is null) return;
        var importRequest = _actorWorkspaceDialogs.RequestImportIdentity(
            source,
            project.Actors.GetAvailableId(source.Id + "_copy"));
        if (importRequest is null) return;
        ExecuteWorkspaceOperation(
            () =>
            {
                var imported = _projectService.ImportActorAsNew(source.Id, importRequest.Id, StoryWorkspace.StoryId);
                imported.DisplayName = importRequest.DisplayName;
                return _projectService.SaveActor(imported);
            },
            document => $"Actor '{document.Id}' 已作为独立副本导入“{StoryWorkspace.StoryDisplayName}”。");
    }

    private void ReferenceActor()
    {
        var project = _projectService.CurrentProject;
        var storyActors = StoryWorkspace.Actors;
        if (project is null || storyActors is null || !TryLeaveActorEditor()) return;
        var existingIds = storyActors.Memberships.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var candidates = Actors.Where(actor => !existingIds.Contains(actor.Id)).ToArray();
        var selected = _actorWorkspaceDialogs.PickActor(
            candidates,
            ActorPickerMode.Reference,
            StoryWorkspace.StoryDisplayName);
        if (selected is null) return;

        try
        {
            _projectService.AddActorReference(StoryWorkspace.StoryId, selected.Id);
            RefreshCurrentStory(selected.Id);
            ReportSuccess(
                $"已引用 Actor '{selected.Id}'；多个剧情将共享同一份角色数据。",
                $"actor/{selected.Id}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("引用 Actor", exception, selected.Id);
        }
    }

    private void RemoveActorReference()
    {
        var membership = SelectedStoryActor;
        if (membership?.Actor is null || !membership.IsReferenced || !TryLeaveActorEditor()) return;
        if (!_actorWorkspaceDialogs.ConfirmRemoveReference(membership.Actor, StoryWorkspace.StoryDisplayName)) return;

        try
        {
            _projectService.RemoveActorReference(StoryWorkspace.StoryId, membership.Id);
            RefreshCurrentStory();
            ReportSuccess(
                $"已从“{StoryWorkspace.StoryDisplayName}”解除 Actor '{membership.Id}' 的引用；角色文件未删除。",
                $"actor/{membership.Id}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("解除 Actor 引用", exception, membership.Id);
        }
    }

    private void ViewActorReferences()
    {
        if (SelectedActor is null) return;
        try
        {
            _actorWorkspaceDialogs.ShowReferences(
                SelectedActor,
                _projectService.GetActorReferences(SelectedActor.Id));
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("查看 Actor 引用", exception, SelectedActor.Id);
        }
    }

    private void DuplicateActor()
    {
        if (SelectedActor is null)
        {
            return;
        }

        ExecuteWorkspaceOperation(
            () => _projectService.DuplicateActor(SelectedActor.Id),
            document => $"Actor 已复制为 '{document.Id}'。");
    }

    private void RenameActor()
    {
        if (SelectedActor is null || _projectService.CurrentProject is null)
        {
            return;
        }

        var sourceId = SelectedActor.Id;
        var suggestedId = _projectService.CurrentProject.Actors.GetAvailableId(sourceId + "_renamed");
        var targetId = _actorWorkspaceDialogs.RequestRename(SelectedActor, suggestedId);
        if (targetId is null)
        {
            return;
        }

        ExecuteWorkspaceOperation(
            () => _projectService.RenameActor(sourceId, targetId),
            document => $"Actor '{sourceId}' 已重命名为 '{document.Id}'。");
    }

    private void DeleteActor()
    {
        if (SelectedActor is null)
        {
            return;
        }
        var deletedId = SelectedActor.Id;
        try
        {
            var references = _projectService.GetActorReferences(deletedId);
            if (references.Count > 0)
            {
                _actorWorkspaceDialogs.ShowReferences(SelectedActor, references);
                ReportWarning(
                    $"Actor '{deletedId}' 仍被 {references.Count} 个剧情引用；请先解除引用。",
                    $"actor/{deletedId}");
                return;
            }
            if (!_actorWorkspaceDialogs.ConfirmDelete(SelectedActor)) return;

            _projectService.DeleteActor(deletedId);
            CurrentActor = null;
            RefreshCurrentStory();
            ReportSuccess($"Actor '{deletedId}' 已从磁盘删除。", $"actor/{deletedId}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("删除 Actor", exception, deletedId);
        }
        finally
        {
            RaiseWorkspaceCommandStates();
        }
    }

    private ProjectResourceType? CurrentStoryResourceType => StoryWorkspace.CurrentRoute switch
    {
        StoryWorkspaceRoutes.Dialogues => ProjectResourceType.Dialogue,
        StoryWorkspaceRoutes.Quests => ProjectResourceType.Quest,
        _ => null,
    };

    private void NewStoryResource()
    {
        _lastUiCommand = nameof(NewStoryResource);
        var project = _projectService.CurrentProject;
        var type = CurrentStoryResourceType;
        try
        {
            if (project is null || type is null || !StoryWorkspace.HasStory || !TryLeaveCurrentStoryResourceEditor()) return;
            if (type == ProjectResourceType.Dialogue)
            {
                var draftRequest = _resourceWorkspaceDialogs.RequestCreate(
                    ProjectResourceType.Dialogue,
                    project.Dialogues.GetAvailableId("new_dialogue"));
                if (draftRequest is null) return;
                var draft = _projectService.CreateDialogueDraftInStory(
                    StoryWorkspace.StoryId,
                    draftRequest.Id,
                    draftRequest.DisplayName);
                _dialogueDrafts[draft.Id] = draft;
                RefreshCurrentStory(selectedResourceId: draft.Id);
                ReportSuccess($"Dialogue 草稿 '{draft.Id}' 已创建。", $"dialogue/{draft.Id}");
                return;
            }

            if (type == ProjectResourceType.Quest)
            {
                var draftRequest = _resourceWorkspaceDialogs.RequestCreate(
                    ProjectResourceType.Quest,
                    project.Quests.GetAvailableId("new_quest"));
                if (draftRequest is null) return;
                var draft = _projectService.CreateQuestDraftInStory(
                    StoryWorkspace.StoryId,
                    draftRequest.Id,
                    draftRequest.DisplayName);
                _questDrafts[draft.Id] = draft;
                RefreshCurrentStory(selectedResourceId: draft.Id);
                ReportSuccess($"Quest 草稿 '{draft.Id}' 已创建。", $"quest/{draft.Id}");
                return;
            }
        }
        catch (Exception exception) when (IsWorkspaceException(exception) || IsRecoverableUiException(exception))
        {
            ReportResourceFailure($"创建 {type}", exception, type ?? ProjectResourceType.Dialogue, SelectedStoryResource?.Id);
        }
    }

    private void DuplicateStoryResource()
    {
        _lastUiCommand = nameof(DuplicateStoryResource);
        var project = _projectService.CurrentProject;
        var type = CurrentStoryResourceType;
        ResourceDescriptor? source = null;
        try
        {
            if (project is null || type is null || !StoryWorkspace.HasStory || !TryLeaveCurrentStoryResourceEditor()) return;

            // The picker is deliberately typed and contains only persisted resources. The
            // identity dialog and Core draft API are the only later mutation points.
            var candidates = GetAllProjectResourceDescriptors(project, type.Value);
            source = _resourceWorkspaceDialogs.PickResource(
                type.Value,
                candidates,
                ResourcePickerMode.ImportAsNew,
                StoryWorkspace.StoryDisplayName);
            if (source is null) return;

            var suggestedId = type == ProjectResourceType.Dialogue
                ? project.Dialogues.GetAvailableId(source.Id + "_copy")
                : project.Quests.GetAvailableId(source.Id + "_copy");
            var identity = _resourceWorkspaceDialogs.RequestImportIdentity(type.Value, source, suggestedId);
            if (identity is null) return;

            if (type == ProjectResourceType.Dialogue)
            {
                var draft = _projectService.CreateDialogueDraftFromExistingInStory(
                    StoryWorkspace.StoryId, source.Id, identity.Id, identity.DisplayName);
                _dialogueDrafts[draft.Id] = draft;
            }
            else
            {
                var draft = _projectService.CreateQuestDraftFromExistingInStory(
                    StoryWorkspace.StoryId, source.Id, identity.Id, identity.DisplayName);
                _questDrafts[draft.Id] = draft;
            }

            RefreshCurrentStory(selectedResourceId: identity.Id);
            ReportSuccess($"{type.Value} 草稿 '{identity.Id}' 已从 '{source.Id}' 创建。", $"{type.Value.ToString().ToLowerInvariant()}/{identity.Id}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception) || IsRecoverableUiException(exception))
        {
            ReportResourceFailure($"复制 {type}", exception, type ?? ProjectResourceType.Dialogue, source?.Id);
        }
    }

    private void ReferenceStoryResource()
    {
        _lastUiCommand = nameof(ReferenceStoryResource);
        var project = _projectService.CurrentProject;
        var type = CurrentStoryResourceType;
        ResourceDescriptor? selected = null;
        try
        {
            if (project is null || type is null || !TryLeaveCurrentStoryResourceEditor()) return;
            var page = type == ProjectResourceType.Dialogue
                ? (StoryResourceMembershipListViewModel?)StoryWorkspace.Dialogues
                : StoryWorkspace.Quests;
            var existingIds = page?.Items.Select(item => item.Id).ToHashSet(StringComparer.Ordinal) ?? [];
            var candidates = GetAllProjectResourceDescriptors(project, type.Value)
                .Where(resource => !existingIds.Contains(resource.Id)).ToArray();
            selected = _resourceWorkspaceDialogs.PickResource(type.Value, candidates, ResourcePickerMode.Reference, StoryWorkspace.StoryDisplayName);
            if (selected is null) return;

            if (type == ProjectResourceType.Dialogue) _projectService.AddDialogueReference(StoryWorkspace.StoryId, selected.Id);
            else _projectService.AddQuestReference(StoryWorkspace.StoryId, selected.Id);
            RefreshCurrentStory(selectedResourceId: selected.Id);
            ReportSuccess($"已引用 {type.Value} '{selected.Id}'；多个剧情共享同一份数据。", $"{type.Value.ToString().ToLowerInvariant()}/{selected.Id}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception) || IsRecoverableUiException(exception))
        {
            ReportResourceFailure($"引用 {type}", exception, type ?? ProjectResourceType.Dialogue, selected?.Id);
        }
    }

    private void RemoveStoryResourceReference()
    {
        _lastUiCommand = nameof(RemoveStoryResourceReference);
        var membership = SelectedStoryResource;
        var type = CurrentStoryResourceType;
        try
        {
            if (membership?.Descriptor is not { } descriptor || type is null || !membership.IsReferenced || !TryLeaveCurrentStoryResourceEditor()) return;
            if (!_resourceWorkspaceDialogs.ConfirmRemoveReference(descriptor, StoryWorkspace.StoryDisplayName)) return;

            if (type == ProjectResourceType.Dialogue) _projectService.RemoveDialogueReference(StoryWorkspace.StoryId, membership.Id);
            else _projectService.RemoveQuestReference(StoryWorkspace.StoryId, membership.Id);
            RefreshCurrentStory();
            ReportSuccess($"已解除 {type.Value} '{membership.Id}' 的剧情引用；资源文件未删除。", $"{type.Value.ToString().ToLowerInvariant()}/{membership.Id}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception) || IsRecoverableUiException(exception))
        {
            ReportResourceFailure($"解除 {type} 引用", exception, type ?? ProjectResourceType.Dialogue, membership?.Id);
        }
    }

    private void ViewStoryResourceReferences()
    {
        _lastUiCommand = nameof(ViewStoryResourceReferences);
        var descriptor = SelectedStoryResource?.Descriptor;
        if (descriptor is null) return;
        try
        {
            var references = descriptor.Type == ProjectResourceType.Dialogue
                ? _projectService.GetDialogueReferences(descriptor.Id)
                : _projectService.GetQuestReferences(descriptor.Id);
            _resourceWorkspaceDialogs.ShowReferences(descriptor, references);
        }
        catch (Exception exception) when (IsWorkspaceException(exception) || IsRecoverableUiException(exception))
        {
            ReportResourceFailure("查看资源引用", exception, descriptor.Type, descriptor.Id);
        }
    }

    private void DeleteStoryResource()
    {
        _lastUiCommand = nameof(DeleteStoryResource);
        var membership = SelectedStoryResource;
        var descriptor = membership?.Descriptor;
        if (descriptor is null) return;
        var resourceType = descriptor.Type;
        var resourceId = descriptor.Id;
        try
        {
            if (membership!.IsReferenced || !TryLeaveCurrentStoryResourceEditor()) return;
            var references = descriptor.Type == ProjectResourceType.Dialogue
                ? _projectService.GetDialogueReferences(descriptor.Id)
                : _projectService.GetQuestReferences(descriptor.Id);
            if (references.Count > 0)
            {
                _resourceWorkspaceDialogs.ShowReferences(descriptor, references);
                ReportWarning($"{descriptor.Type} '{descriptor.Id}' 仍被 {references.Count} 个剧情引用；请先解除引用。", $"{descriptor.Type.ToString().ToLowerInvariant()}/{descriptor.Id}");
                return;
            }
            if (!_resourceWorkspaceDialogs.ConfirmDelete(descriptor)) return;
            if (descriptor.Type == ProjectResourceType.Dialogue) _projectService.DeleteDialogue(descriptor.Id);
            else _projectService.DeleteQuest(descriptor.Id);
            CurrentDialogue = null;
            CurrentQuest = null;
            RefreshCurrentStory();
            ReportSuccess($"{descriptor.Type} '{descriptor.Id}' 已从磁盘删除。", $"{descriptor.Type.ToString().ToLowerInvariant()}/{descriptor.Id}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception) || IsRecoverableUiException(exception))
        {
            ReportResourceFailure($"删除 {resourceType}", exception, resourceType, resourceId);
        }
    }

    private void DeleteCurrentResource()
    {
        if (StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Actors) DeleteActor();
        else DeleteStoryResource();
    }

    private bool CanCreateOrReferenceStoryResource() =>
        HasProject && StoryWorkspace.HasStory && CurrentStoryResourceType is not null;

    private bool CanRemoveStoryResourceReference() =>
        SelectedStoryResource?.IsReferenced == true && ActiveEditor?.IsDirty != true;

    private bool CanDeleteSelectedStoryResource() =>
        SelectedStoryResource is { IsReferenced: false, Descriptor: not null } && ActiveEditor?.IsDirty != true;

    private bool CanDeleteCurrentResource() => StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Actors
        ? CanDeleteSelectedActor()
        : CanDeleteSelectedStoryResource();

    private static IReadOnlyList<ResourceDescriptor> GetAllProjectResourceDescriptors(ProjectSession project, ProjectResourceType type)
    {
        var resources = type == ProjectResourceType.Dialogue
            ? project.Dialogues.ListDialogues().Select(resource => (resource.Id, resource.DisplayName, resource.Path))
            : project.Quests.ListQuests().Select(resource => (resource.Id, resource.DisplayName, resource.Path));
        return resources.Select(resource =>
        {
            var homeStory = project.Registry.GetHomeStory(type, resource.Id);
            var homeStoryName = homeStory is null
                ? null
                : string.IsNullOrWhiteSpace(homeStory.DisplayName) ? homeStory.Id : homeStory.DisplayName;
            return new ResourceDescriptor(type, resource.Id, resource.DisplayName, resource.Path, homeStoryName);
        }).ToArray();
    }

    private void ExecuteWorkspaceOperation(
        Func<ActorDocument> operation,
        Func<ActorDocument, string> successMessage)
    {
        try
        {
            var document = operation();
            var selectedId = document.Id;
            RefreshCurrentStory(selectedId);
            ReportSuccess(successMessage(document), $"actor/{document.Id}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("Actor 操作", exception, SelectedActor?.Id);
        }
        finally
        {
            RaiseWorkspaceCommandStates();
        }
    }

    private bool CanMutateSelectedActor() =>
        SelectedActor is not null &&
        (CurrentActor is null || !CurrentActor.Document.IsDirty);

    private bool CanCreateOrReferenceActor() =>
        HasProject && StoryWorkspace.HasStory &&
        string.Equals(StoryWorkspace.CurrentRoute, StoryWorkspaceRoutes.Actors, StringComparison.Ordinal);

    private bool CanRemoveActorReference() =>
        SelectedStoryActor?.IsReferenced == true && CanMutateSelectedActor();

    private bool CanViewActorReferences() => SelectedActor is not null;

    private bool CanDeleteSelectedActor() =>
        CanMutateSelectedActor() && SelectedStoryActor?.IsReferenced != true;

    private void RefreshCurrentStory(string? selectedActorId = null, string? selectedResourceId = null)
    {
        var project = _projectService.CurrentProject;
        if (project is null) return;
        var storyId = StoryWorkspace.StoryId;
        var route = StoryWorkspace.CurrentRoute;
        CurrentActor = null;
        CurrentDialogue = null;
        CurrentQuest = null;
        CurrentFlow = null;
        _selectedStoryActor = null;
        OnPropertyChanged(nameof(SelectedStoryActor));
        _selectedStoryResource = null;
        OnPropertyChanged(nameof(SelectedStoryResource));
        LoadActorList();
        LoadStoryList();
        if (string.IsNullOrWhiteSpace(storyId))
        {
            SelectedActor = selectedActorId is null
                ? null
                : Actors.FirstOrDefault(actor => actor.Id == selectedActorId);
            return;
        }

        var story = project.Stories.LoadStory(storyId);
        var actors = project.Actors.ListActors();
        var descriptors = GetResourceDescriptors(project, story, _dialogueDrafts.Values.Where(draft => draft.DraftOwnerStoryId == story.Id));
        StoryWorkspace.OpenStory(
            story,
            actors,
            descriptors,
            GetActorHomeStoryNames(project, actors),
                GetResourceHomeStoryNames(project, descriptors, ProjectResourceType.Dialogue),
                GetResourceHomeStoryNames(project, descriptors, ProjectResourceType.Quest),
                _dialogueDrafts.Values.Where(draft => draft.DraftOwnerStoryId == story.Id).ToArray(),
                _questDrafts.Values.Where(draft => draft.DraftOwnerStoryId == story.Id).ToArray());
        StoryWorkspace.SelectRoute(route);
        CurrentFlow = CreateFlowEditor(storyId);
        ProjectHome.SelectedStory = ProjectHome.Stories.FirstOrDefault(item => item.Id == storyId);
        if (selectedActorId is not null)
        {
            SelectedStoryActor = StoryWorkspace.Actors?.Memberships.FirstOrDefault(item => item.Id == selectedActorId);
        }
        if (selectedResourceId is not null)
        {
            SelectedStoryResource = route == StoryWorkspaceRoutes.Dialogues
                ? StoryWorkspace.Dialogues?.Items.FirstOrDefault(item => item.Id == selectedResourceId)
                : StoryWorkspace.Quests?.Items.FirstOrDefault(item => item.Id == selectedResourceId);
        }
        RaiseWorkspaceCommandStates();
    }

    private static bool IsWorkspaceException(Exception exception) =>
        exception is ProjectException or ActorRepositoryException or ActorDataException or ActorValidationException
            or DialogueException or QuestException
            or StoryRepositoryException or StoryNotFoundException or StoryDataException or StoryValidationException;

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
        if (args.PropertyName == nameof(ProjectHomeViewModel.SelectedStory))
        {
            OpenSelectedStoryCommand.RaiseCanExecuteChanged();
            DeleteSelectedStoryCommand.RaiseCanExecuteChanged();
        }
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

    private void OnStoryWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(StoryWorkspaceViewModel.Actors))
        {
            if (_observedStoryActors is not null)
                _observedStoryActors.PropertyChanged -= OnStoryActorsPropertyChanged;
            _observedStoryActors = (sender as StoryWorkspaceViewModel)?.Actors;
            if (_observedStoryActors is not null)
                _observedStoryActors.PropertyChanged += OnStoryActorsPropertyChanged;
            return;
        }

        if (args.PropertyName == nameof(StoryWorkspaceViewModel.Dialogues))
        {
            if (_observedStoryDialogues is not null)
                _observedStoryDialogues.PropertyChanged -= OnStoryResourceListPropertyChanged;
            _observedStoryDialogues = (sender as StoryWorkspaceViewModel)?.Dialogues;
            if (_observedStoryDialogues is not null)
                _observedStoryDialogues.PropertyChanged += OnStoryResourceListPropertyChanged;
            return;
        }

        if (args.PropertyName == nameof(StoryWorkspaceViewModel.Quests))
        {
            if (_observedStoryQuests is not null)
                _observedStoryQuests.PropertyChanged -= OnStoryResourceListPropertyChanged;
            _observedStoryQuests = (sender as StoryWorkspaceViewModel)?.Quests;
            if (_observedStoryQuests is not null)
                _observedStoryQuests.PropertyChanged += OnStoryResourceListPropertyChanged;
            return;
        }

        if (args.PropertyName == nameof(StoryWorkspaceViewModel.Story))
            return;

        if (args.PropertyName == nameof(StoryWorkspaceViewModel.CurrentRoute) && StoryWorkspace.HasStory)
        {
            if (StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Dialogues)
            {
                CurrentActor = null;
                CurrentQuest = null;
                _selectedStoryResource = StoryWorkspace.Dialogues?.SelectedItem;
            }
            else if (StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Quests)
            {
                CurrentActor = null;
                CurrentDialogue = null;
                _selectedStoryResource = StoryWorkspace.Quests?.SelectedItem;
            }
            else
            {
                _selectedStoryResource = null;
                CurrentDialogue = null;
                CurrentQuest = null;
            }
            OnPropertyChanged(nameof(SelectedStoryResource));
            OpenSelectedStoryResource();
            OnPropertyChanged(nameof(ActiveEditor));
            StatusMessage = $"Story：{StoryWorkspace.StoryId} / {StoryWorkspace.SelectedRoute.Title}";
            RaiseWorkspaceCommandStates();
        }
    }

    private void OnStoryActorsPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(StoryActorsViewModel.SelectedMembership) &&
            sender is StoryActorsViewModel actors)
        {
            var previous = SelectedStoryActor;
            SelectedStoryActor = actors.SelectedMembership;
            if (!ReferenceEquals(SelectedStoryActor, actors.SelectedMembership))
                actors.SelectedMembership = previous;
        }
    }

    private void OnStoryResourceListPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(StoryResourceMembershipListViewModel.SelectedItem)) return;
        if (sender == StoryWorkspace.Dialogues && StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Dialogues)
            SelectedStoryResource = StoryWorkspace.Dialogues?.SelectedItem;
        else if (sender == StoryWorkspace.Quests && StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Quests)
            SelectedStoryResource = StoryWorkspace.Quests?.SelectedItem;
    }

    private void RaiseWorkspaceCommandStates()
    {
        NewActorCommand.RaiseCanExecuteChanged();
        ReferenceActorCommand.RaiseCanExecuteChanged();
        RemoveActorReferenceCommand.RaiseCanExecuteChanged();
        ViewActorReferencesCommand.RaiseCanExecuteChanged();
        DuplicateActorCommand.RaiseCanExecuteChanged();
        RenameActorCommand.RaiseCanExecuteChanged();
        DeleteActorCommand.RaiseCanExecuteChanged();
        NewStoryResourceCommand.RaiseCanExecuteChanged();
        ReferenceStoryResourceCommand.RaiseCanExecuteChanged();
        RemoveStoryResourceReferenceCommand.RaiseCanExecuteChanged();
        ViewStoryResourceReferencesCommand.RaiseCanExecuteChanged();
        DeleteStoryResourceCommand.RaiseCanExecuteChanged();
        DeleteCurrentResourceCommand.RaiseCanExecuteChanged();
        DuplicateStoryResourceCommand.RaiseCanExecuteChanged();
        SaveCurrentResourceCommand.RaiseCanExecuteChanged();
        UndoCurrentCommand.RaiseCanExecuteChanged();
        RedoCurrentCommand.RaiseCanExecuteChanged();
        SaveAllCommand.RaiseCanExecuteChanged();
        OpenProjectDirectoryCommand.RaiseCanExecuteChanged();
        ValidateProjectCommand.RaiseCanExecuteChanged();
        PrepareRuntimeReloadCommand.RaiseCanExecuteChanged();
        ShowProjectSettingsCommand.RaiseCanExecuteChanged();
        OpenSelectedStoryCommand.RaiseCanExecuteChanged();
        CreateStoryCommand.RaiseCanExecuteChanged();
        DeleteSelectedStoryCommand.RaiseCanExecuteChanged();
        ShowProjectHomeCommand.RaiseCanExecuteChanged();
        ShowProjectGraphCommand.RaiseCanExecuteChanged();
    }

    private void RefreshProblems()
    {
        if (CurrentFlow is not null)
            ReplaceFlowValidationSource(CurrentFlow);

        if (CurrentActor is not null)
        {
            ReplaceValidationSource($"actor/{CurrentActor.Id}", CurrentActor.Document.ValidationIssues);
            return;
        }

        if (CurrentDialogue is not null)
        {
            ReplaceValidationSource($"dialogue/{CurrentDialogue.Id}", CurrentDialogue.ValidationIssues);
            return;
        }

        if (CurrentQuest is not null)
        {
            ReplaceValidationSource($"quest/{CurrentQuest.Id}", CurrentQuest.ValidationIssues);
            return;
        }

        if (_projectService.CurrentProject is not null)
        {
            ReplaceValidationSource("project", _projectService.ValidateProject());
            UpdateProjectGraphProblems();
            return;
        }

        Problems.ClearAll();
    }

    private void ReplaceValidationSource(string source, IEnumerable<ValidationIssue> issues) =>
        Problems.ReplaceForSource(
            source,
            issues.Select(issue => new ProblemItem(issue.Severity, issue.Code, issue.Message, issue.Field, source)));

    private void ReplaceFlowValidationSource(StoryFlowEditorViewModel flow)
    {
        var rootSource = $"story/{flow.Id}/flow";
        Problems.ReplaceForSourceTree(
            rootSource,
            flow.ValidationIssues.Select(issue => new ProblemItem(
                issue.Severity,
                issue.Code,
                issue.Message,
                issue.Field,
                string.IsNullOrWhiteSpace(issue.NodeId) ? rootSource : $"{rootSource}/{issue.NodeId}")));
    }

    private void UpdateProjectGraphProblems()
    {
        var problems = ProjectHome.Graph.Diagnostics.Select(issue => new ProblemItem(
                issue.Code == "project_graph.target.missing" ? ValidationSeverity.Error : ValidationSeverity.Warning,
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
        bool writeCrashLog = true)
    {
        if (writeCrashLog)
        {
            LogCrash(exception, action);
        }

        var message = $"{action}失败：{exception.Message}";
        var source = actorId is null ? exception.GetType().Name : $"actor/{actorId}";
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

    private void ReportResourceFailure(
        string action,
        Exception exception,
        ProjectResourceType type,
        string? resourceId,
        bool writeCrashLog = true)
    {
        if (writeCrashLog)
        {
            LogCrash(exception, action);
        }

        var id = resourceId ?? "unknown";
        var source = $"{type.ToString().ToLowerInvariant()}/{id}";
        var message = $"{action}失败：{exception.Message}";
        StatusMessage = message;
        Output.Append(message, OutputKind.Error, source);
        Toast.Show(message, ToastKind.Error);

        IReadOnlyList<ValidationIssue>? issues = exception switch
        {
            DialogueValidationException dialogue => dialogue.Issues,
            QuestValidationException quest => quest.Issues,
            _ => null,
        };
        if (issues is not null)
        {
            ReplaceValidationSource(source, issues);
            BottomPanel.SelectedTab = BottomPanel.Tabs.Single(tab => tab.Page == "Problems");
        }
        else
        {
            Problems.ReplaceForSource(source, [new ProblemItem(ValidationSeverity.Error, "operation.failure", message, Source: source)]);
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

    private void OnCurrentActorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        SaveActorCommand.RaiseCanExecuteChanged();
        RaiseCurrentEditorStates();
    }

    private void OnCurrentResourceEditorPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (sender is StoryFlowEditorViewModel flow && eventArgs.PropertyName == nameof(StoryFlowEditorViewModel.IsDirty))
            CaptureFlowRecovery(flow);
        RaiseCurrentEditorStates();
    }

    private void OnFlowHistoryCanExecuteChanged(object? sender, EventArgs eventArgs)
    {
        UndoCurrentCommand.RaiseCanExecuteChanged();
        RedoCurrentCommand.RaiseCanExecuteChanged();
    }

    private void CaptureFlowRecovery(StoryFlowEditorViewModel flow)
    {
        if (_flowRecoveryStore is null) return;
        try
        {
            if (flow.IsDirty)
                _flowRecoveryStore.Save(flow.Document.ToResource(), flow.Document.SourcePath);
            else
                _flowRecoveryStore.Delete(flow.Id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException)
        {
            ReportWarning($"Story Flow '{flow.Id}' 的恢复草稿写入失败：{exception.Message}", $"story/{flow.Id}/flow/recovery");
        }
    }

    private void ReportPendingFlowRecoveries()
    {
        var count = _flowRecoveryStore?.Enumerate().Count ?? 0;
        if (count > 0)
            ReportWarning($"检测到 {count} 个 Story Flow 恢复草稿；打开对应 Story 时可恢复、忽略或删除。", "story/recovery");
    }

    private void RaiseCurrentEditorStates()
    {
        OnPropertyChanged(nameof(ActiveEditor));
        SaveActorCommand.RaiseCanExecuteChanged();
        SaveCurrentResourceCommand.RaiseCanExecuteChanged();
        UndoCurrentCommand.RaiseCanExecuteChanged();
        RedoCurrentCommand.RaiseCanExecuteChanged();
        RaiseWorkspaceCommandStates();
        RefreshProblems();
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
        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete) => false;
    }

    private sealed class NullResourceWorkspaceDialogs : IResourceWorkspaceDialogs
    {
        public ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string storyDisplayName) => null;
        public ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId) => null;
        public ResourceIdentityRequest? RequestImportIdentity(ProjectResourceType type, ResourceDescriptor source, string suggestedId) => null;
        public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates, ResourcePickerMode mode, string storyDisplayName) => null;
        public bool ConfirmDelete(ResourceDescriptor resource) => false;
        public bool ConfirmRemoveReference(ResourceDescriptor resource, string storyDisplayName) => false;
        public void ShowReferences(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references) { }
        public bool ConfirmSaveBeforeSwitch(ResourceDescriptor resource) => false;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ResourceDescriptor resource) => UnsavedChangesChoice.Cancel;
    }

    private sealed class NullFlowWorkspaceDialogs : IFlowWorkspaceDialogs
    {
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(StoryFlowEditorViewModel flow) => UnsavedChangesChoice.Cancel;
    }
}
