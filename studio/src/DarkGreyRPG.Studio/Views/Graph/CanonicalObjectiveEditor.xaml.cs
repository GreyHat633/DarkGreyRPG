using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class CanonicalObjectiveEditor : UserControl
{
    public static readonly DependencyProperty IsInlineProperty = DependencyProperty.Register(nameof(IsInline), typeof(bool),
        typeof(CanonicalObjectiveEditor), new PropertyMetadata(false, (d, _) => ((CanonicalObjectiveEditor)d).SetIds()));
    public bool IsInline { get => (bool)GetValue(IsInlineProperty); set => SetValue(IsInlineProperty, value); }
    public CanonicalObjectiveEditor()
    {
        InitializeComponent(); SetIds();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && e.OriginalSource is TextBox { AcceptsReturn: false } box)
                box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        };
    }
    private void SetIds()
    {
        if (TypeSelector is null) return;
        var prefix = IsInline ? "InlineObjective" : "TaskObjective";
        AutomationProperties.SetAutomationId(TypeSelector, prefix + (IsInline ? "Type" : "TypeSelector"));
        AutomationProperties.SetAutomationId(Description, prefix + "Description");
        AutomationProperties.SetAutomationId(Required, prefix + "Required");
        AutomationProperties.SetAutomationId(ItemSelector, prefix + "ItemSelector");
        AutomationProperties.SetAutomationId(ActorSelector, prefix + "ActorSelector");
        AutomationProperties.SetAutomationId(Prerequisite, prefix + "PrerequisiteToggle");
        AutomationProperties.SetAutomationId(PrerequisiteHelp, prefix + "PrerequisiteHelp");
    }
}
