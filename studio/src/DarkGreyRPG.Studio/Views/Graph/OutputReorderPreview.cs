using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Visual-only reordering. Original geometry remains the hit-test basis throughout a drag.</summary>
internal sealed class OutputReorderPreview : Adorner, IDisposable
{
    private sealed record Row(FrameworkElement Element, double Top, double Height, Transform Transform, double Opacity)
    {
        public TranslateTransform Offset { get; } = new();
    }
    private readonly ItemsControl _list;
    private readonly PublicOutputRow _source;
    private readonly Row[] _rows;
    private readonly int _oldIndex;
    private readonly double _grabOffset;
    private int _destination;
    private Point _pointer;
    private double _gapY;
    private bool _show;
    public double GapHeight => _rows[_oldIndex].Height;
    public double GapY => _gapY;
    internal IReadOnlyList<double> OriginalTops => _rows.Select(row => row.Top).ToArray();

    public OutputReorderPreview(ItemsControl list, PublicOutputRow source, int oldIndex, Point pointer) : base(list)
    {
        _list = list; _source = source; _oldIndex = oldIndex; _destination = oldIndex;
        IsHitTestVisible = false;
        _rows = Enumerable.Range(0, list.Items.Count).Select(index =>
        {
            var element = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(index);
            return new Row(element, element.TranslatePoint(default, list).Y, element.ActualHeight, element.RenderTransform, element.Opacity);
        }).ToArray();
        _grabOffset = Math.Clamp(pointer.Y - _rows[oldIndex].Top, 0, GapHeight);
        foreach (var row in _rows)
        {
            var transform = new TransformGroup();
            transform.Children.Add(row.Transform); transform.Children.Add(row.Offset);
            row.Element.SetCurrentValue(RenderTransformProperty, transform);
        }
        _rows[oldIndex].Element.SetCurrentValue(OpacityProperty, 0d);
        Locate(pointer);
    }

    public int? Locate(Point pointer)
    {
        _pointer = pointer;
        _show = pointer.X >= 0 && pointer.X <= _list.ActualWidth && pointer.Y >= 0 && pointer.Y <= _list.ActualHeight;
        if (!_show) { InvalidateVisual(); return null; }
        var insertion = _rows.Length;
        for (var index = 0; index < _rows.Length; index++)
            if (pointer.Y < _rows[index].Top + _rows[index].Height / 2) { insertion = index; break; }
        var destination = Math.Clamp(insertion > _oldIndex ? insertion - 1 : insertion, 0, _rows.Length - 1);
        var remaining = _rows.Where((_, index) => index != _oldIndex).ToArray();
        var y = _rows[0].Top;
        for (var index = 0; index <= remaining.Length; index++)
        {
            if (index == destination) { _gapY = y; y += GapHeight; }
            if (index == remaining.Length) break;
            var row = remaining[index];
            var offset = y - row.Top;
            if (destination != _destination || !_show || !SystemParameters.ClientAreaAnimation)
            {
                var from = row.Offset.Y;
                row.Offset.BeginAnimation(TranslateTransform.YProperty, null);
                row.Offset.Y = offset;
                if (SystemParameters.ClientAreaAnimation)
                    row.Offset.BeginAnimation(TranslateTransform.YProperty,
                        new DoubleAnimation(from, offset, TimeSpan.FromMilliseconds(120))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.Stop });
            }
            y += row.Height;
        }
        _destination = destination;
        InvalidateVisual();
        return destination;
    }

    protected override void OnRender(DrawingContext drawing)
    {
        if (!_show) return;
        var accent = _list.TryFindResource("AccentFillColorDefaultBrush") as Brush ?? SystemColors.HighlightBrush;
        var text = _list.TryFindResource("TextFillColorPrimaryBrush") as Brush ?? SystemColors.WindowTextBrush;
        var background = _list.TryFindResource("CardBackgroundFillColorDefaultBrush") as Brush ?? SystemColors.WindowBrush;
        var width = Math.Max(20, _list.ActualWidth - 2);
        var gap = new Rect(1, _gapY + 2, width, Math.Max(4, GapHeight - 4));
        drawing.PushOpacity(0.12); drawing.DrawRoundedRectangle(accent, null, gap, 4, 4); drawing.Pop();
        drawing.DrawRoundedRectangle(null, new Pen(accent, 1.5), gap, 4, 4);
        // One row-sized preview, slightly offset so the landing outline stays visible.
        var card = new Rect(10, _pointer.Y - _grabOffset + 2, Math.Max(20, width - 10), Math.Max(4, GapHeight - 4));
        drawing.DrawRoundedRectangle(Window.GetWindow(_list)?.Background ?? SystemColors.WindowBrush, null, card, 4, 4);
        drawing.DrawRoundedRectangle(background, new Pen(accent, 1.5), card, 4, 4);
        var priorityWidth = _source.IsTaskFlow ? 72d : 0d;
        Label("⠿", 13, new Point(card.X + 7, card.Y + (card.Height - 18) / 2), 16);
        Label(_source.DisplayName, 13, new Point(card.X + 27, card.Y + (card.Height - 18) / 2), Math.Max(10, card.Width - 35 - priorityWidth));
        if (_source.IsTaskFlow)
            Label($"优先级 {_destination + 1}", 11, new Point(card.Right - priorityWidth, card.Y + (card.Height - 16) / 2), priorityWidth - 6);
        void Label(string value, double size, Point at, double available)
        {
            var label = new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"), size, text, VisualTreeHelper.GetDpi(this).PixelsPerDip)
            { MaxTextWidth = available, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
            drawing.DrawText(label, at);
        }
    }

    public void Dispose()
    {
        foreach (var row in _rows)
        {
            row.Offset.BeginAnimation(TranslateTransform.YProperty, null);
            row.Element.SetCurrentValue(RenderTransformProperty, row.Transform);
            row.Element.SetCurrentValue(OpacityProperty, row.Opacity);
        }
    }
}
