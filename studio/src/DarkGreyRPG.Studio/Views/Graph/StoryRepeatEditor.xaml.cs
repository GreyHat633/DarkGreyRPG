using System.Windows;
using System.Windows.Controls;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class StoryRepeatEditor : UserControl
{
    public static readonly DependencyProperty ModeAutomationIdProperty = DependencyProperty.Register(
        nameof(ModeAutomationId), typeof(string), typeof(StoryRepeatEditor),
        new PropertyMetadata("StoryStartRepeatMode", (d, e) => System.Windows.Automation.AutomationProperties.SetAutomationId(((StoryRepeatEditor)d).RepeatChoice, (string)e.NewValue)));
    public string ModeAutomationId { get => (string)GetValue(ModeAutomationIdProperty); set => SetValue(ModeAutomationIdProperty, value); }
    public StoryRepeatEditor()
    {
        InitializeComponent();
        System.Windows.Automation.AutomationProperties.SetAutomationId(RepeatChoice, ModeAutomationId);
    }
}
