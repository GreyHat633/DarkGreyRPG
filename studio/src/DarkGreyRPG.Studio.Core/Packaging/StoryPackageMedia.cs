using System.Security.Cryptography;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Core.Packaging;

internal static class StoryPackageMedia
{
    internal static SortedSet<string> Collect(StoryPackageRequiredResources required, Func<string, string> readJson)
    {
        var refs = new SortedSet<string>(StringComparer.Ordinal);
        void Add(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Null) return;
            if (value.ValueKind != JsonValueKind.String || !MediaReference.IsValid(value.GetString()))
                throw new StoryPackageException("资源包含无效的项目媒体引用。");
            refs.Add(value.GetString()!);
        }
        foreach (var path in required.Actors)
        {
            using var doc = JsonDocument.Parse(readJson(path));
            if (doc.RootElement.TryGetProperty("default_portrait_ref", out var portrait)) Add(portrait);
            if (doc.RootElement.TryGetProperty("portrait_variants", out var variants))
                foreach (var variant in variants.EnumerateArray()) Add(variant.GetProperty("media_ref"));
        }
        foreach (var path in required.Sessions)
        {
            var session = GraphResourceEnvelopeSerializer.Deserialize(readJson(path));
            foreach (var node in session.Graph?.Nodes ?? [])
            {
                if (node.Type == "line")
                {
                    if (Graphs.Definitions.CanonicalSessionLineSchema.Validate(node).Count != 0)
                        throw new StoryPackageException("台词数据无效，无法确定全部媒体引用。");
                    foreach (var page in Graphs.Definitions.CanonicalSessionLineSchema.ReadPages(node))
                        if (page.TryGetValue("voice_ref", out var voice)) Add(voice);
                }
                if (node.Type == "music" && node.Properties.TryGetValue("media_ref", out var music)) Add(music);
                if (node.Type == "screen" && node.Properties.TryGetValue("layers", out var layers))
                    foreach (var layer in layers.EnumerateArray()) Add(layer.GetProperty("media_ref"));
            }
        }
        return refs;
    }

    internal static void CopyReachable(string projectRoot, string packageRoot, StoryPackageRequiredResources required)
    {
        foreach (var mediaRef in Collect(required, path => File.ReadAllText(Path.Combine(packageRoot, path))))
        {
            var source = Path.Combine(projectRoot, "resources", mediaRef);
            var target = Path.Combine(packageRoot, mediaRef);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, false);
            using (var stream = File.OpenRead(target)) Validate(mediaRef, stream);
            required.Media.Add(mediaRef);
        }
    }

    internal static void Validate(string mediaRef, Stream stream)
    {
        if (!MediaReference.IsValid(mediaRef)) throw new StoryPackageException("无效的媒体路径。");
        if (stream.Length > 64L * 1024 * 1024) throw new StoryPackageException("运行时媒体超过单个包条目的 64 MiB 上限。");
        var hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (hash != Path.GetFileNameWithoutExtension(mediaRef)) throw new StoryPackageException("媒体指纹与内容不一致：" + mediaRef);
        stream.Position = 0;
        Span<byte> header = stackalloc byte[8];
        var count = stream.Read(header);
        var valid = mediaRef.EndsWith(".ogg", StringComparison.Ordinal) ? count >= 4 && header[..4].SequenceEqual("OggS"u8)
            : mediaRef.EndsWith(".png", StringComparison.Ordinal) ? count == 8 && header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            : count >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        if (!valid) throw new StoryPackageException("媒体格式与内容不一致：" + mediaRef);
        stream.Position = 0;
        using var payload = new MemoryStream(); stream.CopyTo(payload);
        try { DarkGreyRPG.Studio.Core.Media.MediaPayloadValidation.Validate(mediaRef, payload.ToArray()); }
        catch (InvalidDataException exception) { throw new StoryPackageException("媒体容器损坏：" + mediaRef, exception); }
    }
}
