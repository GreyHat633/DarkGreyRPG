using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace DarkGreyRPG.Studio.Views.Graph;

public sealed class AnimationSequenceEditor : UserControl
{
    private readonly CheckBox _smooth = new() { Content = "平滑衔接" };
    private readonly TextBox _smoothTime = new() { Margin = new(0, 3, 0, 6) };
    private readonly ItemsControl _steps = new();
    private readonly Button _remove = new() { Content = "−", ToolTip = "删除选中动画", IsEnabled = false, Margin = new(4, 0, 0, 0) };
    private readonly StackPanel _toolbar = new() { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 6) };
    private Window? _window;
    internal AnimationSessionState Session { get; private set; } = new();
    internal void BindSession(AnimationSessionState session)
    {
        if (ReferenceEquals(Session, session)) return;
        Session.Changed -= SessionChanged;
        Session = session;
        Select(-1, true);
        Session.Changed += SessionChanged;
        _steps.Items.Clear();
    }
    private void SessionChanged(object? sender, EventArgs e)
    { foreach (var row in _steps.Items.OfType<AnimationStepRow>()) row.UpdateExpansion(); }
    public int SelectedIndex { get; private set; } = -1;
    public event Action<int>? SelectionChanged;
    private JsonObject _value = new() { ["animations"] = new JsonArray(), ["morph_duration"] = 0 };
    private bool _projecting;
    public event Action<JsonObject>? Edited;
    public event Action<bool>? ExpansionChanged;
    private readonly AnimatedLinePageBody _body = new();
    private readonly FoldHeader _header = new() { Title = "动画" };
    public bool IsExpanded { get => _body.IsExpanded; set { _body.IsExpanded = value; _header.IsChecked = value; } }
    public void AddStep() { IsExpanded = true; ExpansionChanged?.Invoke(true); var ids = Session.Ids(Sequence).ToList(); ids.Add(Session.NewExpanded()); Sequence.Add(new JsonObject { ["kind"] = "enter", ["type"] = "fade", ["direction"] = "left", ["duration"] = .5, ["delay"] = 0 }); Session.Record(Sequence, ids); SelectedIndex = Sequence.Count - 1; Render(); int selectedIndex = SelectedIndex; Submit(); Select(selectedIndex, true); }
    public AnimationSequenceEditor()
    {
        var panel = new StackPanel { Margin = new(0, 10, 0, 0) };
        var toolbar = _toolbar; toolbar.Tag = this;
        var add = new Button { Content = "+", ToolTip = "添加动画" }; toolbar.Children.Add(add); toolbar.Children.Add(_remove); panel.Children.Add(toolbar);
        panel.Children.Add(_smooth); panel.Children.Add(_smoothTime);
        var help = new TextBlock { Text = "从上一画面的实际显示位置平滑衔接，再播放下面的动画。" }; AuthoringText.SetIsHelp(help, true); panel.Children.Add(help);
        panel.Children.Add(_steps); _body.Child = panel;
        var cardContent = new StackPanel { Margin = new(0, 8, 0, 0) }; cardContent.Children.Add(_header); cardContent.Children.Add(_body); Content = SectionCard(cardContent);
        Loaded += (_, _) => { _window = Window.GetWindow(this); _window?.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(WindowMouseDown), true); Session.Changed -= SessionChanged; Session.Changed += SessionChanged; };
        Unloaded += (_, _) => { _window?.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(WindowMouseDown)); _window = null; Session.Changed -= SessionChanged; Select(-1, true); };
        _header.Click += (_, _) => { IsExpanded = !IsExpanded; ExpansionChanged?.Invoke(IsExpanded); };
        AutomationProperties.SetName(_smoothTime, "平滑衔接时长（秒）");
        _smooth.Checked += (_, _) => SmoothChanged(); _smooth.Unchecked += (_, _) => SmoothChanged();
        _smoothTime.LostKeyboardFocus += (_, _) => SmoothChanged();
        add.Click += (_, _) => AddStep();
        _remove.Click += (_, _) => { if (SelectedIndex >= 0 && SelectedIndex < Sequence.Count) Remove(Sequence[SelectedIndex]!.AsObject()); };
    }
    internal static Border SectionCard(UIElement content) => new() { Child = content, Background = Brushes.Transparent, BorderBrush = new SolidColorBrush(Color.FromRgb(114, 114, 114)), BorderThickness = new(1), CornerRadius = new(4), Padding = new(8), Margin = new(0, 0, 0, 8) };
    private static DependencyObject? InputParent(DependencyObject value) => value is Visual || value is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(value) : LogicalTreeHelper.GetParent(value);
    private void WindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || SelectedIndex < 0) return;
        AnimationSequenceEditor? target = null;
        bool row = false, toolbar = false;
        DependencyObject? source = e.OriginalSource as DependencyObject;
        bool popupItem = false;
        for (var part = source; part is not null; part = InputParent(part))
            if (part is ComboBoxItem) { popupItem = true; break; }
        if (!popupItem && _window is not null && _steps.Items.OfType<AnimationStepRow>().Any(r => r.IsComboOpen))
            source = _window.InputHitTest(e.GetPosition(_window)) as DependencyObject;
        for (var value = source; value is not null; value = InputParent(value))
        {
            if (value is ComboBoxItem item && ItemsControl.ItemsControlFromItemContainer(item) is ComboBox combo) { value = combo; }
            if (value is AnimationStepRow) row = true;
            if (value is StackPanel panel && panel.Tag is AnimationSequenceEditor) toolbar = true;
            if (value is AnimationSequenceEditor editor) { target = editor; break; }
        }
        if (target is not null && ReferenceEquals(target.Session, Session) && (row || toolbar)) return;
        Select(-1, true);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() => Select(-1, true)));
    }
    private JsonArray Sequence => _value["animations"]!.AsArray();
    public void Project(JsonObject value, bool enabled)
    {
        IsEnabled = enabled;
        var next = new JsonObject { ["animations"] = value["animations"]?.DeepClone() ?? new JsonArray(), ["morph_duration"] = value["morph_duration"]?.DeepClone() ?? JsonValue.Create(0) };
        if (_steps.Items.Count > 0 && JsonNode.DeepEquals(next, _value)) return;
        _value = next; _projecting = true;
        try { double seconds = Number(_value["morph_duration"]!); _smooth.IsChecked = seconds > 0; _smoothTime.Text = (seconds > 0 ? seconds : .5).ToString(CultureInfo.InvariantCulture); _smoothTime.Visibility = seconds > 0 ? Visibility.Visible : Visibility.Collapsed; Render(); }
        finally { _projecting = false; }
    }
    private void SmoothChanged()
    {
        if (_projecting) return;
        if (!Seconds(_smoothTime, out var seconds) || _smooth.IsChecked == true && seconds == 0) return;
        _smoothTime.Visibility = _smooth.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        _value["morph_duration"] = _smooth.IsChecked == true ? seconds : 0; Submit();
    }
    internal static bool Seconds(TextBox box, out double value)
    {
        bool valid = double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value is >= 0 and <= 60;
        box.ToolTip = valid ? null : "请输入 0—60 秒。"; return valid;
    }
    internal static double Number(JsonNode value) => double.Parse(value.ToJsonString(), CultureInfo.InvariantCulture);
    private void Render()
    {
        _steps.Items.Clear();
        for (int i = 0; i < Sequence.Count; i++) _steps.Items.Add(new AnimationStepRow(this, Sequence[i]!.AsObject(), i, Session.Ids(Sequence)[i]));
        Select(SelectedIndex, false);
    }
    public void Select(int index, bool notify = false)
    {
        SelectedIndex = index >= 0 && index < Sequence.Count ? index : -1;
        _remove.IsEnabled = SelectedIndex >= 0;
        foreach (var row in _steps.Items.OfType<AnimationStepRow>()) row.SetSelected(row.Index == SelectedIndex);
        if (notify) SelectionChanged?.Invoke(SelectedIndex);
    }
    private void Submit() { if (!_projecting && IsEnabled) Edited?.Invoke((JsonObject)_value.DeepClone()); }
    internal void Remove(JsonObject step) { var ids = Session.Ids(Sequence).ToList(); int index = Sequence.IndexOf(step); ids.RemoveAt(index); Sequence.Remove(step); Session.Record(Sequence, ids); SelectedIndex = Math.Min(index, Sequence.Count - 1); Render(); int selectedIndex = SelectedIndex; Submit(); Select(selectedIndex, true); }
    internal void Move(JsonObject step, int destination)
    {
        int current = Sequence.IndexOf(step); if (current < 0 || destination == current || destination < 0 || destination >= Sequence.Count) return;
        var selected = SelectedIndex >= 0 && SelectedIndex < Sequence.Count ? Sequence[SelectedIndex] : null;
        var ids = Session.Ids(Sequence).ToList(); var id = ids[current]; ids.RemoveAt(current); ids.Insert(destination, id);
        Sequence.RemoveAt(current); Sequence.Insert(destination, step); Session.Record(Sequence, ids); SelectedIndex = selected is null ? -1 : Sequence.IndexOf(selected); Render(); int selectedIndex = SelectedIndex; Submit(); Select(selectedIndex, true);
    }
    internal void Change(JsonObject step, string kind, string type, string direction, double duration, double delay)
    {
        var ids = Session.Ids(Sequence);
        step["kind"] = kind; step["type"] = type; step["direction"] = direction; step["duration"] = duration; step["delay"] = delay; Session.Record(Sequence, ids); Submit();
    }
}

