using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;
[TestClass]
public sealed class Usage0335Tests
{
    [TestMethod]
    public async Task UsageIsTypedSeesUnopenedChildAndIgnoresStaleQueryAfterSelectionChange()
    {
        string media = "media/" + new string('a', 64) + ".ogg";
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        var page = CanonicalSessionLineSchema.CreatePage(); page["text"] = JsonSerializer.SerializeToElement(DynamicContentText.Encode([new(Type: "actor_name", ActorId: "ST-2345-6789-ABCD-EFGH~actor~same"), new(Type: "item_name", ItemId: "ST-2345-6789-ABCD-EFGH~item~same")]));
        page["voice_ref"] = JsonSerializer.SerializeToElement(media); line.Properties["pages"] = JsonSerializer.SerializeToElement(new[] { page });
        using var workspace = new CanonicalStoryWorkspaceViewModel(new(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story", new([])), sessions: [new(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Session", new([line]))]);
        string before = workspace.SessionEditors.Single().Host.Graph.ToJson();
        var hits = workspace.ScanLoadedUsages();
        Assert.HasCount(1, hits.Where(h => h.Key == new UsageKey("actor", "ST-2345-6789-ABCD-EFGH~actor~same"))); Assert.HasCount(1, hits.Where(h => h.Key == new UsageKey("item", "ST-2345-6789-ABCD-EFGH~item~same")));
        var voice = hits.Single(h => h.Key == new UsageKey("media", media)); Assert.AreEqual(page["page_id"].GetString(), voice.Location.PageId);
        workspace.NavigateUsage(voice); Assert.AreEqual("ST-2345-6789-ABCD-EFGH~session~session", workspace.ActiveEditor.Id); Assert.AreEqual(before, workspace.ActiveEditor.Host.Graph.ToJson()); Assert.IsFalse(workspace.ActiveEditor.IsDirty);
        workspace.SelectTreeItem(workspace.SessionItems.Single()); Assert.IsFalse(workspace.IsUsageExpanded);
        var delayed = new TaskCompletionSource<IReadOnlyList<ResourceUsage>>(); workspace.ProjectUsage = _ => delayed.Task;
        workspace.IsUsageExpanded = true; workspace.ReturnToStory(); delayed.SetResult(hits); await Task.Yield();
        Assert.IsEmpty(workspace.UsageResults);
    }
}
