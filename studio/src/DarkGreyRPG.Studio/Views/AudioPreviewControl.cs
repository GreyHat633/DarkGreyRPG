using System.IO;
using System.Windows;
using System.Windows.Controls;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.Views;

/// <summary>Shared inline controls for line voice and music local preview.</summary>
public sealed class AudioPreviewControl : UserControl
{
    private readonly TextBlock _duration = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _rewind = new() { Content = "-5s", MinWidth = 48, ToolTip = "后退 5 秒" };
    private readonly Button _play = new() { MinWidth = 44, ToolTip = "播放或暂停" };
    private readonly Button _forward = new() { Content = "+5s", MinWidth = 48, ToolTip = "前进 5 秒" };
    private readonly Slider _progress = new() { Minimum = 0, Maximum = 1, VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true };
    private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
    private LocalAudioPreviewService? _service;
    private CancellationTokenSource? _loadCancellation;
    private long _loadGeneration;
    private bool _refreshing;
    private readonly System.Windows.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private string? _configuredPath;
    private bool OwnsPreview => _service is not null && !string.IsNullOrEmpty(_configuredPath) && string.Equals(_service.SourcePath, _configuredPath, StringComparison.OrdinalIgnoreCase);

    public AudioPreviewControl()
    {
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        _duration.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        _error.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        BuildVisualTree();
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { AttachService(PreviewService ?? LocalAudioPreviewService.Shared); _timer.Start(); };
        Unloaded += (_, _) =>
        {
            // Unloaded is a real editing-context boundary: release the WAV and
            // decoder/player handles so switching nodes cannot leave audio open.
            CancelPendingLoad();
            if (OwnsPreview) _service?.StopAndRelease();
            _timer.Stop();
            if (_service is not null) _service.StateChanged -= OnServiceChanged;
            _service = null;
        };
    }

    public static readonly DependencyProperty MediaPathProperty = DependencyProperty.Register(
        nameof(MediaPath), typeof(string), typeof(AudioPreviewControl), new PropertyMetadata(null, (d, _) => ((AudioPreviewControl)d).LoadConfiguredMedia()));

    public string? MediaPath
    {
        get => (string?)GetValue(MediaPathProperty);
        set => SetValue(MediaPathProperty, value);
    }

    public static readonly DependencyProperty MediaNameProperty = DependencyProperty.Register(
        nameof(MediaName), typeof(string), typeof(AudioPreviewControl), new PropertyMetadata(null, (d, _) => ((AudioPreviewControl)d).Refresh()));

    public string? MediaName
    {
        get => (string?)GetValue(MediaNameProperty);
        set => SetValue(MediaNameProperty, value);
    }

    public static readonly DependencyProperty PreviewServiceProperty = DependencyProperty.Register(
        nameof(PreviewService), typeof(LocalAudioPreviewService), typeof(AudioPreviewControl), new PropertyMetadata(null, (d, args) => ((AudioPreviewControl)d).AttachService((LocalAudioPreviewService?)args.NewValue)));

    public LocalAudioPreviewService? PreviewService
    {
        get => (LocalAudioPreviewService?)GetValue(PreviewServiceProperty);
        set => SetValue(PreviewServiceProperty, value);
    }

    public static readonly DependencyProperty ProjectDirectoryProperty = DependencyProperty.Register(nameof(ProjectDirectory), typeof(string), typeof(AudioPreviewControl), new PropertyMetadata(null, (d, _) => ((AudioPreviewControl)d).LoadConfiguredMedia()));
    public string? ProjectDirectory { get => (string?)GetValue(ProjectDirectoryProperty); set => SetValue(ProjectDirectoryProperty, value); }
    public static readonly DependencyProperty MediaRefProperty = DependencyProperty.Register(nameof(MediaRef), typeof(string), typeof(AudioPreviewControl), new PropertyMetadata(null, (d, _) => ((AudioPreviewControl)d).LoadConfiguredMedia()));
    public string? MediaRef { get => (string?)GetValue(MediaRefProperty); set => SetValue(MediaRefProperty, value); }

