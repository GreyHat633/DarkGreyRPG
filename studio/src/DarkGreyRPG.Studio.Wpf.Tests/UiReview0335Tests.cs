using System.Text.Json;
using System.Windows;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass, DoNotParallelize]
public sealed class UiReview0335Tests
{
    [STATestMethod]
    public void ChoiceColumnsPackIndependentlyWithoutChangingPortIdentity()
    {
        var choice = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(choice, "option", "flow", "condition");
        var graph = new GraphDocument([choice]);
        new DarkGreyRPG.Studio.Core.Graphs.Editing.GraphEditSession(graph, GraphScope.Session)
            .SetSessionChoiceConditionEnabled("choice", "option", true);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Session);
        var canvas = new CanonicalGraphEditorView(host);
        void Layout() { canvas.Measure(new Size(1200, 800)); canvas.Arrange(new Rect(0, 0, 1200, 800)); canvas.UpdateLayout(); }
        Layout();
        var visual = canvas.NodeVisuals.Single();
        var input = canvas.PortVisuals.Single(p => p.EffectivePortId == "condition");
        var output = canvas.PortVisuals.Single(p => p.EffectivePortId == "flow");
        var flowInput = canvas.PortVisuals.Single(p => p.EffectivePortId == "flow_in");
        Assert.AreEqual(flowInput.GetAnchorPoint(visual).Y, output.GetAnchorPoint(visual).Y, .01);
        Assert.AreEqual(24d, input.GetAnchorPoint(visual).Y - flowInput.GetAnchorPoint(visual).Y, .01);
        Assert.IsFalse(input.DisplayName.StartsWith("条件"));
        Assert.IsLessThan(input.GetAnchorPoint(visual).Y, canvas.PortVisuals.Single(p => p.EffectivePortId == "flow_in").GetAnchorPoint(visual).Y);
        using var inspector = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        inspector.ChoiceOptions.First().ConditionEnabled = false;
        Layout(); Assert.IsFalse(canvas.PortVisuals.Any(p => p.EffectivePortId == "condition"));
        Assert.IsTrue(host.Undo()); Layout();
        Assert.AreEqual(24d, canvas.PortVisuals.Single(p => p.EffectivePortId == "condition").GetAnchorPoint(canvas.NodeVisuals.Single()).Y
            - canvas.PortVisuals.Single(p => p.EffectivePortId == "flow").GetAnchorPoint(canvas.NodeVisuals.Single()).Y, .01);
    }

    [STATestMethod]
    public void ChoiceConditionGapsDoNotReserveRowsAndLabelsStayInsideColumns()
    {
        var choice = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(choice, "option", "flow", "condition");
        var host = new GraphEditorHostViewModel(new GraphDocument([choice]), GraphScope.Session);
        using var inspector = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        Assert.IsTrue(inspector.AddChoiceOption());
        Assert.IsTrue(inspector.AddChoiceOption());
        inspector.ChoiceOptions[0].DisplayText = "很长的中文选项名称，需要单行省略而且不能改变节点宽度";
        inspector.ChoiceOptions[1].DisplayText = "A long English option that must remain within its own column";
        var canvas = new CanonicalGraphEditorView(host);
        void Layout() { canvas.Measure(new Size(1200, 800)); canvas.Arrange(new Rect(0, 0, 1200, 800)); canvas.UpdateLayout(); }
        void Check(int enabled)
        {
            Layout();
            var visual = canvas.NodeVisuals.Single();
            Assert.AreEqual(232d, visual.ActualWidth, .01);
            var inputs = canvas.PortVisuals.Where(p => p.IsInput).OrderBy(p => p.GetAnchorPoint(visual).Y).ToArray();
            Assert.HasCount(enabled + 1, inputs);
            Assert.AreEqual(inputs[0].GetAnchorPoint(visual).Y, visual.ChoiceOptionRows[0].FlowOutput.GetAnchorPoint(visual).Y, .01);
            for (var i = 1; i < inputs.Length; i++)
                Assert.AreEqual(24d, inputs[i].GetAnchorPoint(visual).Y - inputs[i - 1].GetAnchorPoint(visual).Y, .01);
            foreach (var row in visual.ChoiceOptionRows)
            {
                Assert.AreEqual(System.Windows.TextTrimming.CharacterEllipsis, row.DisplayLabel.TextTrimming);
                Assert.AreEqual(System.Windows.TextWrapping.NoWrap, row.DisplayLabel.TextWrapping);
                Assert.AreEqual(row.DisplayLabel.Text, row.OutputGroup.ToolTip);
                Assert.IsTrue(row.DisplayLabel.ActualWidth <= 78d);
            }
        }
        Check(0);
        inspector.ChoiceOptions[2].ConditionEnabled = true;
        Check(1);
        inspector.ChoiceOptions[0].ConditionEnabled = true;
        inspector.ChoiceOptions[1].ConditionEnabled = true;
        Check(3);
        inspector.ChoiceOptions[1].ConditionEnabled = false;
        Check(2);
        Assert.IsTrue(host.Undo()); Check(3);
        Assert.IsTrue(host.Redo()); Check(2);
    }

    [STATestMethod]
    public void AnimationBlankSurfaceOutsideClickAndFoldIdentitySurviveReorder()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        node.Properties["layers"] = JsonSerializer.SerializeToElement(new[] { new { media_ref = "media/" + new string('a', 64) + ".png", morph_key = "picture", x = 0, y = 0, width = 1, height = 1, anchor_x = 0, anchor_y = 0, z = 0, morph_duration = 0, animations = Array.Empty<object>() } });
        var host = new GraphEditorHostViewModel(new GraphDocument([node]), GraphScope.Session);
        using var vm = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        var screen = new SessionScreenEditor { DataContext = vm };
        object Field(object target, string name) => target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(target)!;
        var editor = (AnimationSequenceEditor)Field(screen, "_animation");
        var window = new Window { Content = screen, Width = 700, Height = 900, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            editor.AddStep(); editor.AddStep();
            var items = (System.Windows.Controls.ItemsControl)Field(editor, "_steps");
            var second = (AnimationStepRow)items.Items[1];
            ((System.Windows.Controls.ComboBox)Field(second, "_effects")).SelectedIndex = 1;
            second = (AnimationStepRow)items.Items[1];
            var header = (FoldHeader)Field(second, "_header");
            header.IsChecked = false; header.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.IsFalse(((AnimatedLinePageBody)Field(second, "_body")).IsExpanded);
            second.MoveTo(0);
            Assert.IsFalse(((AnimatedLinePageBody)Field((AnimationStepRow)items.Items[0], "_body")).IsExpanded);
            Assert.IsTrue(host.Undo());
            second = (AnimationStepRow)items.Items[1];
            Assert.IsFalse(((AnimatedLinePageBody)Field(second, "_body")).IsExpanded);
            var border = (System.Windows.Controls.Border)Field(second, "_border");
            Assert.IsNotNull(border.Background, "Card padding must participate in hit testing.");
            border.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
            Assert.AreEqual(1, editor.SelectedIndex);
            window.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 1, System.Windows.Input.MouseButton.Left) { RoutedEvent = System.Windows.Input.Mouse.PreviewMouseDownEvent });
            Assert.AreEqual(-1, editor.SelectedIndex);
            Assert.IsFalse(((System.Windows.Controls.Button)Field(editor, "_remove")).IsEnabled);
        }
        finally { window.Close(); }
    }

    [STATestMethod]
    public void AnimationSelectionAndToolbarDeletionAreSharedAndUndoable()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        node.Properties["layers"] = JsonSerializer.SerializeToElement(new[] { new { media_ref = "media/" + new string('a', 64) + ".png", morph_key = "picture", x = 0, y = 0, width = 1, height = 1, anchor_x = 0, anchor_y = 0, z = 0, morph_duration = 0, animations = Array.Empty<object>() } });
        var host = new GraphEditorHostViewModel(new GraphDocument([node]), GraphScope.Session);
        using var first = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        using var second = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        var a = new SessionScreenEditor { DataContext = first };
        var b = new SessionScreenEditor { DataContext = second };
        object Field(object target, string name) => target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(target)!;
        var left = (AnimationSequenceEditor)Field(a, "_animation");
        var right = (AnimationSequenceEditor)Field(b, "_animation");
        Assert.AreEqual(-1, left.SelectedIndex);
        left.AddStep(); left.AddStep();
        Assert.AreEqual(1, right.SelectedIndex);
        right.Select(0, true);
        Assert.AreEqual(0, left.SelectedIndex);
        ((System.Windows.Controls.Button)Field(left, "_remove")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Assert.AreEqual(1, host.Graph.Nodes.Single().Properties["layers"][0].GetProperty("animations").GetArrayLength());
        Assert.AreEqual(0, right.SelectedIndex);
        Assert.IsTrue(host.Undo());
        Assert.AreEqual(2, host.Graph.Nodes.Single().Properties["layers"][0].GetProperty("animations").GetArrayLength());
        Assert.IsTrue(host.Redo());
        Assert.AreEqual(1, host.Graph.Nodes.Single().Properties["layers"][0].GetProperty("animations").GetArrayLength());
    }

    [STATestMethod]
    public void ChoiceFoldsFollowStableIdsWithoutChangingRuntimeData()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(node, "first", "flow", "condition");
        var host = new GraphEditorHostViewModel(new GraphDocument([node]), GraphScope.Session);
        using var a = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        using var b = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        Assert.IsTrue(a.ChoiceOptions.First().IsExpanded);
        var before = host.Graph.ToJson(); a.ChoiceOptions.First().IsExpanded = false;
        Assert.IsFalse(b.ChoiceOptions.First().IsExpanded); Assert.AreEqual(before, host.Graph.ToJson());
        Assert.IsTrue(a.AddChoiceOption()); Assert.IsTrue(b.ChoiceOptions.Last().IsExpanded);
        var id = a.ChoiceOptions.Last().OptionId;
        a.ChoiceOptions.Last().MoveTo(0);
        Assert.IsTrue(b.ChoiceOptions.Single(o => o.OptionId == id).IsExpanded);
        Assert.IsFalse(b.ChoiceOptions.Single(o => o.OptionId == "first").IsExpanded);
    }

    [STATestMethod]
    public void UndoConditionDisconnectRestoresVisibleWireAfterHiddenEndpoint()
    {
        var choice = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(choice, "option", "flow", "condition");
        var gate = GraphNodeFactory.Create(GraphScope.Session, "logic_input", "gate");
        gate.Properties["port_id"] = JsonSerializer.SerializeToElement("gate");
        gate.Properties["display_name"] = JsonSerializer.SerializeToElement("Gate");
        var graph = new GraphDocument([choice, gate], [new("gate", "logic_out", "choice", "condition", GraphInterfaceKind.Logic)]);
        new DarkGreyRPG.Studio.Core.Graphs.Editing.GraphEditSession(graph, GraphScope.Session)
            .SetSessionChoiceConditionEnabled("choice", "option", true);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Session);
        var canvas = new CanonicalGraphEditorView(host);
        canvas.Measure(new Size(1200, 800)); canvas.Arrange(new Rect(0, 0, 1200, 800)); canvas.UpdateLayout();
        using var inspector = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single(n => n.NodeId == "choice"));
        Assert.HasCount(1, canvas.ConnectionVisuals);
        inspector.ChoiceOptions.First().ConditionEnabled = false;
        Assert.HasCount(0, canvas.ConnectionVisuals);
        Assert.IsTrue(host.Undo());
        Assert.HasCount(1, canvas.ConnectionVisuals);
        Assert.IsTrue(host.Redo());
        Assert.HasCount(0, canvas.ConnectionVisuals);
    }

    [STATestMethod]
    public void PictureSectionsShareStateAndOneShotPreviewReturnsToEditing()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        var host = new GraphEditorHostViewModel(new GraphDocument([node]), GraphScope.Session);
        using var first = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        using var second = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        var a = new SessionScreenEditor { DataContext = first };
        var b = new SessionScreenEditor { DataContext = second };
        object Field(SessionScreenEditor editor, string name) => typeof(SessionScreenEditor).GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(editor)!;
        var animation = (AnimationSequenceEditor)Field(a, "_animation");
        Assert.IsFalse(animation.IsExpanded);
        Assert.IsTrue(((AnimatedLinePageBody)Field(a, "_propertiesBody")).IsExpanded);
        typeof(SessionScreenEditor).GetMethod("SetSections", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(a, [false, true]);
        Assert.IsTrue(((AnimationSequenceEditor)Field(b, "_animation")).IsExpanded);
        Assert.IsFalse(((AnimatedLinePageBody)Field(b, "_propertiesBody")).IsExpanded);
        var before = host.Graph.ToJson();
        typeof(SessionScreenEditor).GetMethod("PlayPreview", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(a, null);
        Assert.IsFalse(((System.Windows.Controls.Primitives.ToggleButton)Field(a, "_previewButton")).IsChecked == true);
        Assert.AreEqual(before, host.Graph.ToJson());
    }

    [STATestMethod]
    public void ChoiceProjectionsShareExpansionPersistSettingsAndKeepFlowFirstAfterUndo()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(node, "option", "option_flow");
        var host = new GraphEditorHostViewModel(new GraphDocument([node]), GraphScope.Session);
        using var first = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        using var second = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
        var canvas = new CanonicalGraphEditorView(host);
        var option = first.ChoiceOptions.First();
        Assert.IsFalse(option.ConditionEnabled);
        option.ConditionEnabled = true;
        Assert.IsTrue(second.ChoiceOptions.First().ConditionEnabled);
        option.DisableWhenFalse = true;
        Assert.IsTrue(second.ChoiceOptions.First().DisableWhenFalse);
        second.ChoiceOptions.First().UnavailableHint = "需要钥匙";
        Assert.AreEqual("需要钥匙", first.ChoiceOptions.First().UnavailableHint);
        Assert.AreEqual("flow_in", host.Nodes.Single().Inputs.First().PortId);
        Assert.IsTrue(host.Undo());
        Assert.AreEqual("", first.ChoiceOptions.First().UnavailableHint);
        Assert.AreEqual("", second.ChoiceOptions.First().UnavailableHint);
        second.ChoiceOptions.First().ConditionEnabled = false;
        Assert.IsFalse(first.ChoiceOptions.First().ConditionEnabled);
        Assert.IsFalse(host.Nodes.Single().Inputs.Any(p => p.PortId != "flow_in"));
        Assert.IsFalse(canvas.PortVisuals.Any(p => p.EffectivePortId != "flow_in" && p.IsInput));
    }

    [STATestMethod]
    public void MarqueeShowsSummaryCoalescesAndReusesSingleInspector()
    {
        using var workspace = new CanonicalStoryWorkspaceViewModel(new(GraphResourceKind.Story, "s", "故事", new([])),
            sessions: [new(GraphResourceKind.Session, "session", "会话", new([GraphNodeFactory.Create(GraphScope.Session, "line", "a"), GraphNodeFactory.Create(GraphScope.Session, "line", "b")]))]);
        workspace.OpenGraphResource(workspace.SessionItems.Single());
        var view = new CanonicalStoryWorkspaceView(workspace);
        var root = new System.Windows.Controls.Grid { Width = 1280, Height = 720 };
        root.Children.Add(view); root.Measure(new Size(1280, 720)); root.Arrange(new Rect(0, 0, 1280, 720)); root.UpdateLayout();
        var graph = view.WorkspaceGraph;
        graph.SelectNode(workspace.ActiveGraphHost.Nodes.First());
        var initial = workspace.NodeInspector;
        Assert.IsNotNull(initial);
        Assert.IsTrue(graph.BeginMarqueeSelection(new Point(-100000, -100000)));
        Assert.IsTrue(graph.UpdateMarqueeSelection(new Point(100000, 100000)));
        Assert.IsTrue(graph.CompleteMarqueeSelection());
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.IsNull(workspace.NodeInspector);
        Assert.AreEqual("已选 2 个节点", workspace.InspectorTitle);
        var count = workspace.InspectorCreationCount;
        for (int i = 0; i < 100; i++) graph.ApplyMarqueeSelection(new Rect(-100000, -100000, 200000, 200000));
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
        Assert.AreEqual(count, workspace.InspectorCreationCount);
        graph.SelectNode(workspace.ActiveGraphHost.Nodes.Last());
        Assert.AreEqual("b", workspace.NodeInspector?.NodeId);
        var next = workspace.NodeInspector;
        workspace.SelectGraphNode(workspace.ActiveGraphHost.Nodes.Last());
        Assert.AreSame(next, workspace.NodeInspector);
        Assert.IsFalse(workspace.HasUsageContext);
        workspace.SelectTreeItem(workspace.SessionItems.Single());
        Assert.IsFalse(workspace.HasUsageContext);
    }

    [STATestMethod]
    public void OtherNodeEditorsObservePeerEditsWithoutReselecting()
    {
        foreach (var (scope, type, property, value) in new[] {
            (GraphScope.Session, "choice", "prompt", JsonSerializer.SerializeToElement("新提示")),
            (GraphScope.Session, "music", "volume", JsonSerializer.SerializeToElement(.35)),
            (GraphScope.Session, "screen", "transition", JsonSerializer.SerializeToElement(new { type = "fade", direction = "left", duration = 1 })),
            (GraphScope.Task, "objective", "description", JsonSerializer.SerializeToElement("新目标")),
            (GraphScope.Story, "action", "message", JsonSerializer.SerializeToElement("新消息")) })
        {
            var node = GraphNodeFactory.Create(scope, type, "node");
            if (type == "action") Assert.IsTrue(CanonicalStoryActionSchema.TryInitializeType(node, CanonicalStoryActionSchema.SendMessage, out _));
            var host = new GraphEditorHostViewModel(new GraphDocument([node]), scope);
            using var first = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
            using var second = new CanonicalNodeInspectorViewModel(host, host.Nodes.Single());
            int before = second.RefreshCount;
            Assert.IsTrue(host.SetNodeProperty("node", property, value), type);
            Assert.IsGreaterThan(before, second.RefreshCount, type);
            Assert.AreEqual(value.GetRawText(), second.Node.Properties[property].GetRawText(), type);
        }
    }
}
