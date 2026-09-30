using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class RepeatAuthoring0334Tests
{
    [STATestMethod]
    public void ChangingDetailModeInterpolatesFromCurrentHeightInBothDirections()
    {
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Story, "story", "Story", new([GraphNodeFactory.CreateStoryStart("start")])));
        using var owner = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        owner.RepeatMode = "cooldown";
        var view = new DarkGreyRPG.Studio.Views.Graph.StoryRepeatEditor { DataContext = owner, VerticalAlignment = System.Windows.VerticalAlignment.Top };
        var window = new System.Windows.Window { Content = view, Width = 300, Height = 500, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show(); Pump(300);
            var details = (DarkGreyRPG.Studio.Views.Graph.AnimatedNaturalHeightBody)view.FindName("Details");
            var top = details.TranslatePoint(new(), view).Y;
            var shortHeight = details.ActualHeight;
            owner.RepeatMode = "scheduled"; Pump(90);
            Assert.AreEqual(top, details.TranslatePoint(new(), view).Y, .5);
            Assert.AreEqual(0d, details.Child.TranslatePoint(new(), details).Y, .5);
            Assert.IsTrue(details.ActualHeight >= shortHeight, "Growing must not restart at zero.");
            Assert.IsTrue(details.ActualHeight <= details.Child.DesiredSize.Height);
            Pump(300);
            Assert.AreEqual(details.Child.DesiredSize.Height, details.ActualHeight, .5);
            var tallHeight = details.ActualHeight;
            Assert.IsTrue(tallHeight > shortHeight);
            owner.RepeatMode = "cooldown"; Pump(70);
            Assert.IsTrue(details.ActualHeight >= shortHeight);
            Assert.IsTrue(details.ActualHeight <= tallHeight);
            // Reverse while still shrinking: start from the displayed height, not zero.
            var duringShrink = details.ActualHeight;
            owner.RepeatMode = "scheduled"; Pump(35);
            Assert.IsTrue(details.ActualHeight >= duringShrink - .5);
            Assert.IsTrue(details.ActualHeight <= tallHeight + .5);
            Pump(300);
            owner.RepeatMode = "once"; Pump(90);
            Assert.IsTrue(details.ActualHeight > 0 || !System.Windows.SystemParameters.ClientAreaAnimation);
            Assert.AreEqual(top, details.TranslatePoint(new(), view).Y, .5);
            Pump(300);
            Assert.AreEqual(0d, details.ActualHeight, .5);
            owner.RepeatMode = "scheduled"; Pump(300);
            owner.RepeatPeriod = "yearly"; Pump(300);
            Assert.IsTrue(details.ActualHeight > tallHeight);
            Assert.AreEqual(details.Child.DesiredSize.Height, details.ActualHeight, .5);
        }
        finally { window.Close(); }
        static void Pump(int milliseconds)
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
    }

    [TestMethod]
    public void ModeChangeCommitsPolicyAndConditionInOneUndoAndPreservesDisabledRule()
    {
        var start = GraphNodeFactory.CreateStoryStart("start");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Story, "story", "Story", new([start])));
        using var inline = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.AreEqual("once", inline.RepeatMode);
        inline.RepeatMode = "cooldown";
        Assert.AreEqual("cooldown", inspector.RepeatMode);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual("once", inspector.RepeatMode);
        Assert.IsTrue(editor.Host.Redo());
        inline.RepeatValue = "48";
        inline.RepeatMode = "once";
        Assert.AreEqual("once", inspector.RepeatMode);
        inline.RepeatMode = "cooldown";
        Assert.AreEqual("48", inspector.RepeatValue);
        inline.RepeatMode = "none";
        Assert.AreEqual("none", inspector.RepeatMode);
    }

    [TestMethod]
    public void RepeatDraftAndUndoShareCanonicalRuleAcrossBothEditors()
    {
        var start = GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start");
        StoryStartSchema.InitializeDefault(start, "trigger");
        using var editor = new CanonicalGraphResourceEditorViewModel(new(GraphResourceKind.Story, "story", "Story", new([start])));
        using var inline = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        inline.IsRepeatable = true;
        inline.RepeatMode = "cooldown";
        Assert.AreEqual("cooldown", inspector.RepeatMode);
        inline.RepeatValue = "0";
        Assert.IsFalse(string.IsNullOrEmpty(inline.RepeatError));
        Assert.AreEqual("24", inspector.RepeatValue);
        inline.RepeatValue = "48";
        Assert.AreEqual("48", inspector.RepeatValue);
        Assert.IsTrue(editor.Host.Undo());
        Assert.AreEqual("24", inline.RepeatValue);
        inspector.RepeatMode = "scheduled";
        inspector.RepeatPeriod = "yearly";
        inspector.RepeatMonth = "2";
        inspector.RepeatDay = "29";
        Assert.AreEqual("", inspector.RepeatError);
        inspector.RepeatDay = "30";
        Assert.IsFalse(string.IsNullOrEmpty(inspector.RepeatError));
        Assert.AreEqual("29", inline.RepeatDay);
        inspector.IsRepeatable = false;
        Assert.AreEqual("仅一次", inline.RepeatSummary);
    }
}
