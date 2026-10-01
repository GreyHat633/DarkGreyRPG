using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>One canonical string, one graph history; dynamic content uses atomic inline objects.</summary>
public sealed class DynamicContentEditor : UserControl
{
    public static readonly DependencyProperty CapacityTextProperty = DependencyProperty.Register(nameof(CapacityText), typeof(string), typeof(DynamicContentEditor), new PropertyMetadata("", (d, _) => ((DynamicContentEditor)d).UpdateCapacity()));
    public static readonly DependencyProperty CapacityHintProperty = DependencyProperty.Register(nameof(CapacityHint), typeof(string), typeof(DynamicContentEditor), new PropertyMetadata("", (d, _) => ((DynamicContentEditor)d).UpdateCapacity()));
    public string CapacityText { get => (string)GetValue(CapacityTextProperty); set => SetValue(CapacityTextProperty, value); }
    public string CapacityHint { get => (string)GetValue(CapacityHintProperty); set => SetValue(CapacityHintProperty, value); }
    private readonly Grid _textSurface = new();
    private readonly TextBlock _capacity = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(5, 2, 5, 4) };
    private CanonicalStoryWorkspaceViewModel? _nameSource;
    private DropCaret? _dropCaret;
    private sealed class DropCaret(RichTextBox owner) : Adorner(owner)
    {
        public Rect Position { get; set; }
        protected override void OnRender(DrawingContext context) => context.DrawLine(new Pen(SystemColors.HighlightBrush, 1.5), Position.TopLeft, Position.BottomLeft);
    }
    private void ClearDropCaret()
    {
        if (_dropCaret is not null) AdornerLayer.GetAdornerLayer(_dropCaret.AdornedElement)?.Remove(_dropCaret);
        _dropCaret = null;
    }
    private void UpdateCapacity()
    {
        _capacity.Text = CapacityText.Split('·')[0].Trim();
        _capacity.ToolTip = string.IsNullOrEmpty(CapacityHint) ? "按标准字体估算可显示容量；动态内容在游戏中按实际文本分页。" : CapacityHint;
        _capacity.Visibility = string.IsNullOrEmpty(CapacityText) ? Visibility.Collapsed : Visibility.Visible;
        if (_body is not null) _body.Padding = new Thickness(3, 3, 3, string.IsNullOrEmpty(CapacityText) ? 3 : 23);
    }
    private const string ClipboardFormat = "DarkGreyRPG.DynamicContent.v1";
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(DynamicContentEditor),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((DynamicContentEditor)d).Project(), null, false, System.Windows.Data.UpdateSourceTrigger.Explicit));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(DynamicContentEditor),
        new PropertyMetadata(false, (d, _) => ((DynamicContentEditor)d).UpdateReadOnly()));
    public static readonly DependencyProperty AcceptsReturnProperty = DependencyProperty.Register(nameof(AcceptsReturn), typeof(bool), typeof(DynamicContentEditor), new PropertyMetadata(false));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool AcceptsReturn { get => (bool)GetValue(AcceptsReturnProperty); set => SetValue(AcceptsReturnProperty, value); }
    public bool ShowInsertButton { get => _insert.Visibility == Visibility.Visible; set => _insert.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
    internal bool IsPickerOpen => _picker?.IsOpen == true;
    internal void ShowPicker(FrameworkElement anchor) => OpenPicker(null, anchor);
    [field: ThreadStatic] internal static event Action<bool>? ResourceDragFinished;
    internal static void NotifyResourceDragFinished(bool accepted) => ResourceDragFinished?.Invoke(accepted);
    private ScrollBarVisibility _scrollVisibility = ScrollBarVisibility.Disabled;
    public ScrollBarVisibility VerticalScrollBarVisibility { get => _scrollVisibility; set { _scrollVisibility = value; if (_body is not null) _body.VerticalScrollBarVisibility = value; } }
    private RichTextBox? _body;
    private Popup? _picker;
    public RichTextBox Body { get { EnsureBody(); return _body!; } }
    private readonly StackPanel _panel = new();
    private readonly TextBlock _preview = new() { TextWrapping = TextWrapping.Wrap, MinHeight = 28, Padding = new Thickness(3) };
    private bool _bodyQueued;
    private readonly Button _insert = new() { Content = "＋ 动态内容", Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(5, 2, 5, 2) };
    private readonly TextBlock _error = new() { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _risk = new() { TextWrapping = TextWrapping.Wrap };
    private void UpdateRisk()
    {
        if (DataContext is not CanonicalChoiceOptionViewModel) { _risk.Text = ""; _risk.Visibility = Visibility.Collapsed; return; }
        try
        {
            var budget = DynamicTextBudget.Measure(Text,
                id => (Workspace?.ActorItems ?? Owner?.ActorItems)?.FirstOrDefault(p => p.Id == id)?.DisplayName,
                id => Items.FirstOrDefault(p => p.Id == id)?.DisplayName);
            _risk.Text = budget.Caption; _risk.ToolTip = budget.Detail;
            _risk.Visibility = string.IsNullOrEmpty(budget.Caption) ? Visibility.Collapsed : Visibility.Visible;
            _risk.Foreground = budget.Bytes >= budget.Maximum * .9 || budget.Unknown ? Brushes.OrangeRed : null;
            if (_risk.Foreground is null) _risk.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or FormatException or InvalidOperationException or KeyNotFoundException) { _risk.Text = "动态内容格式无效"; _risk.Visibility = Visibility.Visible; }
    }
    private bool _projecting, _publishing, _composing;
    private bool _restoringDraft;
    private readonly Stack<string> _draftUndo = new(), _draftRedo = new();
    private string _committedText = "";
    public bool IsComposing => _composing;
    public event KeyEventHandler? BodyKeyDown;
    public DynamicContentEditor()
    {
        // Node templates hide empty help TextBlocks. The lazy input preview must
        // keep its own layout slot, including when the authored value is empty.
        MinHeight = 28;
        _preview.Style = new Style(typeof(TextBlock));
        _preview.SetResourceReference(TextBlock.BackgroundProperty, "ControlFillColorDefaultBrush");
        AllowDrop = true;
        PreviewDragEnter += (_, e) =>
        {
            if (!e.Data.GetDataPresent(CanonicalStoryWorkspaceView.ResourceDragFormat)) return;
            EnsureBody(); Body.UpdateLayout(); ResourceDragOver(this, e);
        };
        PreviewDragOver += ResourceDragOver;
        PreviewDrop += ResourceDrop;
        PreviewDragLeave += (_, _) => ClearDropCaret();
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (_body is not null) return;
            EnsureBody(); Body.UpdateLayout(); Body.Focus();
            if (Body.GetPositionFromPoint(e.GetPosition(Body), true) is { } position) Body.CaretPosition = position;
        };
        _textSurface.Children.Add(_preview); _textSurface.Children.Add(_capacity);
        AuthoringText.SetIsHelp(_capacity, true); UpdateCapacity();
        AuthoringText.SetIsHelp(_risk, true);
        _panel.Children.Add(_insert); _panel.Children.Add(_textSurface); _panel.Children.Add(_risk); _panel.Children.Add(_error); Content = _panel;
        _insert.Click += (_, _) => OpenPicker(null);
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible && _picker is not null) _picker.IsOpen = false;
            QueueVisibleBody();
        };
        Loaded += (_, _) => { ObserveNames(); UpdateReadOnly(); Project(); QueueVisibleBody(); };
        LayoutUpdated += OnPendingLayout;
        Unloaded += (_, _) => { ClearDropCaret(); if (_picker is not null) _picker.IsOpen = false; if (_nameSource is not null) _nameSource.PropertyChanged -= NamesChanged; _nameSource = null; };
        DataContextChanged += (_, _) => { if (_picker is not null) _picker.IsOpen = false; if (IsLoaded) ObserveNames(); };
    }
    private void OnPendingLayout(object? sender, EventArgs e) => QueueVisibleBody();
    private void QueueVisibleBody()
    {
        if (_body is not null || _bodyQueued || !IsVisible || !IsLoaded) return;
        var graph = LinePagesEditor.Ancestor<CanonicalGraphEditorView>(this);
        if (graph is not null)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            var bounds = TransformToAncestor(graph).TransformBounds(new Rect(RenderSize));
            if (!bounds.IntersectsWith(new Rect(graph.RenderSize))) return;
        }
        _bodyQueued = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            _bodyQueued = false;
            if (IsVisible && IsLoaded) EnsureBody();
        }));
    }
    private void EnsureBody()
    {
        if (_body is not null) return;
        LayoutUpdated -= OnPendingLayout;
        // RichTextBox native undo replaces embedded buttons with empty Grid objects.
        // Draft history therefore stores canonical text, while committed edits use graph history.
        _body = new RichTextBox { IsUndoEnabled = false, IsDocumentEnabled = true, AcceptsTab = false, MinHeight = 28, Padding = new Thickness(3), VerticalScrollBarVisibility = _scrollVisibility };
        _textSurface.Children.Remove(_preview);
        _textSurface.Children.Insert(0, _body);
        UpdateCapacity();
        Body.AllowDrop = true;
        Body.SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        Body.SetResourceReference(BackgroundProperty, "ControlFillColorDefaultBrush");
        Body.Document = new FlowDocument(new Paragraph()) { PagePadding = new Thickness(0) };
        Body.Document.SetBinding(FlowDocument.FontFamilyProperty, new System.Windows.Data.Binding(nameof(FontFamily)) { Source = this });
        Body.Document.SetBinding(FlowDocument.FontSizeProperty, new System.Windows.Data.Binding(nameof(FontSize)) { Source = this });
        Body.TextChanged += (_, _) => Publish();
        Body.PreviewKeyDown += OnKey;
        Body.AddHandler(Button.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            if (e.OriginalSource is not Button button || TokenPart(button) is null) return;
            var inline = Body.Document.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<InlineUIContainer>())
                .FirstOrDefault(p => ReferenceEquals(p.Child, button));
            if (inline is not null && !IsReadOnly)
            {
                if (TokenPart(button)?.Type is "item_name" or "actor_name") { Body.Focus(); Body.Selection.Select(inline.ElementStart, inline.ElementEnd); }
                else OpenPicker(inline);
            }
            e.Handled = true;
        }));
        TextCompositionManager.AddPreviewTextInputStartHandler(Body, (_, _) => _composing = true);
        TextCompositionManager.AddPreviewTextInputHandler(Body, (_, _) => { _composing = false; Dispatcher.BeginInvoke(new Action(Publish)); });
        Body.LostKeyboardFocus += (_, _) => { _composing = false; Commit(); };
        DataObject.AddPastingHandler(Body, OnPaste);
        Body.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (_, e) => { Copy(false); e.Handled = true; }));
        Body.CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, (_, e) => { Copy(true); e.Handled = true; }));
        Body.CommandBindings.Add(new CommandBinding(ApplicationCommands.Undo, (_, e) => { UndoDraft(false); e.Handled = true; }, (_, e) => { e.CanExecute = !IsReadOnly && _draftUndo.Count > 0; e.Handled = true; }));
        Body.CommandBindings.Add(new CommandBinding(ApplicationCommands.Redo, (_, e) => { UndoDraft(true); e.Handled = true; }, (_, e) => { e.CanExecute = !IsReadOnly && _draftRedo.Count > 0; e.Handled = true; }));
        Project(); UpdateReadOnly();
    }
    private void UpdateReadOnly() { if (_body is not null) _body.IsReadOnly = IsReadOnly; _insert.IsEnabled = !IsReadOnly; }
    private CanonicalNodeInspectorViewModel? Owner => DataContext as CanonicalNodeInspectorViewModel
        ?? (DataContext as CanonicalLinePageViewModel)?.Owner
        ?? LinePagesEditor.Ancestor<ReorderEntriesEditor>(this)?.DataContext as CanonicalNodeInspectorViewModel;
    private CanonicalStoryWorkspaceViewModel? Workspace => LinePagesEditor.Ancestor<CanonicalStoryWorkspaceView>(this)?.Workspace;
    private GraphEditorHostViewModel? History => Owner?.Host ?? Workspace?.InspectorTaskEditor?.Host;
    private IEnumerable<CanonicalResourceSelectionOption> Items => Workspace?.ItemItems.Select(item => new CanonicalResourceSelectionOption(item.Id, item.DisplayName, true, item)).ToArray()
        ?? Owner?.ObjectiveItemOptions
        ?? [];
    private void ObserveNames()
    {
        if (_nameSource is not null) _nameSource.PropertyChanged -= NamesChanged;
        _nameSource = Workspace;
        if (_nameSource is not null) _nameSource.PropertyChanged += NamesChanged;
        RefreshLabels();
    }
    private void NamesChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CanonicalStoryWorkspaceViewModel.ActorItems) or nameof(CanonicalStoryWorkspaceViewModel.ItemItems)) RefreshLabels();
    }
    private string Label(DynamicContentText.Part part) => part.Type switch
    {
        "actor_name" => (Workspace?.ActorItems ?? Owner?.ActorItems)?.FirstOrDefault(p => p.Id == part.ActorId)?.DisplayName ?? "角色引用缺失",
        "item_name" => Items.FirstOrDefault(p => p.Id == part.ItemId)?.DisplayName ?? "物品引用缺失",
        "item_count" => "持有数量：" + (Items.FirstOrDefault(p => p.Id == part.ItemId)?.DisplayName ?? "物品引用缺失"),
        _ => part.Label
    };
    private void RefreshLabels()
    {
        UpdateRisk();
        if (_body is null) { Project(); return; }
        foreach (var atom in Body.Document.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<InlineUIContainer>()))
            if (atom.Child is Button button && TokenPart(button) is { } part) button.Content = "{" + Label(part) + "}";
    }
    private DynamicContentText.Part? ResourcePart(IDataObject data)
    {
        if (IsReadOnly || !IsEnabled || !data.GetDataPresent(CanonicalStoryWorkspaceView.ResourceDragFormat)) return null;
        if (Workspace is { } workspace && !workspace.IsWritableEditor(workspace.ActiveEditor)) return null;
        return data.GetData(CanonicalStoryWorkspaceView.ResourceDragFormat) switch
        {
            CanonicalStoryActorItem actor when (Workspace?.ActorItems ?? Owner?.ActorItems)?.Any(p => p.Id == actor.Id) == true => new(Type: "actor_name", ActorId: actor.Id),
            CanonicalStoryItemItem item when Items.Any(p => p.Id == item.Id && p.IsResolved) => new(Type: "item_name", ItemId: item.Id),
            _ => null
        };
    }
    private void ResourceDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(CanonicalStoryWorkspaceView.ResourceDragFormat)) return;
        e.Effects = ResourcePart(e.Data) is null ? DragDropEffects.None : DragDropEffects.Link;
        if (e.Effects != DragDropEffects.None && Body.GetPositionFromPoint(e.GetPosition(Body), true) is { } at)
        {
            if (_dropCaret is null && AdornerLayer.GetAdornerLayer(Body) is { } layer)
            { _dropCaret = new(Body) { IsHitTestVisible = false }; layer.Add(_dropCaret); }
            if (_dropCaret is not null) { _dropCaret.Position = at.GetCharacterRect(LogicalDirection.Forward); _dropCaret.InvalidateVisual(); }
        }
        else ClearDropCaret();
        e.Handled = true;
    }
    private void ResourceDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(CanonicalStoryWorkspaceView.ResourceDragFormat)) return;
        ClearDropCaret();
        e.Handled = true; e.Effects = DragDropEffects.None;
        if (ResourcePart(e.Data) is not { } part || Body.GetPositionFromPoint(e.GetPosition(Body), true) is not { } at) return;
        if (Body.Selection.IsEmpty || at.CompareTo(Body.Selection.Start) < 0 || at.CompareTo(Body.Selection.End) > 0) Body.Selection.Select(at, at);
        Insert([part]); e.Effects = DragDropEffects.Link;
    }
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (_composing || e.Key == Key.ImeProcessed) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.Z or Key.Y && (e.Key == Key.Z ? _draftUndo.Count > 0 : _draftRedo.Count > 0))
        {
            UndoDraft(e.Key == Key.Y);
            e.Handled = true; return;
        }
        if (e.Key == Key.Enter && !AcceptsReturn && Keyboard.Modifiers == ModifierKeys.None) Commit();
        BodyKeyDown?.Invoke(this, e);
        if (e.Handled) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.Z or Key.Y && History is { } history)
        {
            if (!IsReadOnly) { if (e.Key == Key.Z) history.Undo(); else history.Redo(); }
            e.Handled = true; return;
        }
        if (!e.Handled && e.Key == Key.Enter && !AcceptsReturn) e.Handled = true;
    }
    private void Project()
    {
        UpdateRisk();
        if (_publishing) return;
        if (_body is null)
        {
            try
            {
                _preview.Inlines.Clear();
                foreach (var part in DynamicContentText.Parse(Text))
                {
                    var run = new Run(part.Type is null ? part.Text : "{" + Label(part) + "}");
                    if (part.Type is not null)
                    {
                        run.FontWeight = FontWeights.SemiBold;
                        run.SetResourceReference(TextElement.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
                    }
                    _preview.Inlines.Add(run);
                }
            }
            catch (Exception error) when (error is System.Text.Json.JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
            { _preview.Text = "〔动态内容无效〕"; }
            return;
        }
        _projecting = true;
        try
        {
            var parts = DynamicContentText.Parse(Text);
            var paragraph = new Paragraph { Margin = new Thickness(0), TextAlignment = TextAlignment.Left };
            foreach (var part in parts) paragraph.Inlines.Add(MakeInline(part));
            Body.Document.Blocks.Clear(); Body.Document.Blocks.Add(paragraph); _error.Text = "";
            if (!_restoringDraft) { _committedText = Text; _draftUndo.Clear(); _draftRedo.Clear(); }
            Body.IsReadOnly = IsReadOnly;
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            Body.Document.Blocks.Clear(); Body.Document.Blocks.Add(new Paragraph(new Run("〔动态内容无效〕")));
            _error.Text = error.Message; Body.IsReadOnly = true;
        }
        finally { _projecting = false; }
    }
    private Inline MakeInline(DynamicContentText.Part part, TextPointer? at = null)
    {
        if (part.Type is null) return at is null ? new Run(part.Text) : new Run(part.Text, at);
        var label = "{" + Label(part) + "}";
        // WPF serializes embedded controls into XAML for native undo. Keep the payload
        // a string, and handle clicks on Body so restored controls remain editable.
        var button = new Button { Content = label, Tag = DynamicContentText.Encode([part]), FontWeight = FontWeights.SemiBold, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(1, 0, 1, 0), Margin = new Thickness(1, 0, 1, 0), ToolTip = "游戏中显示实际内容；点击修改或选择", Focusable = false };
        if (part.Type == "item_count") button.ToolTip += "\n物品 ID：" + part.ItemId;
        if (part.Type is "item_name" or "actor_name") button.ToolTip = "资源名称引用：" + (part.ItemId ?? part.ActorId);
        button.SetResourceReference(ForegroundProperty, "AccentTextFillColorPrimaryBrush");
        var inline = at is null ? new InlineUIContainer(button) : new InlineUIContainer(button, at); inline.BaselineAlignment = BaselineAlignment.Center;
        return inline;
    }
    private static DynamicContentText.Part? TokenPart(Button button) => button.Tag is string encoded
        ? DynamicContentText.Parse(encoded).SingleOrDefault(p => p.Type is not null) : null;
    private static IReadOnlyList<DynamicContentText.Part> Read(TextPointer start, TextPointer end)
    {
        var parts = new List<DynamicContentText.Part>();
        var cursor = start;
        while (cursor.CompareTo(end) < 0)
        {
            var context = cursor.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                var text = cursor.GetTextInRun(LogicalDirection.Forward);
                var length = Math.Min(text.Length, cursor.GetOffsetToPosition(end));
                parts.Add(new(Text: text[..length])); cursor = cursor.GetPositionAtOffset(length)!; continue;
            }
            if (context == TextPointerContext.EmbeddedElement && cursor.GetAdjacentElement(LogicalDirection.Forward) is Button button && TokenPart(button) is { } part) parts.Add(part);
            if (context == TextPointerContext.ElementStart && cursor.GetAdjacentElement(LogicalDirection.Forward) is LineBreak) parts.Add(new(Text: "\n"));
            if (context == TextPointerContext.ElementEnd && cursor.Parent is Paragraph { NextBlock: not null } && cursor.GetNextContextPosition(LogicalDirection.Forward) is { } next && next.CompareTo(end) < 0) parts.Add(new(Text: "\n"));
            cursor = cursor.GetNextContextPosition(LogicalDirection.Forward) ?? end;
        }
        return parts;
    }
    private void Publish()
    {
        if (_projecting || IsReadOnly || _composing) return;
        var value = DynamicContentText.Encode(Read(Body.Document.ContentStart, Body.Document.ContentEnd));
        // The terminal paragraph break belongs to FlowDocument, not the author's text.
        if (value == Text) return;
        if (!_restoringDraft) { _draftUndo.Push(Text); _draftRedo.Clear(); }
        _publishing = true;
        try { SetCurrentValue(TextProperty, value); }
        finally { _publishing = false; }
        UpdateRisk();
        if (DataContext is CanonicalLinePageViewModel page) page.UpdateCapacityDraft(value);
    }
    private void Copy(bool cut)
    {
        if (Body.Selection.IsEmpty) return;
        var encoded = DynamicContentText.Encode(Read(Body.Selection.Start, Body.Selection.End));
        var data = new DataObject();
        data.SetData(ClipboardFormat, encoded);
        data.SetData(DataFormats.UnicodeText, DynamicContentText.Display(encoded));
        Clipboard.SetDataObject(data);
        if (cut && !IsReadOnly) Body.Selection.Text = "";
    }
    public void Commit()
    {
        if (_projecting || IsReadOnly || _composing || Text == _committedText) return;
        GetBindingExpression(TextProperty)?.UpdateSource();
        _committedText = Text;
        _draftUndo.Clear(); _draftRedo.Clear();
    }
    private void UndoDraft(bool redo)
    {
        if (IsReadOnly || _composing) return;
        var source = redo ? _draftRedo : _draftUndo;
        if (source.Count == 0) return;
        var offset = Body.Document.ContentStart.GetOffsetToPosition(Body.CaretPosition);
        (redo ? _draftUndo : _draftRedo).Push(Text);
        _restoringDraft = true;
        try
        {
            SetCurrentValue(TextProperty, source.Pop());
            Body.CaretPosition = Body.Document.ContentStart.GetPositionAtOffset(Math.Min(offset,
                Body.Document.ContentStart.GetOffsetToPosition(Body.Document.ContentEnd))) ?? Body.Document.ContentEnd;
            if (DataContext is CanonicalLinePageViewModel page) page.UpdateCapacityDraft(Text);
        }
        finally { _restoringDraft = false; }
    }
    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (IsReadOnly) return;
        var internalText = e.DataObject.GetData(ClipboardFormat) as string;
        var text = internalText ?? e.DataObject.GetData(DataFormats.UnicodeText) as string;
        if (text is null) return;
        try
        {
            IReadOnlyList<DynamicContentText.Part> parts = internalText is null ? [new(Text: text)] : DynamicContentText.Parse(text);
            if (!AcceptsReturn && parts.Any(p => p.Text?.IndexOfAny(['\r', '\n']) >= 0)) { _error.Text = "单句不支持手动换行，请拆分为多句。"; return; }
            Insert(parts);
        }
        catch (Exception error) when (error is System.Text.Json.JsonException or FormatException or InvalidOperationException or KeyNotFoundException) { _error.Text = error.Message; }
    }
    private void Insert(IEnumerable<DynamicContentText.Part> parts)
    {
        _projecting = true;
        try
        {
            Body.Selection.Text = "";
            var caret = Body.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
            foreach (var part in parts)
            {
                var inline = MakeInline(part, caret); caret = inline.ElementEnd;
            }
            Body.CaretPosition = caret; Body.Focus();
        }
        finally { _projecting = false; }
        Publish();
        Commit();
    }
    private void OpenPicker(InlineUIContainer? existing, FrameworkElement? anchor = null)
    {
        if (IsReadOnly) return;
        if (_picker is not null) _picker.IsOpen = false;
        Commit();
        var start = existing?.ElementStart ?? Body.Selection.Start;
        var end = existing?.ElementEnd ?? Body.Selection.End;
        var content = new StackPanel { Width = 300, Margin = new Thickness(12) };
        content.Children.Add(new TextBlock { Text = "插入动态内容", FontWeight = FontWeights.SemiBold });
        var help = new TextBlock { Text = "在游戏中自动替换为当前玩家的实际信息。", Margin = new Thickness(0, 4, 0, 8) }; AuthoringText.SetIsHelp(help, true); content.Children.Add(help);
        // Keep the picker open while the author starts a drag in the resource library.
        // A capturing popup closes on that first mouse-down and makes drops impossible.
        var popup = new Popup { PlacementTarget = anchor ?? (ShowInsertButton ? _insert : Body), Placement = PlacementMode.Bottom, StaysOpen = true, AllowsTransparency = true };
        popup.HorizontalOffset = Math.Min(0, ((FrameworkElement)popup.PlacementTarget).ActualWidth - 326);
        _picker = popup;
        var ownerWindow = Window.GetWindow(this);
        void Deactivate(object? sender, EventArgs e) { popup.IsOpen = false; LinePagesEditor.ForgetDynamicTarget(this); }
        bool pendingResourceClick = false;
        void OutsideDown(object sender, MouseButtonEventArgs e)
        {
            pendingResourceClick = IsResourceSource(e.OriginalSource as DependencyObject);
            if (!pendingResourceClick) popup.IsOpen = false;
        }
        void OutsideUp(object sender, MouseButtonEventArgs e) { if (pendingResourceClick) { popup.IsOpen = false; LinePagesEditor.ForgetDynamicTarget(this); } }
        void DragFinished(bool accepted) { pendingResourceClick = false; if (!accepted) { popup.IsOpen = false; LinePagesEditor.ForgetDynamicTarget(this); } }
        void Escape(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { popup.IsOpen = false; Body.Focus(); e.Handled = true; } }
        if (ownerWindow is not null) { ownerWindow.Deactivated += Deactivate; ownerWindow.PreviewKeyDown += Escape; }
        ownerWindow?.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OutsideDown), true);
        ownerWindow?.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(OutsideUp), true);
        ResourceDragFinished += DragFinished;
        popup.PreviewKeyDown += Escape;
        popup.Closed += (_, _) =>
        {
            if (ownerWindow is not null) { ownerWindow.Deactivated -= Deactivate; ownerWindow.PreviewKeyDown -= Escape; }
            ownerWindow?.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OutsideDown));
            ownerWindow?.RemoveHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(OutsideUp));
            ResourceDragFinished -= DragFinished;
            if (ReferenceEquals(_picker, popup)) _picker = null;
        };
        var border = new Border { Child = new ScrollViewer { Content = content, MaxHeight = Math.Max(180, SystemParameters.WorkArea.Height - 80), VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), BorderBrush = Brushes.Gray };
        border.SetResourceReference(BackgroundProperty, "ApplicationBackgroundBrush"); popup.Child = border;
        void Accept(DynamicContentText.Part part) { popup.IsOpen = false; if (IsReadOnly) return; Body.Selection.Select(start, end); Insert([part]); }
        foreach (var pair in new[] { ("player_name", "玩家名称", "当前玩家的游戏名称。例如 GreyHat633。"), ("player_level", "玩家经验等级", "Minecraft 经验等级，例如 12。") })
        {
            var label = new StackPanel();
            label.Children.Add(new TextBlock { Text = pair.Item2 });
            var explanation = new TextBlock { Text = pair.Item3, Margin = new Thickness(0, 4, 0, 0) }; AuthoringText.SetIsHelp(explanation, true); label.Children.Add(explanation);
            var button = new Button { Content = label, MinHeight = 56, Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) => Accept(new(Type: pair.Item1)); content.Children.Add(button);
        }
        var objects = new StackPanel(); objects.Children.Add(new TextBlock { Text = "物品持有数量", Margin = new Thickness(0, 0, 0, 6) });
        var selector = new ComboBox { ItemsSource = Items.Where(p => p.IsResolved).ToArray(), ItemTemplate = ItemChoiceTemplate(), Margin = new Thickness(0, 4, 0, 4) };
        TextSearch.SetTextPath(selector, nameof(CanonicalResourceSelectionOption.DisplayName));
        ResourceSelectorDrop.SetKind(selector, ResourceSelectorKind.Item); objects.Children.Add(selector);
        selector.ToolTip = "选择物品，或从资源库拖入物品";
        var insert = new Button { Content = "插入", IsEnabled = false, MinHeight = 32, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 8, 0, 0) }; objects.Children.Add(insert);
        selector.SelectionChanged += (_, _) => insert.IsEnabled = selector.SelectedItem is CanonicalResourceSelectionOption;
        if (existing?.Child is Button previousButton && TokenPart(previousButton) is { Type: "item_count" } previous)
            selector.SelectedItem = selector.Items.OfType<CanonicalResourceSelectionOption>().FirstOrDefault(option => option.Id == previous.ItemId);
        insert.Click += (_, _) => { if (selector.SelectedItem is CanonicalResourceSelectionOption option) Accept(new(Type: "item_count", ItemId: option.Id)); };
        content.Children.Add(new Border { Child = objects, Padding = new Thickness(8), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4) });
        popup.IsOpen = true;
    }
    internal static bool IsResourceSource(DependencyObject? source)
    {
        for (var current = source; current is not null; current = LinePagesEditor.InputParent(current))
            if (current is FrameworkElement { DataContext: ICanonicalStoryTreeItem }) return true;
        return false;
    }
    internal static DataTemplate ItemChoiceTemplate()
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(CanonicalResourceSelectionOption.DisplayName)));
        panel.AppendChild(name);
        var id = new FrameworkElementFactory(typeof(TextBlock));
        id.SetValue(AuthoringText.IsHelpProperty, true);
        id.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(CanonicalResourceSelectionOption.Id)));
        panel.AppendChild(id);
        return new DataTemplate { VisualTree = panel };
    }
}
