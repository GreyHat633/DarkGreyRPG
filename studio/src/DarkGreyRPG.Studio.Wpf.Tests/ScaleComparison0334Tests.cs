using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass, DoNotParallelize]
public sealed class ScaleComparison0334Tests
{
    public TestContext TestContext { get; set; } = null!;

    [STATestMethod]
    public void FixedThreeHundredNodeWorkload()
    {
        // Opt-in benchmark. Timings include managed operations / synchronous WPF layout,
        // not native input, GPU presentation or cold filesystem cache.
        string? output = Environment.GetEnvironmentVariable("DGR_SCALE_OUTPUT");
        if (string.IsNullOrEmpty(output)) { Assert.Inconclusive("Opt-in scale comparison"); return; }
        string root = Path.Combine(Path.GetDirectoryName(output)!, "fixture-" + Guid.NewGuid().ToString("N"));
        new ProjectService().CreateProject(root, "scale", "Scale");
        var store = new CanonicalProjectGraphStore(root);
        store.Stories.Create(new(GraphResourceKind.Story, "story", "Story",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "entry")])));
        for (int s = 0; s < 3; s++)
        {
            var nodes = Enumerable.Range(0, 100).Select(i =>
            {
                var node = GraphNodeFactory.Create(GraphScope.Session, "line", $"line_{s}_{i}");
                node.Properties["pages"] = JsonSerializer.SerializeToElement(Enumerable.Range(0, 5)
                    .Select(p => new { page_id = $"page_{s}_{i}_{p}", text = $"Needle {s} {i} {p} " + new string('a', 120) }));
                return node;
            }).ToArray();
            store.Sessions.Create(new(GraphResourceKind.Session, "session_" + s, "Session " + s,
                new GraphDocument(nodes.Concat([GraphNodeFactory.Create(GraphScope.Session, "start", "start"), new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.Session, "end", "end").Candidate!]))));
        }
        store.Memberships.Create(new("story", new CanonicalStoryMembershipSet { Sessions = ["session_0", "session_1", "session_2"] }));
        var results = new Dictionary<string, object>();
        void Measure(string operation, Action action)
        {
            var values = new List<double>();
            for (int i = 0; i < 23; i++)
            {
                var timer = Stopwatch.StartNew(); action(); timer.Stop();
                if (i >= 3) values.Add(timer.Elapsed.TotalMilliseconds);
            }
            values.Sort();
            results[operation] = new { n = values.Count, median_ms = values[10], p95_ms = values[18], max_ms = values[^1], working_set = Process.GetCurrentProcess().WorkingSet64, samples = values };
            File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        Measure("open_workspace", () => { using var opened = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load("story")); Assert.HasCount(3, opened.SessionItems); });
        using var workspace = new CanonicalStoryWorkspaceViewModel(new CanonicalStoryWorkspaceLoader(store).Load("story"));
        var window = new Window { Width = 1000, Height = 800, Left = -10000, Top = -10000, ShowInTaskbar = false };
        try
        {
            window.Show();
            Measure("switch_graph_model", () => {
                foreach (var item in workspace.SessionItems) {
                    Assert.IsTrue(workspace.OpenGraphResource(item));
                }
            });
            Measure("search_loaded", () => Assert.IsTrue(workspace.SearchLoaded("Needle").Count >= 1500));
            Measure("save_resources", () => { foreach (var item in workspace.SessionItems) store.Sessions.Replace(item.Editor.CreatePersistenceSnapshot()); });
            Measure("export_validated", () => new DgrsStoryPackageExporter(root).Build("story", Path.Combine(root, "scale.dgrs"), "scale-comparison"));
            foreach (var item in workspace.SessionItems) Assert.HasCount(102, store.Sessions.Load(item.Id).Graph!.Nodes);
            File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { window.Close(); }
    }
}
