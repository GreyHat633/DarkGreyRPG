using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Markup;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Graphs.Migration;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ShellViewModel : ObservableObject
{
    private readonly ProjectService _projectService;
    private readonly IProjectFolderPicker _projectFolderPicker;
    private readonly IActorWorkspaceDialogs _actorWorkspaceDialogs;
    private readonly IResourceWorkspaceDialogs _resourceWorkspaceDialogs;
    private readonly ICanonicalStoryResourceDialogs _canonicalStoryResourceDialogs;
    private readonly IItemWorkspaceDialogs _itemWorkspaceDialogs;
    private readonly IProjectWorkspaceDialogs _projectWorkspaceDialogs;
    private readonly IFlowWorkspaceDialogs _flowWorkspaceDialogs;
    private readonly ICrashLogService _crashLogService;
    private readonly Func<string, CanonicalProjectGraphStore> _canonicalGraphStoreFactory;
    private readonly Func<string, CanonicalProjectMigrationPreviewResult> _migrationPreview;
    private readonly Func<CanonicalProjectMigrationPreviewResult, CanonicalProjectMigrationTransactionResult> _migrationApply;
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
    private CanonicalStoryWorkspaceViewModel? _canonicalStoryWorkspace;
    private CanonicalProjectGraphStore? _canonicalGraphStore;
    private CanonicalGraphResourceSaveCoordinator? _canonicalSaveCoordinator;
    private IReadOnlyList<CanonicalStoryDiscoveryIssue> _canonicalStoryDiscoveryIssues = [];
    private StoryResourceMembershipViewModel? _selectedStoryResource;
    private readonly Dictionary<string, DialogueDocument> _dialogueDrafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, QuestDocument> _questDrafts = new(StringComparer.Ordinal);
    private string _searchText = string.Empty;
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
        IFlowWorkspaceDialogs? flowWorkspaceDialogs = null,
        ICrashLogService? crashLogService = null,
        ICanonicalStoryResourceDialogs? canonicalStoryResourceDialogs = null,
        Func<string, CanonicalProjectGraphStore>? canonicalGraphStoreFactory = null,
        Func<string, CanonicalProjectMigrationPreviewResult>? migrationPreview = null,
        Func<CanonicalProjectMigrationPreviewResult, CanonicalProjectMigrationTransactionResult>? migrationApply = null,
        IItemWorkspaceDialogs? itemWorkspaceDialogs = null)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        _projectFolderPicker = projectFolderPicker ?? throw new ArgumentNullException(nameof(projectFolderPicker));
        _actorWorkspaceDialogs = actorWorkspaceDialogs ?? new NullActorWorkspaceDialogs();
        _projectWorkspaceDialogs = projectWorkspaceDialogs ?? new NullProjectWorkspaceDialogs();
        _resourceWorkspaceDialogs = resourceWorkspaceDialogs ?? new NullResourceWorkspaceDialogs();
        _canonicalStoryResourceDialogs = canonicalStoryResourceDialogs ?? new NullCanonicalStoryResourceDialogs();
        _itemWorkspaceDialogs = itemWorkspaceDialogs ?? new NullItemWorkspaceDialogs();
        _flowWorkspaceDialogs = flowWorkspaceDialogs ?? new NullFlowWorkspaceDialogs();
        _crashLogService = crashLogService ?? new CrashLogService();
        _canonicalGraphStoreFactory = canonicalGraphStoreFactory
            ?? (projectDirectory => new CanonicalProjectGraphStore(projectDirectory));
        _migrationPreview = migrationPreview ?? CanonicalProjectMigrationPreview.PreviewProject;
        _migrationApply = migrationApply ?? (preview => new CanonicalProjectMigrationTransaction().Apply(preview));
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
        DeleteSelectedStoryCommand = new RelayCommand(
            DeleteSelectedStory,
            () => HasProject && ProjectHome.SelectedStory is { } story
                && (story.HasCanonicalStory || story.CanDeleteLegacyStory));
        ShowProjectHomeCommand = new RelayCommand(ShowProjectHome, () => HasProject);
        ShowProjectGraphCommand = new RelayCommand(ShowProjectGraph, () => HasProject);
        MigrateCanonicalProjectCommand = new RelayCommand(MigrateCanonicalProject, () => HasProject);
        ExportSelectedStoryPackageCommand = new RelayCommand(
            ExportSelectedStoryPackage,
            () => HasProject && ProjectHome.SelectedStory is not null && !HasUnsavedDocuments());
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

    public RelayCommand MigrateCanonicalProjectCommand { get; }

    public RelayCommand ExportSelectedStoryPackageCommand { get; }

    // Short alias retained for callers that refer to the menu action as project migration.
    public RelayCommand MigrateProjectCommand => MigrateCanonicalProjectCommand;

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
        ? $"{project.Project.DisplayName} — DarkGrey RPG Studio 0.3.1.2B"
        : "DarkGrey RPG Studio 0.3.1.2B";

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
        private set
        {
            if (SetProperty(ref _isResourceBrowserVisible, value))
                OnPropertyChanged(nameof(EffectiveResourceBrowserVisible));
        }
    }

    public bool EffectiveResourceBrowserVisible => IsResourceBrowserVisible && !IsCanonicalStoryWorkspaceVisible;

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
            OnPropertyChanged(nameof(SelectedStoryResourceActionText));
            if (StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Dialogues && StoryWorkspace.Dialogues is not null)
                StoryWorkspace.Dialogues.SelectedItem = value;
            if (StoryWorkspace.CurrentRoute == StoryWorkspaceRoutes.Quests && StoryWorkspace.Quests is not null)
                StoryWorkspace.Quests.SelectedItem = value;
            OpenSelectedStoryResource();
            RaiseWorkspaceCommandStates();
        }
    }

    public string SelectedStoryResourceActionText =>
        SelectedStoryResource?.IsDraft == true ? "放弃草稿" : "删除资源";

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

    public CanonicalStoryWorkspaceViewModel? CanonicalStoryWorkspace => _canonicalStoryWorkspace;
    public bool HasCanonicalStoryWorkspace => CanonicalStoryWorkspace is not null;
    public bool IsCanonicalStoryWorkspaceVisible => _isCanonicalStoryWorkspaceVisible;

    public IWorkspaceEditorViewModel? ActiveEditor => (IWorkspaceEditorViewModel?)(IsCanonicalStoryWorkspaceVisible
            ? CanonicalStoryWorkspace?.ActiveEditor
            : null)
        ?? (StoryWorkspace.CurrentRoute switch
        {
            StoryWorkspaceRoutes.Actors => CurrentActor,
            StoryWorkspaceRoutes.Dialogues => CurrentDialogue,
            StoryWorkspaceRoutes.Quests => CurrentQuest,
            StoryWorkspaceRoutes.Flow => CurrentFlow,
            _ => null,
        });

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

    private void MigrateCanonicalProject()
    {
        var project = _projectService.CurrentProject;
        if (project is null) return;

        if (HasUnsavedDocuments())
        {
            ReportWarning("当前项目有未保存的资源；请先保存后再迁移。", "migration");
            return;
        }

        CanonicalProjectMigrationPreviewResult preview;
        try
        {
            preview = _migrationPreview(project.ProjectDirectory);
        }
        catch (Exception exception)
        {
            ReportFailure("预览 Canonical 迁移", exception, sourceOverride: "migration");
            return;
        }

        // The dialog is the only confirmation boundary.  In particular, an
        // invalid preview is never handed to the apply delegate, even if an
        // injected dialog incorrectly reports confirmation.
        if (!_projectWorkspaceDialogs.ConfirmCanonicalProjectMigration(preview))
            return;
        if (!preview.CanApply)
        {
            ReportWarning("Canonical 迁移预览包含错误，无法应用。", "migration");
            return;
        }

        try
        {
            var result = _migrationApply(preview);
            _canonicalGraphStore = _canonicalGraphStoreFactory(project.ProjectDirectory);
            _canonicalSaveCoordinator = new CanonicalGraphResourceSaveCoordinator(_canonicalGraphStore);
            LoadActorList();
            LoadStoryList();
            RefreshProblems();
            ReportSuccess(
                $"Canonical 迁移成功：已写入 {result.WrittenPaths.Count} 个文件；备份：{result.BackupPath}",
                "migration");
        }
        catch (Exception exception)
        {
            ReportFailure("应用 Canonical 迁移", exception, sourceOverride: "migration");
        }
    }

    private bool OpenProjectFromDirectory(string projectDirectory, bool isRestore)
    {
        try
        {
            var project = _projectService.OpenProject(projectDirectory);
            _dialogueDrafts.Clear();
            _questDrafts.Clear();
            _flowRecoveryStore = new StoryFlowRecoveryStore(project.ProjectDirectory);
            SetCanonicalStoryWorkspace(null);
            _canonicalGraphStore = _canonicalGraphStoreFactory(project.ProjectDirectory);
            _canonicalSaveCoordinator = new CanonicalGraphResourceSaveCoordinator(_canonicalGraphStore);
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
            SetCanonicalStoryWorkspace(null);
            _canonicalGraphStore = _canonicalGraphStoreFactory(project.ProjectDirectory);
            _canonicalSaveCoordinator = new CanonicalGraphResourceSaveCoordinator(_canonicalGraphStore);
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
            ReportSuccess("项目已创建，可开始新建故事。", "Project");
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
        var discovery = _canonicalGraphStore is null
            ? new CanonicalStoryDiscoverySnapshot([])
            : new CanonicalStoryDiscoveryService(_canonicalGraphStore).Discover();
        var canonicalProjectGraph = _canonicalGraphStore is null
            ? new CanonicalProjectStoryGraphSnapshot([], [], [])
            : new CanonicalProjectStoryGraphService(_canonicalGraphStore).Derive();
        _canonicalStoryDiscoveryIssues = discovery.Issues;
        ProjectHome.ReplaceDiscoveredStories(
            stories,
            discovery.Items.Select(ToCanonicalStoryHomeEntry).ToArray(),
            projectDirectory: project.ProjectDirectory,
            canonicalGraph: canonicalProjectGraph);
        UpdateProjectGraphProblems();
        UpdateCanonicalStoryDiscoveryProblems();
        OnPropertyChanged(nameof(ProjectHome));
        OpenSelectedStoryCommand.RaiseCanExecuteChanged();
        DeleteSelectedStoryCommand.RaiseCanExecuteChanged();
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
        var selected = ProjectHome.SelectedStory;
        if (selected is null) return;
        if (CanonicalStoryWorkspace?.StoryEditor.Id == selected.Id && !IsCanonicalStoryWorkspaceVisible)
        {
            ShowRetainedCanonicalStoryWorkspace();
            return;
        }
        if (!TryLeaveCurrentEditor()) return;
        var project = _projectService.CurrentProject;
        if (project is null) return;

        try
        {
            var canonicalResult = TryOpenCanonicalStory(selected.Id);
            if (canonicalResult == CanonicalOpenResult.Opened) return;
            if (canonicalResult == CanonicalOpenResult.Failed)
            {
                SetCanonicalStoryWorkspace(null);
                StoryWorkspace.CloseStory();
                return;
            }
            SetCanonicalStoryWorkspace(null);
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

    private CanonicalOpenResult TryOpenCanonicalStory(string storyId)
    {
        var store = _canonicalGraphStore;
        if (store is null) return CanonicalOpenResult.NotPresent;
        try
        {
            var storyExists = File.Exists(store.Stories.GetPath(storyId));
            var membershipExists = File.Exists(store.Memberships.GetPath(storyId));
            if (!storyExists && !membershipExists) return CanonicalOpenResult.NotPresent;

            var snapshot = new CanonicalStoryWorkspaceLoader(store).Load(storyId);
            var workspace = new CanonicalStoryWorkspaceViewModel(snapshot);
            ConfigureCanonicalResourceActions(workspace);
            ClearAllEditorSelections();
            StoryWorkspace.CloseStory();
            SetCanonicalStoryWorkspace(workspace);
            Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
            ReplaceValidationSource(
                $"canonical/story/{storyId}",
                workspace.ValidationIssues.Concat(workspace.ActiveEditor.ValidationIssues));
            StatusMessage = $"已打开 Canonical Story：{storyId}";
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
                "打开 Canonical Story",
                exception,
                sourceOverride: $"canonical/story/{storyId}");
            return CanonicalOpenResult.Failed;
        }
    }

    private void ConfigureCanonicalResourceActions(CanonicalStoryWorkspaceViewModel workspace)
    {
        if (_projectService.CurrentProject?.Project is { } project)
            workspace.ConfigureProjectBreadcrumb(project.Id, project.DisplayName, ShowProjectHome);
        workspace.CreateResourceRequested = CreateCanonicalStoryResource;
        workspace.ReferenceResourceRequested = ReferenceCanonicalStoryResource;
        workspace.CreateActorRequested = CreateCanonicalStoryActor;
        workspace.ReferenceActorRequested = ReferenceCanonicalStoryActor;
        workspace.CreateItemRequested = CreateCanonicalStoryItem;
        workspace.ReferenceItemRequested = ReferenceCanonicalStoryItem;
        workspace.DeleteResourceRequested = DeleteCanonicalStoryResource;
        workspace.RenameResourceRequested = RenameCanonicalStoryResource;
        workspace.ResourceOrderChangeRequested = PersistCanonicalStoryResourceOrder;
        workspace.RefreshResourceCommandStates();
    }

    private bool PersistCanonicalStoryResourceOrder(
        CanonicalStoryFolderKind folderKind,
        IReadOnlyList<string> handles)
    {
        var workspace = CanonicalStoryWorkspace;
        var store = _canonicalGraphStore;
        if (workspace is null || store is null) return false;
        try
        {
            var membership = store.Memberships.Load(workspace.StoryEditor.Id);
            var order = membership.DisplayOrder;
            switch (folderKind)
            {
                case CanonicalStoryFolderKind.Actors:
                    order.Actors = [.. handles];
                    break;
                case CanonicalStoryFolderKind.Items:
                    order.Items = [.. handles];
                    break;
                case CanonicalStoryFolderKind.Sessions:
                    order.Sessions = [.. handles];
                    break;
                case CanonicalStoryFolderKind.Tasks:
                    order.Tasks = [.. handles];
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(folderKind), folderKind, null);
            }
            membership.SchemaVersion = CanonicalStoryMembershipManifest.CurrentSchemaVersion;
            membership.DisplayOrder = order;
            store.Memberships.Replace(membership);
            return true;
        }
        catch (Exception exception) when (exception is CanonicalStoryMembershipException
            or CanonicalStoryMembershipRepositoryException
            or IOException
            or UnauthorizedAccessException)
        {
            ReportFailure(
                "保存资源顺序",
                exception,
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
                    var next = _actorWorkspaceDialogs.RequestDisplayName(label, actor.Id, actor.DisplayName);
                    if (next is null || string.Equals(next.Trim(), actor.DisplayName, StringComparison.Ordinal)) return;
                    var document = project.Actors.LoadActor(actor.Id);
                    document.DisplayName = next.Trim();
                    project.Actors.SaveActor(document);
                    LoadActorList();
                    ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, CanonicalStoryFolderKind.Actors, actor.Id);
                    ReportSuccess($"Canonical {label} '{actor.Id}' 已重命名。", $"canonical/actor/{actor.Id}");
                    return;
                }
                case CanonicalStoryItemItem itemResource:
                {
                    var isIndividual = itemResource.Item is IndividualItemResource;
                    var label = isIndividual ? "物品" : "物品组";
                    var next = _itemWorkspaceDialogs.RequestDisplayName(label, itemResource.Id, itemResource.DisplayName);
                    if (next is null || string.Equals(next.Trim(), itemResource.DisplayName, StringComparison.Ordinal)) return;
                    var repository = new ItemRepository(project.ProjectDirectory);
                    if (isIndividual)
                    {
                        var original = repository.LoadItem(itemResource.Id);
                        repository.SaveItem(new IndividualItemResource
                        {
                            SchemaVersion = original.SchemaVersion,
                            ItemId = original.ItemId,
                            DisplayName = next.Trim(),
                            Tags = [.. original.Tags],
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
                            Tags = [.. original.Tags],
                        });
                    }
                    ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, CanonicalStoryFolderKind.Items, itemResource.Id);
                    ReportSuccess($"Canonical {label} '{itemResource.Id}' 已重命名。", $"canonical/item/{itemResource.Id}");
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
                "重命名 Canonical 资源",
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
        var next = _canonicalStoryResourceDialogs.RequestDisplayName(label, graph.Id, graph.DisplayName);
        if (next is null || string.Equals(next.Trim(), graph.DisplayName, StringComparison.Ordinal)) return;
        var repository = CanonicalRepository(store, graph.ResourceKind);
        var original = repository.Load(graph.Id);
        var renamed = new GraphResourceEnvelope(original.ResourceKind, original.Id, next.Trim(), original.Graph!)
        {
            SchemaVersion = original.SchemaVersion,
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
        if (!storyWasDirty) workspace.StoryEditor.MarkSaved();
        ReportSuccess(
            $"Canonical {label} '{graph.Id}' 已重命名，Story Flow 聚合显示已同步。",
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
            if (!_actorWorkspaceDialogs.SupportsCanonicalActorKinds)
            {
                var suggestedLegacyId = project.Actors.GetAvailableId("new_actor");
                var legacyRequest = _actorWorkspaceDialogs.RequestCreate(suggestedLegacyId);
                if (legacyRequest is null) return;
                var legacyCreated = new CanonicalStoryActorLifecycleService(store, project.Actors, project.Stories)
                    .CreateOwned(workspace.StoryEditor.Id, legacyRequest.Id, legacyRequest.DisplayName);
                LoadActorList();
                ReloadCanonicalStoryWorkspace(
                    workspace.StoryEditor.Id,
                    CanonicalStoryFolderKind.Actors,
                    legacyCreated.Id);
                ReportSuccess(
                    $"Canonical 角色 '{legacyCreated.Id}' 已创建。",
                    $"canonical/actor/{legacyCreated.Id}");
                return;
            }

            var kind = _actorWorkspaceDialogs.RequestCanonicalCreationKind(workspace.StoryEditor.DisplayName);
            if (kind is null) return;
            var suggestedId = project.Actors.GetAvailableId(
                kind == CanonicalStoryActorKind.Individual ? "new_npc" : "new_group");
            var request = _actorWorkspaceDialogs.RequestCreateCanonical(kind.Value, suggestedId);
            if (request is null) return;
            if (request.Kind != kind.Value)
                throw new InvalidOperationException("Actor creation dialog returned a different identity kind.");

            var created = new CanonicalStoryActorLifecycleService(store, project.Actors, project.Stories)
                .CreateOwned(workspace.StoryEditor.Id, request.Kind, request.Id, request.DisplayName, request.Tags);
            LoadActorList();
            ReloadCanonicalStoryWorkspace(
                workspace.StoryEditor.Id,
                CanonicalStoryFolderKind.Actors,
                created.Id);
            ReportSuccess(
                $"Canonical 角色 '{created.Id}' 已创建。",
                $"canonical/actor/{created.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                "创建 Canonical 角色",
                exception,
                sourceOverride: "canonical/actor");
        }
    }

    private void ReferenceCanonicalStoryActor()
    {
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

            new CanonicalStoryActorLifecycleService(store, project.Actors, project.Stories)
                .AddReference(workspace.StoryEditor.Id, choice.Id);
            ReloadCanonicalStoryWorkspace(
                workspace.StoryEditor.Id,
                CanonicalStoryFolderKind.Actors,
                choice.Id);
            ReportSuccess(
                $"已引用 Canonical 角色 '{choice.Id}'。",
                $"canonical/actor/{choice.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                "引用 Canonical 角色",
                exception,
                sourceOverride: "canonical/actor");
        }
    }

    private void CreateCanonicalStoryResource(GraphResourceKind resourceKind)
    {
        var workspace = CanonicalStoryWorkspace;
        var store = _canonicalGraphStore;
        if (workspace is null || store is null) return;
        try
        {
            var repository = CanonicalRepository(store, resourceKind);
            var suggestedId = repository.GetAvailableId(
                resourceKind == GraphResourceKind.Session ? "new_session" : "new_task");
            var request = _canonicalStoryResourceDialogs.RequestCreate(resourceKind, suggestedId);
            if (request is null) return;

            var created = new CanonicalStoryResourceLifecycleService(store).CreateOwned(
                workspace.StoryEditor.Id,
                resourceKind,
                request.Id,
                request.DisplayName);
            ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, resourceKind, created.Id);
            ReportSuccess(
                $"Canonical {CanonicalKindLabel(resourceKind)} '{created.Id}' 已创建。",
                $"canonical/{resourceKind}/{created.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                $"创建 Canonical {CanonicalKindLabel(resourceKind)}",
                exception,
                sourceOverride: $"canonical/{resourceKind}");
        }
    }

    private void ReferenceCanonicalStoryResource(GraphResourceKind resourceKind)
    {
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
                $"已引用 Canonical {CanonicalKindLabel(resourceKind)} '{choice.Id}'。",
                $"canonical/{resourceKind}/{choice.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                $"引用 Canonical {CanonicalKindLabel(resourceKind)}",
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
                ? repository.GetAvailableItemId("new_item")
                : repository.GetAvailableGroupId("new_group");
            var request = _itemWorkspaceDialogs.RequestCreate(kind, suggestedId);
            if (request is null) return;
            if (request.Kind != kind)
                throw new InvalidOperationException("Item creation dialog returned a different resource kind.");

            var created = new CanonicalStoryItemLifecycleService(store, repository).CreateOwned(
                workspace.StoryEditor.Id,
                kind,
                request.Id,
                request.DisplayName,
                request.Tags);
            ReloadCanonicalStoryWorkspace(workspace.StoryEditor.Id, CanonicalStoryFolderKind.Items, created.Id);
            ReportSuccess(
                $"Canonical {(kind == CanonicalStoryItemKind.Individual ? "物品" : "物品组")} '{created.Id}' 已创建。",
                $"canonical/{(kind == CanonicalStoryItemKind.Individual ? "item" : "item_group")}/{created.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure("创建 Canonical 物品", exception, sourceOverride: "canonical/item");
        }
    }

    private void ReferenceCanonicalStoryItem()
    {
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
                $"已引用 Canonical {(choice.Kind == CanonicalStoryItemKind.Individual ? "物品" : "物品组")} '{choice.Id}'。",
                $"canonical/{(choice.Kind == CanonicalStoryItemKind.Individual ? "item" : "item_group")}/{choice.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure("引用 Canonical 物品", exception, sourceOverride: "canonical/item");
        }
    }

    private void DeleteCanonicalStoryResource(ICanonicalStoryTreeItem item)
    {
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
            if (isReferenced)
            {
                if (!_canonicalStoryResourceDialogs.ConfirmRemoveReference(choice, storyDisplayName)) return;
                service.RemoveReference(storyId, choice.ResourceKind, choice.Id);
                ReloadCanonicalStoryWorkspace(storyId, choice.ResourceKind);
                ReportSuccess(
                    $"已解除 Canonical {CanonicalKindLabel(choice.ResourceKind)} '{choice.Id}' 的引用。",
                    $"canonical/{choice.ResourceKind}/{choice.Id}");
                return;
            }

            if (isMissing)
            {
                ReportWarning(
                    $"拥有的 Canonical {CanonicalKindLabel(choice.ResourceKind)} '{choice.Id}' 文件缺失，无法执行安全删除。",
                    $"canonical/{choice.ResourceKind}/{choice.Id}");
                return;
            }

            var plan = service.GetDeletionPlan(storyId, choice.ResourceKind, choice.Id);
            if (!plan.CanDelete)
            {
                _canonicalStoryResourceDialogs.ShowDeleteBlocked(choice, plan.ReferencingStoryIds);
                ReportWarning(
                    $"Canonical {CanonicalKindLabel(choice.ResourceKind)} '{choice.Id}' 仍被其它 Story 引用，未删除。",
                    $"canonical/{choice.ResourceKind}/{choice.Id}");
                return;
            }
            if (!_canonicalStoryResourceDialogs.ConfirmDeleteOwned(choice)) return;

            service.DeleteOwned(storyId, choice.ResourceKind, choice.Id);
            ReloadCanonicalStoryWorkspace(storyId, choice.ResourceKind);
            ReportSuccess(
                $"Canonical {CanonicalKindLabel(choice.ResourceKind)} '{choice.Id}' 已删除。",
                $"canonical/{choice.ResourceKind}/{choice.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                $"更新 Canonical {CanonicalKindLabel(choice.ResourceKind)}",
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
                ReportSuccess($"已解除 Canonical {label} '{choice.Id}' 的引用；文件未删除。", $"canonical/item/{choice.Id}");
                return;
            }
            if (isMissing)
            {
                ReportWarning($"拥有的 Canonical {label} '{choice.Id}' 文件缺失，无法执行安全删除。", $"canonical/item/{choice.Id}");
                return;
            }

            var plan = service.GetDeletionPlan(storyId, choice.Kind, choice.Id);
            if (!plan.CanDelete)
            {
                _itemWorkspaceDialogs.ShowDeleteBlocked(choice, plan.ReferencingStoryIds);
                ReportWarning($"Canonical {label} '{choice.Id}' 仍被其它 Story 使用，未删除。", $"canonical/item/{choice.Id}");
                return;
            }
            if (!_itemWorkspaceDialogs.ConfirmDeleteOwned(choice)) return;
            service.DeleteOwned(storyId, choice.Kind, choice.Id);
            ReloadCanonicalStoryWorkspace(storyId, CanonicalStoryFolderKind.Items);
            ReportSuccess($"Canonical {label} '{choice.Id}' 已删除。", $"canonical/item/{choice.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure($"更新 Canonical {label}", exception, sourceOverride: $"canonical/item/{choice.Id}");
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
        var service = new CanonicalStoryActorLifecycleService(store, project.Actors, project.Stories);
        try
        {
            if (isReferenced)
            {
                if (!_actorWorkspaceDialogs.ConfirmRemoveReference(actor, workspace.StoryEditor.DisplayName)) return;
                service.RemoveReference(storyId, actor.Id);
                ReloadCanonicalStoryWorkspace(storyId, CanonicalStoryFolderKind.Actors);
                ReportSuccess(
                    $"已解除 Canonical 角色 '{actor.Id}' 的引用；Actor 文件未删除。",
                    $"canonical/actor/{actor.Id}");
                return;
            }

            if (isMissing)
            {
                ReportWarning(
                    $"拥有的 Canonical 角色 '{actor.Id}' 文件缺失，无法执行安全删除。",
                    $"canonical/actor/{actor.Id}");
                return;
            }

            var plan = service.GetDeletionPlan(storyId, actor.Id);
            if (!plan.CanDelete)
            {
                _actorWorkspaceDialogs.ShowReferences(
                    actor,
                    DescribeCanonicalActorBlockers(plan, store, project.Stories));
                ReportWarning(
                    $"Canonical 角色 '{actor.Id}' 仍被其它 canonical 或旧版 Story 占用/引用，未删除。",
                    $"canonical/actor/{actor.Id}");
                return;
            }
            if (!_actorWorkspaceDialogs.ConfirmDelete(actor)) return;

            _projectService.ReleaseOpenActor(actor.Id);
            service.DeleteOwned(storyId, actor.Id);
            LoadActorList();
            ReloadCanonicalStoryWorkspace(storyId, CanonicalStoryFolderKind.Actors);
            ReportSuccess(
                $"Canonical 角色 '{actor.Id}' 已从 actors/ 删除。",
                $"canonical/actor/{actor.Id}");
        }
        catch (Exception exception) when (IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                "更新 Canonical 角色",
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
        ReplaceValidationSource(
            $"canonical/story/{storyId}",
            workspace.ValidationIssues.Concat(workspace.ActiveEditor.ValidationIssues));
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

    private static IReadOnlyList<ResourceDescriptor> DescribeCanonicalActorBlockers(
        CanonicalStoryActorDeletionPlan plan,
        CanonicalProjectGraphStore store,
        StoryRepository legacyStories)
        => plan.Blockers.Select(blocker =>
        {
            var sourceLabel = blocker.IsCanonical ? "Canonical" : "旧版";
            var membershipLabel = blocker.IsOwned ? "占用" : "引用";
            string displayName;
            string path;
            if (blocker.IsCanonical)
            {
                try { displayName = store.Stories.Load(blocker.StoryId).DisplayName; }
                catch (GraphResourceRepositoryException) { displayName = blocker.StoryId; }
                path = store.Memberships.GetPath(blocker.StoryId);
            }
            else
            {
                try { displayName = legacyStories.LoadStory(blocker.StoryId).DisplayName; }
                catch (StoryNotFoundException) { displayName = blocker.StoryId; }
                path = Path.Combine(legacyStories.StoriesDirectory, blocker.StoryId + ".json");
            }

            return new ResourceDescriptor(
                ProjectResourceType.Story,
                blocker.StoryId,
                $"[{sourceLabel} {membershipLabel}] {displayName}",
                path);
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

    private void SetCanonicalStoryWorkspace(CanonicalStoryWorkspaceViewModel? workspace)
    {
        if (ReferenceEquals(_canonicalStoryWorkspace, workspace)) return;
        if (_canonicalStoryWorkspace is not null)
        {
            _canonicalStoryWorkspace.PropertyChanged -= OnCanonicalStoryWorkspacePropertyChanged;
            Problems.RemoveSourceTree($"canonical/story/{_canonicalStoryWorkspace.StoryEditor.Id}");
            _canonicalStoryWorkspace.Dispose();
        }
        _canonicalStoryWorkspace = workspace;
        if (_canonicalStoryWorkspace is not null)
            _canonicalStoryWorkspace.PropertyChanged += OnCanonicalStoryWorkspacePropertyChanged;
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
        StatusMessage = $"已返回 Canonical Story：{CanonicalStoryWorkspace.StoryEditor.Id}";
        Output.Append(StatusMessage, source: $"canonical/story/{CanonicalStoryWorkspace.StoryEditor.Id}");
    }

    private void OnCanonicalStoryWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(CanonicalStoryWorkspaceViewModel.ActiveEditor)
            or nameof(CanonicalStoryWorkspaceViewModel.HasDirtyEditors))
            RaiseCurrentEditorStates();
    }

    private void CreateStory()
    {
        var project = _projectService.CurrentProject;
        if (project is null || _canonicalGraphStore is null) return;

        try
        {
            var request = _resourceWorkspaceDialogs.RequestCreate(
                ProjectResourceType.Story,
                GetAvailableStoryId("new_story"));
            if (request is null) return;

            var story = new CanonicalStoryLifecycleService(
                    _canonicalGraphStore,
                    project.Actors,
                    project.Stories)
                .Create(request.Id, request.DisplayName);
            LoadStoryList();
            ProjectHome.SelectedStory = ProjectHome.Stories.Single(item => item.Id == story.Id);
            ProjectHome.ShowHome();
            StatusMessage = $"Canonical 故事 '{story.Id}' 已创建。";
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
        var selected = ProjectHome.SelectedStory;
        if (selected is null || _projectService.CurrentProject is null) return;

        if (selected.HasCanonicalStory)
        {
            DeleteSelectedCanonicalStory(selected);
            return;
        }
        if (!selected.CanDeleteLegacyStory) return;

        try
        {
            var plan = _projectService.GetStoryDeletionPlan(selected.Id);
            if (plan.Blockers.Count > 0)
            {
                ReportWarning(
                    $"无法删除故事 '{selected.Id}'：{string.Join("；", plan.Blockers)}",
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
            StatusMessage = $"故事 '{selected.Id}' 已从项目中删除。";
            Output.Append(StatusMessage, OutputKind.Success, $"story/{selected.Id}");
            Toast.Show(StatusMessage, ToastKind.Success);
        }
        catch (Exception exception) when (IsWorkspaceException(exception))
        {
            ReportFailure("删除故事", exception, selected.Id);
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
        if (CanonicalStoryWorkspace?.StoryEditor.Id == storyId)
        {
            CanonicalStoryWorkspace.ReturnToStory();
            StatusMessage = $"已从故事图谱打开 Canonical Story Flow：{storyId}";
            Output.Append(StatusMessage, source: $"canonical/story/{storyId}");
            return;
        }
        if (!StoryWorkspace.HasStory || StoryWorkspace.StoryId != storyId) return;
        StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Flow);
        StatusMessage = $"已从故事图谱打开 Story Flow：{storyId}";
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
        if (CanonicalStoryWorkspace?.StoryEditor.Id == storyId)
        {
            if (!CanonicalStoryWorkspace.RequestStoryNodeFocus(nodeId, field))
                ReportWarning($"Canonical Story Flow '{storyId}' 中不存在唯一节点 '{nodeId}'。", $"canonical/story/{storyId}/flow/{nodeId}");
            return;
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
        if (CanonicalStoryWorkspace is { } canonicalWorkspace)
        {
            SetCanonicalStoryWorkspaceVisible(false);
            ProjectHome.SelectedStory = ProjectHome.Stories.FirstOrDefault(story =>
                story.Id == canonicalWorkspace.StoryEditor.Id);
            ProjectHome.ShowHome();
            Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
            return;
        }
        if (!TryLeaveCurrentEditor()) return;
        var storyId = StoryWorkspace.StoryId;
        var canonicalStoryId = CanonicalStoryWorkspace?.StoryEditor.Id;
        ClearAllEditorSelections();
        SetCanonicalStoryWorkspace(null);
        StoryWorkspace.CloseStory();
        LoadStoryList();
        ProjectHome.SelectedStory = ProjectHome.Stories.FirstOrDefault(story =>
            story.Id == (canonicalStoryId ?? storyId));
        ProjectHome.ShowHome();
        Navigation.SelectedItem = Navigation.Items.Single(item => item.Page == "Story");
    }

    private void ShowProjectGraph()
    {
        if (!TryLeaveCurrentEditor()) return;
        ClearAllEditorSelections();
        SetCanonicalStoryWorkspace(null);
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
        if (CanonicalStoryWorkspace?.HasDirtyEditors == true)
        {
            ReportWarning("Canonical Story 仍有未保存的图；请逐个保存后再离开。", "canonical/story");
            return false;
        }
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
            OnPropertyChanged(nameof(SelectedStoryResourceActionText));
            ReportSuccess($"Dialogue 草稿 '{id}' 已放弃。", $"dialogue/{id}");
            RaiseWorkspaceCommandStates();
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
            OnPropertyChanged(nameof(SelectedStoryResourceActionText));
            ReportSuccess($"Quest 草稿 '{id}' 已放弃。", $"quest/{id}");
            RaiseWorkspaceCommandStates();
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
        if (CanonicalStoryWorkspace is not null)
        {
            TrySaveCanonicalResource();
            return;
        }
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

    private bool TrySaveCanonicalResource()
    {
        var workspace = CanonicalStoryWorkspace;
        var coordinator = _canonicalSaveCoordinator;
        if (workspace is null || coordinator is null) return false;
        var editor = workspace.ActiveEditor;
        if (!editor.IsDirty) return true;
        CanonicalAggregateSynchronizationPlan? synchronization = null;
        var synchronizationCommitted = false;
        if (editor.ResourceKind is GraphResourceKind.Session or GraphResourceKind.Task)
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
                    $"已取消保存 Canonical {editor.ResourceKind} '{editor.Id}'；Story Flow 外部连线未更改。",
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
                $"Canonical {editor.ResourceKind} '{editor.Id}' 已保存到磁盘{synchronizationSummary}。",
                $"canonical/{editor.ResourceKind}/{editor.Id}");
            RaiseCurrentEditorStates();
            return true;
        }
        catch (GraphResourceRepositoryException exception)
        {
            if (synchronizationCommitted && !workspace.RollbackLastStoryEdit())
            {
                ReportWarning(
                    $"Canonical {editor.ResourceKind} '{editor.Id}' 写入失败，且 Story Flow 同步回滚失败；请勿继续保存并检查 Problems。",
                    $"canonical/{editor.ResourceKind}/{editor.Id}");
            }
            ReportFailure(
                "保存 Canonical 图资源",
                exception,
                sourceOverride: $"canonical/{editor.ResourceKind}/{editor.Id}");
            RaiseCurrentEditorStates();
            return false;
        }
    }

    private string GetAvailableStoryId(string baseId)
    {
        var usedIds = ProjectHome.Stories.Select(story => story.Id).ToHashSet(StringComparer.Ordinal);
        if (!usedIds.Contains(baseId)) return baseId;
        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseId}_{suffix}";
            if (!usedIds.Contains(candidate)) return candidate;
        }
    }

    private void DeleteSelectedCanonicalStory(StoryListItemViewModel selected)
    {
        var project = _projectService.CurrentProject;
        if (project is null || _canonicalGraphStore is null) return;

        try
        {
            if (CanonicalStoryWorkspace is { HasDirtyEditors: true } workspace
                && string.Equals(workspace.StoryEditor.Id, selected.Id, StringComparison.Ordinal))
            {
                ReportWarning(
                    $"无法删除 Canonical 故事 '{selected.Id}'：请先保存或放弃未保存的编辑。",
                    $"canonical/story/{selected.Id}");
                return;
            }

            var service = new CanonicalStoryLifecycleService(
                _canonicalGraphStore,
                project.Actors,
                project.Stories);
            var plan = service.GetDeletionPlan(selected.Id);
            if (!plan.CanDelete)
            {
                ReportWarning(
                    $"无法删除 Canonical 故事 '{selected.Id}'：{string.Join("；", plan.Blockers.Select(blocker => blocker.Message))}",
                    $"canonical/story/{selected.Id}");
                return;
            }

            var resourcesToDelete = plan.ActorIds.Select(id => $"角色：{id}")
                .Concat(plan.SessionIds.Select(id => $"会话：{id}"))
                .Concat(plan.TaskIds.Select(id => $"任务：{id}"))
                .ToArray();
            if (!_projectWorkspaceDialogs.ConfirmDeleteCanonicalStory(
                    selected.Id,
                    selected.DisplayName,
                    resourcesToDelete))
                return;

            foreach (var actorId in plan.ActorIds)
                _projectService.ReleaseOpenActor(actorId);

            service.Delete(selected.Id);
            if (string.Equals(CanonicalStoryWorkspace?.StoryEditor.Id, selected.Id, StringComparison.Ordinal))
                SetCanonicalStoryWorkspace(null);
            LoadActorList();
            LoadStoryList();
            ProjectHome.SelectedStory = ProjectHome.Stories.FirstOrDefault(story => story.Id == selected.Id);
            ProjectHome.ShowHome();
            ReportSuccess(
                $"Canonical 故事 '{selected.Id}' 已从项目中删除。",
                $"canonical/story/{selected.Id}");
        }
        catch (Exception exception) when (
            IsWorkspaceException(exception)
            || IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure(
                "删除 Canonical 故事",
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
            var document = SaveOpenActor(CurrentActor.Document);
            var selectedId = document.Id;
            RefreshCurrentStory(selectedId);
            ReportSuccess($"Actor '{selectedId}' 已保存到磁盘。", $"actor/{selectedId}");
        }
        catch (Exception exception) when (
            IsWorkspaceException(exception) || IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure("保存 Actor", exception, CurrentActor?.Id);
        }
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

    private bool TrySaveCurrentActor()
    {
        if (CurrentActor is null)
        {
            return true;
        }

        try
        {
            SaveOpenActor(CurrentActor.Document);
            ReportSuccess($"Actor '{CurrentActor.Id}' 已保存到磁盘。", $"actor/{CurrentActor.Id}");
            return true;
        }
        catch (Exception exception) when (
            IsWorkspaceException(exception) || IsCanonicalResourceLifecycleException(exception))
        {
            ReportFailure("保存 Actor", exception, CurrentActor?.Id);
            return false;
        }
    }

    private void SaveAll()
    {
        if (CanonicalStoryWorkspace is not null)
        {
            ReportWarning("Canonical Story 暂不提供跨文件 Save All；请逐个保存当前图。", "canonical/story");
            return;
        }
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
        HasProject && CanonicalStoryWorkspace is null && HasUnsavedDocuments();

    private bool HasUnsavedDocuments() =>
        _projectService.OpenActorDocuments.Any(document => document.IsDirty) ||
        _projectService.OpenDialogueDocuments.Any(document => document.IsDirty) ||
        _projectService.OpenQuestDocuments.Any(document => document.IsDirty) ||
        CurrentFlow?.IsDirty == true ||
        CanonicalStoryWorkspace?.HasDirtyEditors == true;

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
        if (HasUnsavedDocuments())
        {
            ReportWarning("请先保存当前故事及其资源，再导出故事包。", $"story/{story.Id}");
            return;
        }

        try
        {
            var output = Path.Combine(project.ProjectDirectory, "build", "story_packages", story.Id);
            var result = new StoryPackageExporter(project.ProjectDirectory)
                .Build(story.Id, output, "0.3.1.0");
            _lastUiCommand = nameof(ExportSelectedStoryPackage);
            OnPropertyChanged(nameof(LastUiCommand));
            ReportSuccess($"故事包已导出：{result.PackageDirectory}", $"story/{story.Id}");
        }
        catch (Exception exception) when (IsWorkspaceException(exception) || exception is StoryPackageException)
        {
            ReportFailure("导出故事包", exception, story.Id);
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
                $"已引用 Actor '{selected.Id}'；多个故事将共享同一份角色数据。",
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
                    $"Actor '{deletedId}' 仍被 {references.Count} 个故事引用；请先解除引用。",
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
            ReportSuccess($"已引用 {type.Value} '{selected.Id}'；多个故事共享同一份数据。", $"{type.Value.ToString().ToLowerInvariant()}/{selected.Id}");
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
            ReportSuccess($"已解除 {type.Value} '{membership.Id}' 的故事引用；资源文件未删除。", $"{type.Value.ToString().ToLowerInvariant()}/{membership.Id}");
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
        if (membership is null) return;
        if (membership.IsDraft)
        {
            var draft = new ResourceDescriptor(
                membership.ResourceType,
                membership.Id,
                membership.DisplayName,
                string.Empty);
            if (!_resourceWorkspaceDialogs.ConfirmDiscardDraft(draft)) return;
            if (membership.ResourceType == ProjectResourceType.Dialogue)
                DiscardCurrentDialogueDraft();
            else
                DiscardCurrentQuestDraft();
            return;
        }
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
                ReportWarning($"{descriptor.Type} '{descriptor.Id}' 仍被 {references.Count} 个故事引用；请先解除引用。", $"{descriptor.Type.ToString().ToLowerInvariant()}/{descriptor.Id}");
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
        SelectedStoryResource?.IsDraft == true ||
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
            or StoryRepositoryException or StoryNotFoundException or StoryDataException or StoryValidationException
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
        if (args.PropertyName == nameof(ProjectHomeViewModel.SelectedStory))
        {
            OpenSelectedStoryCommand.RaiseCanExecuteChanged();
            DeleteSelectedStoryCommand.RaiseCanExecuteChanged();
            ExportSelectedStoryPackageCommand.RaiseCanExecuteChanged();
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
        MigrateCanonicalProjectCommand.RaiseCanExecuteChanged();
        ExportSelectedStoryPackageCommand.RaiseCanExecuteChanged();
    }

    private void RefreshProblems()
    {
        if (CanonicalStoryWorkspace is { } canonical)
        {
            ReplaceValidationSource(
                $"canonical/story/{canonical.StoryEditor.Id}",
                canonical.ValidationIssues.Concat(canonical.ActiveEditor.ValidationIssues));
            return;
        }
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
            UpdateCanonicalStoryDiscoveryProblems();
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
                issue.Code is "project_graph.target.missing"
                    or "project_graph.target.invalid"
                    or "project_graph.enter_story.malformed"
                    or "project_graph.enter_story.ambiguous"
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
        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete) => false;
    }

    private sealed class NullResourceWorkspaceDialogs : IResourceWorkspaceDialogs
    {
        public ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string storyDisplayName) => null;
        public ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId) => null;
        public ResourceIdentityRequest? RequestImportIdentity(ProjectResourceType type, ResourceDescriptor source, string suggestedId) => null;
        public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates, ResourcePickerMode mode, string storyDisplayName) => null;
        public bool ConfirmDelete(ResourceDescriptor resource) => false;
        public bool ConfirmDiscardDraft(ResourceDescriptor resource) => false;
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
