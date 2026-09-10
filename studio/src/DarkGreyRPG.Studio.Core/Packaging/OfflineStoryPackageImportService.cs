using System.Collections.ObjectModel;
using System.Text;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Core.Packaging;

public sealed record OfflineImportConflict(DgrResourceKind Kind, string Id, string RelativePath, string Message);

public sealed class OfflineImportPlan
{
    internal OfflineImportPlan(
        string projectDirectory,
        OfflineDgrsPackage package,
        IReadOnlyList<NamespaceFileChange> changes,
        IReadOnlyList<OfflineImportConflict> conflicts,
        string? referencedPackagePath,
        IReadOnlyList<OfflineProviderResource> importedResources)
    {
        ProjectDirectory = projectDirectory;
        Package = package;
        Changes = new ReadOnlyCollection<NamespaceFileChange>(changes.ToList());
        Conflicts = new ReadOnlyCollection<OfflineImportConflict>(conflicts.ToList());
        ReferencedPackagePath = referencedPackagePath;
        ImportedResources = new ReadOnlyCollection<OfflineProviderResource>(importedResources.ToList());
        SourceStamp = OfflineProjectStateStamp.Capture(projectDirectory);
    }

    internal string SourceStamp { get; }
    public string ProjectDirectory { get; }
    public OfflineDgrsPackage Package { get; }
    public IReadOnlyList<NamespaceFileChange> Changes { get; }
    public IReadOnlyList<OfflineImportConflict> Conflicts { get; }
    public string? ReferencedPackagePath { get; }
    public IReadOnlyList<OfflineProviderResource> ImportedResources { get; }
    public bool CanApply => Conflicts.Count == 0;
}

public sealed class OfflineStoryPackageImportException : Exception
{
    public OfflineStoryPackageImportException(string message) : base(message) { }
}

/// <summary>Builds and applies an owned-only native import as one file transaction.</summary>
public sealed class OfflineStoryPackageImportService
{
    private readonly NamespaceFileTransaction _transaction;

    public OfflineStoryPackageImportService(NamespaceFileTransaction? transaction = null)
    {
        _transaction = transaction ?? new NamespaceFileTransaction();
    }

