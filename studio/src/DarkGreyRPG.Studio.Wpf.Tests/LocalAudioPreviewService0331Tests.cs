using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class LocalAudioPreviewService0331Tests
{
    [TestMethod]
    public void PreviewDurationFormatsWithoutCrashingWhenMediaOpens()
    {
        var format = typeof(AudioPreviewControl).GetMethod("Format", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        Assert.AreEqual("0:30", format.Invoke(null, [TimeSpan.FromSeconds(30)]));
        Assert.AreEqual("1:02:03", format.Invoke(null, [TimeSpan.FromSeconds(3723)]));
    }

    [TestMethod]
    public void PreviewRejectsNonOggBeforeOpeningWpfPlayer()
    {
        using var service = new LocalAudioPreviewService(
            Path.Combine(Path.GetTempPath(), "missing-ffmpeg.exe"),
            Path.Combine(Path.GetTempPath(), "darkgrey-audio-test", Guid.NewGuid().ToString("N")));
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
        File.WriteAllBytes(path, [0]);
        try
        {
            Assert.ThrowsExactly<InvalidDataException>(() => service.LoadAsync(path).GetAwaiter().GetResult());
            Assert.AreEqual(AudioPreviewState.Empty, service.State);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void SharedControlExposesSeekablePreviewSurface()
    {
        RunOnSta(() =>
        {
            var control = new AudioPreviewControl();
            Assert.IsNotNull(control.Content);
            control.Release();
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (failure is not null) throw new AssertFailedException(failure.ToString());
    }
}
