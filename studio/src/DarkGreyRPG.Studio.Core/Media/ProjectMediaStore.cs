using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Core.Media;

public sealed record MediaImportLimits(long MaximumSourceBytes = 268435456, double MaximumDurationSeconds = 3600);

public sealed record ImportedMedia(string MediaRef, string OriginalName, string SourceFingerprint, long Bytes, double DurationSeconds);

/// <summary>Project-owned import; resources reference only runtime bytes, never the external source.</summary>
public sealed class ProjectMediaStore
{
    private readonly MediaImportLimits _limits;
    private readonly string _root;
    private readonly string _ffmpeg;
    private readonly string _ffprobe;
    public ProjectMediaStore(string projectRoot, string ffmpeg, string ffprobe, MediaImportLimits? limits = null)
    {
        _limits = limits ?? new();
        if (_limits.MaximumSourceBytes <= 0 || !double.IsFinite(_limits.MaximumDurationSeconds) || _limits.MaximumDurationSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(limits));
        _root = Path.GetFullPath(projectRoot);
        _ffmpeg = Path.GetFullPath(ffmpeg);
        _ffprobe = Path.GetFullPath(ffprobe);
    }

    public Task<ImportedMedia> ImportAudioAsync(string source, CancellationToken cancellationToken = default)
        => Task.Run(() => ImportAudioCoreAsync(source, cancellationToken), cancellationToken);

