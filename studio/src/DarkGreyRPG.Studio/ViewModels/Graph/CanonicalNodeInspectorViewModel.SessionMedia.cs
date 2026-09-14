using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalNodeInspectorViewModel
{
    public bool HasLineSpeaker => IsLine && !string.IsNullOrWhiteSpace(_speakerActorId);
    public IReadOnlyList<SessionPortraitOption> PortraitVariantOptions => new[] { new SessionPortraitOption(null, "默认头像") }
        .Concat(_actorItems.FirstOrDefault(item => item.Id == _speakerActorId)?.Actor.PortraitVariants?
            .Select(value => new SessionPortraitOption(value.Name, value.Name)) ?? []).ToArray();

    public string? SelectedPortraitVariant
    {
        get => LineString("portrait_variant");
        set
        {
            if (_disposed || !HasLineSpeaker || !PortraitVariantOptions.Any(option => option.Name == value)) return;
            _host.SetNodeProperty(NodeId, "portrait_variant", JsonSerializer.SerializeToElement(value));
        }
    }

    public string? LineVoiceRef => LineString("voice_ref");
    public string LineVoiceStatus => LineVoiceRef is null ? "未配置语音" : "已配置项目内语音";

    public bool SetLineVoice(string? mediaRef)
    {
        if (_disposed || !IsLine || (mediaRef is not null && !MediaReference.IsAudio(mediaRef))) return false;
        return _host.SetNodeProperty(NodeId, "voice_ref", JsonSerializer.SerializeToElement(mediaRef));
    }

    private string? LineString(string key) => Node.Properties.TryGetValue(key, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

public sealed record SessionPortraitOption(string? Name, string DisplayName);
