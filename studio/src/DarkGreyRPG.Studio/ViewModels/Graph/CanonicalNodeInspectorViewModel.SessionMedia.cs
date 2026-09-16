using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Globalization;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalNodeInspectorViewModel
{
    private double? _lineVolumePreview;

    public CanonicalStoryActorItem? SelectedPortraitActor => _actorItems.FirstOrDefault(item => item.Id == _speakerActorId);
    public string? SelectedPortraitMediaRef => SelectedPortraitVariant is { } name
        ? SelectedPortraitActor?.PortraitSource.PortraitVariants?.FirstOrDefault(value => value.Name == name)?.MediaRef
        : SelectedPortraitActor?.PortraitSource.DefaultPortraitRef;

    private void OnActorPortraitsChanged(object? sender, EventArgs args)
    {
        if (_disposed || sender is not CanonicalStoryActorItem actor || actor.Id != _speakerActorId) return;
        // Rebuilding dropdown items is projection, not permission to clear a saved selection.
        _isProjectingCanonicalChange = true;
        try
        {
            OnPropertyChanged(nameof(PortraitVariantOptions));
            OnPropertyChanged(nameof(SelectedPortraitVariant));
            OnPropertyChanged(nameof(SelectedPortraitMediaRef));
            RefreshLinePages();
        }
        finally { _isProjectingCanonicalChange = false; }
    }
    public bool HasLineSpeaker => IsLine && !string.IsNullOrWhiteSpace(_speakerActorId);
    public IReadOnlyList<SessionPortraitOption> PortraitVariantOptions => new[] { new SessionPortraitOption(null, "默认头像") }
        .Concat(_actorItems.FirstOrDefault(item => item.Id == _speakerActorId)?.PortraitSource.PortraitVariants?
            .Select(value => new SessionPortraitOption(value.Name, value.Name)) ?? []).ToArray();

    public string? SelectedPortraitVariant
    {
        get => LineString("portrait_variant");
        set
        {
            if (_disposed || _isProjectingCanonicalChange || !HasLineSpeaker || !PortraitVariantOptions.Any(option => option.Name == value)) return;
            SetFirstPageProperty("portrait_variant", value);
        }
    }

    private static readonly ConditionalWeakTable<GraphEditorHostViewModel, Dictionary<string, LineAudioState>> LineAudioStates = new();
    private LineAudioState AudioState
    {
        get
        {
            var states = LineAudioStates.GetOrCreateValue(_host);
            if (!states.TryGetValue(NodeId, out var state)) states[NodeId] = state = new();
            return state;
        }
    }
    private sealed class LineAudioState
    {
        public bool Open;
        public event EventHandler? Changed;
        public void Notify() => Changed?.Invoke(this, EventArgs.Empty);
    }
    private void OnLineAudioStateChanged(object? sender, EventArgs args) => OnPropertyChanged(nameof(IsLineAudioEnabled));

    public bool IsLineAudioEnabled
    {
        get => IsLine && (LineVoiceRef is not null || AudioState.Open);
        set
        {
            if (_disposed || _isProjectingCanonicalChange || !IsLine || value == IsLineAudioEnabled) return;
            AudioState.Open = value;
            if (!value && LineVoiceRef is not null) SetLineVoice(null);
            AudioState.Notify();
        }
    }

    public bool IsLineTextSpeedCustom
    {
        get => IsLine && LinePages.FirstOrDefault()?.CustomSpeed == true;
        set
        {
            if (_disposed || _isProjectingCanonicalChange || !IsLine) return;
            SetFirstPageProperty("custom_text_speed", value);
            OnPropertyChanged(nameof(IsLineTextSpeedCustom));
        }
    }

    public double LineTextSpeed
    {
        get => double.TryParse(LineNumber("text_speed", 30), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) ? speed : 30;
        set
        {
            if (_disposed || _isProjectingCanonicalChange || !IsLine || !double.IsFinite(value) || value < 0 || value > 120) return;
            SetFirstPageProperty("text_speed", value);
            OnPropertyChanged(nameof(LineTextSpeed));
        }
    }

    public string? LineVoiceRef => LineString("voice_ref");
    public string LineVoiceStatus => LineVoiceRef is null ? "未配置语音" : "已配置项目内语音";
    public string LineVoiceVolume
    {
        get => LineNumber("voice_volume", 1);
        set => SetLineNumber("voice_volume", value);
    }
    public double LineVoiceVolumeValue
    {
        get => ParseNormalizedNumber(LineVoiceVolume, 1);
        set => SetLineNumber("voice_volume", value.ToString(CultureInfo.InvariantCulture));
    }
    public double LineVoiceVolumeDraft
    {
        get => _lineVolumePreview ?? LineVoiceVolumeValue;
        set
        {
            if (!IsLine || _isProjectingCanonicalChange) return;
            _lineVolumePreview = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1;
            OnPropertyChanged(nameof(LineVoiceVolumeDraft));
            OnPropertyChanged(nameof(LineVoiceVolumeDisplayValue));
            OnPropertyChanged(nameof(LineVoiceVolumeLabel));
        }
    }
    public double LineVoiceVolumeDisplayValue => _lineVolumePreview ?? LineVoiceVolumeValue;
    public string LineVoiceVolumeLabel => $"{Math.Round(LineVoiceVolumeDisplayValue * 100):0}%";

    public bool SetLineVoice(string? mediaRef)
    {
        if (_disposed || !IsLine || (mediaRef is not null && !MediaReference.IsAudio(mediaRef))) return false;
        return SetFirstPageProperty("voice_ref", mediaRef);
    }

    private IReadOnlyDictionary<string, JsonElement> LineProperties => IsLine ? ReadLinePages().FirstOrDefault() ?? new Dictionary<string, JsonElement>() : Node.Properties;
    private string LineNumber(string key, double fallback) => LineProperties.TryGetValue(key, out var value) ? value.ToString() : fallback.ToString(CultureInfo.InvariantCulture);
    private void SetLineNumber(string key, string value)
    {
        if (_disposed || _isProjectingCanonicalChange || !IsLine) return;
        if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
            || !double.IsFinite(number) || number < 0 || number > 1) return;
        SetFirstPageProperty(key, number);
    }

    private string? LineString(string key) => LineProperties.TryGetValue(key, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private bool SetFirstPageProperty(string key, object? value) => LinePages.FirstOrDefault() is { } page && SetPageProperty(page.PageId, key, value);
}

public sealed record SessionPortraitOption(string? Name, string DisplayName);
