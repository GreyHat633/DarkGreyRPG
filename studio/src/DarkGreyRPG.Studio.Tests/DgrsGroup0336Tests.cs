using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class DgrsGroup0336Tests
{
    [TestMethod]
    public void FlatGroupRoundTripValidatesWholeConnectedComponentAndSharedBytes()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        File.WriteAllText(Path.Combine(project.Root, "project.json"), """{"schema_version":3,"identity_format":"story-uid-v1","id":"group_test","display_name":"Group Test"}""");
        var store = new CanonicalProjectGraphStore(project.Root);
        var lifecycle = new CanonicalStoryLifecycleService(store);
        var a = lifecycle.CreateNew("A"); var b = lifecycle.CreateNew("B"); var c = lifecycle.CreateNew("C");
        string Boundary(GraphResourceEnvelope envelope, string type)
        {
            var node = GraphNodeFactory.Create(GraphScope.StoryFlow, type, Guid.NewGuid().ToString("N"));
            node.Properties["port_id"] = JsonSerializer.SerializeToElement("boundary_" + node.Id);
            node.Properties["display_name"] = JsonSerializer.SerializeToElement(type + " " + node.Id);
            var graph = envelope.Graph!;
            graph.Nodes.Add(node); envelope.Graph = graph; store.Stories.Replace(envelope);
            return node.Properties["port_id"].GetString()!;
        }
        var outA = Boundary(a, "logic_output"); var inB = Boundary(b, "logic_input");
        var outB = Boundary(b, "terminate");
        var cGraph = c.Graph!;
        var inC = StoryStartSchema.ReadTriggers(cGraph.Nodes.Single(node => node.Type == "start")).Single().PortId;
        cGraph.Nodes.Single(node => node.Type == "start").Ports.Clear();
        StoryStartSchema.InitializeDefault(cGraph.Nodes.Single(node => node.Type == "start"), inC, StoryStartSchema.FlowDriven);
        c.Graph = cGraph; store.Stories.Replace(c);
        store.StoryLogicGraph.Save([new(a.Id, outA, b.Id, inB), new(b.Id, outB, c.Id, inC, "Flow")]);
        var actor = new ResourceAddress(StoryUid.Parse(a.Id), ResourceKind.Actor, "shared").ToKey();
        var actors = new CanonicalStoryActorLifecycleService(store);
        actors.CreateOwned(a.Id, CanonicalStoryActorKind.Individual, actor, "Shared");
        actors.AddReference(b.Id, actor);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII=");
        var media = "media/" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(png)) + ".png";
        Directory.CreateDirectory(Path.Combine(project.Root, "resources", "media"));
        File.WriteAllBytes(Path.Combine(project.Root, "resources", media), png);
        var actorRepository = new DarkGreyRPG.Studio.Core.Actors.ActorRepository(project.Root);
        var actorDocument = actorRepository.LoadActor(actor);
        actorDocument.DefaultPortraitRef = media;
        actorRepository.SaveActor(actorDocument);
        var names = new StoryGroupNameStore(project.Root);
        var catalog = StoryGroupCatalog.Derive([a.Id, b.Id, c.Id], store.StoryLogicGraph.Load());
        names.Save(catalog.Rename(catalog.Groups.Single().Key, "完整组合"));
        var path = Path.Combine(project.Root, "Exports", "complete.dgrs.g");
        var result = new DgrsGroupPackageExporter(project.Root).Build(b.Id, path, "0.3.3.6");
        Assert.AreEqual(3, result.Manifest.Members.Count);
        Assert.AreEqual("完整组合", result.Manifest.DisplayName);
        Assert.AreEqual(2, result.Connections.Connections.Count);
        Assert.AreEqual(1, result.Entries.Count(entry => entry.EndsWith("r-shared.json")));
        Assert.IsFalse(result.Entries.Any(entry => entry.EndsWith(".dgrs") || entry.EndsWith(".dgrs.g")));
        Assert.AreEqual(3, DgrsGroupPackageValidator.Validate(path).Manifest.Members.Count);
        var original = File.ReadAllBytes(path);
        var originalFingerprints = OfflineDgrsPackageReader.ReadContainer(path).ToDictionary(member => member.Manifest.StoryId, member => member.Fingerprint);
        var renamed = Path.Combine(project.Root, "Exports", "renamed.dgrs.g"); File.Copy(path, renamed);
        using (var zip = ZipFile.Open(renamed, ZipArchiveMode.Update))
        {
            var metadata = JsonNode.Parse(result.Manifest.ToJson())!; metadata["display_name"] = "只改组名";
            zip.GetEntry("manifest.json")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open()); writer.Write(metadata.ToJsonString());
        }
        foreach (var member in OfflineDgrsPackageReader.ReadContainer(renamed)) Assert.AreEqual(originalFingerprints[member.Manifest.StoryId], member.Fingerprint);
        using (var zip = ZipFile.Open(renamed, ZipArchiveMode.Update))
        {
            var storyPath = result.Manifest.Members.Single(member => member.StoryId == b.Id).RequiredResources.Story;
            var entry = zip.GetEntry(storyPath)!; JsonNode definition;
            using (var reader = new StreamReader(entry.Open())) definition = JsonNode.Parse(reader.ReadToEnd())!;
            definition["display_name"] = "B changed"; entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry(storyPath).Open()); writer.Write(definition.ToJsonString());
        }
        foreach (var member in OfflineDgrsPackageReader.ReadContainer(renamed))
            if (member.Manifest.StoryId == b.Id) Assert.AreNotEqual(originalFingerprints[b.Id], member.Fingerprint);
            else Assert.AreEqual(originalFingerprints[member.Manifest.StoryId], member.Fingerprint);
        using var consumer = new TestProjectDirectory(createProjectFile: false);
        var references = new OfflineReferencePackageService();
        var providers = references.AddOrUpdateContainer(consumer.Root, path);
        Assert.AreEqual(3, providers.Count);
        var providerCatalog = OfflineProviderCatalog.Load(consumer.Root);
        Assert.IsEmpty(providerCatalog.Diagnostics);
        Assert.AreEqual(3, providerCatalog.Find(DgrResourceKind.Story).Count);
        Assert.AreEqual(1, providerCatalog.Find(DgrResourceKind.Actor).Count);
        Assert.AreEqual(1, Directory.GetFiles(Path.Combine(consumer.Root, "references")).Length);
        references.Remove(consumer.Root, b.Id);
        Assert.IsEmpty(OfflineProviderCatalog.Load(consumer.Root).Providers);
        using (var linked = new TestProjectDirectory(createProjectFile: false))
        {
            references.AddOrUpdateContainer(linked.Root, path);
            var linkedStore = new CanonicalProjectGraphStore(linked.Root);
            var local = new CanonicalStoryLifecycleService(linkedStore).CreateNew("本地联动");
            var localGraph = local.Graph!;
            var boundary = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_input", "external-input");
            boundary.Properties["port_id"] = JsonSerializer.SerializeToElement("local_public_output");
            boundary.Properties["display_name"] = JsonSerializer.SerializeToElement("External input");
            localGraph.Nodes.Add(boundary); local.Graph = localGraph; linkedStore.Stories.Replace(local);
            var port = boundary.Properties["port_id"].GetString()!;
            linkedStore.StoryLogicGraph.Save([new(a.Id, outA, local.Id, port)]);
            var linkedPath = Path.Combine(linked.Root, "linked.dgrs.g");
            var linkedResult = new DgrsGroupPackageExporter(linked.Root).Build(local.Id, linkedPath, "0.3.3.6");
            Assert.AreEqual(4, linkedResult.Manifest.Members.Count);
            Assert.AreEqual(3, linkedResult.Connections.Connections.Count);
            CollectionAssert.Contains(linkedResult.Manifest.Members.Select(member => member.StoryId).ToArray(), c.Id);
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }
        var importer = new OfflineStoryPackageImportService();
        var plan = importer.BuildPlan(consumer.Root, path);
        Assert.AreEqual(3, plan.StoryUidMap.Count);
        Assert.IsFalse(plan.StoryUidMap.Keys.Intersect(plan.StoryUidMap.Values).Any());
        importer.Apply(plan);
        CollectionAssert.AreEqual(png, File.ReadAllBytes(Path.Combine(consumer.Root, "resources", media)));
        var importedStore = new CanonicalProjectGraphStore(consumer.Root);
        Assert.AreEqual(3, importedStore.Stories.List().Count);
        Assert.AreEqual(2, importedStore.StoryLogicGraph.Load().Connections.Count);
        var copiedActor = new ResourceAddress(StoryUid.Parse(plan.StoryUidMap[a.Id]), ResourceKind.Actor, "shared").ToKey();
        CollectionAssert.Contains(importedStore.Memberships.Load(plan.StoryUidMap[b.Id]).ReferencedResources.Actors.ToArray(), copiedActor);
        Assert.AreEqual("完整组合", new StoryGroupNameStore(consumer.Root).Load().Groups.Single().DisplayName);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        var second = importer.BuildPlan(consumer.Root, path);
        var sharedMediaChange = second.Changes.Single(change => change.RelativePath.Replace('\\', '/') == "resources/" + media);
        CollectionAssert.AreEqual(png, sharedMediaChange.ExpectedBytes);
        CollectionAssert.AreEqual(png, sharedMediaChange.DesiredBytes);
        Assert.IsFalse(second.StoryUidMap.Values.Intersect(plan.StoryUidMap.Values).Any());
        var snapshot = Directory.GetFiles(consumer.Root, "*", SearchOption.AllDirectories).ToDictionary(file => file, File.ReadAllBytes);
        var writes = 0;
        var failing = new OfflineStoryPackageImportService(new DarkGreyRPG.Studio.Core.IO.ProjectFileTransaction((file, bytes) =>
        {
            if (++writes == 2) throw new IOException("injected import failure");
            File.WriteAllBytes(file, bytes);
        }));
        Assert.ThrowsExactly<IOException>(() => failing.Apply(second));
        CollectionAssert.AreEquivalent(snapshot.Keys.ToArray(), Directory.GetFiles(consumer.Root, "*", SearchOption.AllDirectories));
        foreach (var file in snapshot) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key));
        Assert.ThrowsExactly<StoryPackageException>(() => new DgrsStoryPackageExporter(project.Root).Build(a.Id, Path.Combine(project.Root, "single.dgrs")));
        void Reject(string name, Action<ZipArchive> mutate)
        {
            var bad = Path.Combine(project.Root, "Exports", name + ".dgrs.g"); File.Copy(path, bad);
            using (var zip = ZipFile.Open(bad, ZipArchiveMode.Update)) mutate(zip);
            Assert.ThrowsExactly<StoryPackageException>(() => DgrsGroupPackageValidator.Validate(bad));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }
        Reject("missing_member", zip => zip.GetEntry(result.Manifest.Members[1].RequiredResources.Story)!.Delete());
        Reject("extra", zip => { using var writer = new StreamWriter(zip.CreateEntry("extra.json").Open()); writer.Write("{}"); });
        Reject("disconnected", zip =>
        {
            zip.GetEntry(DgrsGroupManifest.GraphPath)!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry(DgrsGroupManifest.GraphPath).Open());
            writer.Write(JsonSerializer.Serialize(CanonicalStoryLogicGraph.Empty));
        });
        Reject("duplicate_uid", zip =>
        {
            var metadata = JsonNode.Parse(result.Manifest.ToJson())!;
            metadata["members"]![1] = metadata["members"]![0]!.DeepClone();
            zip.GetEntry("manifest.json")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open()); writer.Write(metadata.ToJsonString());
        });
        var draft = store.Stories.Load(a.Id); var draftGraph = draft.Graph!;
        draftGraph.Nodes.Add(GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "duplicate")); draft.Graph = draftGraph; store.Stories.Replace(draft);
        Assert.ThrowsExactly<StoryPackageException>(() => new DgrsGroupPackageExporter(project.Root).Build(a.Id, path, "0.3.3.6"));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }
}