public sealed class AnimationStepRow : UserControl
{
    private sealed record Option(string Id, string Name);
    private readonly AnimationSequenceEditor _owner;
    private readonly JsonObject _step;
    private readonly Border _border;
    private readonly Guid _id;
    private readonly AnimatedLinePageBody _body = new();
    private readonly FoldHeader _header;
    private ComboBox? _effects, _direction;
    public bool IsComboOpen => _effects?.IsDropDownOpen == true || _direction?.IsDropDownOpen == true;
    public void UpdateExpansion() { _body.IsExpanded = _owner.Session.Expanded(_id); _header.IsChecked = _body.IsExpanded; }
    public int Index { get; }
    public void SetSelected(bool selected) => _border.BorderBrush = selected ? SystemColors.HighlightBrush : new SolidColorBrush(Color.FromRgb(114, 114, 114));
    public void MoveTo(int index) => _owner.Move(_step, index);
    public AnimationStepRow(AnimationSequenceEditor owner, JsonObject step, int index, Guid id)
    {
        _owner = owner; _step = step; Index = index; _id = id; Background = Brushes.Transparent;
        var panel = new StackPanel();
        var header = new DockPanel();
        var handle = new Button { Content = "⠿", Padding = new(4, 1, 4, 1), DataContext = this, ToolTip = "拖动调整动画顺序" }; EntryReorder.SetIsHandle(handle, true); DockPanel.SetDock(handle, Dock.Left); header.Children.Add(handle);
        _header = new FoldHeader { Title = $"动画 {index + 1}", Margin = new(6, 0, 0, 0) }; header.Children.Add(_header);
        _header.Click += (_, _) => owner.Session.SetExpanded(_id, _header.IsChecked == true);
        var effects = new ComboBox { DisplayMemberPath = "Name", SelectedValuePath = "Id", Margin = new(0, 5, 0, 5), ItemsSource = new[] {
            new Option("enter.fade", "淡入"), new Option("exit.fade", "淡出"), new Option("enter.slide", "滑入"), new Option("exit.slide", "滑出"),
            new Option("enter.wipe", "擦入"), new Option("exit.wipe", "擦除"), new Option("enter.random_lines", "随机出现"), new Option("exit.random_lines", "随机消失") } };
        _effects = effects;
        effects.SelectedValue = step["kind"]!.GetValue<string>() + "." + step["type"]!.GetValue<string>(); panel.Children.Add(effects);
        var direction = new ComboBox { DisplayMemberPath = "Name", SelectedValuePath = "Id", Margin = new(0, 0, 0, 5) }; panel.Children.Add(direction); _direction = direction;
        var duration = new TextBox { Text = AnimationSequenceEditor.Number(step["duration"]!).ToString(CultureInfo.InvariantCulture) };
        var delay = new TextBox { Text = AnimationSequenceEditor.Number(step["delay"]!).ToString(CultureInfo.InvariantCulture) };
        panel.Children.Add(new TextBlock { Text = "时长（秒）" }); panel.Children.Add(duration);
        panel.Children.Add(new TextBlock { Text = "等待（秒）", Margin = new(0, 5, 0, 0) }); panel.Children.Add(delay);
        AutomationProperties.SetName(effects, $"动画 {index + 1} 效果"); AutomationProperties.SetName(duration, $"动画 {index + 1} 时长"); AutomationProperties.SetName(delay, $"动画 {index + 1} 等待");
        _body.Child = panel; var content = new StackPanel { Background = Brushes.Transparent }; content.Children.Add(header); content.Children.Add(_body);
        var border = new Border { Child = content, Background = Brushes.Transparent, BorderBrush = new SolidColorBrush(Color.FromRgb(114, 114, 114)), BorderThickness = new(1), CornerRadius = new(4), Padding = new(8), Margin = new(0, 0, 0, 10) }; Content = border; UpdateExpansion();
        _border = border;
        PreviewMouseDown += (_, _) => owner.Select(SequenceIndex(), true);
        GotKeyboardFocus += (_, _) => owner.Select(SequenceIndex(), true);
        int SequenceIndex() => Index;
        bool projecting = false;
        void Directions(string selected)
        {
            projecting = true;
            var parts = ((string)effects.SelectedValue).Split('.'); bool entering = parts[0] == "enter";
            direction.ItemsSource = parts[1] == "random_lines" ? new[] { new Option("horizontal", "横向"), new Option("vertical", "纵向") } : new[] { new Option("left", entering ? "从左侧" : "向左侧"), new Option("right", entering ? "从右侧" : "向右侧"), new Option("up", entering ? "从上方" : "向上方"), new Option("down", entering ? "从下方" : "向下方") };
            direction.SelectedValue = selected; if (direction.SelectedIndex < 0) direction.SelectedIndex = 0;
            direction.Visibility = parts[1] == "fade" ? Visibility.Collapsed : Visibility.Visible; projecting = false;
        }
        void Submit()
        {
            if (projecting || !AnimationSequenceEditor.Seconds(duration, out var seconds) || !AnimationSequenceEditor.Seconds(delay, out var wait)) return;
            var parts = ((string)effects.SelectedValue).Split('.'); owner.Change(step, parts[0], parts[1], (string)direction.SelectedValue, seconds, wait);
        }
        Directions(step["direction"]!.GetValue<string>());
        effects.SelectionChanged += (_, _) => { Directions("left"); Submit(); };
        direction.SelectionChanged += (_, _) => Submit(); duration.LostKeyboardFocus += (_, _) => Submit(); delay.LostKeyboardFocus += (_, _) => Submit();
    }
}

/// <summary>Session-only identities follow edit snapshots without changing saved animation steps.</summary>
internal sealed class AnimationSessionState
{
    private readonly Dictionary<string, Guid[]> _snapshots = new();
    private readonly Dictionary<Guid, bool> _expanded = new();
    public event EventHandler? Changed;
    public Guid[] Ids(JsonArray steps)
    {
        var key = steps.ToJsonString();
        if (!_snapshots.TryGetValue(key, out var ids))
        {
            ids = steps.Select((_, i) => { var id = Guid.NewGuid(); _expanded[id] = i == 0; return id; }).ToArray();
            _snapshots[key] = ids;
        }
        return ids;
    }
    public void Record(JsonArray steps, IEnumerable<Guid> ids) => _snapshots[steps.ToJsonString()] = ids.ToArray();
    public Guid NewExpanded() { var id = Guid.NewGuid(); _expanded[id] = true; return id; }
    public bool Expanded(Guid id) => _expanded.GetValueOrDefault(id);
    public void SetExpanded(Guid id, bool value) { if (Expanded(id) == value) return; _expanded[id] = value; Changed?.Invoke(this, EventArgs.Empty); }
}
