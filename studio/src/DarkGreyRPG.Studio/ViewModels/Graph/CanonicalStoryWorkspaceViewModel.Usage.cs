using System.Collections.ObjectModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed record UsageKey(string Kind, string Id);
public sealed record ResourceUsage(UsageKey Key, AuthoringSearchHit Location, string Use, bool IsReadOnly = false)
{
    public string Caption => Location.Path + " ＞ " + (Use == "故事资源声明" ? "已添加到故事资源列表" : Use) + (IsReadOnly ? "（只读）" : "");
}
public sealed record MediaUsageContext(string Name, string Reference);

public sealed partial class CanonicalStoryWorkspaceViewModel
{
    public Func<UsageKey, Task<IReadOnlyList<ResourceUsage>>>? ProjectUsage { get; set; }
    public ObservableCollection<ResourceUsage> UsageResults { get; } = [];
    private bool _usageExpanded;
    private long _usageSequence;
    private string _usageStatus = "展开查询当前可读取项目";
    public string UsageHelp => "点击一项，跳转到使用该资源的位置。";
    private UsageKey? _usageKey;
    private MediaUsageContext? _selectedUsageMedia;
    public bool IsUsageExpanded { get => _usageExpanded; set { if (SetProperty(ref _usageExpanded, value) && value) _ = RefreshUsageAsync(); } }
    public string UsageStatus { get => _usageStatus; private set { SetProperty(ref _usageStatus, value); OnPropertyChanged(nameof(UsageTitle)); } }
    public string UsageTitle => IsUsageExpanded ? $"使用位置（{UsageStatus}）" : "使用位置";
    public bool HasUsageContext => _usageKey is not null || UsageMedia.Count > 0;
    public ObservableCollection<MediaUsageContext> UsageMedia { get; } = [];
    public MediaUsageContext? SelectedUsageMedia { get => _selectedUsageMedia; set { if (SetProperty(ref _selectedUsageMedia, value) && value is not null) { _usageKey = new("media", value.Reference); if (IsUsageExpanded) _ = RefreshUsageAsync(); } } }