    public OfflineImportPlan BuildPlan(string projectDirectory, string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        var root = Path.GetFullPath(projectDirectory);
        var package = OfflineDgrsPackageReader.Read(packagePath);
        var changes = new List<NamespaceFileChange>();
        var conflicts = new List<OfflineImportConflict>();
        var imported = new List<OfflineProviderResource>();

        var membership = LoadMembership(package);
        var importedKeys = NamespaceMigrationPlanner.Members(membership.OwnedResources).Append(new DgrResourceKey(DgrResourceKind.Story, package.Manifest.StoryId)).ToHashSet();
        var existingStore = new CanonicalProjectGraphStore(root);
        foreach (var info in existingStore.Memberships.List())
        {
            var existingMembership = existingStore.Memberships.Load(info.StoryId);
            foreach (var key in NamespaceMigrationPlanner.Members(existingMembership.OwnedResources).Where(importedKeys.Contains))
                conflicts.Add(new(key.Kind, key.Id, info.SourcePath, $"{key.Kind} '{key.Id}' is already owned by Story '{info.StoryId}'."));
        }
        var providerCatalog = OfflineProviderCatalog.Load(root);
        if (providerCatalog.Diagnostics.Count != 0)
            throw new OfflineStoryPackageImportException(string.Join("; ", providerCatalog.Diagnostics.Select(issue => issue.Message)));
        foreach (var resource in providerCatalog.Resources.Where(resource => resource.PackageIdentity.PackageId != package.Identity.PackageId
            && importedKeys.Contains(new(resource.Kind, resource.Id))))
            conflicts.Add(new(resource.Kind, resource.Id, resource.PackagePath, $"Full ID is already supplied by referenced package '{resource.PackageIdentity}'."));
        AddResource(DgrResourceKind.Story, package.Manifest.RequiredResources.Story, package.Manifest.StoryId, always: true);
        AddOwned(DgrResourceKind.Actor, package.Manifest.RequiredResources.Actors, membership.OwnedResources.Actors);
        AddOwned(DgrResourceKind.Item, package.Manifest.RequiredResources.Items, membership.OwnedResources.Items);
        AddOwned(DgrResourceKind.ItemGroup, package.Manifest.RequiredResources.ItemGroups, membership.OwnedResources.ItemGroups);
        AddOwned(DgrResourceKind.Session, package.Manifest.RequiredResources.Sessions, membership.OwnedResources.Sessions);
        AddOwned(DgrResourceKind.Task, package.Manifest.RequiredResources.Tasks, membership.OwnedResources.Tasks);

        var membershipPath = package.Manifest.RequiredResources.CanonicalMemberships.FirstOrDefault(path =>
            package.Entries.TryGetValue(path, out var bytes)
            && CanonicalStoryMembershipSerializer.Deserialize(Encoding.UTF8.GetString(bytes)).StoryId == package.Manifest.StoryId);
        if (membershipPath is not null && package.Manifest.RequiredResources.Story.StartsWith("resources/canonical/", StringComparison.Ordinal))
        {
            AddFile(membershipPath, Path.Combine("resources", "canonical", "memberships", DgrResourceId.RelativeJsonPath(package.Manifest.StoryId)),
                DgrResourceKind.Story, package.Manifest.StoryId, imported: false);
            AddNamespacePolicy(package.Manifest.StoryId);
        }

        var referencedProvider = OfflineProviderCatalog.Load(root).Providers
            .FirstOrDefault(provider => string.Equals(provider.Identity.PackageId, package.Identity.PackageId, StringComparison.Ordinal));
        if (referencedProvider is not null
            && !string.Equals(referencedProvider.Fingerprint, package.Fingerprint, StringComparison.Ordinal))
            throw new OfflineStoryPackageImportException($"Referenced package '{package.Identity.PackageId}' has a different fingerprint; refresh the reference before importing.");
        var referencedPath = referencedProvider?.PackagePath;
        if (referencedPath is not null)
        {
            var oldBytes = File.ReadAllBytes(referencedPath);
            changes.Add(new NamespaceFileChange(Path.GetRelativePath(root, referencedPath), oldBytes, null));
        }
        var plan = new OfflineImportPlan(root, package, changes, conflicts, referencedPath, imported);
        if (plan.CanApply)
        {
            try { ValidateDetachedFinalProject(root, package); }
            catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException)
            {
                conflicts.Add(new(DgrResourceKind.Story, package.Manifest.StoryId, string.Empty, "Canonical validation: " + exception.Message));
                plan = new OfflineImportPlan(root, package, changes, conflicts, referencedPath, imported);
            }
        }
        return plan;

        void AddOwned(DgrResourceKind kind, IEnumerable<string> paths, IEnumerable<string> ids)
        {
            var pathList = paths.ToArray();
            foreach (var ownedId in ids.Distinct(StringComparer.Ordinal))
            {
                if (!package.Resources.Any(resource => resource.Kind == kind
                    && string.Equals(resource.Id, ownedId, StringComparison.Ordinal)
                    && pathList.Contains(resource.SourcePath, StringComparer.Ordinal)))
                    throw new StoryPackageException($"DGRS membership owns missing {kind} '{ownedId}'.");
            }
            var owned = ids.ToHashSet(StringComparer.Ordinal);
            foreach (var path in pathList)
            {
                var resource = package.Resources.FirstOrDefault(item => item.Kind == kind && string.Equals(item.SourcePath, path, StringComparison.Ordinal));
                var id = resource?.Id ?? TryReadId(package, kind, path);
                if (owned.Contains(id)) AddResource(kind, path, id, always: false);
            }
        }

        void AddResource(DgrResourceKind kind, string sourcePath, string id, bool always)
        {
            if (!always && string.IsNullOrWhiteSpace(id)) return;
            var destination = Destination(kind, sourcePath, id);
            AddFile(sourcePath, destination, kind, id, imported: true);
            var resource = package.Resources.FirstOrDefault(item => item.Kind == kind && item.Id == id);
            if (resource is not null) imported.Add(resource);
        }

        void AddFile(string sourcePath, string destination, DgrResourceKind kind, string id, bool imported)
        {
            if (!package.Entries.TryGetValue(sourcePath, out var bytes)) throw new StoryPackageException($"DGRS entry '{sourcePath}' is missing.");
            var existing = Path.Combine(root, destination);
            if (Directory.Exists(existing)) conflicts.Add(new(kind, id, destination, $"Native destination '{destination}' is a directory."));
            else if (File.Exists(existing)) conflicts.Add(new(kind, id, destination, $"Native {kind} '{id}' already exists; import would overwrite it."));
            if (imported)
            {
                var logical = FindExistingNativePath(root, kind, id);
                if (logical is not null && !string.Equals(Path.GetFullPath(logical), Path.GetFullPath(existing), StringComparison.OrdinalIgnoreCase))
                    conflicts.Add(new(kind, id, destination, $"Native {kind} '{id}' already exists at '{Path.GetRelativePath(root, logical)}'."));
            }
            changes.Add(new NamespaceFileChange(destination, File.Exists(existing) ? File.ReadAllBytes(existing) : null, bytes));
        }

