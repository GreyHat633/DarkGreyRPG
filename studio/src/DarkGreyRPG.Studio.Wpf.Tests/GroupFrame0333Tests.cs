using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GroupFrame0333Tests
{
    public TestContext TestContext { get; set; } = null!;

    [STATestMethod]
    public void VisibleRenderingDuringMoveAndPageCollapsePreservesGroups()
    {
        // WPF UI rendering intervals, not GPU-present latency or a guaranteed display FPS.
        foreach (int groups in new[] { 0, 300 })
        {
            var nodes = Enumerable.Range(0, 300)
                .Select(i => GraphNodeFactory.Create(GraphScope.Session, "line", "frame_" + i)).ToArray();
            var host = new GraphEditorHostViewModel(new(nodes), GraphScope.Session);
            for (int i = 0; i < nodes.Length; i++) host.SetNodePosition(nodes[i].Id, i % 20 * 500, i / 20 * 700);
            host.RestoreFrames(Enumerable.Range(0, groups)
                .Select(i => new GraphCommentFrame("group_" + i, "Frame " + i, 0, 0, 100, 100, [nodes[i].Id])).ToArray());
            var view = new CanonicalGraphEditorView(host);
            var window = new Window { Content = view, Width = 1000, Height = 800, Left = 200, Top = 40, ShowInTaskbar = false };
            try
            {
                window.Show(); window.UpdateLayout();
                // Complete deferred editor creation before measuring steady-state interaction.
                // Otherwise Loaded-priority work can enter the move phase after its frame warmup.
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                view.SelectNodes(["frame_0"]);
                var toggle = Descendants(view).OfType<Button>()
                    .First(b => AutomationProperties.GetAutomationId(b) == "ToggleLinePage");
                var loop = new DispatcherFrame();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
                timer.Tick += (_, _) => loop.Continue = false;
                var samples = new Dictionary<string, List<double>> { ["move"] = [], ["collapse"] = [] };
                int frames = 0, toggles = 0;
                long previous = 0;
                Exception? failure = null;
                EventHandler render = (_, _) =>
                {
                    try
                    {
                        long now = Stopwatch.GetTimestamp();
                        string phase = frames < 100 ? "move" : "collapse";
                        if (frames > 20 && frames != 100 && previous != 0)
                        {
                            var elapsed = Stopwatch.GetElapsedTime(previous, now).TotalMilliseconds;
                            samples[phase].Add(elapsed);
                            if (elapsed > 100) TestContext.WriteLine($"RENDER_SPIKE groups={groups} frame={frames} ms={elapsed:F3} gen2={GC.CollectionCount(2)} heap={GC.GetTotalMemory(false)}");
                        }
                        previous = now;
                        if (frames < 100) Assert.IsTrue(view.MoveSelectedNodes(new Vector(1, 0)));
                        else if (frames % 20 == 0) { toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); toggles++; }
                        view.InvalidateVisual();
                        if (++frames >= 200) loop.Continue = false;
                    }
                    catch (Exception error) { failure = error; loop.Continue = false; }
                };
                CompositionTarget.Rendering += render;
                timer.Start();
                try { Dispatcher.PushFrame(loop); }
                finally { timer.Stop(); CompositionTarget.Rendering -= render; }
                if (failure != null) throw failure;
                Assert.AreEqual(200, frames, "Visible rendering timed out");
                Assert.AreEqual(5, toggles);
                Assert.HasCount(300, host.Nodes);
                Assert.HasCount(groups, host.Frames);
                if (groups > 0) CollectionAssert.AreEqual(new[] { "frame_0" }, host.Frames.Single(f => f.Id == "group_0").Members);
                foreach (var pair in samples)
                {
                    var sorted = pair.Value.Order().ToArray();
                    TestContext.WriteLine($"WPF_RENDER groups={groups} phase={pair.Key} n={sorted.Length} median_ms={sorted[sorted.Length / 2]:F3} p95_ms={sorted[(int)(sorted.Length * .95)]:F3} max_ms={sorted[^1]:F3}; UI Rendering callback intervals, excludes GPU present");
                }
            }
            finally { window.Close(); }
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
