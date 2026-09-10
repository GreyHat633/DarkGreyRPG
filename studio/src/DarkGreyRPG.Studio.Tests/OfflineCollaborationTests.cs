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
        var actor = catalog.Resolve(DgrResourceKind.Actor, "B:boss");
        Assert.IsNotNull(actor);
        Assert.AreEqual("Guard v1", actor.DisplayName);
        Assert.AreEqual("B", actor.Namespace);
        Assert.AreEqual(imported.Fingerprint, catalog.Providers.Single().Fingerprint);
        Assert.IsTrue(actor.IsReadOnly);
        Assert.AreEqual("B:boss", actor.FullId);
    }

    [TestMethod]
    public void ImportSelectsOwnedResourcesPreservesNamespaceAndConvertsReferenceAtomically()
    {
        using var provider = BuildProvider("Guard");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");
        _ = new OfflineReferencePackageService().AddOrUpdate(consumer.Root, package);

        var plan = new OfflineStoryPackageImportService().BuildPlan(consumer.Root, package);
        Assert.IsTrue(plan.CanApply);
        Assert.IsNotNull(plan.ReferencedPackagePath);
        new OfflineStoryPackageImportService().Apply(plan);

        var store = new CanonicalProjectGraphStore(consumer.Root);
        Assert.AreEqual("B:story", store.Stories.Load("B:story").Id);
        Assert.AreEqual("B:boss", new ActorRepository(consumer.Root).LoadIndividual("B:boss").Id);
        Assert.IsFalse(File.Exists(plan.ReferencedPackagePath));
        Assert.IsTrue(NamespacePolicyStore.Load(consumer.Root)!.IsCustom("B:story"));
    }

    [TestMethod]
    public void ImportConflictIsReportedBeforeMutationAndLeavesProjectUnchanged()
    {
        using var provider = BuildProvider("Guard");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");
        var service = new OfflineStoryPackageImportService();
        service.Import(consumer.Root, package);
        var before = File.ReadAllBytes(new CanonicalProjectGraphStore(consumer.Root).Stories.GetPath("B:story"));
        var conflict = service.BuildPlan(consumer.Root, package);
        Assert.IsFalse(conflict.CanApply);
        StringAssert.Contains(conflict.Conflicts.First(item => item.Id == "B:story").Message, "already exists");
        CollectionAssert.AreEqual(before, File.ReadAllBytes(new CanonicalProjectGraphStore(consumer.Root).Stories.GetPath("B:story")));
    }

    [TestMethod]
    public void ImportPlanRejectsStaleDestinationBeforeWriting()
    {
        using var provider = BuildProvider("Guard");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");
        var service = new OfflineStoryPackageImportService();
        var plan = service.BuildPlan(consumer.Root, package);
        var storyPath = new CanonicalProjectGraphStore(consumer.Root).Stories.GetPath("B:story");
        Directory.CreateDirectory(Path.GetDirectoryName(storyPath)!);
        File.WriteAllText(storyPath, "changed-before-apply");
        Assert.ThrowsExactly<InvalidOperationException>(() => service.Apply(plan));
        Assert.AreEqual("changed-before-apply", File.ReadAllText(storyPath));
    }

    [TestMethod]
    public void ImportPlanDetectsLogicalIdentityAtAnArbitraryCanonicalPath()
    {
        using var provider = BuildProvider("Guard");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");
        var arbitrary = Path.Combine(consumer.Root, "resources", "canonical", "stories", "moved.json");
        Directory.CreateDirectory(Path.GetDirectoryName(arbitrary)!);
        var source = new CanonicalProjectGraphStore(provider.Root).Stories.Load("B:story");
        File.WriteAllText(arbitrary, source.ToJson());

        var plan = new OfflineStoryPackageImportService().BuildPlan(consumer.Root, package);
        Assert.IsFalse(plan.CanApply);
        StringAssert.Contains(plan.Conflicts.Single(item => item.Kind == DgrResourceKind.Story).Message, "moved.json");
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

        var actor = new ActorRepository(provider.Root).LoadIndividual("B:boss");
        actor.DisplayName = "Guard v2";
        new ActorRepository(provider.Root).SaveActor(actor);
        var second = BuildPackage(provider.Root, "Guard v2");
        var updated = service.AddOrUpdate(consumer.Root, second);
        var current = OfflineProviderCatalog.Load(consumer.Root);
        Assert.AreEqual(old.Identity.PackageId, updated.Identity.PackageId);
        Assert.AreNotEqual(old.Fingerprint, current.Providers.Single().Fingerprint);
        Assert.AreEqual("Guard v2", current.Resolve(DgrResourceKind.Actor, "B:boss")!.DisplayName);
        Assert.AreEqual(1, current.Providers.Count);
    }

    [TestMethod]
    public void ConversionRejectsDifferentFingerprintAndRetainsReference()
    {
        using var provider = BuildProvider("Guard v1");
        using var consumer = new TestProjectDirectory();
        var first = BuildPackage(provider.Root, "Guard v1");
        _ = new OfflineReferencePackageService().AddOrUpdate(consumer.Root, first);
        var old = OfflineProviderCatalog.Load(consumer.Root).Providers.Single();
        var actor = new ActorRepository(provider.Root).LoadIndividual("B:boss");
        actor.DisplayName = "Guard v2";
        new ActorRepository(provider.Root).SaveActor(actor);
        var second = BuildPackage(provider.Root, "Guard v2");

        Assert.ThrowsExactly<OfflineStoryPackageImportException>(() =>
            new OfflineStoryPackageImportService().BuildPlan(consumer.Root, second));
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
        var writes = 0;
        var transaction = new NamespaceFileTransaction(
            writeFile: (path, bytes) =>
            {
                File.WriteAllBytes(path, bytes);
                if (++writes == 2) throw new IOException("injected import write failure");
            });

        Assert.ThrowsExactly<IOException>(() => new OfflineStoryPackageImportService(transaction).Apply(plan));
        Assert.IsTrue(File.Exists(plan.ReferencedPackagePath));
        Assert.IsFalse(File.Exists(new CanonicalProjectGraphStore(consumer.Root).Stories.GetPath("B:story")));
        Assert.IsFalse(File.Exists(new ActorRepository(consumer.Root).GetActorPath("B:boss")));
    }

    [TestMethod]
    public void ImportedCustomNamespaceSurvivesGlobalNamespaceChange()
    {
        using var provider = BuildProvider("Guard");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");
        var policyPath = Path.Combine(consumer.Root, NamespacePolicyStore.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllBytes(policyPath, NamespacePolicyStore.Encode(new NamespacePolicy("A")));
        new OfflineStoryPackageImportService().Import(consumer.Root, package);

        var migration = new NamespaceProjectMigrationService();
        migration.Apply(migration.PreviewGlobal(consumer.Root, "A2"));
        var policy = NamespacePolicyStore.Load(consumer.Root)!;
        Assert.AreEqual("A2", policy.GlobalNamespace);
        Assert.AreEqual("B", policy.StoryOverrides["B:story"]);
        Assert.AreEqual("B:story", new CanonicalProjectGraphStore(consumer.Root).Stories.Load("B:story").Id);
    }

    [TestMethod]
    public void ImportKeepsExternalReferencedResourceUnresolvedWithoutCopyingIt()
    {
        using var provider = BuildProvider("Guard", "C:princess");
        using var consumer = new TestProjectDirectory();
        var package = BuildPackage(provider.Root, "Guard");

        new OfflineStoryPackageImportService().Import(consumer.Root, package);
        var membership = new CanonicalProjectGraphStore(consumer.Root).Memberships.Load("B:story");
        CollectionAssert.Contains(membership.ReferencedResources.Actors.ToArray(), "C:princess");
        Assert.IsFalse(File.Exists(new ActorRepository(consumer.Root).GetActorPath("C:princess")));
        Assert.IsNull(OfflineNativeContentCatalog.Load(consumer.Root).SingleOrDefault(resource => resource.Id == "C:princess"));
    }

    private static TestProjectDirectory BuildProvider(string actorName, string? referencedActor = null)
    {
        var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(GraphResourceKind.Story, "B:story", "Story",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start")] )));
        var actor = new ActorRepository(project.Root).CreateIndividual("B:boss", actorName);
        actor.HomeStoryId = "B:story";
        new ActorRepository(project.Root).SaveActor(actor);
        store.Memberships.Create(new CanonicalStoryMembershipManifest("B:story",
            new CanonicalStoryMembershipSet { Actors = ["B:boss"] },
            new CanonicalStoryMembershipSet { Actors = referencedActor is null ? [] : [referencedActor] }));
        return project;
    }

    private static string BuildPackage(string projectRoot, string version)
    {
        var output = Path.Combine(projectRoot, "exports", "input.dgrs");
        _ = new DgrsStoryPackageExporter(projectRoot).Build("B:story", output, version);
        return output;
    }
}