        void AddNamespacePolicy(string storyId)
        {
            if (!DgrResourceId.IsFullId(storyId)) return;
            var policyPath = Path.Combine(root, NamespacePolicyStore.RelativePath);
            NamespacePolicy current;
            byte[]? expected;
            if (File.Exists(policyPath))
            {
                expected = File.ReadAllBytes(policyPath);
                current = NamespacePolicyStore.Decode(expected);
            }
            else
            {
                expected = null;
                current = new NamespacePolicy(DgrResourceId.Namespace(storyId));
            }
            var overrides = current.StoryOverrides.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            overrides[storyId] = DgrResourceId.Namespace(storyId);
            changes.Add(new NamespaceFileChange(NamespacePolicyStore.RelativePath, expected,
                NamespacePolicyStore.Encode(new NamespacePolicy(current.GlobalNamespace, overrides))));
        }
    }

    public void Apply(OfflineImportPlan plan, Action? validateFinalProject = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.CanApply) throw new OfflineStoryPackageImportException(
            "DGRS import aborted because native conflicts were found: "
            + string.Join("; ", plan.Conflicts.Select(conflict => conflict.Message)));
        _transaction.Apply(plan.ProjectDirectory, plan.Changes, () =>
        {
            if (plan.SourceStamp != OfflineProjectStateStamp.Capture(plan.ProjectDirectory))
                throw new InvalidOperationException("Project changed after import preview; build a new Import Plan.");
            ValidateDetachedFinalProject(plan.ProjectDirectory, plan.Package);
            validateFinalProject?.Invoke();
        });
    }

    public OfflineImportPlan Import(string projectDirectory, string packagePath, Action? validateFinalProject = null)
    {
        var plan = BuildPlan(projectDirectory, packagePath);
        Apply(plan, validateFinalProject);
        return plan;
    }

    private static CanonicalStoryMembershipManifest LoadMembership(OfflineDgrsPackage package)
    {
        var path = package.Manifest.RequiredResources.CanonicalMemberships.FirstOrDefault(item => package.Entries.ContainsKey(item));
        if (path is null || !package.Manifest.RequiredResources.Story.StartsWith("resources/canonical/", StringComparison.Ordinal))
            return new CanonicalStoryMembershipManifest(package.Manifest.StoryId);
        var membership = CanonicalStoryMembershipSerializer.Deserialize(Encoding.UTF8.GetString(package.GetEntry(path)));
        if (!string.Equals(membership.StoryId, package.Manifest.StoryId, StringComparison.Ordinal))
            throw new StoryPackageException("DGRS canonical membership does not match manifest story_id.");
        return membership;
    }

    private static string Destination(DgrResourceKind kind, string sourcePath, string id)
        => (kind switch
        {
            DgrResourceKind.Story => sourcePath.StartsWith("resources/canonical/", StringComparison.Ordinal)
                ? Path.Combine("resources", "canonical", "stories", DgrResourceId.RelativeJsonPath(id))
                : Path.Combine("stories", DgrResourceId.RelativeJsonPath(id)),
            DgrResourceKind.Session => Path.Combine("resources", "canonical", "sessions", DgrResourceId.RelativeJsonPath(id)),
            DgrResourceKind.Task => Path.Combine("resources", "canonical", "tasks", DgrResourceId.RelativeJsonPath(id)),
            DgrResourceKind.Actor => Path.Combine("actors", DgrResourceId.RelativeJsonPath(id)),
            DgrResourceKind.Item => Path.Combine("items", DgrResourceId.RelativeJsonPath(id)),
            DgrResourceKind.ItemGroup => Path.Combine("item_groups", DgrResourceId.RelativeJsonPath(id)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        }).Replace(Path.DirectorySeparatorChar, '/');

    private static string TryReadId(OfflineDgrsPackage package, DgrResourceKind kind, string path)
        => package.Resources.FirstOrDefault(resource => resource.Kind == kind && string.Equals(resource.SourcePath, path, StringComparison.Ordinal))?.Id ?? string.Empty;

    private static string? FindExistingNativePath(string root, DgrResourceKind kind, string id)
    {
        return kind switch
        {
            DgrResourceKind.Story => new CanonicalProjectGraphStore(root).Stories.List().FirstOrDefault(item => item.Id == id)?.SourcePath,
            DgrResourceKind.Session => new CanonicalProjectGraphStore(root).Sessions.List().FirstOrDefault(item => item.Id == id)?.SourcePath,
            DgrResourceKind.Task => new CanonicalProjectGraphStore(root).Tasks.List().FirstOrDefault(item => item.Id == id)?.SourcePath,
            DgrResourceKind.Actor => new DarkGreyRPG.Studio.Core.Actors.ActorRepository(root).ListActors().FirstOrDefault(item => item.Id == id)?.SourcePath,
            DgrResourceKind.Item => new DarkGreyRPG.Studio.Core.Items.ItemRepository(root).ListItems().FirstOrDefault(item => item.Id == id)?.Path,
            DgrResourceKind.ItemGroup => new DarkGreyRPG.Studio.Core.Items.ItemRepository(root).ListGroups().FirstOrDefault(item => item.Id == id)?.Path,
            _ => null,
        };
    }

    private static void ValidateDetachedFinalProject(string root, OfflineDgrsPackage package)
    {
        var current = NamespaceProjectMigrationService.ReadProject(root);
        var membership = LoadMembership(package);
        var graphs = current.Graphs.ToList();
        var actors = current.Actors.ToList();
        var items = current.Items.ToList();
        var owned = NamespaceMigrationPlanner.Members(membership.OwnedResources).ToHashSet();
        foreach (var resource in package.Resources)
        {
            if (!(resource.Kind == DgrResourceKind.Story && resource.Id.Equals(package.Manifest.StoryId, StringComparison.Ordinal))
                && !owned.Contains(new DgrResourceKey(resource.Kind, resource.Id))) continue;
            switch (resource.Kind)
            {
                case DgrResourceKind.Story:
                case DgrResourceKind.Session:
                case DgrResourceKind.Task:
                    if (resource.ReadGraphDefinition() is { } graph) graphs.Add(graph);
                    break;
                case DgrResourceKind.Actor:
                    if (resource.ReadActorDefinition() is { } actor) actors.Add(actor);
                    break;
                case DgrResourceKind.Item:
                case DgrResourceKind.ItemGroup:
                    if (resource.ReadItemDefinition() is { } item) items.Add(item);
                    break;
            }
        }
        var memberships = current.Memberships.Append(membership).ToArray();
        var policy = ImportPolicy(root, package.Manifest.StoryId);
        NamespaceProjectValidator.Validate(new NamespaceProjectSnapshot(graphs, memberships, actors, items, current.Logic, policy));
    }

    private static NamespacePolicy ImportPolicy(string root, string storyId)
    {
        if (!DgrResourceId.IsFullId(storyId))
            return NamespacePolicyStore.Load(root) ?? throw new InvalidDataException("A full Story ID is required for imported canonical content.");
        var current = NamespacePolicyStore.Load(root) ?? new NamespacePolicy(DgrResourceId.Namespace(storyId));
        var overrides = current.StoryOverrides.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        overrides[storyId] = DgrResourceId.Namespace(storyId);
        return new NamespacePolicy(current.GlobalNamespace, overrides);
    }
}

