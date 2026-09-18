using System.Collections.ObjectModel;
using System.Text.Json;
using System.Runtime.CompilerServices;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalNodeInspectorViewModel
{
    public ObservableCollection<CanonicalLinePageViewModel> LinePages { get; } = [];
    private static readonly ConditionalWeakTable<GraphEditorHostViewModel, Dictionary<string, LinePageSelection>> PageSelections = new();
    private LinePageSelection Selection
    {
        get
        {
            var selections = PageSelections.GetOrCreateValue(_host);
            if (!selections.TryGetValue(NodeId, out var selection)) selections[NodeId] = selection = new();
            return selection;
        }
    }
    public bool CanRemoveSelectedLinePages => Selection.Ids.Count > 0;
    public string RemoveSelectedLinePagesHint => Selection.Ids.Count == 0 ? "请先选择台词；Ctrl 多选，Shift 连续选择"
        : $"删除选中的 {Selection.Ids.Count} 句台词";
    internal bool IsLinePageSelected(string id) => Selection.Ids.Contains(id);
    public void ClearLinePageSelection()
    {
        if (Selection.Ids.Count == 0 && Selection.Anchor is null) return;
        Selection.Ids.Clear();
        Selection.Anchor = null;
        Selection.Notify();
    }
    private void OnPageSelectionChanged(object? sender, EventArgs e)
    {
        foreach (var page in LinePages) page.RefreshSelection();
        OnPropertyChanged(nameof(CanRemoveSelectedLinePages));
        OnPropertyChanged(nameof(RemoveSelectedLinePagesHint));
    }
    public void SelectLinePage(string id, bool control = false, bool shift = false)
    {
        if (_disposed || !IsLine) return;
        var ids = ReadLinePages().Select(p => p["page_id"].GetString()!).ToArray();
        var index = Array.IndexOf(ids, id);
        if (index < 0) return;
        var anchor = Array.IndexOf(ids, Selection.Anchor);
        if (shift && anchor >= 0)
        {
            if (!control) Selection.Ids.Clear();
            for (var i = Math.Min(anchor, index); i <= Math.Max(anchor, index); i++) Selection.Ids.Add(ids[i]);
        }
        else
        {
            if (!control) Selection.Ids.Clear();
            if (!control || !Selection.Ids.Remove(id)) Selection.Ids.Add(id);
            Selection.Anchor = id;
        }
        Selection.Notify();
    }
    public bool RemoveSelectedLinePages()
    {
        if (_disposed || !IsLine) return false;
        var pages = ReadLinePages();
        var selected = pages.Where(p => Selection.Ids.Contains(p["page_id"].GetString()!)).ToArray();
        if (selected.Length == 0) return false;
        var index = pages.IndexOf(selected[0]);
        pages.RemoveAll(p => Selection.Ids.Contains(p["page_id"].GetString()!));
        if (!_host.SetNodeProperty(NodeId, "pages", JsonSerializer.SerializeToElement(pages))) return false;
        if (pages.Count > 0) SelectLinePage(pages[Math.Min(index, pages.Count - 1)]["page_id"].GetString()!);
        return true;
    }
    private static readonly ConditionalWeakTable<GraphEditorHostViewModel, Dictionary<string, PageAudioGate>> PageAudioGates = new();
    internal PageAudioGate PageAudio(string id)
    {
        var gates = PageAudioGates.GetOrCreateValue(_host);
        var key = NodeId + "/" + id;
        if (!gates.TryGetValue(key, out var gate)) gates[key] = gate = new();
        return gate;
    }
    private List<Dictionary<string, JsonElement>> ReadLinePages()
    {
        var node = _host.Graph.Nodes.FirstOrDefault(n => n.Id == NodeId);
        if (node is null || CanonicalSessionLineSchema.Validate(node).Count != 0) return [];
        return CanonicalSessionLineSchema.ReadPages(node).ToList();
    }

    private void RefreshLinePages()
    {
        if (!IsLine) return;
        var data = ReadLinePages();
        var ids = data.Select(p => p["page_id"].GetString()!).ToArray();
        for (var i = LinePages.Count - 1; i >= 0; i--) if (!ids.Contains(LinePages[i].PageId)) { LinePages[i].Dispose(); LinePages.RemoveAt(i); }
        for (var i = 0; i < data.Count; i++)
        {
            var page = LinePages.FirstOrDefault(p => p.PageId == ids[i]);
            if (page is null) { page = new(this, ids[i]); LinePages.Insert(i, page); }
            else if (LinePages.IndexOf(page) != i) LinePages.Move(LinePages.IndexOf(page), i);
            page.Project(data[i], i, data.Count);
        }
        Selection.Ids.IntersectWith(ids);
        if (!ids.Contains(Selection.Anchor)) Selection.Anchor = null;
        Selection.Notify();
    }

    public string? AddLinePage()
    {
        if (_disposed || !IsLine) return null;
        var pages = ReadLinePages();
        var id = Guid.NewGuid().ToString("N");
        pages.Add(CanonicalSessionLineSchema.CreatePage(id));
        return _host.SetNodeProperty(NodeId, "pages", JsonSerializer.SerializeToElement(pages)) ? id : null;
    }

    public bool RemoveLinePage(string id)
    {
        if (_disposed || !IsLine) return false;
        var pages = ReadLinePages();
        return pages.RemoveAll(p => p["page_id"].GetString() == id) == 1
            && _host.SetNodeProperty(NodeId, "pages", JsonSerializer.SerializeToElement(pages));
    }

    public bool ReorderLinePage(string id, int index)
    {
        if (_disposed || !IsLine) return false;
        var pages = ReadLinePages();
        var old = pages.FindIndex(p => p["page_id"].GetString() == id);
        if (old < 0 || index < 0 || index >= pages.Count || old == index) return false;
        var page = pages[old]; pages.RemoveAt(old); pages.Insert(index, page);
        return _host.SetNodeProperty(NodeId, "pages", JsonSerializer.SerializeToElement(pages));
    }

    internal bool SetPageProperty(string id, string key, object? value)
    {
        if (_disposed || _isProjectingCanonicalChange || !IsLine) return false;
        var pages = ReadLinePages();
        var page = pages.FirstOrDefault(p => p["page_id"].GetString() == id);
        if (page is null) return false;
        page[key] = JsonSerializer.SerializeToElement(value);
        return _host.SetNodeProperty(NodeId, "pages", JsonSerializer.SerializeToElement(pages));
    }
}

