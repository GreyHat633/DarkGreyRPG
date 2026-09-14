using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.Views;

/// <summary>Shared inline controls for line voice and music local preview.</summary>
public sealed class AudioPreviewControl : UserControl
{
    private readonly TextBlock _name = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _duration = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _play = new() { Width = 72, Margin = new Thickness(8, 0, 0, 0) };
    private readonly Button _clear = new() { Content = "清除", Width = 58, Margin = new Thickness(6, 0, 0, 0) };
    private readonly Slider _progress = new() { Minimum = 0, Maximum = 1, VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true };
    private readonly TextBlock _error = new() { Foreground = System.Windows.Media.Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
    private LocalAudioPreviewService? _service;
    private bool _seeking;
    private readonly System.Windows.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private string? _configuredPath;
    private bool OwnsPreview => _service is not null && !string.IsNullOrEmpty(_configuredPath) && string.Equals(_service.SourcePath, _configuredPath, StringComparison.OrdinalIgnoreCase);

    public AudioPreviewControl()
    {
        BuildVisualTree();
        _timer.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { AttachService(PreviewService ?? LocalAudioPreviewService.Shared); _timer.Start(); };
        Unloaded += (_, _) =>
        {
            // Unloaded is a real editing-context boundary: release the WAV and
            // decoder/player handles so switching nodes cannot leave audio open.
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

    public event EventHandler? ClearRequested;
    public static readonly DependencyProperty ProjectDirectoryProperty = DependencyProperty.Register(nameof(ProjectDirectory), typeof(string), typeof(AudioPreviewControl), new PropertyMetadata(null, (d, _) => ((AudioPreviewControl)d).LoadConfiguredMedia()));
    public string? ProjectDirectory { get => (string?)GetValue(ProjectDirectoryProperty); set => SetValue(ProjectDirectoryProperty, value); }
    public static readonly DependencyProperty MediaRefProperty = DependencyProperty.Register(nameof(MediaRef), typeof(string), typeof(AudioPreviewControl), new PropertyMetadata(null, (d, _) => ((AudioPreviewControl)d).LoadConfiguredMedia()));
    public string? MediaRef { get => (string?)GetValue(MediaRefProperty); set => SetValue(MediaRefProperty, value); }

    public async Task<bool> LoadAsync(string path, string? name = null, CancellationToken cancellationToken = default)
    {
        var service = _service ?? LocalAudioPreviewService.Shared;
        AttachService(service);
        _configuredPath = Path.GetFullPath(path);
        try
        {
            await service.LoadAsync(path, name ?? Path.GetFileName(path), cancellationToken);
            Refresh();
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            _error.Text = exception.Message;
            _error.Visibility = Visibility.Visible;
            return false;
        }
    }

    public void Release() => _service?.StopAndRelease();

    private void BuildVisualTree()
    {
        var root = new StackPanel();
        var row = new DockPanel();
        _name.MaxWidth = 150;
        root.Children.Add(_name);
        DockPanel.SetDock(_clear, Dock.Right); row.Children.Add(_clear);
        DockPanel.SetDock(_play, Dock.Right); row.Children.Add(_play);
        DockPanel.SetDock(_duration, Dock.Right); row.Children.Add(_duration);
        root.Children.Add(row);
        root.Children.Add(_progress);
        root.Children.Add(_error);
        _play.Click += async (_, _) =>
        {
            if (_configuredPath is null) return;
            if (!OwnsPreview && !await LoadAsync(_configuredPath, MediaName)) return;
            _service?.TogglePlayPause();
        };
        _clear.Click += (_, _) => { if (OwnsPreview) _service?.StopAndRelease(); ClearRequested?.Invoke(this, EventArgs.Empty); };
        _progress.ValueChanged += (_, _) =>
        {
            if (_seeking) _service?.SeekFraction(_progress.Value);
        };
        _progress.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => _seeking = true));
        _progress.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) =>
        {
            _seeking = false;
            _service?.SeekFraction(_progress.Value);
        }));
        Content = root;
        SetError(string.Empty);
    }

    private void AttachService(LocalAudioPreviewService? service)
    {
        if (ReferenceEquals(_service, service)) { LoadConfiguredMedia(); return; }
        if (_service is not null) _service.StateChanged -= OnServiceChanged;
        _service = service;
        if (_service is not null) _service.StateChanged += OnServiceChanged;
        LoadConfiguredMedia();
        Refresh();
    }

    private void LoadConfiguredMedia()
    {
        if (OwnsPreview) _service?.StopAndRelease();
        try { _configuredPath = MediaPath ?? (ProjectDirectory is not null && MediaRef is not null ? LocalAudioPreviewService.ResolveProjectMediaPath(ProjectDirectory, MediaRef) : null); }
        catch (InvalidDataException exception) { _configuredPath = null; SetError(exception.Message); }
        Refresh();
    }

    private void OnServiceChanged(object? sender, EventArgs args)
    {
        if (Dispatcher.CheckAccess()) Refresh(); else Dispatcher.InvokeAsync(Refresh);
    }

    private void Refresh()
    {
        var service = OwnsPreview ? _service : null;
        _name.Text = string.IsNullOrWhiteSpace(MediaName) ? service?.MediaName ?? (_configuredPath is null ? "未选择音频" : Path.GetFileName(_configuredPath)) : MediaName;
        _play.Content = service?.IsPlaying == true ? "暂停" : service?.State == AudioPreviewState.Loading ? "处理中…" : "试听";
        _play.IsEnabled = _configuredPath is not null && service?.State != AudioPreviewState.Loading;
        _progress.IsEnabled = service?.State is AudioPreviewState.Ready or AudioPreviewState.Paused or AudioPreviewState.Playing;
        if (!_seeking) _progress.Value = service?.Progress ?? 0;
        _duration.Text = service is null || service.Duration <= TimeSpan.Zero ? string.Empty : $"{Format(service.Position)} / {Format(service.Duration)}";
        if (service?.State == AudioPreviewState.Error) SetError(service.Error); else if (service?.State != AudioPreviewState.Loading) SetError(string.Empty);
    }

    private void SetError(string value)
    {
        _error.Text = value;
        _error.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
    }

    private static string Format(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");
}
