using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>
/// Project-owned canonical 0.3.0.0 repository layout, isolated from all legacy
/// Story/Dialogue/Quest roots.
/// </summary>
public sealed class CanonicalProjectGraphStore
{
    public const string ResourcesDirectoryName = "resources";
    public const string CanonicalDirectoryName = "canonical";
    public const string StoriesDirectoryName = "stories";
    public const string SessionsDirectoryName = "sessions";
    public const string TasksDirectoryName = "tasks";
    public const string MembershipsDirectoryName = "memberships";

    public CanonicalProjectGraphStore(string projectDirectory, IAtomicFileWriter? writer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        CanonicalDirectory = Path.Combine(ProjectDirectory, ResourcesDirectoryName, CanonicalDirectoryName);
        StoriesDirectory = Path.Combine(CanonicalDirectory, StoriesDirectoryName);
        SessionsDirectory = Path.Combine(CanonicalDirectory, SessionsDirectoryName);
        TasksDirectory = Path.Combine(CanonicalDirectory, TasksDirectoryName);
        MembershipsDirectory = Path.Combine(CanonicalDirectory, MembershipsDirectoryName);
        Stories = new GraphResourceRepository(StoriesDirectory, GraphResourceKind.Story, writer);
        Sessions = new GraphResourceRepository(SessionsDirectory, GraphResourceKind.Session, writer);
        Tasks = new GraphResourceRepository(TasksDirectory, GraphResourceKind.Task, writer);
        Memberships = new CanonicalStoryMembershipRepository(MembershipsDirectory, writer);
    }

    public string ProjectDirectory { get; }
    public string CanonicalDirectory { get; }
    public string StoriesDirectory { get; }
    public string SessionsDirectory { get; }
    public string TasksDirectory { get; }
    public string MembershipsDirectory { get; }
    public GraphResourceRepository Stories { get; }
    public GraphResourceRepository Sessions { get; }
    public GraphResourceRepository Tasks { get; }
    public CanonicalStoryMembershipRepository Memberships { get; }

    public bool IsInitialized => Directories.All(Directory.Exists);

    public bool HasCanonicalData => Directories.Any(directory =>
        Directory.Exists(directory)
        && Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Any());

    /// <summary>Explicit initialization; constructing or inspecting a store is read-only.</summary>
    public void EnsureDirectories()
    {
        try
        {
            foreach (var directory in Directories) Directory.CreateDirectory(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new GraphResourceRepositoryException(
                "graph.resource.project_store.directory.failed",
                $"Could not initialize canonical project store '{CanonicalDirectory}'.",
                exception);
        }
    }

    private IReadOnlyList<string> Directories =>
        [StoriesDirectory, SessionsDirectory, TasksDirectory, MembershipsDirectory];
}
