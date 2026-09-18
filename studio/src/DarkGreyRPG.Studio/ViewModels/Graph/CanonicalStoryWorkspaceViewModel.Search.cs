using System.Collections.ObjectModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed record AuthoringSearchHit(string StoryId, GraphResourceKind Kind, string ResourceId, string? NodeId,
    string? PageId, string? OptionId, string Path, string Snippet);

public sealed partial class CanonicalStoryWorkspaceViewModel
{
    public Func<string, Task<IReadOnlyList<AuthoringSearchHit>>>? ProjectSearch { get; set; }
    private readonly Dictionary<CanonicalGraphResourceEditorViewModel, IReadOnlyList<AuthoringSearchHit>> _searchDocuments = [];
    private long _searchSequence;
    private string _searchStatus = "";
    public string SearchStatus { get => _searchStatus; private set => SetProperty(ref _searchStatus, value); }
    public Action<AuthoringSearchHit>? ProjectSearchNavigate { get; set; }
    public ObservableCollection<AuthoringSearchHit> SearchResults { get; } = [];
    private AuthoringSearchHit? _searchTarget;
    public AuthoringSearchHit? SearchTarget { get => _searchTarget; private set => SetProperty(ref _searchTarget, value); }

    public async Task SearchAsync(string query)
    {
        var sequence = ++_searchSequence;
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(query)) { SearchStatus = ""; return; }
        SearchStatus = "搜索中…";
        try
        {
            var hits = ProjectSearch is { } search ? await search(query.Trim()) : SearchLoaded(query.Trim());
            if (sequence != _searchSequence) return;
            foreach (var hit in hits) SearchResults.Add(hit);
            SearchStatus = $"{hits.Count} 条结果";
        }
        catch (Exception error)
        {
            if (sequence == _searchSequence) SearchStatus = "搜索未完成：" + error.Message;
        }
    }

    public IReadOnlyList<AuthoringSearchHit> SearchLoaded(string query)
    {
        var results = new List<AuthoringSearchHit>();
        foreach (var editor in Editors)
        {
            if (_searchDocuments.TryGetValue(editor, out var cached)) { results.AddRange(cached); continue; }
            int first = results.Count;
            var referenceItem = SessionItems.Concat(TaskItems).FirstOrDefault(candidate => ReferenceEquals(candidate.Editor, editor));
            string origin = referenceItem?.IsReadOnly == true ? " [只读引用]" : referenceItem?.IsReferenced == true ? " [共享引用]" : "";
            void Add(string text, string suffix, string? node = null, string? page = null, string? option = null)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                results.Add(new(StoryEditor.Id, editor.ResourceKind, editor.Id, node, page, option,
                    $"{StoryEditor.DisplayName} > {editor.DisplayName}{origin} > {suffix}", text));
            }
            Add(editor.DisplayName, "资源名称");
            Add(editor.TaskDescription, "任务说明");
            foreach (var frame in editor.Host.Frames) Add(frame.Title, "分组注释");
            foreach (var node in editor.Document.Graph.Nodes)
            {
                var label = node.DisplayName ?? node.Type;
                Add(label, label, node.Id);
                foreach (var property in node.Properties)
                {
                    if (property.Key == "pages" && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        int index = 0;
                        foreach (var page in property.Value.EnumerateArray())
                        {
                            index++;
                            if (page.TryGetProperty("text", out var text) && page.TryGetProperty("page_id", out var id))
                                Add(text.GetString() ?? "", $"{label} > 第 {index} 句", node.Id, id.GetString());
                        }
                    }
                    else if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        var value = property.Value.GetString() ?? "";
                        Add(value, $"{label} > {property.Key}", node.Id);
                        if (property.Key.Contains("actor", StringComparison.Ordinal))
                        {
                            var actor = ActorItems.FirstOrDefault(candidate => candidate.Id == value);
                            if (actor is not null) Add(actor.DisplayName, $"{label} > 角色", node.Id);
                        }
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Array)
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object) continue;
                            var id = item.TryGetProperty("option_id", out var option) ? option.GetString() : null;
                            foreach (var field in item.EnumerateObject())
                                if (field.Value.ValueKind == JsonValueKind.String) Add(field.Value.GetString() ?? "", $"{label} > {property.Key}", node.Id, option: id);
                        }
                }
            }
            _searchDocuments[editor] = results.Skip(first).ToArray();
        }
        return FilterSearchDocuments(results, query);
    }

    public static IReadOnlyList<AuthoringSearchHit> FilterSearchDocuments(IEnumerable<AuthoringSearchHit> documents, string query)
        => documents.Where(hit => hit.Snippet.Contains(query, StringComparison.OrdinalIgnoreCase)).Select(hit =>
        {
            if (query.Length == 0) return hit; // Detached, full-text index for unopened resources.
            int start = Math.Max(0, hit.Snippet.IndexOf(query, StringComparison.OrdinalIgnoreCase) - 24);
            return hit with { Snippet = hit.Snippet.Substring(start, Math.Min(140, hit.Snippet.Length - start)) };
        }).ToArray();

    public void NavigateSearch(AuthoringSearchHit hit)
    {
        if (hit.StoryId != StoryEditor.Id) { ProjectSearchNavigate?.Invoke(hit); return; }
        var editor = Editors.FirstOrDefault(candidate => candidate.ResourceKind == hit.Kind && candidate.Id == hit.ResourceId);
        if (editor is null) return;
        if (ReferenceEquals(editor, StoryEditor)) ReturnToStory();
        else
        {
            var item = SessionItems.Concat(TaskItems).FirstOrDefault(candidate => ReferenceEquals(candidate.Editor, editor));
            if (item is null || !OpenGraphResource(item) || item.IsReadOnly) return;
        }
        SearchTarget = null;
        SearchTarget = hit;
    }
}
