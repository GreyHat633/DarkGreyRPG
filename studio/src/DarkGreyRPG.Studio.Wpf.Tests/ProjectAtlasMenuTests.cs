using System.Windows;
using System.Windows.Controls;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ProjectAtlasMenuTests
{
    [STATestMethod]
    public void ProjectCanvasMenuHasSingleAddStoryItemAndClickForwardsGraphPoint()
    {
        var view = new CanonicalGraphEditorView(ProjectHost());
        Point? requestedPoint = null;
        view.ProjectStoryCreateRequested = point => requestedPoint = point;
        var graphPoint = new Point(117.5, 246.25);

        var menu = view.CreateCanvasContextMenu(graphPoint);

        Assert.HasCount(1, menu.Items);
        var addStory = (MenuItem)menu.Items[0];
        Assert.AreEqual("添加故事", addStory.Header);
        Assert.IsTrue(addStory.IsEnabled);

        addStory.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.IsTrue(requestedPoint.HasValue);
        Assert.AreEqual(graphPoint, requestedPoint.Value);
    }

    [STATestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, true)]
    [DataRow(true, true, false)]
    public void ProjectCanvasMenuAddStoryIsEnabledOnlyWithCallbackAndWritableView(
        bool hasCallback, bool isReadOnly, bool expectedEnabled)
    {
        var view = new CanonicalGraphEditorView(ProjectHost())
        {
            IsReadOnly = isReadOnly,
        };
        if (hasCallback) view.ProjectStoryCreateRequested = _ => { };

        var menu = view.CreateCanvasContextMenu(new Point(12, 34));

        Assert.HasCount(1, menu.Items);
        var addStory = (MenuItem)menu.Items[0];
        Assert.AreEqual("添加故事", addStory.Header);
        Assert.AreEqual(expectedEnabled, addStory.IsEnabled);
    }

    [STATestMethod]
    public void ProjectViewRejectsGenericRawNodeCreation()
    {
        var host = ProjectHost();
        var view = new CanonicalGraphEditorView(host, () => "raw-node");

        Assert.IsFalse(view.AddNodeAt("terminate", 10, 20));
        Assert.IsEmpty(host.Nodes);
        Assert.IsEmpty(host.Graph.Nodes);
    }

    [STATestMethod]
    public void NonProjectCanvasMenuStillUsesAddNodeSubmenu()
    {
        var view = new CanonicalGraphEditorView(
            new GraphEditorHostViewModel(new GraphDocument([]), GraphScope.Session));

        var menu = view.CreateCanvasContextMenu(new Point(12, 34));

        Assert.HasCount(1, menu.Items);
        var addNode = (MenuItem)menu.Items[0];
        Assert.AreEqual("添加节点", addNode.Header);
        Assert.IsFalse(menu.Items.OfType<MenuItem>().Any(item => Equals(item.Header, "添加故事")));
        Assert.IsTrue(addNode.Items.Count > 0);
    }

    private static GraphEditorHostViewModel ProjectHost()
        => new(new GraphDocument([]), GraphScope.Project);
}
