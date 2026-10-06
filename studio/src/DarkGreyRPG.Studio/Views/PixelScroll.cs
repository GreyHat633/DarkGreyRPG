using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DarkGreyRPG.Studio.Views;

/// <summary>Pixel wheel motion with native hit testing and retained WPF virtualization.</summary>
public static class PixelScroll
{
    private static bool _registered;
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(State), typeof(PixelScroll));

    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        EventManager.RegisterClassHandler(typeof(ScrollViewer), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, args) => { if (ReferenceEquals(sender, args.OriginalSource)) Enable((ScrollViewer)sender); }));
    }

    public static void Enable(ScrollViewer viewer)
    {
        if (viewer.GetValue(StateProperty) is State) return;
        // Pixel mode keeps IScrollInfo/recycling active; do not disable CanContentScroll.
        if (viewer.TemplatedParent is ItemsControl items) VirtualizingPanel.SetScrollUnit(items, ScrollUnit.Pixel);
        viewer.SetValue(StateProperty, new State(viewer));
    }

    private sealed class State
    {
        private readonly ScrollViewer _viewer;
        private readonly DispatcherTimer _timer;
        private double _position, _target;
        private long _updated;
        public State(ScrollViewer viewer)
        {
            _viewer = viewer;
            _timer = new DispatcherTimer(DispatcherPriority.Render, viewer.Dispatcher) { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += Tick;
            viewer.PreviewMouseWheel += Wheel;
            viewer.Unloaded += (_, _) => _timer.Stop();
            viewer.PreviewMouseDown += (_, _) => _timer.Stop();
            viewer.PreviewKeyDown += (_, args) => { if (args.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End) _timer.Stop(); };
        }
        private void Wheel(object sender, MouseWheelEventArgs args)
        {
            var source = args.OriginalSource as DependencyObject;
            while (source is not null && source is not ScrollViewer) source = Graph.LinePagesEditor.InputParent(source);
            if (!ReferenceEquals(source, _viewer) || _viewer.ScrollableHeight <= 0 || args.Delta == 0) return;
            if (!_timer.IsEnabled) _position = _target = _viewer.VerticalOffset;
            _target = Math.Clamp(_target - args.Delta / 120.0 * 48, 0, _viewer.ScrollableHeight);
            args.Handled = true;
            if (!SystemParameters.ClientAreaAnimation) { _viewer.ScrollToVerticalOffset(_target); return; }
            _updated = Stopwatch.GetTimestamp();
            _timer.Start();
        }
        private void Tick(object? sender, EventArgs args)
        {
            double seconds = Math.Min(.1, Stopwatch.GetElapsedTime(_updated).TotalSeconds);
            _updated = Stopwatch.GetTimestamp();
            _target = Math.Clamp(_target, 0, _viewer.ScrollableHeight);
            _position = Math.Clamp(_position + (_target - _position) * (1 - Math.Exp(-24 * seconds)), 0, _viewer.ScrollableHeight);
            if (Math.Abs(_target - _position) < .05) { _position = _target; _timer.Stop(); }
            _viewer.ScrollToVerticalOffset(_position);
        }
    }
}
