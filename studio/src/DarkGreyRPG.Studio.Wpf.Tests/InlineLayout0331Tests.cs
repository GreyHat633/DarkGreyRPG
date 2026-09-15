using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class InlineLayout0331Tests
{
    [STATestMethod]
    public void AllAuthorableNodeKindsDoNotReserveRowsForEmptyMessages()
    {
        foreach (var definition in GraphNodeDefinitionRegistry.All.Where(d => !d.CompatibilityOnly && d.Scope != GraphScope.Project))
        {
            var kind = definition.Scope switch { GraphScope.Session => GraphResourceKind.Session, GraphScope.Task => GraphResourceKind.Task, _ => GraphResourceKind.Story };
            var node = GraphNodeFactory.Create(definition.Scope, definition.Type, "node");
            using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(kind, "resource", "Resource", new GraphDocument([node])));
            using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
            var view = new CanonicalInlineNodeEditorControl { Width = 210, Editor = inspector };
            Layout(view);
            foreach (var label in Descendants(view).OfType<TextBlock>().Where(t => BindingOperations.GetBinding(t, TextBlock.TextProperty) is not null && string.IsNullOrEmpty(t.Text)))
                Assert.IsTrue(label.DesiredSize.Height == 0, $"{definition.Scope}/{definition.Type}: empty {BindingOperations.GetBinding(label, TextBlock.TextProperty)?.Path.Path} occupies {label.DesiredSize.Height}");
        }
    }

    [STATestMethod]
    public void TerminateFieldsStartNearHeaderAndValidationRowsReturnOnlyWhenNeeded()
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "node");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument([node])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        var view = new CanonicalInlineNodeEditorControl { Width = 210, Editor = inspector };
        Layout(view);
        var fieldLabel = Descendants(view).OfType<TextBlock>().Single(t => t.Text == "公共端口显示名");
        Assert.IsTrue(fieldLabel.TranslatePoint(new Point(), view).Y <= 20, "Unrelated sections must not push the first field down.");
        var error = Descendants(view).OfType<TextBlock>().Single(t => BindingOperations.GetBinding(t, TextBlock.TextProperty)?.Path.Path == "StoryActionAmountError");
        var before = view.DesiredSize.Height;
        error.SetCurrentValue(TextBlock.TextProperty, "校验错误必须显示");
        Layout(view);
        Assert.AreEqual(Visibility.Visible, error.Visibility);
        Assert.IsTrue(view.DesiredSize.Height > before);
        error.SetCurrentValue(TextBlock.TextProperty, "");
        Layout(view);
        Assert.AreEqual(before, view.DesiredSize.Height, 0.1);
    }

    [STATestMethod]
    public void NarrowScreenPreviewDoesNotReserveLetterboxSpace()
    {
        var view = new SessionScreenEditor { Width = 210 };
        Layout(view);
        var preview = Descendants(view).OfType<Viewbox>().Single();
        Assert.AreEqual(210.0 * 180 / 320, preview.ActualHeight, 1.0);
        var overlays = Descendants(view).OfType<WrapPanel>().Single(p => p.Children.OfType<CheckBox>().Any());
        foreach (var checkbox in overlays.Children.OfType<CheckBox>())
        {
            var origin = checkbox.TranslatePoint(new Point(), view);
            Assert.IsTrue(origin.X >= 0 && origin.X + checkbox.ActualWidth <= 210.1);
        }
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(210, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, 210, view.DesiredSize.Height));
        view.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
