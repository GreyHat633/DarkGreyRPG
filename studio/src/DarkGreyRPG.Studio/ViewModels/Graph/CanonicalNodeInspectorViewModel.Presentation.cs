using System.Globalization;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalNodeInspectorViewModel
{
    public bool IsMusic => IsSessionNode && Node.Type == "music";
    public bool IsScreen => IsSessionNode && Node.Type == "screen";
    public string MusicStatus => LineString("media_ref") is null ? "停止当前音乐" : "播放项目内音乐";
    public bool MusicLoop { get => Node.Properties.TryGetValue("loop", out var value) && value.ValueKind == JsonValueKind.True;
        set { if (IsMusic && !_isProjectingCanonicalChange) _host.SetNodeProperty(NodeId, "loop", JsonSerializer.SerializeToElement(value)); } }
    public string MusicFadeIn { get => PresentationNumber("fade_in"); set => SetPresentationNumber("fade_in", value); }
    public string MusicFadeOut { get => PresentationNumber("fade_out"); set => SetPresentationNumber("fade_out", value); }
    public string PresentationError { get; private set; } = "";
    private string PresentationNumber(string key) => Node.Properties.TryGetValue(key, out var value) ? value.ToString() : "0";
    private void SetPresentationNumber(string key, string value)
    {
        if (!IsMusic || _isProjectingCanonicalChange) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number < 0 || number > 60)
            PresentationError = "淡入淡出时间必须为 0–60 秒。";
        else { PresentationError = ""; _host.SetNodeProperty(NodeId, key, JsonSerializer.SerializeToElement(number)); }
        OnPropertyChanged(nameof(PresentationError));
    }
    public bool SetMusic(string? mediaRef) => !_disposed && IsMusic && _host.SetSessionMusic(NodeId, mediaRef);
    public JsonElement ScreenLayers => Node.Properties.TryGetValue("layers", out var value) ? value.Clone() : JsonSerializer.SerializeToElement(Array.Empty<object>());
    public bool SetScreenLayers(JsonElement value) => !_disposed && IsScreen && _host.SetNodeProperty(NodeId, "layers", value);
    private void NotifyPresentation()
    {
        foreach (var name in new[] { nameof(IsMusic), nameof(IsScreen), nameof(MusicStatus), nameof(MusicLoop), nameof(MusicFadeIn), nameof(MusicFadeOut), nameof(ScreenLayers) }) OnPropertyChanged(name);
    }
}
