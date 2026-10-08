using System.IO.Compression;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.Settings;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

// These workflows exercise the application-wide edit history clock; concurrent independent shells invalidate redo epochs.
[TestClass]
[DoNotParallelize]
public sealed class OfflineShellWorkflowTests
{
    [TestMethod]
    public void ReferenceUndoRespectsNewerGraphEdits()
    {
        using var fixture = new Fixture();
        var dialogs = new Dialogs { SelectId = "ST-JKLM-NPQR-STUV-WXYZ~actor~boss" };
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A), offlinePackageDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ReferencePackageFromFile(fixture.Package);
        shell.AddExternalReference("ST-2345-6789-ABCD-EFGH");
        var workspace = shell.CanonicalStoryWorkspace!;
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.StoryFlow, "terminate", "new-stop").Candidate!));
        shell.UndoCurrentCommand.Execute(null);
        Assert.IsFalse(workspace.StoryEditor.Host.Nodes.Any(node => node.Id == "new-stop"));
        Assert.HasCount(1, workspace.ActorItems);
        shell.UndoCurrentCommand.Execute(null);
        Assert.IsEmpty(workspace.ActorItems);
        shell.RedoCurrentCommand.Execute(null);
        Assert.HasCount(1, workspace.ActorItems);
        shell.RedoCurrentCommand.Execute(null);
        Assert.IsTrue(workspace.StoryEditor.Host.Nodes.Any(node => node.Id == "new-stop"));
    }

    [TestMethod]
    public void ReferenceOperationsAreIndependentUndoableAndPreserveUnsavedStory()
    {
        using var fixture = new Fixture();
        var dialogs = new Dialogs { RemoveConfirmed = true };
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A), offlinePackageDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ReferencePackageFromFile(fixture.Package);
        shell.UndoCurrentCommand.Execute(null);
        Assert.IsEmpty(shell.ReferencedPackages);
        shell.RedoCurrentCommand.Execute(null);
        Assert.HasCount(1, shell.ReferencedPackages, shell.StatusMessage);
        foreach (var id in new[] { "ST-JKLM-NPQR-STUV-WXYZ~actor~boss", "ST-JKLM-NPQR-STUV-WXYZ~actor~group", "ST-JKLM-NPQR-STUV-WXYZ~item~item", "ST-JKLM-NPQR-STUV-WXYZ~item_group~item_group", "ST-JKLM-NPQR-STUV-WXYZ~session~session", "ST-JKLM-NPQR-STUV-WXYZ~task~task" })
        {
            dialogs.SelectId = id;
            shell.AddExternalReference("ST-2345-6789-ABCD-EFGH");
        }
        var workspace = shell.CanonicalStoryWorkspace!;
        var store = new CanonicalProjectGraphStore(fixture.A);
        var saved = File.ReadAllBytes(store.Stories.GetPath("ST-2345-6789-ABCD-EFGH"));
        workspace.StoryEditor.Document.SetDisplayName("未保存的故事标题");
        workspace.StoryEditor.RefreshFromDocument();
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        foreach (var id in new[] { "ST-JKLM-NPQR-STUV-WXYZ~actor~boss", "ST-JKLM-NPQR-STUV-WXYZ~actor~group", "ST-JKLM-NPQR-STUV-WXYZ~item~item", "ST-JKLM-NPQR-STUV-WXYZ~item_group~item_group", "ST-JKLM-NPQR-STUV-WXYZ~session~session", "ST-JKLM-NPQR-STUV-WXYZ~task~task" })
        {
            var resource = workspace.Folders.SelectMany(folder => folder.Items).Single(item => item.Id == id);
            workspace.SelectTreeItem(resource);
            Assert.IsTrue(workspace.DeleteSelectedResourceCommand.CanExecute(null));
            workspace.DeleteSelectedResourceCommand.Execute(null);
            Assert.IsFalse(workspace.Folders.SelectMany(folder => folder.Items).Any(item => item.Id == id));
            Assert.HasCount(1, shell.ReferencedPackages, shell.StatusMessage);
            shell.UndoCurrentCommand.Execute(null);
            Assert.IsTrue(workspace.Folders.SelectMany(folder => folder.Items).Any(item => item.Id == id));
            shell.RedoCurrentCommand.Execute(null);
            Assert.IsFalse(workspace.Folders.SelectMany(folder => folder.Items).Any(item => item.Id == id));
            shell.UndoCurrentCommand.Execute(null);
        }
        shell.RemoveReferencedPackage("ST-JKLM-NPQR-STUV-WXYZ");
        Assert.IsEmpty(shell.ReferencedPackages);
        Assert.AreSame(workspace, shell.CanonicalStoryWorkspace);
        Assert.AreEqual("未保存的故事标题", workspace.StoryEditor.DisplayName);
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        CollectionAssert.AreEqual(saved, File.ReadAllBytes(store.Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
        shell.UndoCurrentCommand.Execute(null);
        Assert.HasCount(1, shell.ReferencedPackages, shell.StatusMessage);
        Assert.IsEmpty(workspace.MissingItems);
        Assert.AreEqual("未保存的故事标题", workspace.StoryEditor.DisplayName);
    }

    [TestMethod]
    public void PackageInspectorSeparatesMetadataFromNavigableReadOnlyFlow()
    {
        using var fixture = new Fixture();
        var dialogs = new Dialogs();
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A), offlinePackageDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ReferencePackageFromFile(fixture.Package);
        var row = shell.ReferencedPackages.Single();
        Assert.IsFalse(row.Resources.Any(resource => resource.Resource.Kind == "Story"));
        foreach (var resource in row.Resources)
        {
            StringAssert.Contains(resource.Label, resource.Resource.DisplayName);
            StringAssert.Contains(resource.Label, resource.Resource.TypeLabel);
            Assert.IsFalse(resource.Label.Contains(resource.Resource.Id, StringComparison.Ordinal), "Resource label, tooltip and accessibility name must hide the internal identity.");
        }
        CollectionAssert.AreEqual(new[] { "故事", "角色", "物品", "会话", "任务" }, row.Folders.Select(folder => folder.DisplayName).ToArray());
        Assert.AreEqual(row.Resources.Count, row.Folders.Sum(folder => folder.Items.Count));
        Assert.IsTrue(row.Folders[1].Items.All(item => item.Resource.Kind == "Actor"));
        Assert.IsTrue(row.Folders[2].Items.All(item => item.Resource.Kind is "Item" or "ItemGroup"));
        var empty = new ReferencedPackageRow("empty", "empty", "", [], new RelayCommand(() => { }));
        Assert.HasCount(5, empty.Folders);
        Assert.IsTrue(empty.Folders.All(folder => !folder.HasResources));
        row.SelectCommand!.Execute(null);
        row.Resources.Single(resource => resource.Resource.Id == "ST-JKLM-NPQR-STUV-WXYZ~actor~boss").ViewCommand.Execute(null);
        Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ~actor~boss", shell.SelectedReferencedResource?.Id);
        Assert.IsNull(dialogs.Viewed);
        foreach (var folder in row.Folders.Skip(3))
        {
            Assert.IsTrue(folder.HasResources);
            folder.Items.Single().GraphCommand!.Execute(null);
            Assert.AreEqual(folder.Items.Single().Resource.Id, dialogs.Viewed!.Id);
            using var localViewer = new OfflineReadOnlyResourceViewModel(dialogs.Viewed);
            Assert.IsNotNull(localViewer.GraphPreview);
        }

        row.StoryGraphCommand!.Execute(null);
        using var viewer = new OfflineReadOnlyResourceViewModel(dialogs.Viewed!);
        foreach (var type in new[] { "session", "task" })
        {
            var node = GraphNodeFactory.Create(GraphScope.StoryFlow, type, type, type);
            node.Properties["resource_id"] = JsonSerializer.SerializeToElement("ST-JKLM-NPQR-STUV-WXYZ~" + type + "~" + type);
            var host = new DarkGreyRPG.Studio.ViewModels.Graph.GraphEditorHostViewModel(new GraphDocument([node]), GraphScope.StoryFlow);
            Assert.IsTrue(viewer.TryOpenSubgraph(host.Nodes.Single()));
            Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ~" + type + "~" + type, viewer.Choice.Id);
            Assert.IsTrue(viewer.BackCommand.CanExecute(null));
            viewer.BackCommand.Execute(null);
            Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ", viewer.Choice.Id);
        }
    }

    [TestMethod]
    public void ReferencePickerImportPreservesOriginalReferenceAndReexportsFreshOwnership()
    {
        using var fixture = new Fixture();
        var dialogs = new Dialogs();
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A), offlinePackageDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ReferencePackageFromFile(fixture.Package);
        Assert.HasCount(1, shell.ReferencedPackages, shell.StatusMessage);
        var packageRow = shell.ReferencedPackages.Single();
        packageRow.SelectCommand!.Execute(null);
        Assert.AreSame(packageRow, shell.SelectedReferencedPackage);
        StringAssert.Contains(packageRow.Details, ".dgrs");
        StringAssert.Contains(packageRow.Details, "Story UID：ST-JKLM-NPQR-STUV-WXYZ");
        Assert.IsFalse(packageRow.Details.Contains(fixture.OriginB));
        Assert.IsFalse(shell.ProjectHome.Stories.Any(story => story.Id == "ST-JKLM-NPQR-STUV-WXYZ"));
        dialogs.SelectId = "ST-JKLM-NPQR-STUV-WXYZ~actor~boss";
        shell.AddExternalReference("ST-2345-6789-ABCD-EFGH");
        Assert.AreEqual("真实角色名称", dialogs.LastExternal.Single(choice => choice.Id == "ST-JKLM-NPQR-STUV-WXYZ~actor~boss").DisplayName);
        var source = dialogs.LastExternal.Single(choice => choice.Id == "ST-JKLM-NPQR-STUV-WXYZ~actor~boss").Provider;
        StringAssert.Contains(source, ".dgrs");
        Assert.IsFalse(source.Contains("ST-JKLM-NPQR-STUV-WXYZ"));
        Assert.IsFalse(source.Contains(fixture.OriginB));
        var membership = new CanonicalProjectGraphStore(fixture.A).Memberships.Load("ST-2345-6789-ABCD-EFGH");
        CollectionAssert.Contains(membership.ReferencedResources.Actors, "ST-JKLM-NPQR-STUV-WXYZ~actor~boss");
        Assert.IsFalse(membership.OwnedResources.Actors.Contains("ST-JKLM-NPQR-STUV-WXYZ~actor~boss"));
        Assert.IsTrue(shell.CanonicalStoryWorkspace!.ActorItems.Single().IsReadOnly);
        foreach (var id in new[] { "ST-JKLM-NPQR-STUV-WXYZ~session~session", "ST-JKLM-NPQR-STUV-WXYZ~task~task" })
        {
            dialogs.SelectId = id;
            shell.AddExternalReference("ST-2345-6789-ABCD-EFGH");
            var workspace = shell.CanonicalStoryWorkspace!;
            var graph = workspace.SessionItems.Concat(workspace.TaskItems).Single(item => item.Id == id);
            workspace.SelectTreeItem(graph);
            Assert.AreEqual("[引用] ", graph.SourceText);
            Assert.IsFalse(workspace.InspectorOwnershipText.Contains("ST-JKLM-NPQR-STUV-WXYZ"));
            StringAssert.Contains(workspace.InspectorOwnershipText, ".dgrs");
        }
        shell.ImportPackageFromFile(fixture.Package);
        Assert.HasCount(1, shell.ReferencedPackages);
        var importedUid = shell.CanonicalStoryWorkspace!.StoryEditor.Id;
        Assert.AreNotEqual("ST-JKLM-NPQR-STUV-WXYZ", importedUid);
        Assert.IsTrue(StoryUid.IsValid(importedUid));
        Assert.IsTrue(shell.ProjectHome.Stories.Single(story => story.Id == importedUid).HasCanonicalStory);
        Assert.IsFalse(File.Exists(Path.Combine(fixture.A, "resources", "canonical", "namespace_policy.json")));
        var importedStore = new CanonicalProjectGraphStore(fixture.A);
        Assert.AreEqual("守卫会话", importedStore.Sessions.Load(importedUid + "~session~session").DisplayName);
        Assert.AreEqual("守卫任务", importedStore.Tasks.Load(importedUid + "~task~task").DisplayName);
        Assert.AreEqual("任务钥匙", new DarkGreyRPG.Studio.Core.Items.ItemRepository(fixture.A).LoadItem(importedUid + "~item~item").DisplayName);
        Assert.AreEqual("任务物品组", new DarkGreyRPG.Studio.Core.Items.ItemRepository(fixture.A).LoadGroup(importedUid + "~item_group~item_group").DisplayName);
        Assert.IsFalse(new CanonicalStoryWorkspaceLoader(new(fixture.A)).Load("ST-2345-6789-ABCD-EFGH").Actors.Single().IsMissing);
        var output = Path.Combine(fixture.Root, "fork.dgrs");
        new DgrsStoryPackageExporter(fixture.A).Build(importedUid, output);
        using var archive = ZipFile.OpenRead(output);
        using var reader = new StreamReader(archive.GetEntry("project.json")!.Open());
        using var project = JsonDocument.Parse(reader.ReadToEnd());
        Assert.AreEqual(fixture.OriginA, project.RootElement.GetProperty("project_origin_code").GetString());
        Assert.AreNotEqual(fixture.OriginB, project.RootElement.GetProperty("project_origin_code").GetString());
    }

    [TestMethod]
    public void RemovalRequiresConsumerConfirmationAndKeepsMembership()
    {
        using var fixture = new Fixture();
        var dialogs = new Dialogs { SelectId = "ST-JKLM-NPQR-STUV-WXYZ~actor~boss" };
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A), offlinePackageDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ReferencePackageFromFile(fixture.Package);
        shell.AddExternalReference("ST-2345-6789-ABCD-EFGH");
        shell.RemoveReferencedPackage("ST-JKLM-NPQR-STUV-WXYZ");
        Assert.HasCount(1, shell.ReferencedPackages, shell.StatusMessage);
        CollectionAssert.Contains(dialogs.Consumers.ToArray(), "ST-2345-6789-ABCD-EFGH");
        dialogs.RemoveConfirmed = true;
        shell.RemoveReferencedPackage("ST-JKLM-NPQR-STUV-WXYZ");
        Assert.IsEmpty(shell.ReferencedPackages);
        Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ~actor~boss", shell.CanonicalStoryWorkspace!.MissingItems.Single().Id);
    }

    [TestMethod]
    public void EmitIsolatedLiveFixturesWhenRequested()
    {
        var target = Environment.GetEnvironmentVariable("DGR_OFFLINE_LIVE_FIXTURE");
        if (string.IsNullOrWhiteSpace(target)) return;
        using var fixture = new Fixture(target, keep: true);
        var settings = new SettingsService(Path.Combine(fixture.Root, "settings.json"));
        settings.Save(new StudioSettings { LastProject = fixture.A, Theme = ThemePreference.Dark, WindowWidth = 1420, WindowHeight = 900 });
        File.WriteAllText(Path.Combine(fixture.Root, "fingerprint.txt"), OfflineDgrsPackageReader.Read(fixture.Package).Fingerprint);
        File.WriteAllText(Path.Combine(fixture.Root, "origins.json"), JsonSerializer.Serialize(new { fixture.OriginA, fixture.OriginB }));
    }

    [TestMethod]
    public void EmitLiveUpdatesWhenRequested()
    {
        var target = Environment.GetEnvironmentVariable("DGR_OFFLINE_UPDATE_FIXTURE");
        if (string.IsNullOrWhiteSpace(target)) return;
        var b = Path.Combine(target, "ProjectB");
        var actors = new ActorRepository(b);
        var actor = actors.ListActors().Any(value => value.Id == "ST-JKLM-NPQR-STUV-WXYZ~actor~boss")
            ? actors.LoadActor("ST-JKLM-NPQR-STUV-WXYZ~actor~boss") : actors.CreateIndividual("ST-JKLM-NPQR-STUV-WXYZ~actor~boss", "真实角色名称");
        actor.HomeStoryId = "ST-JKLM-NPQR-STUV-WXYZ";
        actor.DisplayName = "更新后的守卫首领";
        actors.SaveActor(actor);
        new DgrsStoryPackageExporter(b).Build("ST-JKLM-NPQR-STUV-WXYZ", Path.Combine(target, "renamed-v2.dgrs"), "2.0");
        var store = new CanonicalProjectGraphStore(b);
        var membership = store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ");
        var owned = membership.OwnedResources;
        owned.Actors.Remove("ST-JKLM-NPQR-STUV-WXYZ~actor~boss");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(membership.StoryId, owned, membership.ReferencedResources));
        actors.DeleteActor("ST-JKLM-NPQR-STUV-WXYZ~actor~boss");
        new DgrsStoryPackageExporter(b).Build("ST-JKLM-NPQR-STUV-WXYZ", Path.Combine(target, "missing-v3.dgrs"), "3.0");
    }

    [TestMethod]
    public void EmitReferenceFlowFixtureWhenRequested()
    {
        var target = Environment.GetEnvironmentVariable("DGR_REFERENCE_FLOW_FIXTURE");
        if (string.IsNullOrWhiteSpace(target)) return;
        using var fixture = new Fixture(target, keep: true);
        var store = new CanonicalProjectGraphStore(Path.Combine(target, "ProjectB"));
        var story = store.Stories.Load("ST-JKLM-NPQR-STUV-WXYZ");
        var graph = story.Graph!;
        foreach (var type in new[] { "session", "task" })
        {
            var node = GraphNodeFactory.Create(GraphScope.StoryFlow, type, type, type == "session" ? "会话入口" : "任务入口");
            node.Properties["resource_id"] = JsonSerializer.SerializeToElement("ST-JKLM-NPQR-STUV-WXYZ~" + type + "~" + type);
            graph.Nodes.Add(node);
        }
        story.Graph = graph;
        store.Stories.Replace(story);
        new DgrsStoryPackageExporter(store.ProjectDirectory).Build("ST-JKLM-NPQR-STUV-WXYZ", fixture.Package, "1.0");
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A));
        shell.OpenProjectCommand.Execute(null);
        shell.ReferencePackageFromFile(fixture.Package);
        var membershipStore = new CanonicalProjectGraphStore(fixture.A);
        var membership = membershipStore.Memberships.Load("ST-2345-6789-ABCD-EFGH");
        var references = new CanonicalStoryMembershipSet { Actors = ["ST-JKLM-NPQR-STUV-WXYZ~actor~boss", "ST-JKLM-NPQR-STUV-WXYZ~actor~group"], Items = ["ST-JKLM-NPQR-STUV-WXYZ~item~item"], ItemGroups = ["ST-JKLM-NPQR-STUV-WXYZ~item_group~item_group"], Sessions = ["ST-JKLM-NPQR-STUV-WXYZ~session~session"], Tasks = ["ST-JKLM-NPQR-STUV-WXYZ~task~task"] };
        membershipStore.Memberships.Replace(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH", membership.OwnedResources, references));
        new CanonicalStoryActorLifecycleService(membershipStore).CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~native", "本地角色");
        new SettingsService(Path.Combine(target, "settings.json")).Save(new StudioSettings { LastProject = fixture.A, Theme = ThemePreference.Dark, WindowMaximized = true });
    }

    [TestMethod]
    public void LocalReferenceDirectoryUsesOwnerFoldersAndTypedServicesWithUndoAndExport()
    {
        using var fixture = new Fixture();
        const string source = "ST-3456-789A-BCDE-FGHJ";
        const string borrower = "ST-4567-89AB-CDEF-GHJK";
        var store = new CanonicalProjectGraphStore(fixture.A);
        foreach (var id in new[] { source, borrower })
        {
            store.Stories.Create(new(GraphResourceKind.Story, id, id == source ? "本地来源" : "借用故事", new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
            store.Memberships.Create(new(id, new()));
        }
        var actors = new CanonicalStoryActorLifecycleService(store);
        actors.CreateOwned(source, CanonicalStoryActorKind.Individual, source + "~actor~boss", "同名资源");
        actors.CreateOwned(source, CanonicalStoryActorKind.Collective, source + "~actor~group", "同名资源");
        var items = new CanonicalStoryItemLifecycleService(store);
        items.CreateOwnedItem(source, source + "~item~item", "同名资源");
        items.CreateOwnedGroup(source, source + "~item_group~group", "同名资源");
        var graphs = new CanonicalStoryResourceLifecycleService(store);
        graphs.CreateOwnedSession(source, source + "~session~session", "同名资源");
        var task = graphs.CreateOwnedTask(source, source + "~task~task", "同名资源");
        task.Graph = new GraphDocument(); store.Tasks.Replace(task);
        actors.AddReference(borrower, source + "~actor~boss");
        var dialogs = new Dialogs();
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A), offlinePackageDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        var ownerChoices = new[] { source + "~actor~boss", source + "~actor~group", source + "~item~item", source + "~item_group~group", source + "~session~session", source + "~task~task" };
        for (var index = 0; index < ownerChoices.Length; index++)
        {
            dialogs.SelectId = ownerChoices[index];
            if (index == 0) shell.AddExternalReference("ST-2345-6789-ABCD-EFGH");
            else
            {
                var folder = index < 2 ? CanonicalStoryFolderKind.Actors : index < 4 ? CanonicalStoryFolderKind.Items
                    : index == 4 ? CanonicalStoryFolderKind.Sessions : CanonicalStoryFolderKind.Tasks;
                shell.CanonicalStoryWorkspace!.SelectFolder(folder);
                shell.CanonicalStoryWorkspace.ReferenceSelectedResourceCommand.Execute(null);
            }
            Assert.IsFalse(shell.StatusMessage.Contains("失败"), shell.StatusMessage);
            Assert.IsFalse(shell.StatusMessage.Contains(source), "No resource UID in operation messages");
            Assert.IsTrue(dialogs.LastChoices.All(choice => choice.SourceStoryId == source && choice.SourceStoryName == "本地来源" && !choice.IsExternal));
            Assert.AreEqual(new[] { 6, 1, 2, 1, 1, 1 }[index], dialogs.LastChoices.Count);
            var workspace = shell.CanonicalStoryWorkspace!;
            Assert.IsTrue(workspace.Folders.SelectMany(folder => folder.Items).Any(item => item.Id == ownerChoices[index]));
            shell.UndoCurrentCommand.Execute(null);
            Assert.IsFalse(workspace.Folders.SelectMany(folder => folder.Items).Any(item => item.Id == ownerChoices[index]));
            shell.RedoCurrentCommand.Execute(null);
            Assert.IsTrue(workspace.Folders.SelectMany(folder => folder.Items).Any(item => item.Id == ownerChoices[index]));
        }
        var loaded = new CanonicalStoryWorkspaceLoader(store).Load("ST-2345-6789-ABCD-EFGH");
        Assert.AreEqual(6, loaded.Actors.Count + loaded.Items.Count + loaded.ItemGroups.Count + loaded.Sessions.Count + loaded.Tasks.Count);
        Assert.IsTrue(shell.CanonicalStoryWorkspace!.ActorItems.All(item => item.IsReadOnly));
        var output = Path.Combine(fixture.Root, "local-reference-roundtrip.dgrs");
        new DgrsStoryPackageExporter(fixture.A).Build("ST-2345-6789-ABCD-EFGH", output, "1.0");
        var package = OfflineDgrsPackageReader.Read(output);
        foreach (var id in ownerChoices) Assert.IsTrue(package.Resources.Any(resource => resource.Id == id));
        var before = File.ReadAllBytes(store.Memberships.GetPath("ST-2345-6789-ABCD-EFGH"));
        Assert.Throws<CanonicalStoryActorLifecycleException>(() => new CanonicalProjectResourceReferenceService(store).AddReference("ST-2345-6789-ABCD-EFGH", DgrResourceKind.Actor, ownerChoices[0], source, false));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Memberships.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.Throws<InvalidOperationException>(() => new CanonicalProjectResourceReferenceService(store).AddReference("ST-2345-6789-ABCD-EFGH", DgrResourceKind.Actor, ownerChoices[0], borrower, false));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Memberships.GetPath("ST-2345-6789-ABCD-EFGH")));
    }

    private sealed class Picker(string root) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => root;
    }

    [TestMethod]
    [DataRow(CanonicalStoryFolderKind.Actors, "引用角色")]
    [DataRow(CanonicalStoryFolderKind.Items, "引用物品")]
    [DataRow(CanonicalStoryFolderKind.Sessions, "引用会话")]
    [DataRow(CanonicalStoryFolderKind.Tasks, "引用任务")]
    public void TypedReferenceDirectoryIncludesOnlyRequestedProviderKinds(CanonicalStoryFolderKind folder, string title)
    {
        using var fixture = new Fixture();
        var dialogs = new Dialogs();
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A), offlinePackageDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ReferencePackageFromFile(fixture.Package);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "ST-2345-6789-ABCD-EFGH"));
        Assert.IsTrue(shell.CanonicalStoryWorkspace!.RequestReference(folder));
        Assert.AreEqual(title, dialogs.LastTitle);
        var allowed = folder switch
        {
            CanonicalStoryFolderKind.Actors => new[] { "Actor" },
            CanonicalStoryFolderKind.Items => new[] { "Item", "ItemGroup" },
            CanonicalStoryFolderKind.Sessions => new[] { "Session" },
            _ => new[] { "Task" },
        };
        Assert.IsNotEmpty(dialogs.LastChoices);
        Assert.IsTrue(dialogs.LastChoices.All(choice => allowed.Contains(choice.Kind) && choice.IsExternal));
        var picker = new OfflineResourcePickerViewModel(dialogs.LastChoices, title);
        picker.SearchText = "守卫";
        Assert.IsTrue(picker.Folders.SelectMany(group => group.Matches).All(choice => allowed.Contains(choice.Kind)));
    }

    [TestMethod]
    public void TypedReferenceRejectsWrongKindReturnedByPickerWithoutChangingMembership()
    {
        using var fixture = new Fixture();
        var dialogs = new Dialogs();
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A), offlinePackageDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null); shell.ReferencePackageFromFile(fixture.Package);
        shell.AddExternalReference("ST-2345-6789-ABCD-EFGH");
        dialogs.ForcedChoice = dialogs.LastChoices.Single(choice => choice.Kind == "Task");
        var path = new CanonicalProjectGraphStore(fixture.A).Memberships.GetPath("ST-2345-6789-ABCD-EFGH");
        var before = File.ReadAllBytes(path);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "ST-2345-6789-ABCD-EFGH"));
        Assert.IsTrue(shell.CanonicalStoryWorkspace!.RequestReference(CanonicalStoryFolderKind.Actors));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        Assert.IsTrue(shell.Problems.HasErrors);
    }
    private sealed class Dialogs : IOfflinePackageDialogs
    {
        public string? SelectId { get; set; }
        public OfflineResourceChoice? ForcedChoice { get; set; }
        public string? LastTitle { get; private set; }
        public bool RemoveConfirmed { get; set; }
        public IReadOnlyList<OfflineResourceChoice> LastExternal { get; private set; } = [];
        public IReadOnlyList<OfflineResourceChoice> LastChoices { get; private set; } = [];
        public IReadOnlyList<string> Consumers { get; private set; } = [];
        public OfflineResourceChoice? Viewed { get; private set; }
        public string? PickPackageFile() => null;
        public OfflineResourceChoice? PickResource(IReadOnlyList<OfflineResourceChoice> resources, string title)
        {
            LastTitle = title;
            LastChoices = resources;
            LastExternal = resources.Where(choice => choice.IsExternal).ToArray();
            return ForcedChoice ?? resources.SingleOrDefault(choice => choice.Id == SelectId);
        }
        public void ShowReadOnlyResource(OfflineResourceChoice choice) => Viewed = choice;
        public bool ConfirmRemoval(string package, IReadOnlyList<string> consumers)
        { Consumers = consumers; return consumers.Count == 0 || RemoveConfirmed; }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly bool _keep;
        public Fixture(string? root = null, bool keep = false)
        {
            Root = root ?? Path.Combine("E:/Java/MinecraftMod/DarkGreyRPG/.tooling/0.3.2.0_B4/offline-collaboration", "test-" + Guid.NewGuid().ToString("N"));
            _keep = keep;
            A = Path.Combine(Root, "ProjectA");
            var b = Path.Combine(Root, "ProjectB");
            var service = new ProjectService();
            OriginA = service.CreateProject(A, "project_a", "Project A").Project.ProjectOriginCode!;
            OriginB = service.CreateProject(b, "project_b", "Project B").Project.ProjectOriginCode!;
            foreach (var (directory, ns) in new[] { (A, "A"), (b, "B") })
            {
                var store = new CanonicalProjectGraphStore(directory);
                store.Stories.Create(new GraphResourceEnvelope(GraphResourceKind.Story, ns == "A" ? "ST-2345-6789-ABCD-EFGH" : "ST-JKLM-NPQR-STUV-WXYZ", ns + " 故事",
                    new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
                store.Memberships.Create(new CanonicalStoryMembershipManifest(ns == "A" ? "ST-2345-6789-ABCD-EFGH" : "ST-JKLM-NPQR-STUV-WXYZ",
                    new CanonicalStoryMembershipSet { Actors = ns == "B" ? ["ST-JKLM-NPQR-STUV-WXYZ~actor~boss"] : [] }));
            }
            var actors = new ActorRepository(b);
            var actor = actors.CreateIndividual("ST-JKLM-NPQR-STUV-WXYZ~actor~boss", "真实角色名称");
            actor.HomeStoryId = "ST-JKLM-NPQR-STUV-WXYZ";
            actors.SaveActor(actor);
            var group = actors.CreateCollective("ST-JKLM-NPQR-STUV-WXYZ~actor~group", "守卫组");
            group.HomeStoryId = "ST-JKLM-NPQR-STUV-WXYZ";
            actors.SaveActor(group);
            var bStore = new CanonicalProjectGraphStore(b);
            var member = bStore.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ");
            var owned = member.OwnedResources;
            owned.Actors.Add("ST-JKLM-NPQR-STUV-WXYZ~actor~group");
            bStore.Memberships.Replace(new CanonicalStoryMembershipManifest(member.StoryId, owned, member.ReferencedResources));
            var itemService = new CanonicalStoryItemLifecycleService(bStore);
            itemService.CreateOwnedItem("ST-JKLM-NPQR-STUV-WXYZ", "ST-JKLM-NPQR-STUV-WXYZ~item~item", "任务钥匙");
            itemService.CreateOwnedGroup("ST-JKLM-NPQR-STUV-WXYZ", "ST-JKLM-NPQR-STUV-WXYZ~item_group~item_group", "任务物品组");
            var graphService = new CanonicalStoryResourceLifecycleService(bStore);
            graphService.CreateOwnedSession("ST-JKLM-NPQR-STUV-WXYZ", "ST-JKLM-NPQR-STUV-WXYZ~session~session", "守卫会话");
            var task = graphService.CreateOwnedTask("ST-JKLM-NPQR-STUV-WXYZ", "ST-JKLM-NPQR-STUV-WXYZ~task~task", "守卫任务");
            // This fixture exercises package ownership, using a valid empty Task rather than an unfinished objective draft.
            task.Graph = new GraphDocument();
            bStore.Tasks.Replace(task);
            Package = Path.Combine(Root, "B-v1.dgrs");
            new DgrsStoryPackageExporter(b).Build("ST-JKLM-NPQR-STUV-WXYZ", Package, "1.0");
        }
        public string Root { get; }
        public string A { get; }
        public string Package { get; }
        public string OriginA { get; }
        public string OriginB { get; }
        public void Dispose() { if (!_keep && Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
