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
    public void ProjectHomeSelectsFirstStoryAndClassifiesEmptyAndSearchStates()
    {
        var home = new ProjectHomeViewModel();
        home.ReplaceStories([]);

        Assert.IsTrue(home.IsEmptyProject);
        Assert.IsFalse(home.IsSearchNoResults);
        Assert.IsNull(home.SelectedStory);

        home.ReplaceStories([
            new StoryResource { Id = "alpha", DisplayName = "Alpha" },
            new StoryResource { Id = "beta", DisplayName = "Beta" },
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
        home.ReplaceStories([
            new StoryResource { Id = "alpha", DisplayName = "Alpha" },
            new StoryResource { Id = "beta", DisplayName = "Beta" },
            new StoryResource { Id = "gamma", DisplayName = "Gamma" },
        ]);
        home.SelectedStory = home.Stories.Single(item => item.Id == "beta");

        home.ReplaceStories([
            new StoryResource { Id = "gamma", DisplayName = "Gamma" },
            new StoryResource { Id = "beta", DisplayName = "Beta Updated" },
            new StoryResource { Id = "alpha", DisplayName = "Alpha" },
        ]);
        Assert.AreEqual("beta", home.SelectedStory?.Id);

        home.ReplaceStories([
            new StoryResource { Id = "alpha", DisplayName = "Alpha" },
            new StoryResource { Id = "gamma", DisplayName = "Gamma" },
        ]);
        Assert.AreEqual("gamma", home.SelectedStory?.Id);
    }

    [TestMethod]
    public void ProjectHomeSearchSelectsVisibleStoryAndRestoresPreSearchSelection()
    {
        var home = new ProjectHomeViewModel();
        home.ReplaceStories([
            new StoryResource { Id = "alpha", DisplayName = "Alpha" },
            new StoryResource { Id = "beta", DisplayName = "Beta" },
            new StoryResource { Id = "gamma", DisplayName = "Gamma" },
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
    public void ProjectHomeMergesCanonicalDiscoveryByIdAndKeepsCanonicalOnlyStoriesVisible()
    {
        var legacy = new[]
        {
            new StoryResource { Id = "legacy", DisplayName = "Legacy" },
            new StoryResource { Id = "shared", DisplayName = "Old Shared" },
        };
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

        home.ReplaceDiscoveredStories(legacy, canonical);

        CollectionAssert.AreEquivalent(
            new[] { "legacy", "shared", "canonical_only", "broken" },
            home.Stories.Select(item => item.Id).ToArray());
        var shared = home.Stories.Single(item => item.Id == "shared");
        Assert.AreEqual("Canonical Shared", shared.DisplayName);
        Assert.IsTrue(shared.HasLegacyStory);
        Assert.IsTrue(shared.HasCanonicalStory);
        Assert.IsFalse(shared.CanDeleteLegacyStory);
        var canonicalOnly = home.Stories.Single(item => item.Id == "canonical_only");
        Assert.IsTrue(canonicalOnly.IsCanonicalOnly);
        Assert.IsFalse(canonicalOnly.CanDeleteLegacyStory);
        Assert.AreEqual("1 个本故事角色 · 2 个引用角色 · 3 个会话 · 4 个任务",
            canonicalOnly.MembershipSummary);
        Assert.AreEqual(5, canonicalOnly.FlowNodeCount);
        var broken = home.Stories.Single(item => item.Id == "broken");
        Assert.AreEqual("数据不完整", broken.TagsText);
        StringAssert.Contains(broken.Description, "缺少 membership");
        Assert.HasCount(2, home.Graph.Nodes);
    }

    [TestMethod]
    public void ProjectHomeSearchesStoriesByIdDisplayNameAndTagsAndBuildsGraph()
    {
        var stories = new[]
        {
            new StoryResource
            {
                Id = "castle_mystery",
                DisplayName = "Castle Mystery",
                Tags = ["main", "mystery"],
                Nodes = [new StoryNodeResource
                {
                    Id = "to_kingdom",
                    Type = "EnterStory",
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["target_story_id"] = JsonSerializer.SerializeToElement("kingdom"),
                    },
                }],
            },
            new StoryResource { Id = "kingdom", DisplayName = "Kingdom Route", Tags = ["branch"] },
        };
        var home = new ProjectHomeViewModel();
        home.ReplaceStories(stories);

        home.SearchText = "mystery";
        CollectionAssert.AreEqual(new[] { "castle_mystery" }, home.FilteredStories.Select(item => item.Id).ToArray());
        home.SearchText = "kingdom";
        CollectionAssert.AreEqual(new[] { "kingdom" }, home.FilteredStories.Select(item => item.Id).ToArray());
        home.SearchText = "branch";
        CollectionAssert.AreEqual(new[] { "kingdom" }, home.FilteredStories.Select(item => item.Id).ToArray());

        home.ShowGraph();
        Assert.IsTrue(home.IsGraphVisible);
        Assert.HasCount(2, home.Graph.Nodes);
        Assert.AreEqual("2 个故事 · 1 条转场 / 1 组关系 · 0 个诊断", home.Graph.Summary);
        Assert.AreEqual("kingdom", home.Graph.Edges.Single().TargetStoryId);
    }

    [TestMethod]
    public void SelectedStoryExposesFullOverviewAndStoryWorkspaceDefaultsActorsWithFourRoutes()
    {
        var story = new StoryResource
        {
            Id = "castle_mystery",
            DisplayName = "Castle Mystery",
            OwnedResources = new StoryMembership { Actors = ["hero"], Dialogues = ["opening"], Quests = ["investigate"] },
            ReferencedResources = new StoryMembership { Actors = ["merchant", "missing"], Dialogues = ["shared"], Quests = ["shared_quest"] },
            Nodes = [new StoryNodeResource { Id = "start", Type = "START" }],
        };
        var actors = new[]
        {
            new ActorResourceInfo("hero", "Hero", "hero.json", ["main"]),
            new ActorResourceInfo("merchant", "Merchant", "merchant.json", ["shop"]),
        };
        var home = new ProjectHomeViewModel();
        home.ReplaceStories([story]);
        home.SelectedStory = home.Stories.Single();

        Assert.AreEqual("Castle Mystery", home.SelectedStory.Overview.DisplayName);
        Assert.AreEqual("castle_mystery", home.SelectedStory.Overview.Id);
        Assert.AreEqual(story.Description, home.SelectedStory.Overview.Description);
        Assert.AreEqual("1 个本故事角色 · 2 个引用角色 · 2 个对话 · 2 个任务", home.SelectedStory.Overview.MembershipSummary);
        Assert.AreEqual(1, home.SelectedStory.Overview.FlowNodeCount);
        Assert.AreEqual(home.SelectedStory.Overview.MembershipSummary, home.SelectedStory.MembershipSummary);
        Assert.AreEqual(home.SelectedStory.Overview.FlowNodeCount, home.SelectedStory.FlowNodeCount);

        var workspace = new StoryWorkspaceViewModel();

        workspace.OpenStory(story, actors);

        Assert.AreEqual(StoryWorkspaceRoutes.Actors, workspace.CurrentRoute);
        CollectionAssert.AreEqual(
            new[] { StoryWorkspaceRoutes.Actors, StoryWorkspaceRoutes.Dialogues, StoryWorkspaceRoutes.Quests, StoryWorkspaceRoutes.Flow },
            workspace.Routes.Select(route => route.Page).ToArray());
        Assert.AreSame(workspace.Actors, workspace.CurrentPage);
        Assert.HasCount(3, workspace.Actors!.Memberships);
        Assert.AreEqual(1, workspace.Actors.OwnedCount);
        Assert.AreEqual(2, workspace.Actors.ReferencedCount);
        Assert.IsTrue(workspace.Actors.Memberships.Single(item => item.Id == "merchant").IsResolved);
        Assert.IsTrue(workspace.Actors.HasMissingActors);

        var changed = new List<string>();
        workspace.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);
        workspace.SelectRoute(StoryWorkspaceRoutes.Dialogues);
        Assert.AreSame(workspace.Dialogues, workspace.CurrentPage);
        CollectionAssert.Contains(changed, nameof(StoryWorkspaceViewModel.SelectedRoute));
        workspace.SelectRoute(StoryWorkspaceRoutes.Actors);
        Assert.AreSame(workspace.Actors, workspace.CurrentPage);
        workspace.SelectRoute(StoryWorkspaceRoutes.Quests);
        Assert.AreSame(workspace.Quests, workspace.CurrentPage);
        workspace.SelectRoute(StoryWorkspaceRoutes.Flow);
        Assert.AreSame(workspace.Flow, workspace.CurrentPage);
    }

    [TestMethod]
    public void StoryActorLibraryMergesSortsMissingAndPreservesVisibleSelectionWhenFiltering()
    {
        var story = new StoryResource
        {
            Id = "library",
            OwnedResources = new StoryMembership { Actors = ["zulu", "alpha"] },
            ReferencedResources = new StoryMembership { Actors = ["beta", "missing", "alpha"] },
        };
        var library = new StoryActorsViewModel(story,
        [
            new ActorResourceInfo("zulu", "Zulu", "zulu.json", []),
            new ActorResourceInfo("alpha", "Alpha", "alpha.json", []),
            new ActorResourceInfo("beta", "Beta", "beta.json", []),
        ]);

        Assert.AreEqual("alpha", library.Memberships[0].Id);
        Assert.IsTrue(library.Memberships.Select(item => item.Id).SequenceEqual(["alpha", "zulu", "beta", "missing"]));
        Assert.IsTrue(library.Memberships.Single(item => item.Id == "missing").IsMissing);
        library.SelectedMembership = library.Memberships.Single(item => item.Id == "zulu");
        library.SearchText = "zulu";
        Assert.AreEqual("zulu", library.SelectedMembership?.Id);
        Assert.HasCount(1, library.FilteredMemberships);
        library.SearchText = "beta";
        Assert.IsNull(library.SelectedMembership);
    }

    [TestMethod]
    public void StoryResourceLibrariesShareMembershipOrderingAndSourceTooltipMetadata()
    {
        var story = new StoryResource
        {
            Id = "library",
            OwnedResources = new StoryMembership { Dialogues = ["owned_b", "owned_a"], Quests = ["quest"] },
            ReferencedResources = new StoryMembership { Dialogues = ["shared", "collision", "missing_dialogue"], Quests = ["shared_quest", "collision", "missing_quest"] },
        };
        var descriptors = new ResourceDescriptor[]
        {
            new(ProjectResourceType.Dialogue, "owned_b", "Bravo", "b.json"),
            new(ProjectResourceType.Dialogue, "owned_a", "Alpha", "a.json"),
            new(ProjectResourceType.Dialogue, "shared", "Shared", "s.json"),
            new(ProjectResourceType.Dialogue, "collision", "Collision Dialogue", "cd.json"),
            new(ProjectResourceType.Quest, "quest", "Quest", "q.json"),
            new(ProjectResourceType.Quest, "shared_quest", "Shared Quest", "sq.json"),
            new(ProjectResourceType.Quest, "collision", "Collision Quest", "cq.json"),
        };
        var dialogueHomeStories = new Dictionary<string, string> { ["shared"] = "Home Story", ["collision"] = "Dialogue Home" };
        var questHomeStories = new Dictionary<string, string> { ["shared_quest"] = "Quest Home", ["collision"] = "Quest Home" };
        var dialogues = new StoryDialoguesViewModel(story, descriptors, dialogueHomeStories);
        var quests = new StoryQuestsViewModel(story, descriptors, questHomeStories);

        Assert.AreEqual("owned_a", dialogues.Items[0].Id);
        Assert.IsTrue(dialogues.Items.Select(item => item.Id).SequenceEqual(["owned_a", "owned_b", "collision", "shared", "missing_dialogue"]));
        Assert.IsTrue(quests.Items.Select(item => item.Id).SequenceEqual(["quest", "collision", "shared_quest", "missing_quest"]));
        Assert.AreEqual("Home Story", dialogues.Items.Single(item => item.Id == "shared").HomeStoryDisplayName);
        StringAssert.Contains(dialogues.Items.Single(item => item.Id == "shared").MembershipTooltip, "Home Story");
        Assert.AreEqual("Dialogue Home", dialogues.Items.Single(item => item.Id == "collision").HomeStoryDisplayName);
        Assert.AreEqual("Quest Home", quests.Items.Single(item => item.Id == "collision").HomeStoryDisplayName);
        Assert.IsTrue(quests.Items.Single(item => item.Id == "missing_quest").IsMissing);
    }

    [TestMethod]
    public void StoryActorLibraryKeepsThirtyFiveItemsInOneFilteredCollection()
    {
        var ids = Enumerable.Range(0, 35).Select(index => $"actor_{index:00}").ToArray();
        var story = new StoryResource
        {
            Id = "large_library",
            OwnedResources = new StoryMembership { Actors = ids[..18].ToList() },
            ReferencedResources = new StoryMembership { Actors = ids[18..].ToList() },
        };
        var actors = ids.Select(id => new ActorResourceInfo(id, id, id + ".json", [])).ToArray();
        var library = new StoryActorsViewModel(story, actors);

        Assert.HasCount(35, library.Memberships);
        Assert.HasCount(35, library.FilteredMemberships);
        Assert.IsTrue(library.Memberships.Take(18).All(item => item.IsOwned));
        Assert.IsTrue(library.Memberships.Skip(18).All(item => item.IsReferenced));
    }

    [TestMethod]
    public void MainWindowResourceLibrariesDeclareRecyclingVirtualizationAndSharedVectorIndicators()
    {
        var path = FindRepositoryFile("studio/src/DarkGreyRPG.Studio/MainWindow.xaml");
        var xaml = File.ReadAllText(path);

        Assert.AreEqual(3, xaml.Split("VirtualizingPanel.VirtualizationMode=\"Recycling\"", StringSplitOptions.None).Length - 1);
        Assert.AreEqual(3, xaml.Split("ScrollViewer.CanContentScroll=\"True\"", StringSplitOptions.None).Length - 1);
        Assert.IsFalse(xaml.Contains("FilteredOwnedMemberships", StringComparison.Ordinal));
        Assert.IsFalse(xaml.Contains("FilteredReferencedMemberships", StringComparison.Ordinal));
        Assert.IsTrue(xaml.Contains("ReferenceIconGeometry", StringComparison.Ordinal));
        Assert.IsTrue(xaml.Contains("MissingIconGeometry", StringComparison.Ordinal));
        Assert.AreEqual(1, xaml.Split("Story 角色页面命令栏", StringSplitOptions.None).Length - 1);
        Assert.AreEqual(1, xaml.Split("Story 对话页面命令栏", StringSplitOptions.None).Length - 1);
        Assert.AreEqual(1, xaml.Split("Story 任务页面命令栏", StringSplitOptions.None).Length - 1);
        StringAssert.Contains(xaml, "StoryWorkspace.Actors.FilteredMemberships.Count, StringFormat={}{0} 个角色");
        StringAssert.Contains(xaml, "StoryWorkspace.Dialogues.FilteredItems.Count, StringFormat={}{0} 个对话");
        StringAssert.Contains(xaml, "StoryWorkspace.Quests.FilteredItems.Count, StringFormat={}{0} 个任务");
        Assert.AreEqual(1, File.ReadAllText(FindRepositoryFile("studio/src/DarkGreyRPG.Studio/Views/StoryFlowEditorView.xaml"))
            .Split("Story 流程页面命令栏", StringSplitOptions.None).Length - 1);
        Assert.AreEqual(3, xaml.Split("StoryResourceLibraryWidth, ElementName=RootWindow", StringSplitOptions.None).Length - 1);
        Assert.AreEqual(4, xaml.Split("GridSplitter Grid.Column=\"1\"", StringSplitOptions.None).Length - 1);
        StringAssert.Contains(xaml, "MinWidth=\"220\" MaxWidth=\"380\"");
    }

    [TestMethod]
    public void ShellOpensProjectHomeThenStoryWithoutSelectingGlobalActor()
    {
        using var f = new CurrentNavigationFixture();
        Assert.IsNull(f.Shell.SelectedActor);
        Assert.IsTrue(f.Shell.ProjectHome.IsHomeVisible);
        f.Open();
        var workspace = f.Shell.CanonicalStoryWorkspace!;
        Assert.AreEqual(CurrentNavigationFixture.Owner, workspace.StoryEditor.Id);
        Assert.IsNull(f.Shell.SelectedActor);
        workspace.StoryEditor.Host.AddNode(new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.StoryFlow, "terminate", "draft").Candidate!);
        Assert.IsTrue(workspace.SelectFolder(CanonicalStoryFolderKind.Actors));
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        Assert.IsTrue(workspace.SelectTreeItem(workspace.ActorItems.Single()));
        Assert.AreEqual(CurrentNavigationFixture.Owner + "~actor~hero", workspace.SelectedActor?.Id);
        Assert.IsNull(f.Shell.CurrentActor); // Canonical selection uses its own Inspector, not the retired global editor.
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
        var recovery = new StoryFlowRecoveryStore(f.Root);
        recovery.Save(new StoryResource { Id = "legacy_story", DisplayName = "Legacy draft", Nodes = [new() { Id = "draft", Type = "play_dialogue" }] });
        var legacyPath = Path.Combine(recovery.RecoveryDirectory, "legacy_story.json");
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
        var recovery = new StoryFlowRecoveryStore(f.Root);
        recovery.Save(new StoryResource { Id = "legacy_story", Nodes = [new() { Id = "draft", Type = "play_dialogue" }] });
        var path = Path.Combine(recovery.RecoveryDirectory, "legacy_story.json");
        var before = File.ReadAllBytes(path);
        f.Open();
        f.Shell.CanonicalStoryWorkspace!.StoryEditor.Host.SetNodePosition("start", 765, 432);
        f.Shell.SaveCurrentResourceCommand.Execute(null);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        Assert.IsNotNull(recovery.Load("legacy_story"));
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

    private sealed class FakeFlowWorkspaceDialogs : IFlowWorkspaceDialogs
    {
        public UnsavedChangesChoice Choice { get; set; }
        public StoryFlowRecoveryChoice RecoveryChoice { get; set; } = StoryFlowRecoveryChoice.Ignore;
        public StoryFlowRecoveryChoice? LastRecoveryChoice { get; private set; }
        public int UnsavedPromptCount { get; private set; }
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(StoryFlowEditorViewModel flow)
        {
            UnsavedPromptCount++;
            return Choice;
        }
        public StoryFlowRecoveryChoice ChooseRecovery(StoryFlowRecoverySnapshot snapshot)
        {
            LastRecoveryChoice = RecoveryChoice;
            return RecoveryChoice;
        }
    }
}