    private string ActorUsageKind(string id) => ActorItems.FirstOrDefault(item => item.Id == id)?.Actor.Type == CollectiveActorResource.ResourceType ? "actor_group" : "actor";
    private string ItemUsageKind(string id) => ItemItems.FirstOrDefault(item => item.Id == id)?.Item is CollectiveItemResource ? "item_group" : "item";
    private void SelectUsageContext()
    {
        ++_usageSequence; _usageExpanded = false; UsageResults.Clear(); UsageMedia.Clear(); _selectedUsageMedia = null;
        _usageKey = InspectorSelection switch
        {
            CanonicalStoryActorItem actor => new(ActorUsageKind(actor.Id), actor.Id),
            CanonicalStoryItemItem item => new(ItemUsageKind(item.Id), item.Id),
            _ => null
        };
        UsageStatus = "展开查询当前可读取项目";
        OnPropertyChanged(nameof(IsUsageExpanded)); OnPropertyChanged(nameof(HasUsageContext)); OnPropertyChanged(nameof(SelectedUsageMedia)); OnPropertyChanged(nameof(UsageTitle));
    }
    public async Task RefreshUsageAsync()
    {
        if (_usageKey is not { } key || _disposed || !IsUsageExpanded) return;
        long sequence = ++_usageSequence;
        UsageStatus = "正在查找…";
        try
        {
            var hits = ProjectUsage is { } project ? await project(key) : ScanLoadedUsages().Where(u => u.Key == key).ToArray();
            if (_disposed || sequence != _usageSequence || !IsUsageExpanded) return;
            UsageResults.Clear();
            foreach (var hit in hits.Where(u => u.Location.FieldPath?.StartsWith("membership:", StringComparison.Ordinal) != true).DistinctBy(u => (u.Key, u.Location.StoryId, u.Location.Kind, u.Location.ResourceId, u.Location.NodeId, u.Location.PageId, u.Location.OptionId, u.Location.FieldPath))) UsageResults.Add(hit);
            UsageStatus = UsageResults.Count.ToString();
        }
        catch (Exception) { if (sequence == _usageSequence && !_disposed) UsageStatus = "查询未完成，请重试"; }
    }
    public IReadOnlyList<ResourceUsage> ScanLoadedUsages()
    {
        var hits = new List<ResourceUsage>();
        foreach (var editor in Editors)
        {
            bool readOnly = SessionItems.Concat(TaskItems).Any(item => ReferenceEquals(item.Editor, editor) && item.IsReadOnly);
            if (editor.TaskDescription.StartsWith(DynamicContentText.Prefix, StringComparison.Ordinal))
                foreach (var part in DynamicContentText.Parse(editor.TaskDescription))
                {
                    UsageKey? key = part.ActorId is { } actor ? new(ActorUsageKind(actor), actor) : part.ItemId is { } item ? new(ItemUsageKind(item), item) : null;
                    if (key is not null) hits.Add(new(key, new(StoryEditor.Id, editor.ResourceKind, editor.Id, null, null, null, $"{StoryEditor.DisplayName} ＞ {editor.DisplayName} ＞ 任务说明", "", FieldPath: "task_metadata.description"), "动态内容引用", readOnly));
                }
            foreach (var node in editor.Document.Graph.Nodes)
            {
                void Add(string kind, string? id, string field, string use, string? page, string? option)
                {
                    if (string.IsNullOrEmpty(id)) return;
                    var title = node.DisplayName ?? GraphNodeDefinitionRegistry.Get(editor.Host.Scope, node.Type)?.DisplayName ?? "节点";
                    var peers = editor.Document.Graph.Nodes.Where(n => n.Type == node.Type).ToList();
                    if (peers.Count > 1) title += $" {peers.IndexOf(node) + 1}";
                    var location = $"{StoryEditor.DisplayName} ＞ {editor.DisplayName} ＞ {title}";
                    if (page is not null) location += $" ＞ 第 {CanonicalSessionLineSchema.ReadPages(node).ToList().FindIndex(p => p["page_id"].GetString() == page) + 1} 句";
                    if (option is not null && node.Properties.TryGetValue("options", out var options)) location += $" ＞ 选项 {options.EnumerateArray().ToList().FindIndex(o => o.GetProperty("option_id").GetString() == option) + 1}";
                    hits.Add(new(new(kind, id), new(StoryEditor.Id, editor.ResourceKind, editor.Id, node.Id, page, option,
                        location, "", FieldPath: field), use, readOnly));
                }
                void Walk(JsonElement value, string path, string? page = null, string? option = null)
                {
                    if (value.ValueKind == JsonValueKind.Object)
                    {
                        if (value.TryGetProperty("page_id", out var p)) page = p.GetString();
                        if (value.TryGetProperty("option_id", out var o)) option = o.GetString();
                        foreach (var field in value.EnumerateObject()) Walk(field.Value, path + "." + field.Name, page, option);
                    }
                    else if (value.ValueKind == JsonValueKind.Array)
                    { int index = 0; foreach (var item in value.EnumerateArray()) Walk(item, $"{path}[{index++}]", page, option); }
                    else if (value.ValueKind == JsonValueKind.String)
                    {
                        var text = value.GetString() ?? ""; string field = path.Split('.').Last();
                        if (text.StartsWith(DynamicContentText.Prefix, StringComparison.Ordinal))
                        {
                            foreach (var part in DynamicContentText.Parse(text))
                                if (part.ActorId is { } actor) Add(ActorUsageKind(actor), actor, path, "角色名称引用", page, option);
                                else if (part.ItemId is { } item) Add(ItemUsageKind(item), item, path, part.Type == "item_name" ? "物品名称引用" : "物品数量引用", page, option);
                        }
                        if (field is "media_ref" or "voice_ref" || node.Type == "music" && field == "track_ref") Add("media", text, path, field == "voice_ref" ? "台词语音" : "画面／音乐", page, option);
                        if (field == "speaker_actor_id") Add(ActorUsageKind(text), text, path, "说话角色", page, option);
                        if (field == "actor_id" || node.Type == "objective" && field == "entity") Add(ActorUsageKind(text), text, path, "目标角色", page, option);
                        if (field is "item" or "item_id") Add(ItemUsageKind(text), text, path, node.Type == "reward" ? "奖励物品" : "目标物品", page, option);
                        if (field == "resource_id" && node.Type is "session" or "task") Add(node.Type, text, path, "图资源引用", page, option);
                    }
                }
                foreach (var property in node.Properties) Walk(property.Value, property.Key);
                if (node.Type == "line" && node.Properties.TryGetValue("speaker_actor_id", out var speaker))
                {
                    var actor = ActorItems.FirstOrDefault(a => a.Id == speaker.GetString());
                    if (actor is not null)
                        foreach (var page in CanonicalSessionLineSchema.ReadPages(node))
                        {
                            string? variant = page.TryGetValue("portrait_variant", out var v) ? v.GetString() : null;
                            var portrait = string.IsNullOrEmpty(variant) ? actor.PortraitSource.DefaultPortraitRef : actor.PortraitSource.PortraitVariants?.FirstOrDefault(p => p.Name == variant)?.MediaRef;
                            if (!string.IsNullOrEmpty(portrait)) Add("media", portrait, "portrait_variant", "台词头像使用", page["page_id"].GetString(), null);
                        }
                }
            }
        }
        void Declaration(string kind, string id, string name, bool readOnly)
            => hits.Add(new(new(kind, id), new(StoryEditor.Id, GraphResourceKind.Story, StoryEditor.Id, null, null, null, $"{StoryEditor.DisplayName} ＞ {name}", "", FieldPath: $"membership:{kind}:{id}"), "故事资源声明", readOnly));
        foreach (var item in ActorItems) Declaration(ActorUsageKind(item.Id), item.Id, item.DisplayName, item.IsReadOnly);
        foreach (var item in ItemItems) Declaration(ItemUsageKind(item.Id), item.Id, item.DisplayName, item.IsReadOnly);
        foreach (var item in SessionItems.Concat(TaskItems)) Declaration(item.Editor.ResourceKind == GraphResourceKind.Session ? "session" : "task", item.Id, item.DisplayName, item.IsReadOnly);
        foreach (var actor in ActorItems)
        {
            var portrait = actor.PortraitSource;
            foreach (var media in new[] { portrait.DefaultPortraitRef }.Concat((portrait.PortraitVariants ?? []).Select(p => p.MediaRef)).Where(r => !string.IsNullOrEmpty(r)))
                hits.Add(new(new("media", media!), new(StoryEditor.Id, GraphResourceKind.Story, StoryEditor.Id, null, null, null, $"{StoryEditor.DisplayName} ＞ {actor.DisplayName}", "", FieldPath: "actor:" + actor.Id), "角色头像声明", actor.IsReadOnly));
        }
        return hits;
    }
    public void NavigateUsage(ResourceUsage usage)
    {
        if (usage.Location.FieldPath?.StartsWith("actor:", StringComparison.Ordinal) == true && usage.Location.StoryId == StoryEditor.Id)
        { var actor = ActorItems.FirstOrDefault(a => a.Id == usage.Location.FieldPath[6..]); if (actor is not null) SelectTreeItem(actor); return; }
        NavigateSearch(usage.Location);
    }
}
