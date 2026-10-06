using DarkGreyRPG.Studio.Core.IO;
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
        IReadOnlyList<ProjectFileChange> changes,
        IReadOnlyList<OfflineImportConflict> conflicts,
        string? referencedPackagePath,
        IReadOnlyList<OfflineProviderResource> importedResources)
    {
        ProjectDirectory = projectDirectory;
        Package = package;
        Changes = new ReadOnlyCollection<ProjectFileChange>(changes.ToList());
        Conflicts = new ReadOnlyCollection<OfflineImportConflict>(conflicts.ToList());
        ReferencedPackagePath = referencedPackagePath;
        ImportedResources = new ReadOnlyCollection<OfflineProviderResource>(importedResources.ToList());
        SourceStamp = OfflineProjectStateStamp.Capture(projectDirectory);
    }

    internal CurrentProjectSnapshot FinalSnapshot { get; init; } = null!;
    public IReadOnlyDictionary<string, string> StoryUidMap { get; init; } = new Dictionary<string, string>();
    public string ImportedStoryId => StoryUidMap[Package.Manifest.StoryId];
    internal string SourceStamp { get; }
    public string ProjectDirectory { get; }
    public OfflineDgrsPackage Package { get; }
    public IReadOnlyList<ProjectFileChange> Changes { get; }
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
    private readonly ProjectFileTransaction _transaction;

    public OfflineStoryPackageImportService(ProjectFileTransaction? transaction = null)
    {
        _transaction = transaction ?? new ProjectFileTransaction();
    }

    public OfflineImportPlan BuildPlan(string projectDirectory, string packagePath)
    {
        return WholeContainerImport.Build(projectDirectory, packagePath);
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
            CurrentProjectValidator.Validate(plan.FinalSnapshot);
            validateFinalProject?.Invoke();
        });
    }

    public OfflineImportPlan Import(string projectDirectory, string packagePath, Action? validateFinalProject = null)
    {
        var plan = BuildPlan(projectDirectory, packagePath);
        Apply(plan, validateFinalProject);
        return plan;
    }

}

/// <summary>Physical references folder operations; package identity, never filename, selects updates.</summary>
public sealed class OfflineReferencePackageService
{
    private readonly ProjectFileTransaction _transaction;

    public OfflineReferencePackageService(ProjectFileTransaction? transaction = null)
    {
        _transaction = transaction ?? new ProjectFileTransaction();
    }

    public OfflineDgrsPackage AddOrUpdate(string projectDirectory, string packagePath)
        => AddOrUpdateContainer(projectDirectory, packagePath)[0];

    public IReadOnlyList<OfflineDgrsPackage> AddOrUpdateContainer(string projectDirectory, string packagePath)
    {
        var root = Path.GetFullPath(projectDirectory);
        var packages = OfflineDgrsPackageReader.ReadContainer(packagePath);
        var ids = packages.Select(package => package.Manifest.StoryId).ToHashSet(StringComparer.Ordinal);
        var resources = packages.SelectMany(package => package.Resources).DistinctBy(resource => (resource.Kind, resource.Id)).ToArray();
        var catalog = OfflineProviderCatalog.Load(root);
        if (catalog.Diagnostics.Count != 0) throw new StoryPackageException(string.Join("; ", catalog.Diagnostics.Select(issue => issue.Message)));
        var nativeOverlap = OfflineNativeContentCatalog.Load(root)
            .Where(native => resources.Any(provider => provider.Kind == native.Kind && provider.Id == native.Id)).ToArray();
        if (nativeOverlap.Length != 0)
            throw new StoryPackageException("Cannot reference a container whose identities overlap native content: "
                + string.Join(", ", nativeOverlap.Select(item => $"{item.Kind}:{item.Id}")));
        var overlaps = catalog.Providers.Where(provider => ids.Contains(provider.Manifest.StoryId))
            .Select(provider => provider.PackagePath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (overlaps.Length > 1 || overlaps.Length == 1
            && !ids.SetEquals(catalog.Providers.Where(provider => provider.PackagePath == overlaps[0]).Select(provider => provider.Manifest.StoryId)))
            throw new StoryPackageException("A reference update must replace one complete container with the same member set.");
        var priorPath = overlaps.SingleOrDefault();
        var providerOverlap = catalog.Resources.Where(existing => existing.PackagePath != priorPath
            && resources.Any(candidate => candidate.Kind == existing.Kind && candidate.Id == existing.Id)).ToArray();
        if (providerOverlap.Length != 0)
            throw new StoryPackageException("Cannot add a provider with ambiguous identities: "
                + string.Join(", ", providerOverlap.Select(item => $"{item.Kind}:{item.Id}")));
        var relative = priorPath is null ? NewReferenceRelativePath(root, packagePath, packages[0].Manifest.StoryId)
            : Path.GetRelativePath(root, priorPath);
        var target = Path.Combine(root, relative);
        var expected = File.Exists(target) ? File.ReadAllBytes(target) : null;
        _transaction.Apply(root, [new ProjectFileChange(relative, expected, packages[0].ArchiveBytes)], () => { });
        return OfflineDgrsPackageReader.ReadContainer(target);
    }

    public void Remove(string projectDirectory, string packageId, bool allowUnresolvedReferences = false)
    {
        var root = Path.GetFullPath(projectDirectory);
        var provider = OfflineProviderCatalog.Load(root).Providers.FirstOrDefault(item => item.Identity.PackageId == packageId);
        if (provider is null) throw new StoryPackageException($"Referenced package '{packageId}' was not found.");
        var container = OfflineProviderCatalog.Load(root).Providers.Where(item => item.PackagePath == provider.PackagePath).ToArray();
        if (!allowUnresolvedReferences && container.Any(member => HasReferences(root, member)))
            throw new OfflineStoryPackageImportException($"Removing package '{packageId}' would create unresolved references.");
        var relative = Path.GetRelativePath(root, provider.PackagePath);
        var bytes = File.ReadAllBytes(provider.PackagePath);
        _transaction.Apply(root, [new ProjectFileChange(relative, bytes, null)], () => { });
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
        return store.StoryLogicGraph.Load().Connections.Any(edge => ids.Contains(edge.SourceStoryId) || ids.Contains(edge.TargetStoryId));
    }

    private static string SafeReferenceFileName(string sourcePath, string packageId)
    {
        var name = Path.GetFileName(sourcePath);
        if ((name.EndsWith(".dgrs", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dgrs.g", StringComparison.OrdinalIgnoreCase))
            && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0) return name;
        return StoryUid.Parse(packageId).Value + (sourcePath.EndsWith(".dgrs.g", StringComparison.OrdinalIgnoreCase) ? ".dgrs.g" : ".dgrs");
    }

    private static string NewReferenceRelativePath(
        string root,
        string sourcePath,
        string packageId)
    {
        var fileName = SafeReferenceFileName(sourcePath, packageId);
        var candidate = Path.Combine(root, "references", fileName);
        if (!File.Exists(candidate)) return Path.Combine("references", fileName);
        var extension = sourcePath.EndsWith(".dgrs.g", StringComparison.OrdinalIgnoreCase) ? ".dgrs.g" : ".dgrs";
        var identityName = StoryUid.Parse(packageId).Value + extension;
        candidate = Path.Combine(root, "references", identityName);
        var suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(root, "references", packageId + "-" + suffix++ + extension);
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