/// <summary>Physical references folder operations; package identity, never filename, selects updates.</summary>
public sealed class OfflineReferencePackageService
{
    private readonly NamespaceFileTransaction _transaction;

    public OfflineReferencePackageService(NamespaceFileTransaction? transaction = null)
    {
        _transaction = transaction ?? new NamespaceFileTransaction();
    }

    public OfflineDgrsPackage AddOrUpdate(string projectDirectory, string packagePath)
    {
        var root = Path.GetFullPath(projectDirectory);
        var package = OfflineDgrsPackageReader.Read(packagePath);
        var catalog = OfflineProviderCatalog.Load(root);
        var nativeOverlap = OfflineNativeContentCatalog.Load(root)
            .Where(native => package.Resources.Any(provider => provider.Kind == native.Kind
                && string.Equals(provider.Id, native.Id, StringComparison.Ordinal)))
            .ToArray();
        if (nativeOverlap.Length != 0)
            throw new StoryPackageException("Cannot reference a package whose full IDs overlap native content: "
                + string.Join(", ", nativeOverlap.Select(item => $"{item.Kind}:{item.Id}")));
        var priorCandidates = catalog.Providers.Where(provider => string.Equals(provider.Identity.PackageId, package.Identity.PackageId, StringComparison.Ordinal)).ToArray();
        if (priorCandidates.Length > 1)
            throw new StoryPackageException($"Referenced package identity '{package.Identity.PackageId}' is ambiguous; multiple reference files declare it.");
        var prior = priorCandidates.SingleOrDefault();
        var providerOverlap = catalog.Resources
            .Where(existing => !string.Equals(existing.PackageIdentity.PackageId, package.Identity.PackageId, StringComparison.Ordinal)
                && package.Resources.Any(candidate => candidate.Kind == existing.Kind
                    && string.Equals(candidate.Id, existing.Id, StringComparison.Ordinal)))
            .ToArray();
        if (providerOverlap.Length != 0)
            throw new StoryPackageException("Cannot add a provider with ambiguous full IDs: "
                + string.Join(", ", providerOverlap.Select(item => $"{item.Kind}:{item.Id}")));
        var relative = prior is null
            ? NewReferenceRelativePath(root, packagePath, package.Identity.PackageId)
            : Path.GetRelativePath(root, prior.PackagePath);
        var target = Path.Combine(root, relative);
        var expected = File.Exists(target) ? File.ReadAllBytes(target) : null;
        var desired = package.ArchiveBytes;
        _transaction.Apply(root,
            [new NamespaceFileChange(relative, expected, desired)],
            () => { });
        return OfflineDgrsPackageReader.Read(target);
    }

