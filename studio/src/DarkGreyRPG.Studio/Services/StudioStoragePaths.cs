using System.IO;

namespace DarkGreyRPG.Studio.Services;

/// <summary>All application-owned state is relative to this copy of Studio.</summary>
public sealed class StudioStoragePaths
{
    public static StudioStoragePaths Default { get; } = new(ResolveRoot(AppContext.BaseDirectory, Environment.ProcessPath));

    // The native root apphost loads its managed entry point and runtime from Program.
    // Keep user data beside that apphost, including after moving the whole copy.
    public static string ResolveRoot(string applicationDirectory, string? processPath)
    {
        var application = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationDirectory));
        var parent = Path.GetDirectoryName(application);
        if (Path.GetFileName(application).Equals("Program", StringComparison.OrdinalIgnoreCase)
            && parent is not null && !string.IsNullOrWhiteSpace(processPath)
            && Path.GetFullPath(processPath).Equals(Path.Combine(parent, "DarkGreyRPGStudio.exe"), StringComparison.OrdinalIgnoreCase))
            return parent;
        return application;
    }

    public StudioStoragePaths(string applicationDirectory)
    {
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationDirectory));
        Data = Path.Combine(Root, "Data");
    }

    public string Root { get; }
    public string Data { get; }
    public string Config => Path.Combine(Data, "Config");
    public string Settings => Path.Combine(Config, "settings.json");
    public string Projects => Path.Combine(Data, "Projects");
    public string Cache => Path.Combine(Data, "Cache");
    public string Logs => Path.Combine(Data, "Logs");
    public string Temp => Path.Combine(Data, "Temp");
    public string Exports => Path.Combine(Data, "Exports");
    public string MediaTools => Path.Combine(Root, "Tools", "FFmpeg");

    public void Initialize()
    {
        EnsureNoDirectoryLinks(Root);
        foreach (var directory in new[] { Config, Projects, Cache, Logs, Temp, Exports })
        {
            EnsureNoDirectoryLinks(directory);
            Directory.CreateDirectory(directory);
        }
        var probe = Path.Combine(Temp, "write-check-" + Guid.NewGuid().ToString("N"));
        using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                   1, FileOptions.DeleteOnClose)) { }
    }

    public bool Contains(string path) => IsWithin(path, Root);
    public bool ContainsProject(string path) => IsWithin(path, Projects)
        && !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)), Projects, StringComparison.OrdinalIgnoreCase);

    public static bool IsWithin(string path, string directory)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        return full.Equals(root, StringComparison.OrdinalIgnoreCase)
            || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureNoDirectoryLinks(string path)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(path)); current is not null; current = current.Parent)
        {
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Studio 数据目录不能包含目录链接：" + current.FullName);
        }
    }
}
