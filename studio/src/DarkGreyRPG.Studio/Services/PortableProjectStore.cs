using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using DarkGreyRPG.Studio.Settings;

namespace DarkGreyRPG.Studio.Services;

/// <summary>Imports external projects while retaining explicitly created project locations.</summary>
public sealed class PortableProjectStore(StudioStoragePaths paths, ISettingsService? settingsService = null)
{
    private readonly ISettingsService _settings = settingsService ?? new SettingsService(storagePaths: paths);
    private readonly HashSet<string> _createdInPlace = new(StringComparer.OrdinalIgnoreCase);
    public StudioStoragePaths Paths { get; } = paths;

    public void ValidateCreationDestination(string directory)
    {
        var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        StudioStoragePaths.EnsureNoDirectoryLinks(destination);
        if ((Paths.Contains(destination) && !Paths.ContainsProject(destination))
            || StudioStoragePaths.IsWithin(Paths.Root, destination))
            throw new IOException("请选择 Data/Projects 下的项目文件夹，或 Studio 程序目录以外的位置。");
    }

    public void RememberCreatedProject(string directory)
    {
        if (Paths.ContainsProject(directory)) return;
        var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        _createdInPlace.Add(destination);
        var settings = _settings.Load();
        _settings.Save(settings with
        {
            ExternalProjects = settings.ExternalProjects.Concat(_createdInPlace)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        });
    }

    public string PrepareOpen(string directory, bool isRestore)
    {
        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (_createdInPlace.Contains(source) || _settings.Load().ExternalProjects.Any(path =>
                Path.TrimEndingDirectorySeparator(path).Equals(source, StringComparison.OrdinalIgnoreCase)))
        {
            ValidateCreationDestination(source);
            ValidateTree(source);
            return source;
        }
        if (isRestore && !Paths.ContainsProject(source))
            throw new IOException("上次项目未由此 Studio 管理，请通过“打开项目”导入。");
        return Import(source);
    }

    public string Import(string sourceDirectory)
    {
        var source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory));
        StudioStoragePaths.EnsureNoDirectoryLinks(source);
        if (!File.Exists(Path.Combine(source, "project.json")))
            throw new IOException("请选择包含 project.json 的项目目录。");
        if (Paths.ContainsProject(source))
        {
            ValidateTree(source);
            return source;
        }
        // An ancestor of Studio would recursively copy the running application into itself.
        if (StudioStoragePaths.IsWithin(Paths.Root, source))
            throw new IOException("项目目录不能包含 Studio 程序目录。");
        ValidateTree(source);
        Paths.Initialize();
        var destination = AvailableDirectory(new DirectoryInfo(source).Name);
        var staging = Path.Combine(Paths.Temp, "project-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(staging, Path.GetRelativePath(source, directory)));
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                // Recheck at the point of copying, including changes since the initial inventory.
                StudioStoragePaths.EnsureNoDirectoryLinks(Path.GetDirectoryName(file)!);
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("项目包含文件链接：" + file);
                var target = Path.Combine(staging, Path.GetRelativePath(source, file));
                File.Copy(file, target, false);
                using var original = File.OpenRead(file);
                using var copied = File.OpenRead(target);
                if (original.Length != copied.Length || !SHA256.HashData(original).AsSpan().SequenceEqual(SHA256.HashData(copied)))
                    throw new IOException("项目文件复制校验失败：" + file);
            }
            // Validate only metadata here; actual format migration runs on the installed copy.
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(staging, "project.json")));
            if (manifest.RootElement.ValueKind != JsonValueKind.Object)
                throw new IOException("项目描述文件无效。");
            ValidateTree(source);
            var sourceFiles = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(source, file)).Order(StringComparer.OrdinalIgnoreCase);
            var copiedFiles = Directory.GetFiles(staging, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(staging, file)).Order(StringComparer.OrdinalIgnoreCase);
            if (!sourceFiles.SequenceEqual(copiedFiles, StringComparer.OrdinalIgnoreCase))
                throw new IOException("复制期间项目文件列表发生变化，请重试。");
            Directory.Move(staging, destination);
            return destination;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    public string AvailableDirectory(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName) || folderName is "." or ".."
            || folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.IsPathRooted(folderName))
            throw new IOException("项目文件夹名无效。");
        StudioStoragePaths.EnsureNoDirectoryLinks(Paths.Projects);
        var candidate = Path.Combine(Paths.Projects, folderName);
        for (var suffix = 2; Directory.Exists(candidate) || File.Exists(candidate); suffix++)
            candidate = Path.Combine(Paths.Projects, folderName + "_" + suffix);
        return candidate;
    }

    private static void ValidateTree(string root)
    {
        // Enumerate level by level so a directory junction is rejected before it is followed.
        foreach (var entry in Directory.EnumerateFileSystemEntries(root))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("项目包含目录或文件链接，无法作为独立本地项目导入：" + entry);
            if ((attributes & FileAttributes.Directory) != 0) ValidateTree(entry);
        }
    }
}