    private async Task<ImportedMedia> ImportAudioCoreAsync(string source, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension is not (".mp3" or ".wav" or ".ogg")) throw new InvalidDataException("请选择 MP3、WAV 或 OGG 音频。");
        if (new FileInfo(source).Length > _limits.MaximumSourceBytes) throw new InvalidDataException("音频文件超过当前导入大小限制。");
        var workspace = Path.Combine(_root, "resources", "media_work", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            // Snapshot before probing/conversion: external changes cannot race the imported master.
            var master = Path.Combine(workspace, "source" + extension);
            await using (var input = File.OpenRead(source))
            await using (var output = new FileStream(master, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920]; long copied = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, cancellationToken)) != 0)
                {
                    copied += count;
                    if (copied > _limits.MaximumSourceBytes) throw new InvalidDataException("音频文件超过当前导入大小限制。");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }
            }
            var probe = await RunAsync(_ffprobe, workspace, ["-v", "error", "-select_streams", "a:0", "-show_entries", "stream=codec_name,channels:format=duration", "-of", "json", master], cancellationToken);
            using var info = JsonDocument.Parse(probe);
            var streams = info.RootElement.GetProperty("streams");
            if (streams.GetArrayLength() == 0) throw new InvalidDataException("文件中没有可用音轨。");
            var codec = streams[0].GetProperty("codec_name").GetString();
            var channels = streams[0].GetProperty("channels").GetInt32();
            var duration = info.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var durationValue)
                && double.TryParse(durationValue.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            if (!double.IsFinite(duration) || duration <= 0 || duration > _limits.MaximumDurationSeconds) throw new InvalidDataException("音频时长无效或超过当前导入时长限制。");
            var runtime = Path.Combine(workspace, "runtime.ogg");
            if (extension == ".ogg" && codec == "vorbis" && channels is 1 or 2)
            {
                await RunAsync(_ffmpeg, workspace, ["-nostdin", "-v", "error", "-xerror", "-i", master, "-map", "0:a:0", "-f", "null", "-"], cancellationToken);
                File.Copy(master, runtime);
            }
            else
                await RunAsync(_ffmpeg, workspace, ["-nostdin", "-v", "error", "-xerror", "-i", master, "-map", "0:a:0", "-vn", "-map_metadata", "-1", "-c:a", "libvorbis", "-ac", "2", "-ar", "48000", "-q:a", "5", "-fflags", "+bitexact", "-flags:a", "+bitexact", runtime], cancellationToken);
            var sourceHash = await HashAsync(master, cancellationToken);
            var hash = await HashAsync(runtime, cancellationToken);
            var mediaRef = "media/" + hash + ".ogg";
            Install(master, Path.Combine(_root, "resources", "media_sources", sourceHash + extension));
            Install(runtime, Resolve(mediaRef));
            var result = new ImportedMedia(mediaRef, Path.GetFileName(source), sourceHash, new FileInfo(runtime).Length, duration);
            var metadata = Path.Combine(_root, "resources", "media_metadata", hash + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(metadata)!);
            if (!File.Exists(metadata)) await File.WriteAllTextAsync(metadata, JsonSerializer.Serialize(result), cancellationToken);
            return result;
        }
        finally
        {
            // This GUID directory is created by this import and is never an external path.
            if (Directory.Exists(workspace)) Directory.Delete(workspace, true);
        }
    }

    public Task<ImportedMedia> ImportImageAsync(string source, CancellationToken cancellationToken = default)
        => Task.Run(() => ImportImageCoreAsync(source, cancellationToken), cancellationToken);

    private async Task<ImportedMedia> ImportImageCoreAsync(string source, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(source).ToLowerInvariant();
        if (extension == ".jpeg") extension = ".jpg";
        if (extension is not (".png" or ".jpg")) throw new InvalidDataException("请选择 PNG 或 JPG 图片。");
        if (new FileInfo(source).Length > _limits.MaximumSourceBytes) throw new InvalidDataException("图片超过当前导入大小限制。");
        var workspace = Path.Combine(_root, "resources", "media_work", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var master = Path.Combine(workspace, "image" + extension);
            await using (var input = File.OpenRead(source))
            await using (var output = new FileStream(master, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                await input.CopyToAsync(output, cancellationToken);
            var probe = await RunAsync(_ffprobe, workspace, ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=codec_name,width,height", "-of", "json", master], cancellationToken);
            using var info = JsonDocument.Parse(probe);
            var streams = info.RootElement.GetProperty("streams");
            if (streams.GetArrayLength() != 1) throw new InvalidDataException("图片中没有可用画面。");
            var stream = streams[0];
            var codec = stream.GetProperty("codec_name").GetString();
            var width = stream.GetProperty("width").GetInt32(); var height = stream.GetProperty("height").GetInt32();
            if ((extension == ".png" && codec != "png") || (extension == ".jpg" && codec != "mjpeg")
                || width <= 0 || height <= 0 || (long)width * height > 33554432)
                throw new InvalidDataException("图片格式不匹配或像素数量超过当前限制。");
            await RunAsync(_ffmpeg, workspace, ["-nostdin", "-v", "error", "-xerror", "-i", master, "-frames:v", "1", "-f", "null", "-"], cancellationToken);
            var hash = await HashAsync(master, cancellationToken);
            var mediaRef = "media/" + hash + extension;
            Install(master, Resolve(mediaRef));
            var result = new ImportedMedia(mediaRef, Path.GetFileName(source), hash, new FileInfo(master).Length, 0);
            var metadata = Path.Combine(_root, "resources", "media_metadata", hash + ".json");
            Directory.CreateDirectory(Path.GetDirectoryName(metadata)!);
            if (!File.Exists(metadata)) await File.WriteAllTextAsync(metadata, JsonSerializer.Serialize(result), cancellationToken);
            return result;
        }
        finally { if (Directory.Exists(workspace)) Directory.Delete(workspace, true); }
    }

    public string Resolve(string mediaRef)
    {
        if (!MediaReference.IsValid(mediaRef)) throw new InvalidDataException("无效的项目媒体引用。");
        return Path.Combine(_root, "resources", mediaRef.Replace('/', Path.DirectorySeparatorChar));
    }

    public async Task VerifyAsync(string mediaRef, CancellationToken cancellationToken = default)
    {
        if (await HashAsync(Resolve(mediaRef), cancellationToken) != Path.GetFileNameWithoutExtension(mediaRef))
            throw new InvalidDataException("项目媒体内容校验失败。");
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static byte[] FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    private static void Install(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            if (!FileHash(source).SequenceEqual(FileHash(destination)))
                throw new InvalidDataException("媒体指纹位置已有损坏文件。");
            return;
        }
        // Destination appears atomically; readers never observe a partial derivative.
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.Copy(source, temporary); File.Move(temporary, destination, false); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static async Task<string> ReadLimitedAsync(StreamReader reader)
    {
        var text = new System.Text.StringBuilder(); var buffer = new char[4096]; int count;
        while ((count = await reader.ReadAsync(buffer)) != 0)
            if (text.Length < 65536) text.Append(buffer, 0, Math.Min(count, 65536 - text.Length));
        return text.ToString();
    }

    private static async Task<string> RunAsync(string executable, string workspace, string[] arguments, CancellationToken cancellationToken)
    {
        var callerToken = cancellationToken;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeout.Token);
        cancellationToken = linked.Token;
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = workspace };
        start.Environment["TEMP"] = workspace; start.Environment["TMP"] = workspace;
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("无法启动音频转换工具。");
        using var registration = cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var output = ReadLimitedAsync(process.StandardOutput);
        var error = ReadLimitedAsync(process.StandardError);
        try { await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException)
        {
            await process.WaitForExitAsync(CancellationToken.None);
            if (!callerToken.IsCancellationRequested) throw new IOException("媒体处理超时，请检查源文件。");
            throw;
        }
        var message = await error;
        if (process.ExitCode != 0) throw new InvalidDataException("媒体处理失败：" + message);
        return await output;
    }
}
