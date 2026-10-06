using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class CanonicalGraphEditorView
{
    public static readonly DependencyProperty StoryGroupsProperty = DependencyProperty.Register(
        nameof(StoryGroups), typeof(StoryGroupCatalog), typeof(CanonicalGraphEditorView),
        new PropertyMetadata(StoryGroupCatalog.Empty, (sender, _) => ((CanonicalGraphEditorView)sender).DrawCommentFrames()));

    public StoryGroupCatalog StoryGroups
    {
        get => (StoryGroupCatalog)GetValue(StoryGroupsProperty);
        set => SetValue(StoryGroupsProperty, value);
    }

    public Action<string, string>? RenameStoryGroupRequested { get; set; }
    public Func<IReadOnlyCollection<string>, bool>? DeleteStoryGroupsRequested { get; set; }

}
