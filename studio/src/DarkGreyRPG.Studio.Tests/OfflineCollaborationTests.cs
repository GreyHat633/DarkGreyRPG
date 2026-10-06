using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class OfflineCollaborationTests
{
    [TestMethod]
    public void ReferenceCopiesPackageAndCatalogKeepsFullIdentityAndReadOnlyDefinition()
    {
        using var provider = BuildProvider("Guard v1");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard v1");
        var imported = new OfflineReferencePackageService().AddOrUpdate(consumer.Root, package);
        File.Delete(package);

        var catalog = OfflineProviderCatalog.Load(consumer.Root);
        var actor = catalog.Resolve(DgrResourceKind.Actor, "ST-2345-6789-ABCD-EFGH~actor~boss");
        Assert.IsNotNull(actor);
        Assert.AreEqual("Guard v1", actor.DisplayName);
        Assert.AreEqual(ResourceAddress.FromKey(actor.Id).StoryUid.Value, actor.OwnerStoryUid);
        Assert.AreEqual(imported.Fingerprint, catalog.Providers.Single().Fingerprint);
        Assert.IsTrue(actor.IsReadOnly);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~actor~boss", actor.Id);
    }

    [TestMethod]
    public void ImportAllocatesNewIdentitiesAndPreservesOriginalReference()
    {
        using var provider = BuildProvider("Guard");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");
        _ = new OfflineReferencePackageService().AddOrUpdate(consumer.Root, package);

        var plan = new OfflineStoryPackageImportService().BuildPlan(consumer.Root, package);
        Assert.IsTrue(plan.CanApply);
        var referencePath = OfflineProviderCatalog.Load(consumer.Root).Providers.Single().PackagePath;
        var referenceBytes = File.ReadAllBytes(referencePath);
        new OfflineStoryPackageImportService().Apply(plan);

        var store = new CanonicalProjectGraphStore(consumer.Root);
        Assert.AreNotEqual("ST-2345-6789-ABCD-EFGH", plan.ImportedStoryId);
        Assert.AreEqual(plan.ImportedStoryId, store.Stories.Load(plan.ImportedStoryId).Id);
        var importedActor = store.Memberships.Load(plan.ImportedStoryId).OwnedResources.Actors.Single();
        Assert.AreEqual(plan.ImportedStoryId, ResourceAddress.FromKey(importedActor).StoryUid.Value);
        Assert.AreEqual("Guard", new ActorRepository(consumer.Root).LoadIndividual(importedActor).DisplayName);
        CollectionAssert.AreEqual(referenceBytes, File.ReadAllBytes(referencePath));
    }

    [TestMethod]
    public void RepeatedImportAllocatesIndependentStoriesAndPreservesExistingContent()
    {
        using var provider = BuildProvider("Guard");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");
        var service = new OfflineStoryPackageImportService();
        var first = service.Import(consumer.Root, package);
        var store = new CanonicalProjectGraphStore(consumer.Root);
        var before = File.ReadAllBytes(store.Stories.GetPath(first.ImportedStoryId));
        var second = service.BuildPlan(consumer.Root, package);
        Assert.IsTrue(second.CanApply);
        Assert.AreNotEqual(first.ImportedStoryId, second.ImportedStoryId);
        service.Apply(second);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Stories.GetPath(first.ImportedStoryId)));
        Assert.HasCount(2, store.Stories.List());
    }

    [TestMethod]
    public void ImportPlanRejectsStaleDestinationBeforeWriting()
    {
        using var provider = BuildProvider("Guard");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");
        var service = new OfflineStoryPackageImportService();
        var plan = service.BuildPlan(consumer.Root, package);
        var storyPath = new CanonicalProjectGraphStore(consumer.Root).Stories.GetPath(plan.ImportedStoryId);
        Directory.CreateDirectory(Path.GetDirectoryName(storyPath)!);
        File.WriteAllText(storyPath, "changed-before-apply");
        Assert.ThrowsExactly<InvalidOperationException>(() => service.Apply(plan));
        Assert.AreEqual("changed-before-apply", File.ReadAllText(storyPath));
    }

    [TestMethod]
    public void ImportPreservesExistingLogicalIdentityAtAnArbitraryCanonicalPath()
    {
        using var provider = BuildProvider("Guard");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");
        var arbitrary = Path.Combine(consumer.Root, "resources", "canonical", "stories", "moved.json");
        Directory.CreateDirectory(Path.GetDirectoryName(arbitrary)!);
        var source = new CanonicalProjectGraphStore(provider.Root).Stories.Load("ST-2345-6789-ABCD-EFGH");
        File.WriteAllText(arbitrary, source.ToJson());
        new CanonicalProjectGraphStore(consumer.Root).Memberships.Create(new CanonicalStoryMembershipManifest(source.Id));

        var plan = new OfflineStoryPackageImportService().BuildPlan(consumer.Root, package);
        Assert.IsTrue(plan.CanApply);
        Assert.AreNotEqual(source.Id, plan.ImportedStoryId);
        new OfflineStoryPackageImportService().Apply(plan);
        Assert.AreEqual(source.ToJson(), File.ReadAllText(arbitrary));
    }

    [TestMethod]
    public void SamePackageIdentityUpdatesReferenceAndFingerprintWithoutFilenameMatching()
    {
        using var provider = BuildProvider("Guard v1");
        using var consumer = new TestProjectDirectory();
        var first = BuildPackage(provider.Root, "Guard v1");
        var service = new OfflineReferencePackageService();
        _ = service.AddOrUpdate(consumer.Root, first);
        var old = OfflineProviderCatalog.Load(consumer.Root).Providers.Single();

        var actor = new ActorRepository(provider.Root).LoadIndividual("ST-2345-6789-ABCD-EFGH~actor~boss");
        actor.DisplayName = "Guard v2";
        new ActorRepository(provider.Root).SaveActor(actor);
        var second = BuildPackage(provider.Root, "Guard v2");
        var updated = service.AddOrUpdate(consumer.Root, second);
        var current = OfflineProviderCatalog.Load(consumer.Root);
        Assert.AreEqual(old.Identity.PackageId, updated.Identity.PackageId);
        Assert.AreNotEqual(old.Fingerprint, current.Providers.Single().Fingerprint);
        Assert.AreEqual("Guard v2", current.Resolve(DgrResourceKind.Actor, "ST-2345-6789-ABCD-EFGH~actor~boss")!.DisplayName);
        Assert.AreEqual(1, current.Providers.Count);
    }

    [TestMethod]
    public void ImportOfNewSnapshotPreservesExistingReferenceSnapshot()
    {
        using var provider = BuildProvider("Guard v1");
        using var consumer = new TestProjectDirectory();
        var first = BuildPackage(provider.Root, "Guard v1");
        _ = new OfflineReferencePackageService().AddOrUpdate(consumer.Root, first);
        var old = OfflineProviderCatalog.Load(consumer.Root).Providers.Single();
        var actor = new ActorRepository(provider.Root).LoadIndividual("ST-2345-6789-ABCD-EFGH~actor~boss");
        actor.DisplayName = "Guard v2";
        new ActorRepository(provider.Root).SaveActor(actor);
        var second = BuildPackage(provider.Root, "Guard v2");

        var imported = new OfflineStoryPackageImportService().Import(consumer.Root, second);
        var actorId = new CanonicalProjectGraphStore(consumer.Root).Memberships.Load(imported.ImportedStoryId).OwnedResources.Actors.Single();
        Assert.AreEqual("Guard v2", new ActorRepository(consumer.Root).LoadActor(actorId).DisplayName);
        Assert.AreEqual(old.Fingerprint, OfflineProviderCatalog.Load(consumer.Root).Providers.Single().Fingerprint);
    }

    [TestMethod]
    public void ImportWriteFailureRollsBackNativeWritesAndRetainsReference()
    {
        using var provider = BuildProvider("Guard");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");
        _ = new OfflineReferencePackageService().AddOrUpdate(consumer.Root, package);
        var plan = new OfflineStoryPackageImportService().BuildPlan(consumer.Root, package);
        var referencePath = OfflineProviderCatalog.Load(consumer.Root).Providers.Single().PackagePath;
        var referenceBytes = File.ReadAllBytes(referencePath);
        var writes = 0;
        var transaction = new ProjectFileTransaction(
            writeFile: (path, bytes) =>
            {
                File.WriteAllBytes(path, bytes);
                if (++writes == 2) throw new IOException("injected import write failure");
            });

        Assert.ThrowsExactly<IOException>(() => new OfflineStoryPackageImportService(transaction).Apply(plan));
        CollectionAssert.AreEqual(referenceBytes, File.ReadAllBytes(referencePath));
        foreach (var change in plan.Changes.Where(change => change.ExpectedBytes is null))
            Assert.IsFalse(File.Exists(Path.Combine(consumer.Root, change.RelativePath)));
        Assert.IsFalse(File.Exists(new CanonicalProjectGraphStore(consumer.Root).Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.IsFalse(File.Exists(new ActorRepository(consumer.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~boss")));
    }

    [TestMethod]
    public void ExportRejectsUnresolvedDependencyAndPreservesPreviousPackage()
    {
        using var provider = BuildProvider("Guard", "ST-JKLM-NPQR-STUV-WXYZ~actor~princess");
        using var consumer = new TestProjectDirectory();
        var output = Path.Combine(provider.Root, "exports", "input.dgrs");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, "previous export");
        Assert.ThrowsExactly<StoryPackageException>(() => BuildPackage(provider.Root, "Guard"));
        Assert.AreEqual("previous export", File.ReadAllText(output));
        Assert.IsEmpty(new CanonicalProjectGraphStore(consumer.Root).Stories.List());
    }

    private static TestProjectDirectory BuildProvider(string actorName, string? referencedActor = null)
    {
        var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start")] )));
        var actor = new ActorRepository(project.Root).CreateIndividual("ST-2345-6789-ABCD-EFGH~actor~boss", actorName);
        actor.HomeStoryId = "ST-2345-6789-ABCD-EFGH";
        new ActorRepository(project.Root).SaveActor(actor);
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet { Actors = ["ST-2345-6789-ABCD-EFGH~actor~boss"] },
            new CanonicalStoryMembershipSet { Actors = referencedActor is null ? [] : [referencedActor] }));
        return project;
    }

    private static string BuildPackage(string projectRoot, string version)
    {
        var output = Path.Combine(projectRoot, "exports", "input.dgrs");
        _ = new DgrsStoryPackageExporter(projectRoot).Build("ST-2345-6789-ABCD-EFGH", output, version);
        return output;
    }
}
