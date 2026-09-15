using System.Windows;
using System.Windows.Controls;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ProjectGraphHelpLayout0331Tests
{
    [STATestMethod]
    public void LogicErrorTextBlockCollapsesWhenEmptyShowsErrorsAndCollapsesWhenCleared()
    {
        using var directory = new ProjectGraph0331Directory();
        var view = new ProjectGraphView();
        var clean = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        view.DataContext = clean;
        Layout(view);
        var error = (TextBlock)view.FindName("LogicError");
        Assert.AreEqual(Visibility.Collapsed, error.Visibility);

        var store = new DarkGreyRPG.Studio.Core.Graphs.Resources.CanonicalProjectGraphStore(directory.Root);
        Directory.CreateDirectory(Path.GetDirectoryName(store.StoryLogicGraph.Path)!);
        File.WriteAllText(store.StoryLogicGraph.Path, "{}");
        var failed = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        view.DataContext = failed;
        Layout(view);
        Assert.AreEqual(Visibility.Visible, error.Visibility);
        StringAssert.Contains(error.Text, "Story Logic graph");

        view.DataContext = clean;
        Layout(view);
        Assert.AreEqual(Visibility.Collapsed, error.Visibility);
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(640, double.PositiveInfinity));
        view.Arrange(new Rect(0, 0, 640, view.DesiredSize.Height));
        view.UpdateLayout();
    }
}
