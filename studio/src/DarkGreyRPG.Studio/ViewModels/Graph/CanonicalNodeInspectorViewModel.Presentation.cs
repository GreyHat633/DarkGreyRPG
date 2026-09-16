using System.Globalization;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalNodeInspectorViewModel
{
    private double? _musicVolumePreview;

    public bool IsMusic => IsSessionNode && Node.Type == "music";
    public bool IsScreen => IsSessionNode && Node.Type == "screen";
    public string? AudioMediaRef => IsMusic ? LineString("media_ref") : LineVoiceRef;
    public string MusicVolume { get => PresentationNumber("volume", "1"); set => SetMusicVolume(value); }
    public double MusicVolumeValue
    {
        get => ParseNormalizedNumber(MusicVolume, 1);
        set => SetMusicVolume(value.ToString(CultureInfo.InvariantCulture));
    }
    public double MusicVolumeDraft
    {
        get => _musicVolumePreview ?? MusicVolumeValue;
        set
        {
            if (!IsMusic || _isProjectingCanonicalChange) return;
            _musicVolumePreview = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1;
            OnPropertyChanged(nameof(MusicVolumeDraft));
            OnPropertyChanged(nameof(MusicVolumeDisplayValue));
            OnPropertyChanged(nameof(MusicVolumeLabel));
        }
    }
    public double MusicVolumeDisplayValue => _musicVolumePreview ?? MusicVolumeValue;
    public string MusicVolumeLabel => $"{Math.Round(MusicVolumeDisplayValue * 100):0}%";
    public bool MusicLoop { get => Node.Properties.TryGetValue("loop", out var value) && value.ValueKind == JsonValueKind.True;
        set { if (IsMusic && !_isProjectingCanonicalChange) _host.SetNodeProperty(NodeId, "loop", JsonSerializer.SerializeToElement(value)); } }
    public string MusicFadeIn { get => PresentationNumber("fade_in"); set => SetPresentationNumber("fade_in", value); }
    public string MusicFadeOut { get => PresentationNumber("fade_out"); set => SetPresentationNumber("fade_out", value); }
    public string PresentationError { get; private set; } = "";
    private string PresentationNumber(string key, string fallback = "0") => Node.Properties.TryGetValue(key, out var value) ? value.ToString() : fallback;
    private void SetPresentationNumber(string key, string value)
    {
        if (_disposed || !IsMusic || _isProjectingCanonicalChange) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number) || number < 0 || number > 60)
            PresentationError = "淡入淡出时间必须为 0–60 秒。";
        else { PresentationError = ""; _host.SetNodeProperty(NodeId, key, JsonSerializer.SerializeToElement(number)); }
        OnPropertyChanged(nameof(PresentationError));
    }
    private void SetMusicVolume(string value)
    {
        if (_disposed || !IsMusic || _isProjectingCanonicalChange) return;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || !double.IsFinite(number) || number < 0 || number > 1) return;
        _host.SetNodeProperty(NodeId, "volume", JsonSerializer.SerializeToElement(number));
    }
    public bool SetMusic(string? mediaRef) => !_disposed && IsMusic && _host.SetSessionMusic(NodeId, mediaRef);
    public JsonElement ScreenLayers => Node.Properties.TryGetValue("layers", out var value) ? value.Clone() : JsonSerializer.SerializeToElement(Array.Empty<object>());
    public bool SetScreenLayers(JsonElement value) => !_disposed && IsScreen && _host.SetNodeProperty(NodeId, "layers", value);
    private void NotifyPresentation()
    {
        foreach (var name in new[] { nameof(IsMusic), nameof(IsScreen), nameof(MusicLoop), nameof(MusicVolume), nameof(MusicVolumeValue), nameof(MusicVolumeDraft), nameof(MusicVolumeDisplayValue), nameof(MusicVolumeLabel), nameof(MusicFadeIn), nameof(MusicFadeOut), nameof(ScreenLayers) }) OnPropertyChanged(name);
    }

    public void CommitVolumePreview(bool commit = true)
    {
        if (_disposed) return;
        if (IsMusic && _musicVolumePreview is { } music)
        {
            if (commit) SetMusicVolume(music.ToString(CultureInfo.InvariantCulture));
            _musicVolumePreview = null;
            NotifyPresentation();
        }
        else if (IsLine && _lineVolumePreview is { } line)
        {
            if (commit) SetLineNumber("voice_volume", line.ToString(CultureInfo.InvariantCulture));
            _lineVolumePreview = null;
            OnPropertyChanged(nameof(LineVoiceVolume));
            OnPropertyChanged(nameof(LineVoiceVolumeValue));
            OnPropertyChanged(nameof(LineVoiceVolumeDraft));
            OnPropertyChanged(nameof(LineVoiceVolumeDisplayValue));
            OnPropertyChanged(nameof(LineVoiceVolumeLabel));
        }
    }

    private static double ParseNormalizedNumber(string value, double fallback)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && double.IsFinite(number) && number is >= 0 and <= 1 ? number : fallback;
}
