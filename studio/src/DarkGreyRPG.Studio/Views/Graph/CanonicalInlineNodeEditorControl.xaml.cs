using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>High-frequency canonical parameter controls hosted directly inside a graph node.</summary>
public partial class CanonicalInlineNodeEditorControl : UserControl
{
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor), typeof(CanonicalNodeInspectorViewModel), typeof(CanonicalInlineNodeEditorControl));

    public CanonicalInlineNodeEditorControl() => InitializeComponent();

    public CanonicalNodeInspectorViewModel? Editor
    {
        get => (CanonicalNodeInspectorViewModel?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    private void DraftTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { AcceptsReturn: false } textBox) return;
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

}
