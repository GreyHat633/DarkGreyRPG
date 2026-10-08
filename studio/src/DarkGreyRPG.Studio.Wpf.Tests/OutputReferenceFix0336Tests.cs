using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass, DoNotParallelize]
public sealed class OutputReferenceFix0336Tests
{
    private const string Story = "ST-2345-6789-ABCD-EFGH";
    [STATestMethod]
    public void ReferencedPackagePanelDoesNotRemeasureContentFromLayoutUpdated()
    {
        var panel = new AnimatedPackageExpander
        {
            VerticalAlignment = VerticalAlignment.Top,
            Header = "引用故事包",
            Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:g="clr-namespace:DarkGreyRPG.Studio.Views.Graph;assembly=DarkGreyRPGStudio"
                 xmlns:v="clr-namespace:DarkGreyRPG.Studio.Views;assembly=DarkGreyRPGStudio" TargetType="v:AnimatedPackageExpander">
                 <StackPanel><TextBlock Height="32" Text="{TemplateBinding Header}"/>
                  <g:AnimatedLinePageBody x:Name="PART_Reveal" IsExpanded="{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}}">
                   <ContentPresenter x:Name="PART_Content" Content="{TemplateBinding Content}"/>
                  </g:AnimatedLinePageBody>
                 </StackPanel>
                </ControlTemplate>
                """),
            Content = new ScrollViewer { MaxHeight = 230, Content = new ItemsControl { ItemsSource = Enumerable.Range(0, 100).Select(index => $"故事包 {index}").ToArray() } }
        };
        using var window = new TestWindow(panel);
        var passes = 0;
        panel.LayoutUpdated += (_, _) => passes++;
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(120) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
        Assert.IsTrue(passes < 8, $"Collapsed package panel scheduled {passes} layout passes.");
        Assert.AreEqual(32d, panel.ActualHeight);
        panel.IsExpanded = true;
        var end = DateTime.UtcNow.AddMilliseconds(300);
        while (DateTime.UtcNow < end) Pump();
        window.UpdateLayout();
        Assert.IsTrue(panel.ActualHeight > 32 && panel.ActualHeight <= 262);
    }

    [STATestMethod]
    public void ClippedWatermarkSettlesLayoutAndStillResizes()
    {
        var box = new TextBox { Width = 240, Height = 32, Focusable = false };
        TextInputWatermark.SetText(box, "输入名称");
        var content = new Border { ClipToBounds = true, Child = box };
        using var window = new TestWindow(content);
        var layouts = 0;
        content.LayoutUpdated += (_, _) => layouts++;
        WaitForIdleLayout();
        Assert.IsTrue(layouts < 8, $"Idle watermark scheduled {layouts} layout passes.");
        box.Width = 180;
        window.UpdateLayout();
        var adorners = System.Windows.Documents.AdornerLayer.GetAdornerLayer(box)!.GetAdorners(box)!;
        Assert.AreEqual(box.RenderSize.Width, adorners.Single().RenderSize.Width);
        layouts = 0;
        WaitForIdleLayout();
        Assert.IsTrue(layouts < 8, $"Resized watermark scheduled {layouts} layout passes.");

        static void WaitForIdleLayout()
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(120) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
    }

    [STATestMethod]
    public void ReorderLandingGapAcceptsTheNativeDropHit()
    {
        using var outputs = new PublicOutputsViewModel(Outputs(GraphScope.Task), true);
        using var window = new TestWindow(new PublicOutputsEditor { DataContext = outputs, VerticalAlignment = VerticalAlignment.Top });
        foreach (var list in Descendants<ItemsControl>(window.Content).Where(control => control.ItemsSource == outputs.Flow || control.ItemsSource == outputs.Logic))
        {
            var source = (PublicOutputRow)list.Items[0];
            using var preview = new OutputReorderPreview(list, source, 0, new Point(8, 10));
            Assert.AreEqual(2, preview.Locate(new Point(8, list.ActualHeight - 2)));
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(150) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame);
            Assert.IsNotNull(list.InputHitTest(new Point(8, preview.GapY + preview.GapHeight / 2)), "The visible landing gap must remain an OLE drop target after its neighbors move away.");
        }
    }

    [STATestMethod]
    public void ReorderPreviewKeepsHeightAndOriginalHitGeometryWithoutChangingData()
    {
        foreach (var scope in new[] { GraphScope.Session, GraphScope.Task })
        foreach (var kind in new[] { GraphInterfaceKind.Flow, GraphInterfaceKind.Logic })
        {
            var host = Outputs(scope);
            using var outputs = new PublicOutputsViewModel(host, true);
            var rows = kind == GraphInterfaceKind.Flow ? outputs.Flow : outputs.Logic;
            var list = new ItemsControl { ItemsSource = rows, Width = 210 };
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(FrameworkElement.HeightProperty, 44d);
            list.ItemTemplate = new DataTemplate { VisualTree = factory };
            using var window = new TestWindow(list);
            var original = host.Graph.ToJson();
            var height = list.ActualHeight;
            var first = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(0);
            var second = (FrameworkElement)list.ItemContainerGenerator.ContainerFromIndex(1);
            using (var preview = new OutputReorderPreview(list, rows[0], 0, new Point(12, 15)))
            {
                Assert.AreEqual(44d, preview.GapHeight);
                Assert.AreEqual(1, preview.Locate(new Point(12, 85)));
                Assert.AreEqual(44d, preview.GapY);
                // Animated neighbors must not feed back into hit testing.
                for (var repeat = 0; repeat < 10; repeat++) Assert.AreEqual(1, preview.Locate(new Point(12, 85)));
                CollectionAssert.AreEqual(new[] { 0d, 44d, 88d }, preview.OriginalTops.ToArray());
                Assert.AreEqual(0d, first.Opacity);
                Assert.AreEqual(height, list.ActualHeight);
                Assert.AreEqual(original, host.Graph.ToJson());
                Assert.IsFalse(host.CanUndo);
                Assert.IsNull(preview.Locate(new Point(-1, 85)));
            }
            Assert.AreEqual(1d, first.Opacity);
            Assert.AreEqual(Transform.Identity, second.RenderTransform);
            var id = rows[0].PortId;
            Assert.IsTrue(rows[0].MoveTo(1));
            Assert.AreEqual(id, rows[1].PortId);
            Assert.IsTrue(host.Undo());
            Assert.AreEqual(original, host.Graph.ToJson());
            Assert.IsFalse(host.CanUndo);
        }
    }

    [STATestMethod]
    public void SharedListPreviewMovesExpandedGroupsAsWholeRowsAndRestoresOnCancel()
    {
        var list = new StackPanel { Width = 210 };
        var rows = new FrameworkElement[] { new Border { Height = 130 }, new Border { Height = 44 }, new Border { Height = 70 } };
        foreach (var row in rows) list.Children.Add(row);
        using var window = new TestWindow(list);
        using (var preview = new OutputReorderPreview(list, rows, "展开的故事组", false, 0, new Point(12, 18)))
        {
            Assert.AreEqual(130d, preview.GapHeight);
            Assert.AreEqual(2, preview.Locate(new Point(12, 240)));
            Assert.AreEqual(114d, preview.GapY);
            CollectionAssert.AreEqual(new[] { 0d, 130d, 174d }, preview.OriginalTops.ToArray());
            for (var i = 0; i < 5; i++) Assert.AreEqual(2, preview.Locate(new Point(12, 240)));
            preview.SetScrollShift(-30);
            Assert.AreEqual(1, preview.Locate(new Point(12, 130)));
            Assert.AreEqual(14d, preview.GapY);
            Assert.AreEqual(0d, rows[0].Opacity);
            Assert.IsNull(preview.Locate(new Point(-1, 50)));
        }
        CollectionAssert.AreEqual(rows, list.Children.Cast<FrameworkElement>().ToArray());
        foreach (var row in rows)
        {
            Assert.AreEqual(1d, row.Opacity);
            Assert.AreEqual(Transform.Identity, row.RenderTransform);
        }
    }

    [STATestMethod]
    public void ViewportExitRestoresRowsAndReentryKeepsOriginalGeometry()
    {
        var list = new StackPanel { Width = 210 };
        var rows = new FrameworkElement[] { new Border { Height = 44 }, new Border { Height = 44 }, new Border { Height = 44 } };
        foreach (var row in rows) list.Children.Add(row);
        using var window = new TestWindow(list);
        using var preview = new OutputReorderPreview(list, rows, "边界资源", false, 0, new Point(12, 18));
        var height = list.ActualHeight;
        var viewport = new Rect(3, 4, 195, 119);
        for (var repeat = 0; repeat < 20; repeat++)
        {
            Assert.AreEqual(2, preview.Locate(new Point(197.5, 122.5), viewport));
            Assert.AreEqual(0d, rows[0].Opacity);
            Assert.IsNull(preview.Locate(new Point(198.5, 122.5), viewport));
            Assert.AreEqual(1d, rows[0].Opacity);
            foreach (var row in rows)
                Assert.AreEqual(0d, ((TranslateTransform)((TransformGroup)row.RenderTransform).Children[^1]).Y);
            CollectionAssert.AreEqual(new[] { 0d, 44d, 88d }, preview.OriginalTops.ToArray());
            Assert.AreEqual(1, preview.Locate(new Point(12, 85), viewport));
            Assert.AreEqual(44d, preview.GapY);
            Assert.AreEqual(height, list.ActualHeight);
        }
    }

    [STATestMethod]
    public void CreatingTasksDoesNotRebuildUnrelatedOutputEditorsOrLoseDrafts()
    {
        using var directory = new DirectoryFixture();
        var store = new CanonicalProjectGraphStore(directory.Path);
        var session = new GraphResourceEnvelope(GraphResourceKind.Session, Story + "~session~session", "会话", Outputs(GraphScope.Session).Graph);
        store.Sessions.Create(session);
        var aggregate = CanonicalAggregateNodeFactory.Create(session, "session-placement").Candidate!;
        store.Stories.Create(new(GraphResourceKind.Story, Story, "故事", new GraphDocument([GraphNodeFactory.CreateStoryStart("start"), aggregate])));
        store.Memberships.Create(new(Story, new() { Sessions = [session.Id] }));
        using var workspace = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load(Story));
        var view = new CanonicalStoryWorkspaceView(workspace);
        Arrange(view, 1280, 900);
        var visual = view.GraphView.NodeVisuals.Single(node => node.InlineEditor?.NodeId == "session-placement");
        var editor = visual.InlineEditor!;
        var outputs = editor.PublicOutputs!;
        var row = outputs.Flow[0];
        outputs.FlowExpanded = false;
        var height = visual.ActualHeight;
        var notices = new List<string>();
        workspace.PropertyChanged += (_, e) => notices.Add(e.PropertyName ?? "");
        for (var index = 0; index < 4; index++)
        {
            new CanonicalStoryResourceLifecycleService(store).CreateOwnedTask(Story, Story + "~task~new" + index, "新任务" + index);
            workspace.ApplyResourceSnapshot(new CanonicalStoryWorkspaceLoader(store).Load(Story), CanonicalStoryFolderKind.Tasks);
            Pump(); view.UpdateLayout();
            Assert.AreSame(visual, view.GraphView.NodeVisuals.Single(node => node.InlineEditor?.NodeId == "session-placement"));
            Assert.AreSame(editor, visual.InlineEditor);
            Assert.AreSame(outputs, editor.PublicOutputs);
            Assert.AreSame(row, outputs.Flow[0]);
            Assert.IsFalse(outputs.FlowExpanded);
        }
        Assert.IsFalse(notices.Contains(nameof(workspace.ActorItems)));
        Assert.IsFalse(notices.Contains(nameof(workspace.ItemItems)));
        Assert.IsFalse(notices.Contains(nameof(workspace.SessionItems)));
        // A new option updates the existing editor and leaves output identities untouched.
        new CanonicalStoryActorLifecycleService(store).CreateOwned(Story, Story + "~actor~boss", "酒馆老板");
        workspace.ApplyResourceSnapshot(new CanonicalStoryWorkspaceLoader(store).Load(Story), CanonicalStoryFolderKind.Actors);
        Assert.AreSame(editor, visual.InlineEditor);
        Assert.IsTrue(editor.ActorOptions.Any(option => option.DisplayName == "酒馆老板"));
        Assert.AreSame(row, outputs.Flow[0]);
        var task = workspace.TaskItems.First().Editor.Host;
        using var objective = new CanonicalNodeInspectorViewModel(task, task.Nodes.Single());
        objective.ObjectiveRequiredText = "unfinished";
        objective.UpdateResourceOptions(workspace.ActorItems, workspace.ItemItems);
        Assert.AreEqual("unfinished", objective.ObjectiveRequiredText);
        Assert.IsFalse(task.CanUndo);
    }

    [STATestMethod]
    public void ResourceOptionRefreshPreservesFocusedTextAndUncommittedRename()
    {
        var parent = new GraphEditorHostViewModel(new GraphDocument([GraphNodeFactory.CreateStoryStart("start")]), GraphScope.StoryFlow);
        using var inspector = new CanonicalNodeInspectorViewModel(parent, parent.Nodes[0]);
        var child = Outputs(GraphScope.Task);
        inspector.ConfigurePublicOutputs(child);
        using var window = new TestWindow(new PublicOutputsEditor { DataContext = inspector.PublicOutputs });
        var text = Descendants<TextBox>(window.Content).First();
        text.Focus(); text.Text = "未提交名称";
        var outputs = inspector.PublicOutputs!;
        var row = outputs.Flow[0];
        var original = child.Graph.ToJson();
        inspector.UpdateResourceOptions([new(new ActorResourceInfo(Story + "~actor~boss", "老板", "unused", []))],
            [new(new IndividualItemResource { ItemId = Story + "~item~coin", DisplayName = "铜币" })]);
        inspector.ConfigurePublicOutputs(child);
        Pump();
        Assert.AreSame(outputs, inspector.PublicOutputs);
        Assert.AreSame(row, outputs.Flow[0]);
        Assert.AreEqual("未提交名称", text.Text);
        Assert.IsTrue(text.IsKeyboardFocused);
        Assert.AreEqual(original, child.Graph.ToJson());
        Assert.IsFalse(child.CanUndo);
    }

    [STATestMethod]
    public void ChangingSectionContextSnapsWhileUserExpansionStillAnimates()
    {
        var first = new PublicOutputsViewModel(Outputs(GraphScope.Task), true) { FlowExpanded = false };
        var second = new PublicOutputsViewModel(Outputs(GraphScope.Task), true);
        using var window = new TestWindow(new PublicOutputsEditor { DataContext = first });
        var body = Descendants<AnimatedLinePageBody>(window.Content).First();
        Assert.IsFalse(body.IsExpanded);
        ((PublicOutputsEditor)window.Content).DataContext = second;
        Pump(); window.UpdateLayout();
        Assert.IsTrue(body.IsExpanded);
        Assert.IsFalse(body.HasAnimatedProperties);
        first.Dispose(); second.Dispose();
    }

    [STATestMethod]
    public void PickerSearchKeepsFoldersSixLabelsAndRestoresExpansionWithoutDisplayingIds()
    {
        var kinds = new[] { "Actor", "Actor", "Item", "ItemGroup", "Session", "Task" };
        var choices = kinds.Select((kind, index) => new OfflineResourceChoice(kind, "secret-uid-" + index, "资源" + index, "source", index == 1 ? "{\"type\":\"collective\"}" : "{}")
            { SourceStoryId = index < 3 ? "first" : "second", SourceStoryName = index < 3 ? "同名故事" : "远方故事", IsExternal = index >= 3 }).ToArray();
        var picker = new OfflineResourcePickerViewModel(choices, "引用资源");
        Assert.HasCount(2, picker.Folders);
        CollectionAssert.AreEqual(new[] { "[角色]", "[角色组]", "[物品]", "[物品组]", "[会话]", "[任务]" }, choices.Select(choice => choice.TypeLabel).ToArray());
        picker.Folders[0].IsExpanded = true;
        picker.SearchText = "远方";
        Assert.HasCount(1, picker.Folders);
        Assert.AreEqual("远方故事", picker.Folders[0].DisplayName);
        Assert.IsTrue(picker.Folders[0].IsExpanded);
        picker.SearchText = "角色组";
        Assert.HasCount(1, picker.Folders);
        Assert.AreEqual(choices[1], picker.Folders[0].Matches.Single());
        picker.SelectedChoice = choices[1];
        picker.SearchText = "不存在";
        Assert.IsFalse(picker.CanConfirm);
        picker.SearchText = "";
        Assert.IsTrue(picker.Folders[0].IsExpanded);
        Assert.IsFalse(picker.Folders[1].IsExpanded);
        var dialog = new OfflinePackageResourcePickerDialog(picker);
        dialog.Show(); Pump(); dialog.UpdateLayout();
        Assert.IsFalse(Descendants<TextBlock>(dialog).Any(text => text.Text.Contains("secret-uid")));
        var buttons = Descendants<Button>(dialog).Where(button => button.Content is "选择" or "取消").ToArray();
        Assert.AreEqual("选择", buttons[0].Content);
        Assert.AreEqual("取消", buttons[1].Content);
        dialog.Close();
    }

    private static GraphEditorHostViewModel Outputs(GraphScope scope)
    {
        var graph = new GraphDocument();
        foreach (var kind in new[] { scope == GraphScope.Task ? "settle" : "end", "logic_output" })
            for (var index = 0; index < 3; index++) graph.Nodes.Add(new GraphNodeAuthoringService().Create(graph, scope, kind, kind + index).Candidate!);
        return new(graph, scope);
    }
    private static void Arrange(FrameworkElement element, double width, double height)
    { element.Measure(new(width, height)); element.Arrange(new(0, 0, width, height)); element.UpdateLayout(); }
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T value) yield return value;
            foreach (var value2 in Descendants<T>(child)) yield return value2;
        }
    }
    private sealed class TestWindow : IDisposable
    {
        private readonly Window _window;
        public TestWindow(FrameworkElement content) { Content = content; _window = new Window { Content = content, Width = 400, Height = 700 }; _window.Show(); Pump(); _window.UpdateLayout(); }
        public FrameworkElement Content { get; }
        public void UpdateLayout() => _window.UpdateLayout();
        public void Dispose() => _window.Close();
    }
    private sealed class DirectoryFixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "temp", "output-refresh-" + Guid.NewGuid().ToString("N"));
        public DirectoryFixture() => Directory.CreateDirectory(Path);
        public void Dispose() { if (Directory.Exists(Path)) Directory.Delete(Path, true); }
    }
}
