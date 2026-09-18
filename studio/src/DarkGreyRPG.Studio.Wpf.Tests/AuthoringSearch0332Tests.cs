using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class AuthoringSearch0332Tests
{
    [TestMethod]
    public async Task OlderAsyncSearchCannotOverwriteNewerResults()
    {
        using var workspace = new CanonicalStoryWorkspaceViewModel(new GraphResourceEnvelope(GraphResourceKind.Story, "story", "故事", new([])));
        var older = new TaskCompletionSource<IReadOnlyList<AuthoringSearchHit>>();
        var newer = new AuthoringSearchHit("story", GraphResourceKind.Story, "story", null, null, null, "新结果", "新结果");
        workspace.ProjectSearch = query => query == "旧" ? older.Task : Task.FromResult<IReadOnlyList<AuthoringSearchHit>>([newer]);
        var first = workspace.SearchAsync("旧");
        await workspace.SearchAsync("新");
        older.SetResult([]);
        await first;
        Assert.AreEqual(newer, workspace.SearchResults.Single());
        var background = await Task.Run(() =>
        {
            using var detached = new CanonicalStoryWorkspaceViewModel(new GraphResourceEnvelope(GraphResourceKind.Story, "other", "后台", new([])));
            return detached.SearchLoaded("后台");
        });
        Assert.AreEqual(1, background.Count);
    }

    private sealed class TemporaryProjectDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DgrSearch0332-" + Guid.NewGuid().ToString("N"));
        public TemporaryProjectDirectory() => System.IO.Directory.CreateDirectory(Path);
        public void Dispose() => System.IO.Directory.Delete(Path, true);
    }
    [TestMethod]
    public void UnsavedPageSearchRetainsIdentityAfterReorderWithoutMutatingGraph()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        using var workspace = new CanonicalStoryWorkspaceViewModel(new(GraphResourceKind.Story, "story", "故事", new([])),
            sessions: [new(GraphResourceKind.Session, "session", "会话", new([line]))]);
        var editor = workspace.SessionEditors.Single();
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        inspector.LinePages.Single().Text = "没有命中";
        inspector.AddLinePage();
        var page = inspector.LinePages.Last();
        page.Text = "这里有未保存的关键词 ABC";
        var hit = workspace.SearchLoaded("关键词").Single();
        Assert.AreEqual(page.PageId, hit.PageId);
        Assert.IsTrue(hit.Path.Contains("第 2 句"));
        Assert.IsTrue(page.Move(0));
        var before = editor.Host.Graph.ToJson();
        workspace.NavigateSearch(hit);
        Assert.AreSame(editor, workspace.ActiveEditor);
        Assert.AreEqual(page.PageId, workspace.SearchTarget!.PageId);
        Assert.AreEqual(before, editor.Host.Graph.ToJson());
        Assert.AreEqual(page.PageId, workspace.SearchLoaded("abc").Single().PageId);
        Assert.IsTrue(workspace.SearchLoaded("关键词").Single().Path.Contains("第 1 句"));
    }

    [TestMethod]
    public void FrameSidecarRoundTripDoesNotRewriteRuntimeResource()
    {
        using var directory = new TemporaryProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Path);
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Session, "session", "会话", new([]));
        store.Sessions.Create(envelope);
        var path = store.Sessions.GetPath("session");
        var before = System.IO.File.ReadAllBytes(path);
        using var editor = new CanonicalGraphResourceEditorViewModel(envelope);
        editor.Host.AddFrame([], 10, 20, 200, 100);
        new CanonicalGraphResourceSaveCoordinator(store).Replace(editor);
        CollectionAssert.AreEqual(before, System.IO.File.ReadAllBytes(path));
        var layout = new CanonicalGraphLayoutStore(directory.Path);
        Assert.AreEqual("分组注释", layout.LoadFrames("session:session").Single().Title);
        Assert.IsFalse(editor.IsDirty);
    }
}
