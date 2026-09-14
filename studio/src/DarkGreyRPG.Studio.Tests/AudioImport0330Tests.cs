using System.Diagnostics;
using DarkGreyRPG.Studio.Core.Media;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class AudioImport0330Tests
{
    [TestMethod]
    public async Task PngAndJpegImportsDecodeAndRemainOwnedAfterSourcesAreRemoved()
    {
        var tools = Environment.GetEnvironmentVariable("DGR_MEDIA_TOOLS");
        if (string.IsNullOrWhiteSpace(tools)) { Assert.Inconclusive("Set DGR_MEDIA_TOOLS."); return; }
        using var project = new TestProjectDirectory();
        string png = Path.Combine(project.Root, "外部头像.png"), jpg = Path.Combine(project.Root, "外部头像.jpg");
        await File.WriteAllBytesAsync(png, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+ip1sAAAAASUVORK5CYII="));
        string ffmpeg = Path.Combine(tools, "ffmpeg.exe");
        var processInfo = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-nostdin", "-v", "error", "-i", png, "-frames:v", "1", "-pix_fmt", "yuvj444p", jpg }) processInfo.ArgumentList.Add(arg);
        using (var process = Process.Start(processInfo)!) { var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); Assert.AreEqual(0, process.ExitCode, await error); }
        var store = new ProjectMediaStore(project.Root, ffmpeg, Path.Combine(tools, "ffprobe.exe"));
        var first = await store.ImportImageAsync(png); var second = await store.ImportImageAsync(jpg);
        Assert.AreEqual(first.MediaRef, (await store.ImportImageAsync(png)).MediaRef);
        File.Delete(png); File.Delete(jpg);
        await store.VerifyAsync(first.MediaRef); await store.VerifyAsync(second.MediaRef);
        MediaPayloadValidation.Validate(first.MediaRef, await File.ReadAllBytesAsync(store.Resolve(first.MediaRef)));
        MediaPayloadValidation.Validate(second.MediaRef, await File.ReadAllBytesAsync(store.Resolve(second.MediaRef)));
    }

    [TestMethod]
    public async Task WavAndMp3BecomeOwnedVorbisAndDuplicateImportsReuseBytes()
    {
        var tools = Environment.GetEnvironmentVariable("DGR_MEDIA_TOOLS");
        if (string.IsNullOrWhiteSpace(tools)) { Assert.Inconclusive("Set DGR_MEDIA_TOOLS to the isolated FFmpeg directory."); return; }
        var root = Path.Combine(Path.GetTempPath(), "dgr-audio-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var wav = Path.Combine(root, "外部 原音.wav");
            using (var writer = new BinaryWriter(File.Create(wav)))
            {
                var samples = 8000;
                writer.Write("RIFF"u8.ToArray()); writer.Write(36 + samples * 2); writer.Write("WAVEfmt "u8.ToArray());
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000); writer.Write((short)2); writer.Write((short)16);
                writer.Write("data"u8.ToArray()); writer.Write(samples * 2);
                for (var i = 0; i < samples; i++) writer.Write((short)(12000 * Math.Sin(i * Math.PI * 2 * 440 / 16000)));
            }
            var ffmpeg = Path.Combine(tools, "ffmpeg.exe");
            var store = new ProjectMediaStore(Path.Combine(root, "project"), ffmpeg, Path.Combine(tools, "ffprobe.exe"));
            var first = await store.ImportAudioAsync(wav);
            var second = await store.ImportAudioAsync(wav);
            Assert.AreEqual(first.MediaRef, second.MediaRef);
            Assert.IsTrue(first.DurationSeconds > 0);
            Assert.IsTrue(first.MediaRef.EndsWith(".ogg", StringComparison.Ordinal));
            var mp3 = Path.Combine(root, "音轨.mp3");
            var start = new ProcessStartInfo(ffmpeg) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-nostdin", "-v", "error", "-i", wav, "-c:a", "libmp3lame", mp3 }) start.ArgumentList.Add(arg);
            using (var process = Process.Start(start)!)
            {
                var error = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync(); Assert.AreEqual(0, process.ExitCode, await error);
            }
            var importedMp3 = await store.ImportAudioAsync(mp3);
            File.Delete(wav); File.Delete(mp3);
            await store.VerifyAsync(first.MediaRef); await store.VerifyAsync(importedMp3.MediaRef);
            var direct = await store.ImportAudioAsync(store.Resolve(first.MediaRef));
            Assert.AreEqual(first.MediaRef, direct.MediaRef);
            var payload = await File.ReadAllBytesAsync(store.Resolve(first.MediaRef));
            MediaPayloadValidation.Validate(first.MediaRef, payload);
            var broken = (byte[])payload.Clone(); broken[^1] ^= 1;
            Assert.ThrowsExactly<InvalidDataException>(() => MediaPayloadValidation.Validate(first.MediaRef, broken));
            var fixture = Environment.GetEnvironmentVariable("DGR_AUDIO_FIXTURE");
            if (!string.IsNullOrWhiteSpace(fixture)) { Directory.CreateDirectory(Path.GetDirectoryName(fixture)!); File.WriteAllBytes(fixture, payload); }
            await File.AppendAllTextAsync(store.Resolve(first.MediaRef), "corrupt");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.VerifyAsync(first.MediaRef));
        }
        finally { Directory.Delete(root, true); }
    }
}
