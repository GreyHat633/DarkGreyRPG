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
        var dialogs = new Dialogs { SelectId = "B:boss" };
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A), offlinePackageDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ReferencePackageFromFile(fixture.Package);
        shell.AddExternalReference("A:story");
        var workspace = shell.CanonicalStoryWorkspace!;
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "new-stop")));
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
    public void IdentityRenameUpdatesTypedReferencesAndRejectsCollisions()
    {
        using var fixture = new Fixture();
        var root = Path.Combine(fixture.Root, "ProjectB");
        var migration = new NamespaceProjectMigrationService();
        var store = new CanonicalProjectGraphStore(root);
        var story = store.Stories.Load("B:story");
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "session", "session-link");
        node.Properties["resource_id"] = JsonSerializer.SerializeToElement("B:session");
        story.Graph!.Nodes.Add(node);
        store.Stories.Replace(story);
        foreach (var (kind, id) in new[] {
            (DgrResourceKind.Actor, "B:boss"), (DgrResourceKind.Actor, "B:group"),
            (DgrResourceKind.Item, "B:item"), (DgrResourceKind.ItemGroup, "B:item_group"),
            (DgrResourceKind.Session, "B:session"), (DgrResourceKind.Task, "B:task") })
        {
            var next = id + "Renamed";
            var preview = migration.PreviewResourceRename(root, kind, id, next, "重命名资源");
            migration.Apply(preview);
            var snapshot = NamespaceProjectMigrationService.ReadProject(root);
            Assert.IsTrue(NamespaceMigrationPlanner.Inventory(snapshot).Contains(new DgrResourceKey(kind, next)));
            Assert.IsFalse(NamespaceMigrationPlanner.Inventory(snapshot).Contains(new DgrResourceKey(kind, id)));
            Assert.IsTrue(NamespaceMigrationPlanner.Members(store.Memberships.Load("B:story").OwnedResources).Contains(new DgrResourceKey(kind, next)));
            migration.Undo(preview);
        }
        Assert.Throws<InvalidOperationException>(() => migration.PreviewResourceRename(root, DgrResourceKind.Actor, "B:boss", "B:group"));
        var caseOnly = migration.PreviewResourceRename(root, DgrResourceKind.Actor, "B:boss", "B:Boss");
        migration.Apply(caseOnly);
        Assert.AreEqual("B:Boss", new ActorRepository(root).LoadActor("B:Boss").Id);
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
        foreach (var id in new[] { "B:boss", "B:group", "B:item", "B:item_group", "B:session", "B:task" })
        {
            dialogs.SelectId = id;
            shell.AddExternalReference("A:story");
        }
        var workspace = shell.CanonicalStoryWorkspace!;
        var store = new CanonicalProjectGraphStore(fixture.A);
        var saved = File.ReadAllBytes(store.Stories.GetPath("A:story"));
        workspace.StoryEditor.Document.SetDisplayName("未保存的故事标题");
        workspace.StoryEditor.RefreshFromDocument();
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        foreach (var id in new[] { "B:boss", "B:group", "B:item", "B:item_group", "B:session", "B:task" })
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
        shell.RemoveReferencedPackage("B:story");
        Assert.IsEmpty(shell.ReferencedPackages);
        Assert.AreSame(workspace, shell.CanonicalStoryWorkspace);
        Assert.AreEqual("未保存的故事标题", workspace.StoryEditor.DisplayName);
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        CollectionAssert.AreEqual(saved, File.ReadAllBytes(store.Stories.GetPath("A:story")));
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
        CollectionAssert.AreEqual(new[] { "角色", "物品", "会话", "任务" }, row.Folders.Select(folder => folder.DisplayName).ToArray());
        Assert.AreEqual(row.Resources.Count, row.Folders.Sum(folder => folder.Items.Count));
        Assert.IsTrue(row.Folders[0].Items.All(item => item.Resource.Kind == "Actor"));
        Assert.IsTrue(row.Folders[1].Items.All(item => item.Resource.Kind is "Item" or "ItemGroup"));
        var empty = new ReferencedPackageRow("empty", "empty", "", [], new RelayCommand(() => { }));
        Assert.HasCount(4, empty.Folders);
        Assert.IsTrue(empty.Folders.All(folder => !folder.HasResources));
        row.SelectCommand!.Execute(null);
        row.Resources.Single(resource => resource.Resource.Id == "B:boss").ViewCommand.Execute(null);
        Assert.AreEqual("B:boss", shell.SelectedReferencedResource?.Id);
        Assert.IsNull(dialogs.Viewed);
        foreach (var folder in row.Folders.Skip(2))
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
            node.Properties["resource_id"] = JsonSerializer.SerializeToElement("B:" + type);
            var host = new DarkGreyRPG.Studio.ViewModels.Graph.GraphEditorHostViewModel(new GraphDocument([node]), GraphScope.StoryFlow);
            Assert.IsTrue(viewer.TryOpenSubgraph(host.Nodes.Single()));
            Assert.AreEqual("B:" + type, viewer.Choice.Id);
            Assert.IsTrue(viewer.BackCommand.CanExecute(null));
            viewer.BackCommand.Execute(null);
            Assert.AreEqual("B:story", viewer.Choice.Id);
        }
    }

    [TestMethod]
    public void ReferencePickerConversionAndReexportUseNativeOwnershipAndDestinationOrigin()
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
        StringAssert.Contains(packageRow.Details, "Story ID：B:story");
        Assert.IsFalse(packageRow.Details.Contains(fixture.OriginB));
        Assert.IsFalse(shell.ProjectHome.Stories.Any(story => story.Id == "B:story"));
        dialogs.SelectId = "B:boss";
        shell.AddExternalReference("A:story");
        Assert.AreEqual("真实角色名称", dialogs.LastExternal.Single(choice => choice.Id == "B:boss").DisplayName);
        var source = dialogs.LastExternal.Single(choice => choice.Id == "B:boss").Provider;
        StringAssert.Contains(source, ".dgrs");
        StringAssert.Contains(source, "Story ID：B:story");
        Assert.IsFalse(source.Contains(fixture.OriginB));
        var membership = new CanonicalProjectGraphStore(fixture.A).Memberships.Load("A:story");
        CollectionAssert.Contains(membership.ReferencedResources.Actors, "B:boss");
        Assert.IsFalse(membership.OwnedResources.Actors.Contains("B:boss"));
        Assert.IsTrue(shell.CanonicalStoryWorkspace!.ActorItems.Single().IsReadOnly);
        foreach (var id in new[] { "B:session", "B:task" })
        {
            dialogs.SelectId = id;
            shell.AddExternalReference("A:story");
            var workspace = shell.CanonicalStoryWorkspace!;
            var graph = workspace.SessionItems.Concat(workspace.TaskItems).Single(item => item.Id == id);
            workspace.SelectTreeItem(graph);
            Assert.AreEqual("[引用] ", graph.SourceText);
            StringAssert.Contains(workspace.InspectorOwnershipText, "Story ID：B:story");
            StringAssert.Contains(workspace.InspectorOwnershipText, ".dgrs");
        }
        shell.ImportPackageFromFile(fixture.Package);
        Assert.IsEmpty(shell.ReferencedPackages);
        Assert.IsTrue(shell.ProjectHome.Stories.Single(story => story.Id == "B:story").HasCanonicalStory);
        Assert.AreEqual("B:story", shell.CanonicalStoryWorkspace!.StoryEditor.Id);
        Assert.IsTrue(NamespacePolicyStore.Load(fixture.A)!.IsCustom("B:story"));
        var importedStore = new CanonicalProjectGraphStore(fixture.A);
        Assert.AreEqual("守卫会话", importedStore.Sessions.Load("B:session").DisplayName);
        Assert.AreEqual("守卫任务", importedStore.Tasks.Load("B:task").DisplayName);
        Assert.AreEqual("任务钥匙", new DarkGreyRPG.Studio.Core.Items.ItemRepository(fixture.A).LoadItem("B:item").DisplayName);
        Assert.AreEqual("任务物品组", new DarkGreyRPG.Studio.Core.Items.ItemRepository(fixture.A).LoadGroup("B:item_group").DisplayName);
        Assert.IsFalse(new CanonicalStoryWorkspaceLoader(new(fixture.A)).Load("A:story").Actors.Single().IsMissing);
        var output = Path.Combine(fixture.Root, "fork.dgrs");
        new DgrsStoryPackageExporter(fixture.A).Build("B:story", output);
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
        var dialogs = new Dialogs { SelectId = "B:boss" };
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A), offlinePackageDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ReferencePackageFromFile(fixture.Package);
        shell.AddExternalReference("A:story");
        shell.RemoveReferencedPackage("B:story");
        Assert.HasCount(1, shell.ReferencedPackages, shell.StatusMessage);
        CollectionAssert.Contains(dialogs.Consumers.ToArray(), "A:story");
        dialogs.RemoveConfirmed = true;
        shell.RemoveReferencedPackage("B:story");
        Assert.IsEmpty(shell.ReferencedPackages);
        Assert.AreEqual("B:boss", shell.CanonicalStoryWorkspace!.MissingItems.Single().Id);
    }

    [TestMethod]
    public void EmitIsolatedLiveFixturesWhenRequested()
    {
        var target = Environment.GetEnvironmentVariable("DGR_OFFLINE_LIVE_FIXTURE");
        if (string.IsNullOrWhiteSpace(target)) return;
        using var fixture = new Fixture(target, keep: true);
        var settings = new SettingsService(Path.Combine(fixture.Root, "settings.json"));
        settings.Save(new StudioSettings { GlobalNamespace = "A", LastProject = fixture.A, Theme = ThemePreference.Dark, WindowWidth = 1420, WindowHeight = 900 });
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
        var actor = actors.ListActors().Any(value => value.Id == "B:boss")
            ? actors.LoadActor("B:boss") : actors.CreateIndividual("B:boss", "真实角色名称");
        actor.HomeStoryId = "B:story";
        actor.DisplayName = "更新后的守卫首领";
        actors.SaveActor(actor);
        new DgrsStoryPackageExporter(b).Build("B:story", Path.Combine(target, "renamed-v2.dgrs"), "2.0");
        var store = new CanonicalProjectGraphStore(b);
        var membership = store.Memberships.Load("B:story");
        var owned = membership.OwnedResources;
        owned.Actors.Remove("B:boss");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(membership.StoryId, owned, membership.ReferencedResources));
        actors.DeleteActor("B:boss");
        new DgrsStoryPackageExporter(b).Build("B:story", Path.Combine(target, "missing-v3.dgrs"), "3.0");
    }

    [TestMethod]
    public void EmitReferenceFlowFixtureWhenRequested()
    {
        var target = Environment.GetEnvironmentVariable("DGR_REFERENCE_FLOW_FIXTURE");
        if (string.IsNullOrWhiteSpace(target)) return;
        using var fixture = new Fixture(target, keep: true);
        var store = new CanonicalProjectGraphStore(Path.Combine(target, "ProjectB"));
        var story = store.Stories.Load("B:story");
        var graph = story.Graph!;
        foreach (var type in new[] { "session", "task" })
        {
            var node = GraphNodeFactory.Create(GraphScope.StoryFlow, type, type, type == "session" ? "会话入口" : "任务入口");
            node.Properties["resource_id"] = JsonSerializer.SerializeToElement("B:" + type);
            graph.Nodes.Add(node);
        }
        story.Graph = graph;
        store.Stories.Replace(story);
        new DgrsStoryPackageExporter(store.ProjectDirectory).Build("B:story", fixture.Package, "1.0");
        var shell = new ShellViewModel(new ProjectService(), new Picker(fixture.A));
        shell.OpenProjectCommand.Execute(null);
        shell.ReferencePackageFromFile(fixture.Package);
        var membershipStore = new CanonicalProjectGraphStore(fixture.A);
        var membership = membershipStore.Memberships.Load("A:story");
        var references = new CanonicalStoryMembershipSet { Actors = ["B:boss", "B:group"], Items = ["B:item"], ItemGroups = ["B:item_group"], Sessions = ["B:session"], Tasks = ["B:task"] };
        membershipStore.Memberships.Replace(new CanonicalStoryMembershipManifest("A:story", membership.OwnedResources, references));
        new CanonicalStoryActorLifecycleService(membershipStore).CreateOwned("A:story", "A:native", "本地角色");
        new SettingsService(Path.Combine(target, "settings.json")).Save(new StudioSettings { GlobalNamespace = "A", LastProject = fixture.A, Theme = ThemePreference.Dark, WindowMaximized = true });
    }

    private sealed class Picker(string root) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => root;
    }
    private sealed class Dialogs : IOfflinePackageDialogs
    {
        public string? SelectId { get; set; }
        public bool RemoveConfirmed { get; set; }
        public IReadOnlyList<OfflineResourceChoice> LastExternal { get; private set; } = [];
        public IReadOnlyList<string> Consumers { get; private set; } = [];
        public OfflineResourceChoice? Viewed { get; private set; }
        public string? PickPackageFile() => null;
        public OfflineResourceChoice? PickResource(IReadOnlyList<OfflineResourceChoice> native, IReadOnlyList<OfflineResourceChoice> external, string title)
        {
            LastExternal = external;
            return external.SingleOrDefault(choice => choice.Id == SelectId);
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
            Root = root ?? Path.Combine("E:/Java/MinecraftMod/DarkGrey_RPG/.tooling/0.3.2.0_B4/offline-collaboration", "test-" + Guid.NewGuid().ToString("N"));
            _keep = keep;
            A = Path.Combine(Root, "ProjectA");
            var b = Path.Combine(Root, "ProjectB");
            var service = new ProjectService();
            OriginA = service.CreateProject(A, "project_a", "Project A").Project.ProjectOriginCode!;
            OriginB = service.CreateProject(b, "project_b", "Project B").Project.ProjectOriginCode!;
            foreach (var (directory, ns) in new[] { (A, "A"), (b, "B") })
            {
                var store = new CanonicalProjectGraphStore(directory);
                store.Stories.Create(new GraphResourceEnvelope(GraphResourceKind.Story, ns + ":story", ns + " 故事",
                    new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
                store.Memberships.Create(new CanonicalStoryMembershipManifest(ns + ":story",
                    new CanonicalStoryMembershipSet { Actors = ns == "B" ? ["B:boss"] : [] }));
                var policy = Path.Combine(directory, NamespacePolicyStore.RelativePath);
                File.WriteAllBytes(policy, NamespacePolicyStore.Encode(new NamespacePolicy(ns)));
            }
            var actors = new ActorRepository(b);
            var actor = actors.CreateIndividual("B:boss", "真实角色名称");
            actor.HomeStoryId = "B:story";
            actors.SaveActor(actor);
            var group = actors.CreateCollective("B:group", "守卫组");
            group.HomeStoryId = "B:story";
            actors.SaveActor(group);
            var bStore = new CanonicalProjectGraphStore(b);
            var member = bStore.Memberships.Load("B:story");
            var owned = member.OwnedResources;
            owned.Actors.Add("B:group");
            bStore.Memberships.Replace(new CanonicalStoryMembershipManifest(member.StoryId, owned, member.ReferencedResources));
            var itemService = new CanonicalStoryItemLifecycleService(bStore);
            itemService.CreateOwnedItem("B:story", "B:item", "任务钥匙");
            itemService.CreateOwnedGroup("B:story", "B:item_group", "任务物品组");
            var graphService = new CanonicalStoryResourceLifecycleService(bStore);
            graphService.CreateOwnedSession("B:story", "B:session", "守卫会话");
            graphService.CreateOwnedTask("B:story", "B:task", "守卫任务");
            Package = Path.Combine(Root, "B-v1.dgrs");
            new DgrsStoryPackageExporter(b).Build("B:story", Package, "1.0");
        }
        public string Root { get; }
        public string A { get; }
        public string Package { get; }
        public string OriginA { get; }
        public string OriginB { get; }
        public void Dispose() { if (!_keep && Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
