using System.Globalization;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalNodeInspectorViewModel
{
    public bool IsTitle => _host.Scope == GraphScope.StoryFlow && Node.Type == "title";
    public string TitleMain { get => LineString("main") ?? ""; set => SetTitle("main", value, false); }
    public string TitleSubtitle { get => LineString("subtitle") ?? ""; set => SetTitle("subtitle", value, false); }
    public string TitleFadeIn { get => PresentationNumber("fade_in"); set => SetTitle("fade_in", value, true); }
    public string TitleStay { get => PresentationNumber("stay"); set => SetTitle("stay", value, true); }
    public string TitleFadeOut { get => PresentationNumber("fade_out"); set => SetTitle("fade_out", value, true); }
    public string TitleError { get; private set; } = "";
    private void SetTitle(string field, string value, bool number)
    {
        if (!IsTitle || _disposed || _isProjectingCanonicalChange) return;
        JsonElement json;
        if (number)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed) || parsed < 0 || parsed > 60)
            { TitleError = "时间必须为 0–60 秒。"; OnPropertyChanged(nameof(TitleError)); return; }
            json = JsonSerializer.SerializeToElement(parsed);
        }
        else json = JsonSerializer.SerializeToElement(value);
        TitleError = _host.SetNodeProperty(NodeId, field, json) ? "" : "主标题不能为空，标题文字不能超过 1024 字符。";
        OnPropertyChanged(nameof(TitleError));
    }
    private void NotifyTitle()
    {
        foreach (var name in new[] { nameof(IsTitle), nameof(TitleMain), nameof(TitleSubtitle), nameof(TitleFadeIn), nameof(TitleStay), nameof(TitleFadeOut) }) OnPropertyChanged(name);
    }
}
