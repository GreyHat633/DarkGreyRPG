using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CurrentGraphIdentity0336Tests
{
    private static readonly StoryUid Owner = StoryUid.Parse("ST-2345-6789-ABCD-EFGH");
    [TestMethod]
    public void CurrentSingleContainerExportsAndReopensWithStructuredResources()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        new CanonicalStoryLifecycleService(store).Create(Owner.Value, "容器故事");
        var actor = new ResourceAddress(Owner, ResourceKind.Actor, "actor").ToKey();
        new CanonicalStoryActorLifecycleService(store).CreateOwned(Owner.Value, CanonicalStoryActorKind.Individual, actor, "角色");
        CurrentProjectValidator.Validate(CurrentProjectInventory.Read(project.Root));
        var path = Path.Combine(project.Root, "export", "current.dgrs");
        var exported = new DarkGreyRPG.Studio.Core.Packaging.DgrsStoryPackageExporter(project.Root).Build(Owner.Value, path, "0.3.3.6");
        Assert.AreEqual(2, exported.Manifest.FormatVersion);
        var reopened = DarkGreyRPG.Studio.Core.Packaging.OfflineDgrsPackageReader.Read(path);
        Assert.IsTrue(reopened.Resources.Any(resource => resource.Id == actor));
        Assert.AreEqual(Owner.Value, reopened.Manifest.StoryId);
        void RejectMutated(string name, Action<System.IO.Compression.ZipArchive> mutate)
        {
            var candidate = Path.Combine(project.Root, "export", name + ".dgrs");
            File.Copy(path, candidate);
            using (var zip = System.IO.Compression.ZipFile.Open(candidate, System.IO.Compression.ZipArchiveMode.Update)) mutate(zip);
            Assert.ThrowsExactly<DarkGreyRPG.Studio.Core.Packaging.StoryPackageException>(() => DarkGreyRPG.Studio.Core.Packaging.DgrsPackageValidator.Validate(candidate));
            Assert.AreEqual(Owner.Value, DarkGreyRPG.Studio.Core.Packaging.DgrsPackageValidator.Validate(path).Manifest.StoryId);
        }
        RejectMutated("extra", zip => { using var writer = new StreamWriter(zip.CreateEntry("extra.json").Open()); writer.Write("{}"); });
        RejectMutated("missing", zip => zip.GetEntry(exported.Manifest.RequiredResources.Actors.Single())!.Delete());
        RejectMutated("hidden_missing", zip =>
        {
            zip.GetEntry(exported.Manifest.RequiredResources.Actors.Single())!.Delete();
            var metadata = JsonNode.Parse(exported.Manifest.ToJson())!.AsObject();
            metadata["required_resources"]!["actors"] = new JsonArray();
            zip.GetEntry("manifest.json")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open());
            writer.Write(metadata.ToJsonString());
        });
        using var consumerProject = new TestProjectDirectory(createProjectFile: false);
        new DarkGreyRPG.Studio.Core.Packaging.OfflineReferencePackageService().AddOrUpdate(consumerProject.Root, path);
        var consumerStore = new CanonicalProjectGraphStore(consumerProject.Root);
        var consumerStory = new CanonicalStoryLifecycleService(consumerStore).CreateNew("引用故事");
        new CanonicalExternalReferenceService(consumerStore).AddReference(consumerStory.Id, DgrResourceKind.Actor, actor);
        var consumerPath = Path.Combine(consumerProject.Root, "export", "consumer.dgrs");
        new DarkGreyRPG.Studio.Core.Packaging.DgrsStoryPackageExporter(consumerProject.Root).Build(consumerStory.Id, consumerPath, "0.3.3.6");
        var consumerPackage = DarkGreyRPG.Studio.Core.Packaging.OfflineDgrsPackageReader.Read(consumerPath);
        Assert.IsTrue(consumerPackage.Resources.Any(resource => resource.Id == actor));
        Assert.AreEqual(0, new ActorRepository(consumerProject.Root).ListActors().Count);
        using var importProject = new TestProjectDirectory(createProjectFile: false);
        var imported = new DarkGreyRPG.Studio.Core.Packaging.OfflineStoryPackageImportService().Import(importProject.Root, consumerPath);
        Assert.AreNotEqual(consumerStory.Id, imported.ImportedStoryId);
        var importedStore = new CanonicalProjectGraphStore(importProject.Root);
        Assert.AreEqual(actor, importedStore.Memberships.Load(imported.ImportedStoryId).ReferencedResources.Actors.Single());
        Assert.IsNotNull(imported.ReferencedPackagePath);
        Assert.IsEmpty(DarkGreyRPG.Studio.Core.Packaging.OfflineProviderCatalog.Load(importProject.Root).Diagnostics);
        var reexport = Path.Combine(importProject.Root, "export", "copied.dgrs");
        new DarkGreyRPG.Studio.Core.Packaging.DgrsStoryPackageExporter(importProject.Root).Build(imported.ImportedStoryId, reexport, "0.3.3.6");
        Assert.IsTrue(DarkGreyRPG.Studio.Core.Packaging.OfflineDgrsPackageReader.Read(reexport).Resources.Any(resource => resource.Id == actor));
        var manifest = JsonNode.Parse(exported.Manifest.ToJson())!.AsObject();
        manifest.Remove("identity_format");
        Assert.ThrowsExactly<DarkGreyRPG.Studio.Core.Packaging.StoryPackageException>(() => DarkGreyRPG.Studio.Core.Packaging.StoryPackageManifest.Parse(manifest.ToJsonString()));
    }
    [TestMethod]
    public void GraphReferencesAndDynamicContentUseStructuredWireAddresses()
    {
        var actor = new ResourceAddress(Owner, ResourceKind.Actor, "target");
        var session = new ResourceAddress(Owner, ResourceKind.Session, "session");
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        line.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement(actor.ToKey());
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Session, session.ToKey(), "对话", new GraphDocument([line]));
        var json = JsonNode.Parse(envelope.ToJson())!.AsObject();
        Assert.AreEqual(Owner.Value, json["graph"]!["nodes"]![0]!["properties"]!["speaker_actor_id"]!["story_uid"]!.GetValue<string>());
        Assert.AreEqual(actor.ToKey(), GraphResourceEnvelope.FromJson(json.ToJsonString()).Graph!.Nodes.Single().Properties["speaker_actor_id"].GetString());
        json["graph"]!["nodes"]![0]!["properties"]!["speaker_actor_id"] = actor.ToKey();
        Assert.ThrowsExactly<GraphResourceEnvelopeException>(() => GraphResourceEnvelope.FromJson(json.ToJsonString()));
        var text = DynamicContentText.Encode([new(Text: "名称："), new(Type: "actor_name", ActorId: actor.ToKey())]);
        Assert.AreEqual(actor.ToKey(), DynamicContentText.ActorReferences(text).Single());
        Assert.IsTrue(text.Contains("story_uid", StringComparison.Ordinal));
        Assert.ThrowsExactly<FormatException>(() => DynamicContentText.Parse("\u001eDGR1\u001f[]"));
        Assert.AreEqual("\u001eDGR1\u001f[]", DynamicContentText.Parse(DynamicContentText.Encode([new(Text: "\u001eDGR1\u001f[]")])).Single().Text);
    }
    [TestMethod]
    public void OwnedResourceCreationAndForeignReferencesPreserveOwners()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var stories = new CanonicalStoryLifecycleService(store);
        stories.Create(Owner.Value, "所有者");
        var consumer = stories.CreateNew("引用者").Id;
        var actor = new ResourceAddress(Owner, ResourceKind.Actor, "shared").ToKey();
        var item = new ResourceAddress(Owner, ResourceKind.Item, "shared").ToKey();
        var session = new ResourceAddress(Owner, ResourceKind.Session, "shared").ToKey();
        var actors = new CanonicalStoryActorLifecycleService(store);
        var items = new CanonicalStoryItemLifecycleService(store);
        var graphs = new CanonicalStoryResourceLifecycleService(store);
        actors.CreateOwned(Owner.Value, CanonicalStoryActorKind.Individual, actor, "角色");
        items.CreateOwned(Owner.Value, CanonicalStoryItemKind.Individual, item, "物品");
        graphs.CreateOwned(Owner.Value, GraphResourceKind.Session, session, "会话");
        actors.AddReference(consumer, actor);
        graphs.AddReference(consumer, GraphResourceKind.Session, session);
        Assert.AreEqual(actor, store.Memberships.Load(consumer).ReferencedResources.Actors.Single());
        Assert.AreEqual(session, store.Memberships.Load(consumer).ReferencedResources.Sessions.Single());
        Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(() => actors.CreateOwned(consumer, CanonicalStoryActorKind.Individual, actor, "错误所有者"));
        Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(() => items.CreateOwned(consumer, CanonicalStoryItemKind.Individual, item, "错误所有者"));
        Assert.ThrowsExactly<CanonicalStoryResourceLifecycleException>(() => graphs.CreateOwned(consumer, GraphResourceKind.Session, session, "错误所有者"));
    }
    [TestMethod]
    public void ActorAndItemRepositoriesUseStructuredAddressesAndOwnerPaths()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var actors = new ActorRepository(project.Root);
        var items = new ItemRepository(project.Root);
        var actorAddress = new ResourceAddress(Owner, ResourceKind.Actor, "same");
        var itemAddress = new ResourceAddress(Owner, ResourceKind.Item, "same");
        var groupAddress = new ResourceAddress(Owner, ResourceKind.ItemGroup, "same");
        actors.SaveActor(actors.CreateIndividual(actorAddress.ToKey(), "角色"));
        items.SaveItem(items.CreateItem(itemAddress.ToKey(), "物品"));
        items.SaveGroup(items.CreateGroup(groupAddress.ToKey(), "物品组"));
        Assert.AreEqual(Owner.Value, actors.LoadActor(actorAddress.ToKey()).HomeStoryId);
        Assert.AreEqual(itemAddress.ToKey(), items.LoadItem(itemAddress.ToKey()).Id);
        Assert.AreEqual(groupAddress.ToKey(), items.LoadGroup(groupAddress.ToKey()).Id);
        using var json = JsonDocument.Parse(File.ReadAllText(actors.GetActorPath(actorAddress.ToKey())));
        Assert.AreEqual(JsonValueKind.Object, json.RootElement.GetProperty("npc_id").ValueKind);
        var malformed = JsonNode.Parse(json.RootElement.GetRawText())!.AsObject();
        malformed["npc_id"] = actorAddress.ToKey();
        Assert.ThrowsExactly<ActorDataException>(() => ActorSerializer.Deserialize(malformed.ToJsonString()));
        var actor = new IndividualActorResource { NpcId = actorAddress.ToKey(), DisplayName = "wrong", HomeStoryId = "ST-JKLM-NPQR-STUV-WXYZ" };
        Assert.ThrowsExactly<ActorValidationException>(() => ActorSerializer.Serialize(actor, ActorIdPolicy.NewResource));
        Assert.ThrowsExactly<ItemValidationException>(() => ItemSerializer.Serialize(new IndividualItemResource { ItemId = groupAddress.ToKey(), DisplayName = "wrong" }));
    }
    [TestMethod]
    public void ProjectGateRejectsOldOrMissingMarkersWithoutMutation()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var service = new ProjectService();
        service.CreateProject(project.Root, "current", "当前项目");
        var path = Path.Combine(project.Root, "project.json");
        var baseline = File.ReadAllText(path);
        Assert.AreEqual(ProjectResource.CurrentIdentityFormat, service.OpenProject(project.Root).Project.IdentityFormat);
        foreach (Action<JsonObject> change in new Action<JsonObject>[] {
            root => root["schema_version"] = 2,
            root => root.Remove("schema_version"),
            root => root.Remove("identity_format"),
            root => root["identity_format"] = "namespace" })
        {
            var json = JsonNode.Parse(baseline)!.AsObject(); change(json);
            File.WriteAllText(path, json.ToJsonString());
            var before = File.ReadAllBytes(path);
            Assert.ThrowsExactly<ProjectException>(() => service.OpenProject(project.Root));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        }
    }
    [TestMethod]
    public void NewStoryLifecycleUsesImmutableUidAndCompensatesFailedDeletion()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var lifecycle = new CanonicalStoryLifecycleService(store);
        var first = lifecycle.CreateNew("同名故事");
        var second = lifecycle.CreateNew("同名故事");
        Assert.IsTrue(StoryUid.IsValid(first.Id));
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => lifecycle.Create("Author:story", "旧身份"));
        var before = File.ReadAllBytes(store.Stories.GetPath(first.Id));
        var deletions = 0;
        var failing = new CanonicalStoryLifecycleService(store, deleteFile: path =>
        {
            if (++deletions == 2) throw new IOException("injected deletion failure");
            File.Delete(path);
        });
        Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => failing.Delete(first.Id));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Stories.GetPath(first.Id)));
        Assert.AreEqual(first.Id, store.Memberships.Load(first.Id).StoryId);
        lifecycle.Delete(first.Id);
        Assert.AreEqual(second.Id, store.Stories.List().Single().Id);
    }

    [TestMethod]
    public void GraphRepositoriesPersistUidAndStructuredOwnedAddress()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var address = new ResourceAddress(Owner, ResourceKind.Session, "r17");
        store.Stories.Create(new(GraphResourceKind.Story, Owner.Value, "故事", new GraphDocument()));
        store.Sessions.Create(new(GraphResourceKind.Session, address.ToKey(), "会话", new GraphDocument()));
        Assert.AreEqual(Owner.Value, store.Stories.Load(Owner.Value).Id);
        Assert.AreEqual(address.ToKey(), store.Sessions.Load(address.ToKey()).Id);
        using var json = JsonDocument.Parse(File.ReadAllText(store.Sessions.GetPath(address.ToKey())));
        Assert.AreEqual(JsonValueKind.Object, json.RootElement.GetProperty("id").ValueKind);
        Assert.AreEqual(Owner.Value, json.RootElement.GetProperty("id").GetProperty("story_uid").GetString());
        Assert.ThrowsExactly<GraphResourceRepositoryException>(() => store.Stories.Load("Author:story"));
        Assert.ThrowsExactly<GraphResourceRepositoryException>(() => store.Sessions.Load("session"));
    }

    [TestMethod]
    public void CurrentGraphRejectsOldVersionMarkerStringAddressAndWrongKind()
    {
        var address = new ResourceAddress(Owner, ResourceKind.Task, "r17");
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Task, address.ToKey(), "任务", new GraphDocument());
        var baseline = envelope.ToJson();
        foreach (Action<JsonObject> change in new Action<JsonObject>[] {
            root => root["schema_version"] = "3",
            root => root["schema_version"] = 1,
            root => root.Remove("identity_format"),
            root => root["identity_format"] = "namespace",
            root => root["id"] = "Author:task",
            root => root["id"] = address.ToKey(),
            root => root["id"]!["kind"] = "session" })
        {
            var json = JsonNode.Parse(baseline)!.AsObject(); change(json);
            Assert.ThrowsExactly<GraphResourceEnvelopeException>(() => GraphResourceEnvelope.FromJson(json.ToJsonString()));
        }
    }

    [TestMethod]
    public void MembershipEnforcesOwnerAndKindButKeepsExternalOwner()
    {
        var own = new ResourceAddress(Owner, ResourceKind.Session, "r17");
        var external = new ResourceAddress(StoryUid.Parse("ST-JKLM-NPQR-STUV-WXYZ"), ResourceKind.Actor, "r17");
        var member = new CanonicalStoryMembershipManifest(Owner.Value,
            new() { Sessions = [own.ToKey()] }, new() { Actors = [external.ToKey()] });
        var roundtrip = CanonicalStoryMembershipManifest.FromJson(member.ToJson());
        Assert.AreEqual(external.ToKey(), roundtrip.ReferencedResources.Actors.Single());
        member.OwnedResources = new() { Actors = [external.ToKey()] };
        Assert.ThrowsExactly<CanonicalStoryMembershipException>(() => member.ToJson());
        member.OwnedResources = new() { Actors = [own.ToKey()] };
        Assert.ThrowsExactly<CanonicalStoryMembershipException>(() => member.ToJson());
        var json = JsonNode.Parse(roundtrip.ToJson())!.AsObject();
        json["schema_version"] = 3;
        Assert.ThrowsExactly<CanonicalStoryMembershipException>(() => CanonicalStoryMembershipManifest.FromJson(json.ToJsonString()));
    }
}
