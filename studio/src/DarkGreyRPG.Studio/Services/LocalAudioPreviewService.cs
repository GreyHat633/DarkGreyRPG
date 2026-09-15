using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Threading;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Services;

public enum AudioPreviewState
{
    Empty,
    Loading,
    Ready,
    Playing,
    Paused,
    Error,
}

/// <summary>
/// One local author-preview voice for the whole Studio process. OGG is decoded
/// by the shipped ffmpeg binary to a private WAV before WPF opens it; WPF's
/// MediaPlayer is therefore only used for the seekable PCM derivative.
/// </summary>
public sealed class LocalAudioPreviewService : IDisposable
{
    private static readonly object OwnershipGate = new();
    private static LocalAudioPreviewService? _active;
    private static readonly Dictionary<Dispatcher, LocalAudioPreviewService> SharedByDispatcher = [];

    /// <summary>
    /// One shared service per WPF dispatcher. A normal Studio process has one
    /// dispatcher, while this keeps offscreen/test STA dispatchers from
    /// reading a MediaPlayer owned by another thread.
    /// </summary>
    public static LocalAudioPreviewService Shared
    {
        get
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            lock (OwnershipGate)
            {
                if (!SharedByDispatcher.TryGetValue(dispatcher, out var service))
                {
                    service = CreateDefault();
                    SharedByDispatcher.Add(dispatcher, service);
                }
                return service;
            }
        }
    }

    /// <summary>Host hook for project/window shutdown.</summary>
    public static void StopShared()
    {
        LocalAudioPreviewService[] services;
        lock (OwnershipGate) services = SharedByDispatcher.Values.ToArray();
        foreach (var service in services) service.StopAndRelease();
    }

    /// <summary>Resolves a validated project OGG reference without accepting arbitrary paths.</summary>
    public static string ResolveProjectMediaPath(string projectRoot, string mediaRef)
    {
        if (!MediaReference.IsAudio(mediaRef)) throw new InvalidDataException("试听媒体必须是项目内的 OGG 引用。");
        return Path.Combine(Path.GetFullPath(projectRoot), "resources", mediaRef.Replace('/', Path.DirectorySeparatorChar));
    }

    private readonly string _ffmpegPath;
    private readonly string _tempRoot;
    private readonly Dispatcher _dispatcher;
    private readonly MediaPlayer _player;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private CancellationTokenSource? _loadCancellation;
    private string? _temporaryWav;
    private TaskCompletionSource<bool>? _openCompletion;
    private bool _disposed;
    private AudioPreviewState _state = AudioPreviewState.Empty;
    private string _mediaName = string.Empty;
    private string _error = string.Empty;

    public LocalAudioPreviewService(string ffmpegPath, string tempRoot)
    {
        _ffmpegPath = Path.GetFullPath(ffmpegPath ?? throw new ArgumentNullException(nameof(ffmpegPath)));
        _tempRoot = Path.GetFullPath(tempRoot ?? throw new ArgumentNullException(nameof(tempRoot)));
        Directory.CreateDirectory(_tempRoot);
        _dispatcher = Dispatcher.CurrentDispatcher;
        _player = new MediaPlayer();
        _player.MediaOpened += PlayerOnMediaOpened;
        _player.MediaEnded += PlayerOnMediaEnded;
        _player.MediaFailed += PlayerOnMediaFailed;
    }

    public event EventHandler? StateChanged;

    public AudioPreviewState State => _state;
    public string MediaName => _mediaName;
    public string? SourcePath { get; private set; }
    public string Error => _error;
    public bool IsPlaying => _state == AudioPreviewState.Playing;
    public TimeSpan Duration => _player.NaturalDuration.HasTimeSpan ? _player.NaturalDuration.TimeSpan : TimeSpan.Zero;
    public TimeSpan Position => _player.Position;
    public double Progress => Duration <= TimeSpan.Zero ? 0 : Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0, 1);
    public bool CanSeek => _state is AudioPreviewState.Ready or AudioPreviewState.Paused or AudioPreviewState.Playing
        && Duration > TimeSpan.Zero;

    public async Task LoadAsync(string sourcePath, string? mediaName = null, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(sourcePath)) throw new ArgumentException("音频路径不能为空。", nameof(sourcePath));
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source)) throw new FileNotFoundException("找不到要试听的音频。", source);
        if (!string.Equals(Path.GetExtension(source), ".ogg", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("本地试听要求 OGG 音频。");

        var loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previousCancellation = Interlocked.Exchange(ref _loadCancellation, loadCancellation);
        Cancel(previousCancellation);
        var acquired = false;
        string? output = null;

        try
        {
            await _loadGate.WaitAsync(loadCancellation.Token).ConfigureAwait(false);
            acquired = true;
            loadCancellation.Token.ThrowIfCancellationRequested();
            StopOtherServices();
            CloseCore();
            SourcePath = source;
            _mediaName = string.IsNullOrWhiteSpace(mediaName) ? Path.GetFileName(source) : mediaName;
            SetState(AudioPreviewState.Loading, string.Empty);
            var directory = Path.Combine(_tempRoot, "audio-preview", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            output = Path.Combine(directory, "preview.wav");
            _temporaryWav = output;
            await DecodeAsync(source, output, loadCancellation.Token).ConfigureAwait(false);
            loadCancellation.Token.ThrowIfCancellationRequested();
            await OpenOnDispatcherAsync(output, loadCancellation.Token).ConfigureAwait(false);
            loadCancellation.Token.ThrowIfCancellationRequested();
            lock (OwnershipGate)
            {
                loadCancellation.Token.ThrowIfCancellationRequested();
                ThrowIfDisposed();
                _active = this;
            }
        }
        catch (OperationCanceledException)
        {
            if (acquired)
            {
                CloseCore();
                SetState(AudioPreviewState.Empty, string.Empty);
            }
            else if (output is not null) DeletePreviewArtifact(output);
            throw;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception)
        {
            if (acquired)
            {
                CloseCore();
                SetState(AudioPreviewState.Error, exception.Message);
            }
            else if (output is not null) DeletePreviewArtifact(output);
            throw new InvalidDataException("音频试听失败：" + exception.Message, exception);
        }
        finally
        {
            if (acquired) _loadGate.Release();
            Interlocked.CompareExchange(ref _loadCancellation, null, loadCancellation);
            loadCancellation.Dispose();
        }
    }

    public void Play()
    {
        ThrowIfDisposed();
        if (_state is not (AudioPreviewState.Ready or AudioPreviewState.Paused)) return;
        StopOtherServices();
        _player.Play();
        lock (OwnershipGate) _active = this;
        SetState(AudioPreviewState.Playing, string.Empty);
    }

    public void Pause()
    {
        ThrowIfDisposed();
        if (_state != AudioPreviewState.Playing) return;
        _player.Pause();
        SetState(AudioPreviewState.Paused, string.Empty);
    }

    public void TogglePlayPause()
    {
        if (IsPlaying) Pause(); else Play();
    }

    public void Seek(TimeSpan position)
    {
        ThrowIfDisposed();
        if (!CanSeek) return;
        var duration = Duration;
        _player.Position = duration <= TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromTicks(Math.Clamp(position.Ticks, 0, duration.Ticks));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SeekBy(TimeSpan offset)
    {
        ThrowIfDisposed();
        if (!CanSeek) return;
        var currentTicks = Position.Ticks;
        var offsetTicks = offset.Ticks;
        var targetTicks = offsetTicks > 0 && currentTicks > long.MaxValue - offsetTicks
            ? long.MaxValue
            : offsetTicks < 0 && currentTicks < long.MinValue - offsetTicks
                ? long.MinValue
                : currentTicks + offsetTicks;
        Seek(TimeSpan.FromTicks(targetTicks));
    }

    public void SeekFraction(double fraction) => Seek(TimeSpan.FromTicks((long)(Math.Clamp(fraction, 0, 1) * Duration.Ticks)));

    public void StopAndRelease()
    {
        ThrowIfDisposed();
        Cancel(_loadCancellation);
        CloseCore();
        lock (OwnershipGate) if (ReferenceEquals(_active, this)) _active = null;
        _mediaName = string.Empty;
        SetState(AudioPreviewState.Empty, string.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cancel(_loadCancellation);
        RunOnDispatcher(CloseCore);
        lock (OwnershipGate) if (ReferenceEquals(_active, this)) _active = null;
        lock (OwnershipGate)
        {
            foreach (var entry in SharedByDispatcher.Where(entry => ReferenceEquals(entry.Value, this)).ToArray())
                SharedByDispatcher.Remove(entry.Key);
        }
        _player.MediaOpened -= PlayerOnMediaOpened;
        _player.MediaEnded -= PlayerOnMediaEnded;
        _player.MediaFailed -= PlayerOnMediaFailed;
        _loadCancellation?.Dispose();
    }

    private static LocalAudioPreviewService CreateDefault()
    {
        var tools = Path.Combine(AppContext.BaseDirectory, "media-tools", "ffmpeg", "ffmpeg.exe");
        var temp = Path.Combine(Path.GetTempPath(), "DarkGreyRPGStudio");
        return new LocalAudioPreviewService(tools, temp);
    }

    private async Task DecodeAsync(string source, string output, CancellationToken cancellationToken)
    {
        if (!File.Exists(_ffmpegPath)) throw new FileNotFoundException("Studio 未找到随附的 ffmpeg 音频解码器。", _ffmpegPath);
        var start = new ProcessStartInfo(_ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = false,
            WorkingDirectory = Path.GetDirectoryName(output)!,
        };
        start.Environment["TEMP"] = Path.GetDirectoryName(output)!;
        start.Environment["TMP"] = Path.GetDirectoryName(output)!;
        foreach (var arg in new[] { "-nostdin", "-v", "error", "-xerror", "-i", source, "-map", "0:a:0", "-vn", "-ac", "2", "-ar", "48000", "-c:a", "pcm_s16le", "-y", output }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("无法启动音频解码器。");
        using var registration = cancellationToken.Register(() => { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } });
        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0 || !File.Exists(output) || new FileInfo(output).Length < 44)
            throw new InvalidDataException(string.IsNullOrWhiteSpace(error) ? "OGG 解码器未生成可播放音频。" : error.Trim());
    }

    private Task OpenOnDispatcherAsync(string output, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _openCompletion = completion;
        return InvokeOnDispatcherAsync(() => _player.Open(new Uri(output)), cancellationToken)
            .ContinueWith(async task =>
            {
                await task.ConfigureAwait(false);
                using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken)))
                    await completion.Task.ConfigureAwait(false);
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap();
    }

    private void PlayerOnMediaOpened(object? sender, EventArgs e)
    {
        _openCompletion?.TrySetResult(true);
        SetState(AudioPreviewState.Ready, string.Empty);
    }

    private void PlayerOnMediaEnded(object? sender, EventArgs e)
    {
        SetState(AudioPreviewState.Ready, string.Empty);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PlayerOnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        var message = e.ErrorException?.Message ?? "WPF 无法打开解码后的试听音频。";
        _openCompletion?.TrySetException(new InvalidDataException(message, e.ErrorException));
        SetState(AudioPreviewState.Error, message);
    }

    private void StopOtherServices()
    {
        LocalAudioPreviewService? other;
        lock (OwnershipGate) other = _active;
        if (other is not null && !ReferenceEquals(other, this)) other.StopAndRelease();
    }

    private void CloseCore()
    {
        SourcePath = null;
        RunOnDispatcher(() =>
        {
            _player.Stop();
            _player.Close();
        });
        if (_temporaryWav is { } temporary)
        {
            try
            {
                var directory = Path.GetDirectoryName(temporary);
                if (File.Exists(temporary)) File.Delete(temporary);
                if (directory is not null && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            _temporaryWav = null;
        }
        _openCompletion = null;
    }

    private static void DeletePreviewArtifact(string output)
    {
        try
        {
            var directory = Path.GetDirectoryName(output);
            if (File.Exists(output)) File.Delete(output);
            if (directory is not null && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void Cancel(CancellationTokenSource? cancellation)
    {
        if (cancellation is null) return;
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private void SetState(AudioPreviewState state, string error)
    {
        _state = state; _error = error;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private Task InvokeOnDispatcherAsync(Action action, CancellationToken cancellationToken)
    {
        if (_dispatcher.CheckAccess()) { action(); return Task.CompletedTask; }
        return _dispatcher.InvokeAsync(action, DispatcherPriority.Normal, cancellationToken).Task;
    }

    private void RunOnDispatcher(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.Invoke(action, DispatcherPriority.Normal);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LocalAudioPreviewService));
    }
}
