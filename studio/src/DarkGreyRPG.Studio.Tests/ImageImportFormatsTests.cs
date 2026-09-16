using System.Diagnostics;
using System.Security.Cryptography;
using DarkGreyRPG.Studio.Core.Media;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ImageImportFormatsTests
{
    private static string Tools => Environment.GetEnvironmentVariable("DGR_MEDIA_TOOLS")
        ?? throw new AssertInconclusiveException("Set DGR_MEDIA_TOOLS.");

    private static async Task Run(params string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(Tools, "ffmpeg.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-nostdin", "-v", "error", "-y" }.Concat(arguments)) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.AreEqual(0, process.ExitCode, await error);
    }

    [TestMethod]
    [DataRow("png", "png", false, true)]
    [DataRow("jpg", "mjpeg", false, false)]
    [DataRow("webp", "libwebp", false, true)]
    [DataRow("bmp", "bmp", false, false)]
    [DataRow("gif", "gif", true, false)]
    [DataRow("tiff", "tiff", false, true)]
    [DataRow("apng", "apng", true, true)]
    [DataRow("webp", "libwebp_anim", true, true)]
    public async Task SupportedFormatsBecomeStaticOwnedPng(string extension, string codec, bool animated, bool alpha)
    {
        using var project = new TestProjectDirectory();
        var source = Path.Combine(project.Root, "原图." + extension);
        // Two distinct frames prove that animation imports the first frame rather than the last.
        var raw = Path.Combine(project.Root, "frames.rgba");
        var pixels = new byte[16 * 12 * 4 * 2];
        for (int i = 0; i < pixels.Length; i += 4)
        { pixels[i] = (byte)(i < pixels.Length / 2 ? 255 : 0); pixels[i + 2] = (byte)(i < pixels.Length / 2 ? 0 : 255); pixels[i + 3] = 128; }
        await File.WriteAllBytesAsync(raw, pixels);
        await Run("-f", "rawvideo", "-pixel_format", "rgba", "-video_size", "16x12", "-framerate", "2", "-i", raw,
            "-frames:v", animated ? "2" : "1", "-c:v", codec, source);
        var original = await File.ReadAllBytesAsync(source);
        var store = new ProjectMediaStore(project.Root, Path.Combine(Tools, "ffmpeg.exe"), Path.Combine(Tools, "ffprobe.exe"));
        var imported = await store.ImportImageAsync(source);
        Assert.AreEqual(16, imported.PixelWidth); Assert.AreEqual(12, imported.PixelHeight);
        Assert.IsTrue(imported.MediaRef.EndsWith(".png"));
        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(original)), imported.SourceFingerprint);
        CollectionAssert.AreEqual(original, await File.ReadAllBytesAsync(source));
        Assert.AreEqual(imported.MediaRef, (await store.ImportImageAsync(source)).MediaRef);
        var decoded = Path.Combine(project.Root, "decoded.rgba");
        await Run("-i", store.Resolve(imported.MediaRef), "-pix_fmt", "rgba", "-f", "rawvideo", decoded);
        var actual = await File.ReadAllBytesAsync(decoded);
        Assert.HasCount(16 * 12 * 4, actual); // Exactly one frame.
        Assert.IsGreaterThan((byte)200, actual[0]); Assert.IsLessThan((byte)30, actual[2]);
        if (alpha) Assert.AreEqual((byte)128, actual[3]);
        File.Delete(source);
        await store.VerifyAsync(imported.MediaRef);
        MediaPayloadValidation.Validate(imported.MediaRef, await File.ReadAllBytesAsync(store.Resolve(imported.MediaRef)));
    }

    [TestMethod]
    public async Task MismatchedExtensionImportsButCorruptionAndPixelLimitLeaveNoMedia()
    {
        using var project = new TestProjectDirectory();
        var jpg = Path.Combine(project.Root, "source.jpg");
        await Run("-f", "lavfi", "-i", "color=red:s=16x12", "-frames:v", "1", jpg);
        var disguised = Path.Combine(project.Root, "实际是JPEG.png"); File.Copy(jpg, disguised);
        var store = new ProjectMediaStore(project.Root, Path.Combine(Tools, "ffmpeg.exe"), Path.Combine(Tools, "ffprobe.exe"));
        Assert.AreEqual((await store.ImportImageAsync(jpg)).MediaRef, (await store.ImportImageAsync(disguised)).MediaRef);
        var broken = Path.Combine(project.Root, "broken.png"); await File.WriteAllTextAsync(broken, "broken");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.ImportImageAsync(broken));
        var huge = Path.Combine(project.Root, "huge.png");
        await Run("-f", "lavfi", "-i", "color=red:s=8194x4096", "-frames:v", "1", huge);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.ImportImageAsync(huge));
        Assert.HasCount(1, Directory.GetFiles(Path.Combine(project.Root, "resources", "media")));
        Assert.HasCount(0, Directory.GetDirectories(Path.Combine(project.Root, "resources", "media_work")));
    }
}
