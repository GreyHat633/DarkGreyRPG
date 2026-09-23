using System.Diagnostics;
using System.Windows;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class GroupScale0333Tests
{
    public TestContext TestContext { get; set; } = null!;

    [STATestMethod]
    public void ThreeHundredGroupsRetainMembershipDuringRepeatedLayoutAndUndo()
    {
        // Actual WPF layout timings, not composition/frame-rate measurements or native input.
        foreach (int groupCount in new[] { 0, 300 })
        {
            var nodes = Enumerable.Range(0, 300)
                .Select(i => GraphNodeFactory.Create(GraphScope.Session, "line", "scale_" + i)).ToArray();
            var host = new GraphEditorHostViewModel(new(nodes), GraphScope.Session);
            for (int i = 0; i < nodes.Length; i++) host.SetNodePosition("scale_" + i, i % 20 * 500, i / 20 * 700);
            host.RestoreFrames(Enumerable.Range(0, groupCount)
                .Select(i => new GraphCommentFrame("scale_group_" + i, "Scale group " + i, 0, 0, 100, 100, ["scale_" + i])).ToArray());
            var view = new CanonicalGraphEditorView(host);
            var window = new Window { Content = view, Width = 1000, Height = 800, Left = -10000, Top = -10000, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                view.SelectNodes(["scale_0"]);
                var samples = new List<double>();
                for (int i = 0; i < 35; i++)
                {
                    var timer = Stopwatch.StartNew();
                    Assert.IsTrue(view.MoveSelectedNodes(new Vector(1, 0)));
                    window.UpdateLayout();
                    timer.Stop();
                    if (i >= 5) samples.Add(timer.Elapsed.TotalMilliseconds);
                }
                Assert.HasCount(300, host.Nodes);
                Assert.HasCount(groupCount, host.Frames);
                Assert.AreEqual(35d, host.Nodes.Single(n => n.NodeId == "scale_0").X);
                host.CommitGroupMove(
                    new Dictionary<string, GraphEditorNodePosition> { ["scale_0"] = new(0, 0) },
                    new Dictionary<string, GraphEditorNodePosition> { ["scale_0"] = new(35, 0) });
                Assert.IsTrue(host.Undo());
                window.UpdateLayout();
                Assert.AreEqual(0d, host.Nodes.Single(n => n.NodeId == "scale_0").X);
                if (groupCount > 0)
                    CollectionAssert.AreEqual(new[] { "scale_0" }, host.Frames.Single(f => f.Id == "scale_group_0").Members);
                samples.Sort();
                TestContext.WriteLine($"GROUP_LAYOUT nodes=300 groups={groupCount} samples={samples.Count} median_ms={samples[15]:F3} p95_ms={samples[28]:F3} max_ms={samples[^1]:F3}; excludes compositor and native input");
            }
            finally { window.Close(); }
        }
    }
}
