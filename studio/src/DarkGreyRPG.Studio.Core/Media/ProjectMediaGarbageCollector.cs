using System.Text.Json;
using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Core.Media;

public sealed record MediaGarbageCollectionResult(int RuntimeFiles, int SourceFiles, int WorkDirectories);

/// <summary>Only call before the first editor opens, with no other Studio process using the project.</summary>
public static class ProjectMediaGarbageCollector
{
    public static MediaGarbageCollectionResult CollectAtStartup(string projectRoot)
    {
        var root = Path.GetFullPath(projectRoot);
        var required = new StoryPackageRequiredResources
        {
            Actors = FilesUnder(root, "actors").Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList(),
            Sessions = FilesUnder(root, "resources/canonical/sessions").Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList(),
        };
        // Parse every authoritative root before deleting anything. Invalid saved data aborts collection.
        var reachable = StoryPackageMedia.Collect(required, path => File.ReadAllText(Path.Combine(root, path)));
        var runtimeFiles = FilesUnder(root, "resources/media").ToArray();
        var metadataFiles = FilesUnder(root, "resources/media_metadata").ToArray();
        var sourceFiles = FilesUnder(root, "resources/media_sources").ToArray();
        _ = FilesUnder(root, "resources/media_work").ToArray();
        var knownMetadata = new HashSet<string>(StringComparer.Ordinal);
        var sourceRoots = new HashSet<string>(StringComparer.Ordinal);
        var unusedMetadata = new List<string>();
        foreach (var path in metadataFiles)
        {
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, path)));
            if (json.RootElement.ValueKind != JsonValueKind.Object
                || !json.RootElement.TryGetProperty("MediaRef", out var referenceValue) || referenceValue.ValueKind != JsonValueKind.String
                || !json.RootElement.TryGetProperty("SourceFingerprint", out var fingerprintValue) || fingerprintValue.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("媒体元数据不完整，未执行回收。");
            var reference = referenceValue.GetString(); var fingerprint = fingerprintValue.GetString();
            if (!MediaReference.IsValid(reference) || fingerprint is null || !Regex.IsMatch(fingerprint, "\\A[0-9a-f]{64}\\z"))
                throw new InvalidDataException("媒体元数据无效，未执行回收。");
            knownMetadata.Add(reference!);
            if (reachable.Contains(reference!)) sourceRoots.Add(fingerprint); else unusedMetadata.Add(path);
        }
        var runtimeCount = 0; var sourceCount = 0; var workCount = 0;
        foreach (var path in runtimeFiles)
        {
            var reference = path["resources/".Length..];
            if (MediaReference.IsValid(reference) && !reachable.Contains(reference)) { File.Delete(Path.Combine(root, path)); runtimeCount++; }
        }
        foreach (var path in unusedMetadata) File.Delete(Path.Combine(root, path));
        foreach (var path in reachable.Any(reference => reference.EndsWith(".ogg", StringComparison.Ordinal) && !knownMetadata.Contains(reference)) ? [] : sourceFiles)
        {
            var file = Path.GetFileName(path); var fingerprint = Path.GetFileNameWithoutExtension(file);
            if (Regex.IsMatch(file, "\\A[0-9a-f]{64}\\.(mp3|wav|ogg)\\z") && !sourceRoots.Contains(fingerprint))
            { File.Delete(Path.Combine(root, path)); sourceCount++; }
        }
        var workRoot = Path.Combine(root, "resources", "media_work");
        if (Directory.Exists(workRoot)) foreach (var directory in Directory.GetDirectories(workRoot))
        {
            if (Regex.IsMatch(Path.GetFileName(directory), "\\A[0-9a-f]{32}\\z")) { Directory.Delete(directory, true); workCount++; }
        }
        return new(runtimeCount, sourceCount, workCount);
    }

    private static IEnumerable<string> FilesUnder(string root, string relative)
    {
        var directory = Path.Combine(root, relative);
        if (!Directory.Exists(directory)) return [];
        var result = new List<string>();
        var pending = new Stack<string>(); pending.Push(directory);
        // Root and ancestors must not redirect collection outside the project.
        for (var parent = new DirectoryInfo(directory); parent is not null; parent = parent.Parent)
        {
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("媒体回收不遍历链接目录。");
            if (parent.FullName.Equals(root, StringComparison.OrdinalIgnoreCase)) break;
        }
        while (pending.TryPop(out var current)) foreach (var entry in new DirectoryInfo(current).EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("媒体回收不遍历链接。");
            if (entry is DirectoryInfo) pending.Push(entry.FullName);
            else result.Add(Path.GetRelativePath(root, entry.FullName).Replace('\\', '/'));
        }
        return result;
    }
}