    public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
        nameof(Volume), typeof(double), typeof(AudioPreviewControl), new PropertyMetadata(1d, (d, _) => ((AudioPreviewControl)d).ApplyVolume()));

    public double Volume
    {
        get => (double)GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public async Task<bool> LoadAsync(string path, string? name = null, CancellationToken cancellationToken = default)
    {
        var service = _service ?? LocalAudioPreviewService.Shared;
        AttachService(service);
        service.Volume = Volume;
        var configuredPath = Path.GetFullPath(path);
        var generation = Interlocked.Increment(ref _loadGeneration);
        var loadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var previous = Interlocked.Exchange(ref _loadCancellation, loadCancellation);
        Cancel(previous);
        _configuredPath = configuredPath;
        try
        {
            await service.LoadAsync(configuredPath, name ?? Path.GetFileName(configuredPath), loadCancellation.Token);
            if (!IsCurrentLoad(generation, service, configuredPath)) return false;
            Refresh();
            return true;
        }
        catch (OperationCanceledException) when (!IsCurrentLoad(generation, service, configuredPath))
        {
            return false;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            if (IsCurrentLoad(generation, service, configuredPath)) SetError(exception.Message);
            return false;
        }
        finally
        {
            Interlocked.CompareExchange(ref _loadCancellation, null, loadCancellation);
            loadCancellation.Dispose();
        }
    }

    public void Release()
    {
        CancelPendingLoad();
        _service?.StopAndRelease();
    }

    private void BuildVisualTree()
    {
        var root = new StackPanel();
        var row = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _rewind.HorizontalAlignment = HorizontalAlignment.Left;
        _play.HorizontalAlignment = HorizontalAlignment.Center;
        _forward.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_rewind, 0); row.Children.Add(_rewind);
        Grid.SetColumn(_play, 1); row.Children.Add(_play);
        Grid.SetColumn(_forward, 2); row.Children.Add(_forward);
        root.Children.Add(row);
        root.Children.Add(_progress);
        _duration.HorizontalAlignment = HorizontalAlignment.Center;
        root.Children.Add(_duration);
        root.Children.Add(_error);
        _play.Click += async (_, _) =>
        {
            var path = _configuredPath;
            var service = _service;
            if (path is null || service is null) return;
            if (!OwnsPreview && !await LoadAsync(path, MediaName)) return;
            if (OwnsPreview && ReferenceEquals(_service, service)) service.TogglePlayPause();
        };
        _rewind.Click += (_, _) => { if (OwnsPreview) _service?.SeekBy(TimeSpan.FromSeconds(-5)); };
        _forward.Click += (_, _) => { if (OwnsPreview) _service?.SeekBy(TimeSpan.FromSeconds(5)); };
        _progress.ValueChanged += (_, _) =>
        {
            if (!_refreshing && _progress.IsEnabled && OwnsPreview) _service?.SeekFraction(_progress.Value);
        };
        Content = root;
        SetError(string.Empty);
        Refresh();
    }

    private void AttachService(LocalAudioPreviewService? service)
    {
        if (ReferenceEquals(_service, service)) { LoadConfiguredMedia(); return; }
        CancelPendingLoad();
        if (_service is not null)
        {
            if (OwnsPreview) _service.StopAndRelease();
            _service.StateChanged -= OnServiceChanged;
        }
        _service = service;
        if (_service is not null) _service.StateChanged += OnServiceChanged;
        LoadConfiguredMedia();
        Refresh();
    }

    private void LoadConfiguredMedia()
    {
        CancelPendingLoad();
        if (OwnsPreview) _service?.StopAndRelease();
        try { _configuredPath = MediaPath ?? (ProjectDirectory is not null && MediaRef is not null ? LocalAudioPreviewService.ResolveProjectMediaPath(ProjectDirectory, MediaRef) : null); }
        catch (InvalidDataException exception) { _configuredPath = null; SetError(exception.Message); }
        Refresh();
    }

    private void OnServiceChanged(object? sender, EventArgs args)
    {
        if (Dispatcher.CheckAccess()) Refresh(); else Dispatcher.InvokeAsync(Refresh);
    }

    private void ApplyVolume()
    {
        if (OwnsPreview && _service is not null) _service.Volume = Volume;
    }

    private void Refresh()
    {
        var service = OwnsPreview ? _service : null;
        _play.Content = service?.IsPlaying == true ? "⏸" : "▶";
        _play.IsEnabled = _configuredPath is not null && _service is not null && _service.State != AudioPreviewState.Loading;
        var canSeek = service?.CanSeek == true;
        _rewind.IsEnabled = canSeek;
        _forward.IsEnabled = canSeek;
        _progress.IsEnabled = canSeek;
        _refreshing = true;
        try { _progress.Value = service?.Progress ?? 0; }
        finally { _refreshing = false; }
        _duration.Text = service is null || service.Duration <= TimeSpan.Zero ? string.Empty : $"{Format(service.Position)} / {Format(service.Duration)}";
        if (service?.State == AudioPreviewState.Error) SetError(service.Error); else if (service?.State != AudioPreviewState.Loading) SetError(string.Empty);
    }

    private void CancelPendingLoad()
    {
        Interlocked.Increment(ref _loadGeneration);
        Cancel(Interlocked.Exchange(ref _loadCancellation, null));
    }

    private static void Cancel(CancellationTokenSource? cancellation)
    {
        if (cancellation is null) return;
        try { cancellation.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private bool IsCurrentLoad(long generation, LocalAudioPreviewService service, string path) =>
        generation == Volatile.Read(ref _loadGeneration)
        && ReferenceEquals(_service, service)
        && string.Equals(_configuredPath, path, StringComparison.OrdinalIgnoreCase);

    private void SetError(string value)
    {
        _error.Text = value;
        _error.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string Format(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");
}
