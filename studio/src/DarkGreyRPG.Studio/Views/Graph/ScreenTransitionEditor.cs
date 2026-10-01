using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace DarkGreyRPG.Studio.Views.Graph;

public sealed class ScreenTransitionEditor : UserControl
{
    public ScreenTransitionEditor()
    {
        var body = new StackPanel(); body.Children.Add(new TextBlock { Text = "进入转场" });
        var type = new ComboBox { DisplayMemberPath = "Name", Margin = new Thickness(0,3,0,3) };
        type.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("TransitionOptions"));
        type.SetBinding(ComboBox.SelectedItemProperty, new Binding("SelectedTransition") { Mode = BindingMode.TwoWay }); body.Children.Add(type);
        var direction = new ComboBox { DisplayMemberPath = "Name", Margin = new Thickness(0,3,0,3) };
        direction.SetBinding(ItemsControl.ItemsSourceProperty, new Binding("TransitionDirections"));
        direction.SetBinding(ComboBox.SelectedItemProperty, new Binding("SelectedTransitionDirection") { Mode = BindingMode.TwoWay });
        direction.SetBinding(VisibilityProperty, new Binding("HasTransitionDirection") { Converter = new BooleanToVisibilityConverter() }); body.Children.Add(direction);
        var time = new TextBox { ToolTip = "时长（秒），0—60", Margin = new Thickness(0,3,0,3) };
        time.SetBinding(TextBox.TextProperty, new Binding("TransitionDuration") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus });
        time.SetBinding(VisibilityProperty, new Binding("HasTransitionDuration") { Converter = new BooleanToVisibilityConverter() }); body.Children.Add(time);
        var help = new TextBlock { Text = "转场与台词并行；连续画面节点只显示最后的完整目标。复制画面后移动／缩放图片，使用平滑自然过渡。", TextWrapping = TextWrapping.Wrap };
        AuthoringText.SetIsHelp(help, true); body.Children.Add(help); Content = body;
    }
}