    public void Remove(string projectDirectory, string packageId, bool allowUnresolvedReferences = false)
    {
        var root = Path.GetFullPath(projectDirectory);
        var provider = OfflineProviderCatalog.Load(root).Providers.FirstOrDefault(item => item.Identity.PackageId == packageId);
        if (provider is null) throw new StoryPackageException($"Referenced package '{packageId}' was not found.");
        if (!allowUnresolvedReferences && HasReferences(root, provider))
            throw new OfflineStoryPackageImportException($"Removing package '{packageId}' would create unresolved references.");
        var relative = Path.GetRelativePath(root, provider.PackagePath);
        var bytes = File.ReadAllBytes(provider.PackagePath);
        _transaction.Apply(root, [new NamespaceFileChange(relative, bytes, null)], () => { });
    }

    private static bool HasReferences(string root, OfflineDgrsPackage provider)
    {
        var ids = provider.Resources.Select(resource => resource.Id).ToHashSet(StringComparer.Ordinal);
        var store = new CanonicalProjectGraphStore(root);
        foreach (var info in store.Memberships.List())
        {
            var membership = store.Memberships.Load(info.StoryId);
            if (membership.ReferencedResources.Actors.Concat(membership.ReferencedResources.Items).Concat(membership.ReferencedResources.ItemGroups)
                .Concat(membership.ReferencedResources.Sessions).Concat(membership.ReferencedResources.Tasks).Any(ids.Contains)) return true;
        }
        return false;
    }

    private static string SafeReferenceFileName(string sourcePath, string packageId)
    {
        var name = Path.GetFileName(sourcePath);
        if (string.Equals(Path.GetExtension(name), ".dgrs", StringComparison.OrdinalIgnoreCase)
            && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0) return name;
        return DgrResourceId.PackageFileName(packageId);
    }

    private static string NewReferenceRelativePath(
        string root,
        string sourcePath,
        string packageId)
    {
        var fileName = SafeReferenceFileName(sourcePath, packageId);
        var candidate = Path.Combine(root, "references", fileName);
        if (!File.Exists(candidate)) return Path.Combine("references", fileName);
        var identityName = DgrResourceId.PackageFileName(packageId);
        candidate = Path.Combine(root, "references", identityName);
        var suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(root, "references", Path.GetFileNameWithoutExtension(identityName) + "-" + suffix++ + ".dgrs");
        }
        return Path.GetRelativePath(root, candidate);
    }
}


internal static class OfflineProjectStateStamp
{
    public static string Capture(string root)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var files = new List<string>();
        var project = Path.Combine(root, "project.json");
        if (File.Exists(project)) files.Add(project);
        foreach (var directory in new[] { "actors", "items", "item_groups", "stories", "resources", "references" })
        {
            var path = Path.Combine(root, directory);
            if (Directory.Exists(path)) files.AddRange(Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories));
        }
        foreach (var path in files.OrderBy(path => path, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path) + "\0"));
            hash.AppendData(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
}
