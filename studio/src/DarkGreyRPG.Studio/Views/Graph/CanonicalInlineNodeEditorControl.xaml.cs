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

    private CanonicalStoryWorkspaceView? WorkspaceView()
    {
        DependencyObject? parent = this;
        while (parent is not null)
        {
            if (parent is CanonicalStoryWorkspaceView view) return view;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
        }
        return null;
    }
    private void ImportLineAudio_OnClick(object sender, RoutedEventArgs e) => WorkspaceView()?.ImportLineAudio_OnClick(sender, e);
    private void RemoveLineAudio_OnClick(object sender, RoutedEventArgs e) => WorkspaceView()?.RemoveLineAudio_OnClick(sender, e);
    private void AudioPreview_ClearRequested(object? sender, EventArgs e)
    {
        if (Editor is { } inspector) { if (inspector.IsMusic) inspector.SetMusic(null); else inspector.SetLineVoice(null); }
    }

    private void DraftTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { AcceptsReturn: false } textBox) return;
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

}
