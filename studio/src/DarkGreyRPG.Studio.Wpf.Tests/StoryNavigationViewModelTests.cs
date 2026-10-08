using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Views.Graph;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Core.Actors;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class StoryNavigationViewModelTests
{
    [TestMethod]
    public void GroupMembersReorderWithoutChangingTopLevelMembershipOrConnections()
    {
        using var f = new CurrentNavigationFixture();
        const string member = "ST-JKLM-NPQR-STUV-WXYZ", single = "ST-AAAA-BBBB-CCCC-DDDD";
        var lifecycle = new CanonicalStoryLifecycleService(f.Store);
        lifecycle.Create(member, "ZZ Member"); lifecycle.Create(single, "Single");
        AddBoundary(f.Store, CurrentNavigationFixture.Owner, "logic_output");
        AddBoundary(f.Store, member, "logic_input");
        f.Store.StoryLogicGraph.Save([new(CurrentNavigationFixture.Owner, "boundary", member, "boundary")]);
        f.Shell.OpenProjectCommand.Execute(null);
        var graph = f.Shell.ProjectHome.Graph;
        var group = graph.StoryGroups.Groups.Single();
        var connections = File.ReadAllBytes(f.Store.StoryLogicGraph.Path);
        string[] Members() => f.Shell.ProjectHome.GroupedStories.Cast<StoryListItemViewModel>().Where(story => story.NavigationGroup?.Key == group.Key).Select(story => story.Id).ToArray();
        string[] Top() => f.Shell.ProjectHome.GroupedStories.Cast<StoryListItemViewModel>().Select(story => story.NavigationKey).Distinct().ToArray();
        var original = Members(); var top = Top();
        graph.MoveNavigationEntry(original[1], original[0]);
        CollectionAssert.AreEqual(original.Reverse().ToArray(), Members());
        CollectionAssert.AreEqual(top, Top());
        CollectionAssert.AreEqual(connections, File.ReadAllBytes(f.Store.StoryLogicGraph.Path));
        f.Shell.UndoCurrentCommand.Execute(null);
        CollectionAssert.AreEqual(original, Members());
        f.Shell.RedoCurrentCommand.Execute(null);
        CollectionAssert.AreEqual(original.Reverse().ToArray(), Members());
        var memberTokens = graph.Presentation.NavigationOrder.Where(group.Members.Contains).ToArray();
        graph.MoveNavigationEntry(group.Key, single, top[0] == group.Key);
        CollectionAssert.AreEqual(memberTokens, graph.Presentation.NavigationOrder.Where(group.Members.Contains).ToArray());
        var beforeInvalid = graph.Presentation.NavigationOrder.ToArray();
        graph.MoveNavigationEntry(member, single);
        CollectionAssert.AreEqual(beforeInvalid, graph.Presentation.NavigationOrder);
        f.Shell.OpenProjectCommand.Execute(null);
        CollectionAssert.AreEqual(original.Reverse().ToArray(), Members());
        CollectionAssert.AreEqual(connections, File.ReadAllBytes(f.Store.StoryLogicGraph.Path));
    }

    [STATestMethod]
    public void NavigationLandingUsesRealGroupedContainersWithInheritedDataContext()
    {
        using var f = new CurrentNavigationFixture();
        const string member = "ST-JKLM-NPQR-STUV-WXYZ", single = "ST-AAAA-BBBB-CCCC-DDDD";
        var lifecycle = new CanonicalStoryLifecycleService(f.Store);
        lifecycle.Create(member, "Member"); lifecycle.Create(single, "Single");
        AddBoundary(f.Store, CurrentNavigationFixture.Owner, "logic_output");
        AddBoundary(f.Store, member, "logic_input");
        f.Store.StoryLogicGraph.Save([new(CurrentNavigationFixture.Owner, "boundary", member, "boundary")]);
        f.Shell.OpenProjectCommand.Execute(null);
        var list = new DarkGreyRPG.Studio.Views.StoryNavigationList
        { ItemsSource = f.Shell.ProjectHome.GroupedStories, DataContext = f.Shell, Width = 260, Height = 320 };
        list.Template = (System.Windows.Controls.ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ListBox">
              <ScrollViewer><ItemsPresenter/></ScrollViewer>
            </ControlTemplate>
            """);
        list.GroupStyle.Add(new System.Windows.Controls.GroupStyle());
        var window = new System.Windows.Window { Content = list, ShowActivated = false, ShowInTaskbar = false, Left = -10000 };
        try
        {
            window.Show(); list.UpdateLayout();
            var containers = Containers(list).ToArray();
            var target = containers.FirstOrDefault(row => (row.Content as System.Windows.Data.CollectionViewGroup)?.Name is StoryListItemViewModel story && story.Id == single);
            Assert.IsNotNull(target, $"Containers: {string.Join(';', containers.Select(row => $"{row.Content?.GetType().Name}:{row.DataContext?.GetType().Name}"))}; stories={list.Items.Count}");
            target.DataContext = f.Shell;
            Assert.AreSame(f.Shell, target.DataContext);
            var point = target.TranslatePoint(new System.Windows.Point(8, target.ActualHeight / 4), list);
            var landing = DarkGreyRPG.Studio.Views.StoryNavigationDrag.Locate(list, f.Shell.ProjectHome.Graph.StoryGroups.Groups.Single().Key, point);
            Assert.IsNotNull(landing);
            Assert.AreEqual(f.Shell.ProjectHome.Stories.Single(story => story.Id == single).NavigationKey, landing.Key);
        }
        finally { window.Close(); }
        static IEnumerable<System.Windows.Controls.GroupItem> Containers(System.Windows.DependencyObject root)
        {
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is System.Windows.Controls.GroupItem row) yield return row;
                foreach (var descendant in Containers(child)) yield return descendant;
            }
        }
    }

    [TestMethod]
    public void MixedStoryGroupDeletionUnlinksWholeReferenceAndPreservesOriginalPackage()
    {
        using var f = new CurrentNavigationFixture();
        const string external = "ST-JKLM-NPQR-STUV-WXYZ";
        var source = Path.Combine(f.Root, "source-fixture");
        new ProjectService().CreateProject(source, "source", "Source");
        var sourceStore = new CanonicalProjectGraphStore(source);
        new CanonicalStoryLifecycleService(sourceStore).Create(external, "External");
        AddBoundary(sourceStore, external, "logic_input");
        AddBoundary(f.Store, CurrentNavigationFixture.Owner, "logic_output");
        var original = Path.Combine(source, "original.dgrs");
        new DgrsStoryPackageExporter(source).Build(external, original, "0.3.3.6");
        var originalBytes = File.ReadAllBytes(original);
        var installed = Path.Combine(f.Root, "references", "external.dgrs");
        Directory.CreateDirectory(Path.GetDirectoryName(installed)!); File.Copy(original, installed);
        f.Store.StoryLogicGraph.Save([new(CurrentNavigationFixture.Owner, "boundary", external, "boundary")]);
        f.Shell.OpenProjectCommand.Execute(null); f.Dialogs.AllowGroupDeletion = true;
        Assert.IsTrue(f.Shell.ProjectHome.Graph.DeleteStoryGroupsRequested!([f.Shell.ProjectHome.Graph.StoryGroups.Groups.Single().Key]));
        Assert.IsFalse(File.Exists(installed)); Assert.IsEmpty(f.Store.Stories.List());
        CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(original));
        f.Shell.UndoCurrentCommand.Execute(null);
        CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(installed));
        Assert.HasCount(1, f.Store.Stories.List()); Assert.HasCount(1, f.Store.StoryLogicGraph.Load().Connections);
    }

    [TestMethod]
    public void OutsideResourceDependencyBlocksGroupDeletionWithoutWritingAnyMember()
    {
        using var f = new CurrentNavigationFixture();
        const string other = "ST-JKLM-NPQR-STUV-WXYZ", outsider = "ST-AAAA-BBBB-CCCC-DDDD";
        var lifecycle = new CanonicalStoryLifecycleService(f.Store);
        lifecycle.Create(other, "Member"); lifecycle.Create(outsider, "Outside");
        AddBoundary(f.Store, CurrentNavigationFixture.Owner, "logic_output"); AddBoundary(f.Store, other, "logic_input");
        f.Store.StoryLogicGraph.Save([new(CurrentNavigationFixture.Owner, "boundary", other, "boundary")]);
        new CanonicalStoryActorLifecycleService(f.Store).AddReference(outsider, CurrentNavigationFixture.Owner + "~actor~hero");
        f.Shell.OpenProjectCommand.Execute(null); f.Dialogs.AllowGroupDeletion = true;
        var paths = new[] { f.Store.Stories.GetPath(CurrentNavigationFixture.Owner), f.Store.Stories.GetPath(other), f.Store.StoryLogicGraph.Path };
        var before = paths.Select(File.ReadAllBytes).ToArray();
        Assert.IsFalse(f.Shell.ProjectHome.Graph.DeleteStoryGroupsRequested!([f.Shell.ProjectHome.Graph.StoryGroups.Groups.Single().Key]));
        for (var i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]));
    }

    private static void AddBoundary(CanonicalProjectGraphStore store, string id, string type)
    {
        var resource = store.Stories.Load(id); var graph = resource.Graph!;
        var node = new GraphNodeAuthoringService().Create(graph, GraphScope.StoryFlow, type, "boundary").Candidate!;
        node.Properties["port_id"] = JsonSerializer.SerializeToElement("boundary");
        graph.Nodes.Add(node); resource.Graph = graph; store.Stories.Replace(resource);
    }

    [TestMethod]
    public void StoryGroupDeletionRestoresAllOwnedFilesAndConnectionsWithUndoRedo()
    {
        using var f = new CurrentNavigationFixture();
        const string other = "ST-JKLM-NPQR-STUV-WXYZ";
        new CanonicalStoryLifecycleService(f.Store).Create(other, "Other");
        foreach (var (id, type) in new[] { (CurrentNavigationFixture.Owner, "logic_output"), (other, "logic_input") })
        {
            var resource = f.Store.Stories.Load(id); var graph = resource.Graph!;
            var node = new GraphNodeAuthoringService().Create(graph, GraphScope.StoryFlow, type, "boundary").Candidate!;
            node.Properties["port_id"] = JsonSerializer.SerializeToElement("boundary");
            graph.Nodes.Add(node); resource.Graph = graph; f.Store.Stories.Replace(resource);
        }
        f.Store.StoryLogicGraph.Save([new(CurrentNavigationFixture.Owner, "boundary", other, "boundary")]);
        f.Shell.OpenProjectCommand.Execute(null);
        var files = Directory.GetFiles(f.Root, "*.json", SearchOption.AllDirectories)
            .Where(path => path.Contains("resources")) .ToDictionary(path => path, File.ReadAllBytes);
        f.Dialogs.AllowGroupDeletion = true;
        var key = f.Shell.ProjectHome.Graph.StoryGroups.Groups.Single().Key;
        Assert.IsTrue(f.Shell.ProjectHome.Graph.DeleteStoryGroupsRequested!([key]));
        Assert.IsEmpty(f.Store.Stories.List());
        f.Shell.UndoCurrentCommand.Execute(null);
        foreach (var pair in files) CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key));
        f.Shell.RedoCurrentCommand.Execute(null);
        Assert.IsEmpty(f.Store.Stories.List());
        f.Shell.UndoCurrentCommand.Execute(null);
        Assert.HasCount(2, f.Store.Stories.List());
    }

    [TestMethod]
    public void AggregateOutputsUseGlobalUndoRedoAcrossResourceAndStoryViews()
    {
        using var f = new CurrentNavigationFixture();
        var id = CurrentNavigationFixture.Owner + "~session~history";
        var resource = new CanonicalStoryResourceLifecycleService(f.Store).CreateOwned(CurrentNavigationFixture.Owner,
            GraphResourceKind.Session, id, "Session");
        var author = new GraphNodeAuthoringService();
        var sessionGraph = resource.Graph!;
        for (var i = 0; i < 2; i++) sessionGraph.Nodes.Add(author.Create(sessionGraph, GraphScope.Session, "end", "end" + i).Candidate!);
        resource.Graph = sessionGraph;
        f.Store.Sessions.Replace(resource);
        f.Shell.OpenProjectCommand.Execute(null); f.Open();
        var workspace = f.Shell.CanonicalStoryWorkspace!;
        var session = workspace.SessionItems.Single();
        var aggregate = CanonicalAggregateNodeFactory.Create(workspace.StoryEditor.Document.Graph,
            session.Editor.CreatePersistenceSnapshot(), "aggregate").Candidate!;
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(aggregate));
        var terminal = author.Create(workspace.StoryEditor.Document.Graph, GraphScope.StoryFlow, "terminate", "terminal").Candidate!;
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(terminal));
        var connectedPort = aggregate.Ports.First(port => !port.IsInput && port.Kind == GraphInterfaceKind.Flow).Id;
        var connection = new GraphConnection("aggregate", connectedPort, "terminal", "flow_in", GraphInterfaceKind.Flow);
        Assert.IsTrue(workspace.StoryEditor.Host.Connect(
            GraphEditorEndpoint.Output("aggregate", connectedPort, GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("terminal", "flow_in", GraphInterfaceKind.Flow)));
        using var inspector = new CanonicalNodeInspectorViewModel(workspace.StoryEditor.Host,
            workspace.StoryEditor.Host.Nodes.Single(node => node.NodeId == "aggregate"));
        workspace.ConfigurePublicOutputs(inspector);
        var rows = inspector.PublicOutputs!;
        var first = rows.Flow[0].PortId;
        var second = rows.Flow[1].PortId;
        Assert.IsTrue(rows.Flow[1].MoveTo(0));
        Assert.AreEqual(second, rows.Flow[0].PortId);
        f.Shell.SaveAllCommand.Execute(null);
        Assert.IsFalse(session.Editor.IsDirty);
        Assert.AreEqual(connection, workspace.StoryEditor.Document.Graph.Connections.Single());
        Assert.IsTrue(f.Shell.UndoCurrentCommand.CanExecute(null));
        f.Shell.UndoCurrentCommand.Execute(null);
        Assert.AreEqual(first, rows.Flow[0].PortId);
        f.Shell.RedoCurrentCommand.Execute(null);
        Assert.AreEqual(second, rows.Flow[0].PortId);
        rows.Flow[0].DisplayName = "Accepted";
        workspace.OpenGraphResource(session);
        f.Shell.UndoCurrentCommand.Execute(null);
        Assert.AreNotEqual("Accepted", rows.Flow[0].DisplayName);
        workspace.ReturnToStory();
        f.Shell.UndoCurrentCommand.Execute(null);
        Assert.AreEqual(first, rows.Flow[0].PortId);
        f.Shell.RedoCurrentCommand.Execute(null);
        Assert.AreEqual(second, rows.Flow[0].PortId);
        f.Shell.RedoCurrentCommand.Execute(null);
        Assert.AreEqual("Accepted", rows.Flow[0].DisplayName);
        Assert.AreEqual(connection, workspace.StoryEditor.Document.Graph.Connections.Single());
    }

    [TestMethod]
    public void ProjectInspectorRenamesRealStoryOutputsWithGlobalHistoryAndStableIdentity()
    {
        using var fixture = new CurrentNavigationFixture();
        fixture.Shell.OpenProjectCommand.Execute(null); fixture.Open();
        var workspace = fixture.Shell.CanonicalStoryWorkspace!;
        var terminal = new GraphNodeAuthoringService().Create(workspace.StoryEditor.Document.Graph,
            GraphScope.StoryFlow, "terminate", "terminal").Candidate!;
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(terminal));
        var portId = terminal.Properties["port_id"].GetString();
        var beforeName = terminal.Properties["display_name"].GetString();
        fixture.Shell.ShowProjectHomeCommand.Execute(null);
        var outputs = fixture.Shell.ProjectHome.Graph.SelectedOutputs!;
        Assert.IsTrue(outputs.CanRename);
        outputs.Flow.Single(row => row.PortId == portId).DisplayName = "接受委托";
        Assert.AreEqual("接受委托", workspace.StoryEditor.Document.Graph.Nodes.Single(node => node.Id == "terminal").Properties["display_name"].GetString());
        Assert.AreEqual(portId, outputs.Flow.Single().PortId);
        Assert.AreEqual("接受委托", fixture.Shell.ProjectHome.Graph.CanonicalHost!.Nodes.Single().Outputs.Single(port => port.Id == portId).DisplayName);
        fixture.Shell.UndoCurrentCommand.Execute(null);
        Assert.AreEqual(beforeName, outputs.Flow.Single().DisplayName);
        fixture.Shell.RedoCurrentCommand.Execute(null);
        Assert.AreEqual("接受委托", outputs.Flow.Single().DisplayName);
        outputs.Flow.Single().DisplayName = " ";
        Assert.AreEqual("接受委托", outputs.Flow.Single().DisplayName);
        fixture.Shell.SaveAllCommand.Execute(null);
        Assert.AreEqual("接受委托", fixture.Store.Stories.Load(CurrentNavigationFixture.Owner).Graph!.Nodes.Single(node => node.Id == "terminal").Properties["display_name"].GetString());
    }

    [TestMethod]
    public void ProjectHomeSelectsFirstStoryAndClassifiesEmptyAndSearchStates()
    {
        var home = new ProjectHomeViewModel();
        home.ReplaceDiscoveredStories([]);

        Assert.IsTrue(home.IsEmptyProject);
        Assert.IsFalse(home.IsSearchNoResults);
        Assert.IsNull(home.SelectedStory);

        home.ReplaceDiscoveredStories([
            Home("alpha", "Alpha"),
            Home("beta", "Beta"),
        ]);
        Assert.AreEqual("alpha", home.SelectedStory?.Id);
        Assert.IsFalse(home.IsEmptyProject);

        home.SearchText = "missing";
        Assert.IsTrue(home.IsSearchNoResults);
        Assert.IsNull(home.SelectedStory);
        Assert.IsTrue(home.ClearSearchCommand.CanExecute(null));
        home.ClearSearchCommand.Execute(null);
        Assert.AreEqual("alpha", home.SelectedStory?.Id);
        Assert.IsFalse(home.IsSearchNoResults);
    }

    [TestMethod]
    public void ProjectHomeRefreshPreservesSelectionAndFallsBackToAdjacentStory()
    {
        var home = new ProjectHomeViewModel();
        home.ReplaceDiscoveredStories([
            Home("alpha", "Alpha"),
            Home("beta", "Beta"),
            Home("gamma", "Gamma"),
        ]);
        home.SelectedStory = home.Stories.Single(item => item.Id == "beta");

        home.ReplaceDiscoveredStories([
            Home("gamma", "Gamma"),
            Home("beta", "Beta Updated"),
            Home("alpha", "Alpha"),
        ]);
        Assert.AreEqual("beta", home.SelectedStory?.Id);

        home.ReplaceDiscoveredStories([
            Home("alpha", "Alpha"),
            Home("gamma", "Gamma"),
        ]);
        Assert.AreEqual("gamma", home.SelectedStory?.Id);
    }

    [TestMethod]
    public void ProjectHomeSearchSelectsVisibleStoryAndRestoresPreSearchSelection()
    {
        var home = new ProjectHomeViewModel();
        home.ReplaceDiscoveredStories([
            Home("alpha", "Alpha"),
            Home("beta", "Beta"),
            Home("gamma", "Gamma"),
        ]);
        home.SelectedStory = home.Stories.Single(item => item.Id == "beta");

        home.SearchText = "gamma";
        Assert.AreEqual("gamma", home.SelectedStory?.Id);
        home.SearchText = "al";
        Assert.AreEqual("alpha", home.SelectedStory?.Id);
        home.SearchText = string.Empty;
        Assert.AreEqual("beta", home.SelectedStory?.Id);
    }

    [TestMethod]
    public void ProjectHomeShowsCurrentDiscoveryAndPreciseIncompleteState()
    {
        var canonical = new[]
        {
            new CanonicalStoryHomeEntry(
                "canonical_only", "Canonical Only", 1, 2, 3, 4, 5,
                IsComplete: true, IsValid: true, Diagnostics: []),
            new CanonicalStoryHomeEntry(
                "shared", "Canonical Shared", 0, 0, 0, 0, 1,
                IsComplete: true, IsValid: true, Diagnostics: []),
            new CanonicalStoryHomeEntry(
                "broken", "broken", 0, 0, 0, 0, 0,
                IsComplete: false, IsValid: false, Diagnostics: ["缺少 membership"]),
        };
        var home = new ProjectHomeViewModel();

        home.ReplaceDiscoveredStories(canonical);

        CollectionAssert.AreEquivalent(
            new[] { "shared", "canonical_only", "broken" },
            home.Stories.Select(item => item.Id).ToArray());
        var shared = home.Stories.Single(item => item.Id == "shared");
        Assert.AreEqual("Canonical Shared", shared.DisplayName);
        Assert.IsTrue(shared.HasCanonicalStory);
        var canonicalOnly = home.Stories.Single(item => item.Id == "canonical_only");
        Assert.AreEqual("1 个本故事角色 · 2 个引用角色 · 3 个会话 · 4 个任务",
            canonicalOnly.MembershipSummary);
        Assert.AreEqual(5, canonicalOnly.FlowNodeCount);
        var broken = home.Stories.Single(item => item.Id == "broken");
        Assert.AreEqual("数据不完整", broken.TagsText);
        StringAssert.Contains(broken.Description, "缺少 membership");
        Assert.HasCount(3, home.Graph.Nodes);
    }

    [TestMethod]
    public void ProjectHomeSearchesCurrentStoryNamesAndIdsAndOpensGraph()
    {
        var home = new ProjectHomeViewModel();
        home.ReplaceDiscoveredStories([Home("castle_mystery", "Castle Mystery"), Home("kingdom", "Kingdom Route")]);
        home.SearchText = "mystery";
        CollectionAssert.AreEqual(new[] { "castle_mystery" }, home.FilteredStories.Select(item => item.Id).ToArray());
        home.SearchText = "Route";
        CollectionAssert.AreEqual(new[] { "kingdom" }, home.FilteredStories.Select(item => item.Id).ToArray());
        home.ShowGraph();
        Assert.IsTrue(home.IsGraphVisible);
        Assert.HasCount(2, home.Graph.Nodes);
    }

    [TestMethod]
    public void MainWindowUsesCurrentWorkspaceTreeAndInspector()
    {
        var xaml = File.ReadAllText(FindRepositoryFile("studio/src/DarkGreyRPG.Studio/MainWindow.xaml"));
        Assert.IsFalse(xaml.Contains("{Binding StoryWorkspace.", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("DialogueEditorView", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("QuestEditorView", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "CanonicalStoryWorkspace");
        StringAssert.Contains(xaml, "CanonicalStoryWorkspaceView");
    }

    [TestMethod]
    public void ShellOpensProjectHomeThenCurrentStoryInspector()
    {
        using var f = new CurrentNavigationFixture();
        Assert.IsTrue(f.Shell.ProjectHome.IsHomeVisible);
        f.Open();
        var workspace = f.Shell.CanonicalStoryWorkspace!;
        Assert.AreEqual(CurrentNavigationFixture.Owner, workspace.StoryEditor.Id);
        workspace.StoryEditor.Host.AddNode(new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.StoryFlow, "terminate", "draft").Candidate!);
        Assert.IsTrue(workspace.SelectFolder(CanonicalStoryFolderKind.Actors));
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        Assert.IsTrue(workspace.SelectTreeItem(workspace.ActorItems.Single()));
        Assert.AreEqual(CurrentNavigationFixture.Owner + "~actor~hero", workspace.SelectedActor?.Id);
    }

    [TestMethod]
    public void RetainedGraphSurvivesNavigationAndCloseChoicesRespectBaseline()
    {
        using var f = new CurrentNavigationFixture();
        f.Open();
        var workspace = f.Shell.CanonicalStoryWorkspace!;
        workspace.StoryEditor.Host.AddNode(new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.StoryFlow, "logic_output", "invalid").Candidate!);
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        f.Shell.ShowProjectHomeCommand.Execute(null);
        Assert.IsTrue(f.Shell.ProjectHome.IsHomeVisible);
        f.Open();
        Assert.AreSame(workspace, f.Shell.CanonicalStoryWorkspace);
        Assert.IsTrue(workspace.StoryEditor.Host.Graph.Nodes.Any(node => node.Id == "invalid"));
        f.Dialogs.Choice = UnsavedChangesChoice.Cancel;
        Assert.IsFalse(f.Shell.TryClose());
        f.Dialogs.Choice = UnsavedChangesChoice.Save;
        using (var held = new FileStream(f.Store.Stories.GetPath(CurrentNavigationFixture.Owner), FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.IsFalse(f.Shell.TryClose());
        f.Dialogs.Choice = UnsavedChangesChoice.Discard;
        Assert.IsTrue(f.Shell.TryClose());
        Assert.IsFalse(f.Store.Stories.Load(CurrentNavigationFixture.Owner).Graph!.Nodes.Any(node => node.Id == "invalid"));
    }

    [TestMethod]
    public void ShellUndoCommandTracksFlowHistoryAvailability()
    {
        using var f = new CurrentNavigationFixture(); f.Open();
        var host = f.Shell.CanonicalStoryWorkspace!.StoryEditor.Host;
        var before = host.Nodes.Count;
        Assert.IsFalse(f.Shell.UndoCurrentCommand.CanExecute(null));
        Assert.IsTrue(host.AddNode(new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.StoryFlow, "terminate", "added").Candidate!));
        Assert.IsTrue(f.Shell.UndoCurrentCommand.CanExecute(null));
        f.Shell.UndoCurrentCommand.Execute(null);
        Assert.HasCount(before, host.Nodes);
        f.Shell.RedoCurrentCommand.Execute(null);
        Assert.HasCount(before + 1, host.Nodes);
    }

    [TestMethod]
    public void OpeningFlowProblemNavigatesSelectsNodeAndCarriesField()
    {
        using var f = new CurrentNavigationFixture(); f.Open();
        var workspace = f.Shell.CanonicalStoryWorkspace!;
        var start = workspace.StoryEditor.Host.Nodes.Single();
        Assert.IsTrue(workspace.SelectGraphNode(start));
        workspace.NodeInspector!.StoryStartTriggers.Single().RadiusText = "invalid";
        var problem = f.Shell.Problems.Problems.Single(item => item.Code == "graph.story.start.trigger.authoring_invalid");
        Assert.AreEqual(start.NodeId, problem.NodeId);
        Assert.AreEqual(CurrentNavigationFixture.Owner, problem.GraphResourceId);
        f.Shell.ShowProjectHomeCommand.Execute(null);
        f.Shell.OpenProblem(problem);
        Assert.AreSame(workspace, f.Shell.CanonicalStoryWorkspace);
        Assert.AreEqual(start.NodeId, workspace.StoryNodeFocusRequest?.NodeId);
        Assert.AreEqual(StoryStartSchema.RadiusProperty, workspace.StoryNodeFocusRequest?.Field);
        Assert.AreEqual(0, f.Dialogs.Prompts);
    }

    [TestMethod]
    public void LegacyRecoveryIsNotAppliedToCurrentStoryOrRewritten()
    {
        using var f = new CurrentNavigationFixture();
        var currentPath = f.Store.Stories.GetPath(CurrentNavigationFixture.Owner);
        var before = File.ReadAllBytes(currentPath);
        var legacyPath = Path.Combine(f.Root, "resources", "editor", "recovery", "legacy_story.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, """{"schema_version":1,"resource":{"id":"legacy_story","nodes":[{"id":"draft","type":"play_dialogue"}]}}""");
        var recoveryBefore = File.ReadAllBytes(legacyPath);
        f.Shell.OpenProjectCommand.Execute(null); f.Open();
        Assert.IsFalse(f.Shell.CanonicalStoryWorkspace!.StoryEditor.Host.Nodes.Any(node => node.NodeId == "draft"));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(currentPath));
        CollectionAssert.AreEqual(recoveryBefore, File.ReadAllBytes(legacyPath));
    }

    [TestMethod]
    public void SavingCurrentGraphPreservesUnrelatedLegacyRecovery()
    {
        using var f = new CurrentNavigationFixture();
        var path = Path.Combine(f.Root, "resources", "editor", "recovery", "legacy_story.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"schema_version":1,"resource":{"id":"legacy_story","nodes":[{"id":"draft","type":"play_dialogue"}]}}""");
        var before = File.ReadAllBytes(path);
        f.Open();
        f.Shell.CanonicalStoryWorkspace!.StoryEditor.Host.SetNodePosition("start", 765, 432);
        f.Shell.SaveCurrentResourceCommand.Execute(null);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public void ProjectGraphProblemRouteOpensCurrentStoryAndCarriesField()
    {
        using var f = new CurrentNavigationFixture();
        var problem = new ProblemItem(ValidationSeverity.Error, "project_graph.target.missing", "Missing target", "target_story_id",
            "project-graph/" + CurrentNavigationFixture.Owner + "/start");
        f.Shell.OpenProblem(problem);
        var workspace = f.Shell.CanonicalStoryWorkspace!;
        Assert.AreEqual(CurrentNavigationFixture.Owner, workspace.StoryEditor.Id);
        Assert.AreEqual("start", workspace.StoryNodeFocusRequest?.NodeId);
        Assert.AreEqual("target_story_id", workspace.StoryNodeFocusRequest?.Field);
        f.Shell.ShowProjectHomeCommand.Execute(null);
        f.Shell.ShowProjectGraphCommand.Execute(null);
        f.Shell.FocusProjectGraphProblems();
        Assert.IsFalse(f.Shell.Problems.Problems.Any(problem => problem.Code == "project_graph.story.isolated"));
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void ResourceProblemFocusDoesNotSelectSameNamedStoryNode(GraphResourceKind kind)
    {
        using var f = new CurrentNavigationFixture();
        var resourceId = CurrentNavigationFixture.Owner + (kind == GraphResourceKind.Session ? "~session~resource" : "~task~resource");
        new CanonicalStoryResourceLifecycleService(f.Store).CreateOwned(CurrentNavigationFixture.Owner, kind, resourceId, "Resource");
        f.Shell.OpenProjectCommand.Execute(null); f.Open();
        var workspace = f.Shell.CanonicalStoryWorkspace!;
        workspace.StoryEditor.Host.AddNode(new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.StoryFlow, "logic_output", "same_node").Candidate!);
        var resource = workspace.SessionItems.Concat(workspace.TaskItems).Single();
        workspace.OpenGraphResource(resource);
        resource.Editor.Host.AddNode(GraphNodeFactory.Create(resource.Editor.Scope, "logic_output", "same_node"));
        resource.Editor.Host.SetAuthoringIssue("test-input", new("test.authoring_invalid", "Invalid input", "value", NodeId: "same_node"));
        var problem = f.Shell.Problems.Problems.First(item => item.NodeId == "same_node" && item.GraphResourceId == resourceId);
        f.Shell.ShowProjectHomeCommand.Execute(null);
        f.Shell.OpenProblem(problem);
        Assert.AreSame(resource.Editor, workspace.ActiveEditor);
        Assert.AreEqual(resourceId, workspace.StoryNodeFocusRequest?.ResourceId);
        Assert.AreEqual("same_node", workspace.StoryNodeFocusRequest?.NodeId);
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        Assert.IsTrue(resource.Editor.IsDirty);
    }

    private sealed class CurrentNavigationFixture : IDisposable
    {
        public const string Owner = "ST-2345-6789-ABCD-EFGH";
        public string Root { get; } = CreateProjectDirectory();
        public CanonicalProjectGraphStore Store { get; }
        public ShellViewModel Shell { get; }
        public CurrentProjectDialogs Dialogs { get; } = new();
        public CurrentNavigationFixture()
        {
            var service = new ProjectService(); service.CreateProject(Root, "navigation", "Navigation");
            Store = new(Root);
            Store.Stories.Create(new(GraphResourceKind.Story, Owner, "Owner",
                new([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "region")])));
            Store.Memberships.Create(new(Owner));
            service.CreateActorInStory(Owner, Owner + "~actor~hero", "Hero");
            Shell = new(new ProjectService(), new FixedProjectFolderPicker(Root), projectWorkspaceDialogs: Dialogs);
            Shell.OpenProjectCommand.Execute(null);
        }
        public void Open() => Shell.OpenStory(Shell.ProjectHome.Stories.Single());
        public void Dispose() => TryDelete(Root);
    }
    private sealed class CurrentProjectDialogs : IProjectWorkspaceDialogs
    {
        public bool AllowGroupDeletion { get; set; }
        public bool ConfirmDeleteStoryGroups(string groupNames, string summary, IReadOnlyList<string> affectedPaths) => AllowGroupDeletion;
        public UnsavedChangesChoice Choice { get; set; } = UnsavedChangesChoice.Cancel;
        public int Prompts { get; private set; }
        public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null) => null;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() { Prompts++; return Choice; }
        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete) => false;
    }

    private static CanonicalStoryHomeEntry Home(string id, string name) => new(id, name, 0, 0, 0, 0, 0, true, true, []);

    private static string CreateProjectDirectory() =>
        Path.Combine(AppContext.BaseDirectory, ".test-data", "darkgrey-story-vm-" + Guid.NewGuid().ToString("N"));

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
        }

        Assert.Fail($"Unable to locate repository file '{relativePath}'.");
        return string.Empty;
    }

    private static void TryDelete(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private sealed class FixedProjectFolderPicker(string directory) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => directory;
    }
}
