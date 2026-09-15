using System.Windows.Controls;
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

    [TestMethod]
    public void PreviewControlUsesClassicTransportLayoutWithoutStatusActions()
    {
        RunOnSta(() =>
        {
            var control = new AudioPreviewControl();
            var root = (StackPanel)control.Content!;
            var transport = (Grid)root.Children[0];
            var buttons = transport.Children.OfType<Button>().ToArray();

            CollectionAssert.AreEqual(new object[] { "-5s", "▶", "+5s" }, buttons.Select(button => button.Content).ToArray());
            Assert.IsFalse(buttons[0].IsEnabled);
            Assert.IsFalse(buttons[2].IsEnabled);
            Assert.IsInstanceOfType(root.Children[1], typeof(Slider));
            Assert.IsFalse(((Slider)root.Children[1]).IsEnabled);
            Assert.IsFalse(root.Children.OfType<TextBlock>().Any(text => text.Text is "试听" or "清除" or "未选择音频"));
            control.Release();
        });
    }

    [TestMethod]
    public void SeekControlsRemainUnavailableUntilMediaIsReady()
    {
        using var service = new LocalAudioPreviewService(
            Path.Combine(Path.GetTempPath(), "missing-ffmpeg.exe"),
            Path.Combine(Path.GetTempPath(), "darkgrey-audio-test", Guid.NewGuid().ToString("N")));

        Assert.IsFalse(service.CanSeek);
        service.SeekBy(TimeSpan.FromSeconds(5));
        service.SeekFraction(-1);
        service.SeekFraction(2);
        Assert.AreEqual(AudioPreviewState.Empty, service.State);
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