internal sealed class LinePageSelection
{
    public HashSet<string> Ids { get; } = new(StringComparer.Ordinal);
    public string? Anchor;
    public event EventHandler? Changed;
    public void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}

internal sealed class PageAudioGate
{
    public bool Open;
    public bool Expanded = true;
    public event EventHandler? Changed;
    public void Notify() => Changed?.Invoke(this, EventArgs.Empty);
}

public sealed class CanonicalLinePageViewModel : ObservableObject, IDisposable
{
    public CanonicalNodeInspectorViewModel Owner { get; }
    public string PageId { get; }
    private Dictionary<string, JsonElement> _data = [];
    private bool _projecting;
    private readonly PageAudioGate _audio;
    public int Order { get; private set; }
    public string Caption => $"第 {Order + 1} 句";
    public string DragLabel => $"拖动第 {Order + 1} 句";
    public string RemoveLabel => $"删除第 {Order + 1} 句";
    public bool CanRemove { get; private set; }
    public bool IsSelected => Owner.IsLinePageSelected(PageId);
    internal void RefreshSelection() => OnPropertyChanged(nameof(IsSelected));
    internal CanonicalLinePageViewModel(CanonicalNodeInspectorViewModel owner, string id) { Owner = owner; PageId = id; _audio = owner.PageAudio(id); _audio.Changed += AudioChanged; }
    private void AudioChanged(object? sender, EventArgs args)
    {
        OnPropertyChanged(nameof(AudioEnabled));
        OnPropertyChanged(nameof(IsExpanded));
        OnPropertyChanged(nameof(ExpansionGlyph));
        OnPropertyChanged(nameof(ExpansionLabel));
    }
    public bool IsExpanded
    {
        get => _audio.Expanded;
        set { if (_audio.Expanded == value) return; _audio.Expanded = value; _audio.Notify(); }
    }
    public string ExpansionGlyph => IsExpanded ? "▲" : "▼";
    public string ExpansionLabel => $"{(IsExpanded ? "收起" : "展开")}{Caption}";
    public void Dispose() => _audio.Changed -= AudioChanged;
    internal void Project(Dictionary<string, JsonElement> data, int order, int count)
    {
        _projecting = true;
        try { _data = data; Order = order; CanRemove = count > 0; OnPropertyChanged(string.Empty); }
        finally { _projecting = false; }
    }
    private string? String(string key) => _data.TryGetValue(key, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    private double Number(string key, double fallback) => _data.TryGetValue(key, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var n) ? n : fallback;
    private void Set(string key, object? value) { if (!_projecting) Owner.SetPageProperty(PageId, key, value); }
    public string Text { get => String("text") ?? ""; set => Set("text", value); }
    public bool HasSpeaker => Owner.HasLineSpeaker;
    public IReadOnlyList<SessionPortraitOption> PortraitOptions => Owner.PortraitVariantOptions;
    public string? PortraitVariant { get => String("portrait_variant"); set { if (PortraitOptions.Any(p => p.Name == value)) Set("portrait_variant", value); } }
    public CanonicalStoryActorItem? PortraitActor => Owner.SelectedPortraitActor;
    public string? PortraitMediaRef => PortraitVariant is { } name ? PortraitActor?.PortraitSource.PortraitVariants?.FirstOrDefault(p => p.Name == name)?.MediaRef : PortraitActor?.PortraitSource.DefaultPortraitRef;
    public bool CustomSpeed { get => _data.TryGetValue("custom_text_speed", out var p) && p.ValueKind == JsonValueKind.True; set => Set("custom_text_speed", value); }
    public double Speed { get => Number("text_speed", 30); set { if (double.IsFinite(value) && value >= 0 && value <= 120) Set("text_speed", value); } }
    public string? VoiceRef => String("voice_ref");
    public bool AudioEnabled { get => VoiceRef is not null || _audio.Open; set { _audio.Open = value; if (!value) SetVoice(null); _audio.Notify(); } }
    public double Volume { get => Number("voice_volume", 1); set { if (double.IsFinite(value) && value >= 0 && value <= 1) Set("voice_volume", value); } }
    private double? _volumeDraft;
    public double VolumeDraft { get => _volumeDraft ?? Volume; set { if (_projecting) return; _volumeDraft = Math.Clamp(value, 0, 1); OnPropertyChanged(); OnPropertyChanged(nameof(VolumeLabel)); } }
    public string VolumeLabel => $"{Math.Round(VolumeDraft * 100):0}%";
    public void CommitVolume(bool commit = true) { var value = _volumeDraft; _volumeDraft = null; if (commit && value.HasValue) Volume = value.Value; OnPropertyChanged(nameof(VolumeDraft)); OnPropertyChanged(nameof(VolumeLabel)); }
    public bool SetVoice(string? reference) => (reference is null || MediaReference.IsAudio(reference)) && Owner.SetPageProperty(PageId, "voice_ref", reference);
    public bool Remove() => Owner.RemoveLinePage(PageId);
    public bool Move(int index) => Owner.ReorderLinePage(PageId, index);
}
