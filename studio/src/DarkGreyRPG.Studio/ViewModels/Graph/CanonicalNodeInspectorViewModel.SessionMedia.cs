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
            _host.SetNodeProperty(NodeId, "portrait_variant", JsonSerializer.SerializeToElement(value));
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
        get => IsLine && _host.Graph.Nodes.First(n => n.Id == NodeId).Properties.TryGetValue("custom_text_speed", out var flag) && flag.ValueKind == JsonValueKind.True;
        set
        {
            if (_disposed || _isProjectingCanonicalChange || !IsLine) return;
            _host.SetNodeProperty(NodeId, "custom_text_speed", JsonSerializer.SerializeToElement(value));
            OnPropertyChanged(nameof(IsLineTextSpeedCustom));
        }
    }

    public double LineTextSpeed
    {
        get => double.TryParse(LineNumber("text_speed", 30), NumberStyles.Float, CultureInfo.InvariantCulture, out var speed) ? speed : 30;
        set
        {
            if (_disposed || _isProjectingCanonicalChange || !IsLine || !double.IsFinite(value) || value < 0 || value > 120) return;
            _host.SetNodeProperty(NodeId, "text_speed", JsonSerializer.SerializeToElement(value));
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
        return _host.SetNodeProperty(NodeId, "voice_ref", JsonSerializer.SerializeToElement(mediaRef));
    }

    private string LineNumber(string key, double fallback) => Node.Properties.TryGetValue(key, out var value) ? value.ToString() : fallback.ToString(CultureInfo.InvariantCulture);
    private void SetLineNumber(string key, string value)
    {
        if (_disposed || _isProjectingCanonicalChange || !IsLine) return;
        if (!double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
            || !double.IsFinite(number) || number < 0 || number > 1) return;
        _host.SetNodeProperty(NodeId, key, JsonSerializer.SerializeToElement(number));
    }

    private string? LineString(string key) => Node.Properties.TryGetValue(key, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

public sealed record SessionPortraitOption(string? Name, string DisplayName);
