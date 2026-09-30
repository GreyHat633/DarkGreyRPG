using System.Windows.Documents;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class DynamicContent0334Tests
{
    [STATestMethod]
    public void LazyPreviewAcceptsResourceDragBeforeBodyHasBeenCreated()
    {
        var editor = new DynamicContentEditor { Text = DynamicContentText.Encode([new(Type: "player_name")]) };
        editor.Measure(new System.Windows.Size(300, 100));
        editor.Arrange(new System.Windows.Rect(0, 0, 300, 100));
        Assert.IsFalse(Descendants(editor).OfType<System.Windows.Controls.RichTextBox>().Any());
        Assert.IsTrue(editor.AllowDrop);
        var data = new System.Windows.DataObject(CanonicalStoryWorkspaceView.ResourceDragFormat, "unsupported resource");
        var constructor = typeof(System.Windows.DragEventArgs).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single(c => c.GetParameters().Length == 5);
        var args = (System.Windows.DragEventArgs)constructor.Invoke([data, System.Windows.DragDropKeyStates.LeftMouseButton, System.Windows.DragDropEffects.Link, editor, new System.Windows.Point(10, 10)]);
        args.RoutedEvent = System.Windows.DragDrop.PreviewDragEnterEvent;
        editor.RaiseEvent(args);
        Assert.IsTrue(Descendants(editor).OfType<System.Windows.Controls.RichTextBox>().Any());
        Assert.IsTrue(args.Handled);
        Assert.AreEqual(System.Windows.DragDropEffects.None, args.Effects);
        Assert.AreEqual("player_name", DynamicContentText.Parse(editor.Text).Single().Type);
    }

    private static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
    {
        yield return root;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var item in Descendants(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) yield return item;
    }
    [STATestMethod]
    public void ResourceDropReplacesOnlyTargetSelectionAndKeepsTypedGroupIdentity()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var workspace = new DarkGreyRPG.Studio.ViewModels.Graph.CanonicalStoryWorkspaceViewModel(
            new(DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind.Story, "story", "Story", new([])),
            actors: [new("actor", "Merchant", "actor.json", [])],
            sessions: [new(DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind.Session, "session", "Session", new([line]))],
            items: [new DarkGreyRPG.Studio.Core.Items.CollectiveItemResource { GroupId = "group", DisplayName = "Group" }]);
        workspace.OpenGraphResource(workspace.SessionItems.Single());
        using var owner = new DarkGreyRPG.Studio.ViewModels.Graph.CanonicalNodeInspectorViewModel(workspace.ActiveGraphHost, workspace.ActiveGraphHost.Nodes.Single(), workspace.ActorItems, workspace.ItemItems);
        owner.AddLinePage(); owner.AddLinePage();
        owner.LinePages[0].Text = "untouched"; owner.LinePages[1].Text = "replace me";
        var view = new LinePagesEditor { DataContext = owner, Width = 320 };
        var window = new System.Windows.Window { Content = view, Width = 360, Height = 700, ShowInTaskbar = false };
        try {
            window.Show(); window.UpdateLayout(); window.Dispatcher.Invoke(() => {}, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var editor = Descendants(view).OfType<DynamicContentEditor>().ElementAt(1);
            editor.Body.Focus(); editor.Body.SelectAll(); window.UpdateLayout();
            var data = new System.Windows.DataObject(CanonicalStoryWorkspaceView.ResourceDragFormat, workspace.ItemItems.Single());
            var constructor = typeof(System.Windows.DragEventArgs).GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single(c => c.GetParameters().Length == 5);
            var args = (System.Windows.DragEventArgs)constructor.Invoke([data, System.Windows.DragDropKeyStates.LeftMouseButton, System.Windows.DragDropEffects.Link, editor.Body, new System.Windows.Point(10, 10)]);
            args.RoutedEvent = System.Windows.DragDrop.PreviewDropEvent; editor.Body.RaiseEvent(args);
            Assert.AreEqual(System.Windows.DragDropEffects.Link, args.Effects);
            Assert.AreEqual("untouched", owner.LinePages[0].Text);
            var part = DynamicContentText.Parse(owner.LinePages[1].Text).Single();
            Assert.AreEqual("item_name", part.Type); Assert.AreEqual("group", part.ItemId);
            var atom = ((Paragraph)editor.Body.Document.Blocks.FirstBlock!).Inlines.OfType<InlineUIContainer>().Single();
            Assert.AreEqual("{Group}", ((System.Windows.Controls.Button)atom.Child).Content);
        } finally { window.Close(); }
    }

    [STATestMethod]
    public void SharedLineToolbarTargetsFocusedPageAndRejectsOtherNode()
    {
        var a = DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphNodeFactory.Create(DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphScope.Session, "line", "a");
        var b = DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphNodeFactory.Create(DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphScope.Session, "line", "b");
        using var resource = new DarkGreyRPG.Studio.ViewModels.Graph.CanonicalGraphResourceEditorViewModel(new(
            DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind.Session, "session", "Session", new([a, b])));
        using var first = new DarkGreyRPG.Studio.ViewModels.Graph.CanonicalNodeInspectorViewModel(resource.Host, resource.Host.Nodes.First());
        using var other = new DarkGreyRPG.Studio.ViewModels.Graph.CanonicalNodeInspectorViewModel(resource.Host, resource.Host.Nodes.Last());
        first.AddLinePage(); first.AddLinePage(); other.AddLinePage();
        first.LinePages[0].Text = "untouched"; first.LinePages[1].Text = "replace";
        var view = new LinePagesEditor { DataContext = first, Width = 320 };
        var second = new LinePagesEditor { DataContext = other, Width = 320 };
        var panel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        panel.Children.Add(view); panel.Children.Add(second);
        var window = new System.Windows.Window { Content = panel, Width = 700, Height = 800, ShowInTaskbar = false };
        void Pump() { window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); }
        try
        {
            window.Show(); Pump();
            var fields = Descendants(view).OfType<DynamicContentEditor>().ToArray();
            var toolbar = (System.Windows.Controls.Button)view.FindName("DynamicInsert");
            Assert.IsFalse(toolbar.IsEnabled);
            Assert.IsTrue(fields.All(field => !field.ShowInsertButton));
            fields[1].Body.Focus(); fields[1].Body.SelectAll(); Pump();
            Assert.IsTrue(toolbar.IsEnabled);
            toolbar.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); Pump();
            var popup = (System.Windows.Controls.Primitives.Popup)typeof(DynamicContentEditor).GetField("_picker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(fields[1])!;
            Assert.IsTrue(popup.IsOpen);
            var nameButton = Descendants(popup.Child).OfType<System.Windows.Controls.Button>().First(button => button.Content is System.Windows.Controls.StackPanel);
            nameButton.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); Pump();
            Assert.AreEqual("untouched", first.LinePages[0].Text);
            Assert.AreEqual("player_name", DynamicContentText.Parse(first.LinePages[1].Text).Single().Type);
            Assert.IsFalse(popup.IsOpen);
            Descendants(second).OfType<DynamicContentEditor>().First().Body.Focus(); Pump();
            Assert.IsFalse(toolbar.IsEnabled);
        }
        finally { window.Close(); }
    }
    [STATestMethod]
    public void SameNameItemChoicesShowDistinctStableIds()
    {
        foreach (var id in new[] { "demo:stone_a", "demo:stone_b" })
        {
            var template = DynamicContentEditor.ItemChoiceTemplate(); template.Seal();
            var row = (System.Windows.Controls.StackPanel)template.LoadContent();
            row.DataContext = new DarkGreyRPG.Studio.ViewModels.Graph.CanonicalResourceSelectionOption(id, "同名物品", true);
            var window = new System.Windows.Window { Content = row, Width = 300, Height = 160, ShowInTaskbar = false };
            try
            {
                window.Show(); window.UpdateLayout();
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Assert.AreEqual("同名物品", ((System.Windows.Controls.TextBlock)row.Children[0]).Text);
                Assert.AreEqual(id, ((System.Windows.Controls.TextBlock)row.Children[1]).Text);
                Assert.IsTrue(AuthoringText.GetIsHelp(row.Children[1]));
            }
            finally { window.Close(); }
        }
    }
    [STATestMethod]
    public void CanvasMouseRoutingAcceptsRichTextContentElements()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "line", "clickable-line");
        node.Properties["pages"] = System.Text.Json.JsonSerializer.SerializeToElement(new[] { new { page_id = "page", text = "Click this inline text" } });
        var host = new DarkGreyRPG.Studio.ViewModels.Graph.GraphEditorHostViewModel(new([node]), GraphScope.Session);
        var view = new CanonicalGraphEditorView(host);
        var window = new System.Windows.Window { Content = view, Width = 1000, Height = 800, ShowInTaskbar = false };
        IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }
        try
        {
            window.Show(); window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var editor = Descendants(view).OfType<DynamicContentEditor>().First();
            _ = editor.Body;
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            editor = Descendants(view).OfType<DynamicContentEditor>().First();
            var run = ((Paragraph)editor.Body.Document.Blocks.FirstBlock!).Inlines.OfType<Run>().Single();
            var press = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent, Source = run };
            Assert.IsTrue(editor.IsLoaded, "Editor must be attached");
            Assert.AreSame(editor.Body, LinePagesEditor.Ancestor<System.Windows.Controls.RichTextBox>(run));
            Assert.AreSame(run, press.OriginalSource);
            // Explicit source reproduces the native exception even if template refresh has detached
            // this editor's panel. Native focus/selection is verified separately on the delivered EXE.
            typeof(CanonicalGraphEditorView).GetMethod("CanvasViewport_OnPreviewMouseDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(view, [view, press]);
            // The native RichTextBox may mark the fully routed event handled after the canvas returns.
            run.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
            Assert.AreEqual("Click this inline text", host.Graph.Nodes.Single().Properties["pages"][0].GetProperty("text").GetString());
        }
        finally { window.Close(); }
    }
    [STATestMethod]
    public void OffCanvasEditorCreatesItsBodyWhenMovedIntoView()
    {
        var near = GraphNodeFactory.Create(GraphScope.Session, "line", "near");
        var far = GraphNodeFactory.Create(GraphScope.Session, "line", "far");
        var host = new DarkGreyRPG.Studio.ViewModels.Graph.GraphEditorHostViewModel(new([near, far]), GraphScope.Session);
        host.SetNodePosition("near", 0, 0);
        host.SetNodePosition("far", 10000, 0);
        var view = new CanonicalGraphEditorView(host);
        var window = new System.Windows.Window { Content = view, Width = 1000, Height = 800, ShowInTaskbar = false };
        IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }
        void Settle()
        {
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
        }
        try
        {
            window.Show(); Settle();
            int before = Descendants(view).OfType<System.Windows.Controls.RichTextBox>().Count();
            Assert.IsGreaterThanOrEqualTo(1, before);
            host.SetNodePosition("far", 450, 0); Settle();
            int after = Descendants(view).OfType<System.Windows.Controls.RichTextBox>().Count();
            Assert.AreEqual(before + 1, after);
            Assert.HasCount(2, host.Nodes);
        }
        finally { window.Close(); }
    }
    [TestMethod]
    public void SharedStudioRuntimeVectorsAgree()
    {
        using var vectors = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "dynamic-content-0334.json")));
        foreach (var vector in vectors.RootElement.EnumerateArray())
        {
            var input = vector.GetProperty("input").GetString()!;
            if (vector.TryGetProperty("invalid", out _))
            {
                var rejected = false;
                try { DynamicContentText.Parse(input); }
                catch (Exception error) when (error is System.Text.Json.JsonException or FormatException or InvalidOperationException or KeyNotFoundException) { rejected = true; }
                Assert.IsTrue(rejected, input);
            }
            else Assert.AreEqual(vector.GetProperty("expected").GetString(), DynamicContentText.Resolve(input,
                p => p.Type == "player_name" ? "Alice" : p.Type == "player_level" ? "12" : "3"));
        }
    }
    [TestMethod]
    public void OldTokenLookingTextRemainsLiteralAndNewPartsRoundTrip()
    {
        const string old = "欢迎 {{player_name}}，〔玩家名称〕 \\ n";
        Assert.AreEqual(old, DynamicContentText.Display(old));
        var parts = new[] { new DynamicContentText.Part(Text: old), new DynamicContentText.Part(Type: "player_name"), new DynamicContentText.Part(Type: "item_count", ItemId: "demo:iron") };
        var encoded = DynamicContentText.Encode(parts);
        CollectionAssert.AreEqual(parts, DynamicContentText.Parse(encoded).ToArray());
        Assert.AreEqual(old + "Alice3", DynamicContentText.Resolve(encoded, p => p.Type == "player_name" ? "Alice" : "3"));
        CollectionAssert.AreEqual(new[] { "demo:iron" }, DynamicContentText.ItemReferences(encoded).ToArray());
    }
    [STATestMethod]
    public void LoadedEditorCanDeleteAndUndoAtomicContent()
    {
        var encoded = DynamicContentText.Encode([new(Text: "Hello "), new(Type: "player_name")]);
        var editor = new DynamicContentEditor { Text = encoded };
        var window = new System.Windows.Window { Content = editor, Width = 400, Height = 200, ShowInTaskbar = false };
        try
        {
            window.Show();
            editor.Body.Focus();
            editor.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.IsTrue(editor.Body.IsDocumentEnabled);
            var atom = ((Paragraph)editor.Body.Document.Blocks.FirstBlock!).Inlines.OfType<InlineUIContainer>().Single();
            editor.Body.Selection.Select(atom.ElementStart, atom.ElementEnd);
            editor.Body.Selection.Text = "";
            Assert.AreEqual("Hello ", editor.Text);
            Assert.IsTrue(System.Windows.Input.ApplicationCommands.Undo.CanExecute(null, editor.Body));
            System.Windows.Input.ApplicationCommands.Undo.Execute(null, editor.Body);
            Assert.AreEqual(encoded, editor.Text);
            Assert.IsInstanceOfType<System.Windows.Controls.Button>(((Paragraph)editor.Body.Document.Blocks.FirstBlock!).Inlines.OfType<InlineUIContainer>().Single().Child);
            System.Windows.Input.ApplicationCommands.Redo.Execute(null, editor.Body);
            Assert.AreEqual("Hello ", editor.Text);
        }
        finally { window.Close(); }
    }
    [STATestMethod]
    public void RichEditorPreservesAtomicDataWithoutTrailingParagraphText()
    {
        var editor = new DynamicContentEditor { Text = "你好" };
        editor.Body.CaretPosition = editor.Body.Document.ContentEnd;
        editor.Body.CaretPosition.InsertTextInRun("世界");
        Assert.AreEqual("你好世界", editor.Text);
        var encoded = DynamicContentText.Encode([new(Text: "欢迎"), new(Type: "player_name"), new(Text: "！")]);
        editor.Text = encoded;
        var paragraph = (Paragraph)editor.Body.Document.Blocks.FirstBlock!;
        Assert.AreEqual(1, paragraph.Inlines.OfType<InlineUIContainer>().Count());
        Assert.AreEqual(encoded, editor.Text);
        var atom = paragraph.Inlines.OfType<InlineUIContainer>().Single();
        editor.Body.Selection.Select(atom.ElementStart, atom.ElementEnd);
        editor.Body.Selection.Text = "";
        Assert.AreEqual("欢迎！", editor.Text);
    }
}
