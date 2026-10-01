using System.Globalization;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Edits only the selected image's entering or leaving effect.</summary>
public sealed class LayerEffectEditor : UserControl
{
    private sealed record Option(string Id, string Name);
    private readonly ComboBox _type = new() { DisplayMemberPath = "Name", SelectedValuePath = "Id", Margin = new(0, 3, 0, 3) };
    private readonly ComboBox _direction = new() { DisplayMemberPath = "Name", SelectedValuePath = "Id", Margin = new(0, 3, 0, 3) };
    private readonly TextBox _duration = new() { Margin = new(0, 3, 0, 3) };
    private readonly StackPanel _timing = new();
    private readonly bool _exit;
    private bool _projecting;
    public event Action<JsonObject>? Edited;
    public LayerEffectEditor(bool exit)
    {
        _exit = exit;
        var panel = new StackPanel { Margin = new(0, 8, 0, 0) };
        panel.Children.Add(new TextBlock { Text = exit ? "退出效果" : "进入效果" });
        var options = new List<Option> { new("none", "无"), new("fade", exit ? "淡出" : "淡入"), new("slide", exit ? "滑出" : "滑入"), new("wipe", exit ? "擦除" : "擦入"), new("random_lines", exit ? "随机消失" : "随机出现") };
        if (!exit) options.Add(new("morph", "平滑移动"));
        _type.ItemsSource = options;
        panel.Children.Add(_type); panel.Children.Add(_direction);
        _timing.Children.Add(new TextBlock { Text = "时长（秒）" }); _timing.Children.Add(_duration); panel.Children.Add(_timing);
        var help = new TextBlock { Text = exit ? "下一画面移除或替换此图片时播放。" : "图片首次出现时播放；平滑移动用于衔接复制后的同一图片。" };
        AuthoringText.SetIsHelp(help, true); panel.Children.Add(help); Content = panel;
        System.Windows.Automation.AutomationProperties.SetName(_type, exit ? "图片退出效果" : "图片进入效果");
        System.Windows.Automation.AutomationProperties.SetName(_duration, exit ? "图片退出时长" : "图片进入时长");
        _type.SelectionChanged += (_, _) => { if (!_projecting) { Directions("left"); Submit(); } };
        _direction.SelectionChanged += (_, _) => { if (!_projecting) Submit(); };
        _duration.LostKeyboardFocus += (_, _) => Submit();
        _duration.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Submit(); };
    }
    public void Project(JsonObject effect, bool enabled)
    {
        _projecting = true;
        try { IsEnabled = enabled; _type.SelectedValue = effect["type"]?.GetValue<string>() ?? "none"; Directions(effect["direction"]?.GetValue<string>() ?? "left"); _duration.Text = (effect["duration"]?.GetValue<double>() ?? .5).ToString("0.###", CultureInfo.InvariantCulture); }
        finally { _projecting = false; }
    }
    private void Directions(string selected)
    {
        bool projecting = _projecting; _projecting = true;
        try
        {
            string type = _type.SelectedValue as string ?? "none";
            _direction.ItemsSource = type == "random_lines" ? new[] { new Option("horizontal", "横向"), new Option("vertical", "纵向") } : new[] { new Option("left", _exit ? "向左侧" : "从左侧"), new Option("right", _exit ? "向右侧" : "从右侧"), new Option("up", _exit ? "向上方" : "从上方"), new Option("down", _exit ? "向下方" : "从下方") };
            _direction.SelectedValue = selected; if (_direction.SelectedIndex < 0) _direction.SelectedIndex = 0;
            _direction.Visibility = type is "slide" or "wipe" or "random_lines" ? Visibility.Visible : Visibility.Collapsed;
            _timing.Visibility = type == "none" ? Visibility.Collapsed : Visibility.Visible;
        }
        finally { _projecting = projecting; }
    }
    private void Submit()
    {
        if (_projecting || !IsEnabled) return;
        if (!double.TryParse(_duration.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || !double.IsFinite(seconds) || seconds is < 0 or > 60) { _duration.ToolTip = "请输入 0—60 秒。"; return; }
        _duration.ToolTip = null;
        Edited?.Invoke(new JsonObject { ["type"] = _type.SelectedValue as string ?? "none", ["direction"] = _direction.SelectedValue as string ?? "left", ["duration"] = seconds });
    }
}
