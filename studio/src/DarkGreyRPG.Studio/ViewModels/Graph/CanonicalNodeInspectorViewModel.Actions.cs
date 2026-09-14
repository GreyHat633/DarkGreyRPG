using System.Globalization;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalNodeInspectorViewModel
{
    private bool _advancedActions;
    private string _extraActionType = "";
    private readonly Dictionary<string, string> _actionExtraDrafts = new(StringComparer.Ordinal);
    public bool IsGiveHealthAction => IsStoryAction && _actionType == CanonicalStoryActionSchema.GiveHealth;
    public bool IsTeleportAction => IsStoryAction && _actionType == CanonicalStoryActionSchema.Teleport;
    public bool IsGiveBuffAction => IsStoryAction && _actionType == CanonicalStoryActionSchema.GiveBuff;
    public bool IsCommandAction => IsStoryAction && _actionType == CanonicalStoryActionSchema.ExecuteCommand;
    public bool AdvancedActions
    {
        get => _advancedActions;
        set
        {
            if (_advancedActions == value) return;
            _advancedActions = value;
            if (!value && IsCommandAction) _host.ChangeStoryActionType(NodeId, CanonicalStoryActionSchema.SendMessage);
            OnPropertyChanged(); OnPropertyChanged(nameof(StoryActionTypeOptions)); OnPropertyChanged(nameof(SelectedStoryActionType));
        }
    }
    public IReadOnlyList<CanonicalStoryActionTypeOption> StoryActionTypeOptions => CanonicalStoryActionSchema.ActionTypes
        .Where(type => AdvancedActions || type != CanonicalStoryActionSchema.ExecuteCommand)
        .Select(type => new CanonicalStoryActionTypeOption(type, CanonicalStoryActionSchema.AuthoringDisplayNameFor(type))).ToArray();
    public string HealthDelta { get => Extra("amount"); set => SetExtra("amount", value, 1); }
    public string TeleportDimension { get => Extra("dimension_id"); set => SetExtra("dimension_id", value, 2); }
    public string TeleportX { get => Extra("x"); set => SetExtra("x", value, 1); }
    public string TeleportY { get => Extra("y"); set => SetExtra("y", value, 1); }
    public string TeleportZ { get => Extra("z"); set => SetExtra("z", value, 1); }
    public string BuffDurationDelta { get => Extra("duration_delta"); set => SetExtra("duration_delta", value, 2); }
    public string BuffLevelDelta { get => Extra("level_delta"); set => SetExtra("level_delta", value, 2); }
    public string BuffModId { get => Extra("mod_id"); set => SetExtra("mod_id", value, 0); }
    public string BuffInternalName { get => Extra("buff_name"); set => SetExtra("buff_name", value, 0); }
    public string AdvancedCommand { get => Extra("command"); set => SetExtra("command", value, 0); }
    public bool ModBuff
    {
        get => IsGiveBuffAction && Extra("mod_extension").Equals("True", StringComparison.OrdinalIgnoreCase);
        set { if (!_isProjectingCanonicalChange && IsGiveBuffAction) _host.ChangeStoryBuffMode(NodeId, value); }
    }
    public bool VanillaBuff => IsGiveBuffAction && !ModBuff;
    public IReadOnlyList<CanonicalStoryActionSchema.VanillaBuffOption> VanillaBuffOptions => CanonicalStoryActionSchema.VanillaBuffs;
    public CanonicalStoryActionSchema.VanillaBuffOption? SelectedVanillaBuff
    {
        get => VanillaBuffOptions.FirstOrDefault(buff => buff.Value == Extra("buff"));
        set { if (value is not null && VanillaBuff && !_isProjectingCanonicalChange) SetExtra("buff", value.Value, 0); }
    }
    public string ActionExtraError { get; private set; } = "";

    private string Extra(string key)
    {
        if (_actionExtraDrafts.TryGetValue(key, out var draft)) return draft;
        var current = _host.Nodes.FirstOrDefault(node => node.NodeId == NodeId);
        return current?.Properties.TryGetValue(key, out var value) == true ? value.ToString() : "";
    }
    private void SetExtra(string key, string? text, int numeric)
    {
        if (!IsStoryAction || _isProjectingCanonicalChange) return;
        text ??= "";
        JsonElement value;
        if (numeric == 2 && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) value = JsonSerializer.SerializeToElement(integer);
        else if (numeric == 1 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)) value = JsonSerializer.SerializeToElement(number);
        else if (numeric == 0) value = JsonSerializer.SerializeToElement(text);
        else { ExtraError(key, text, "请输入合法数值，整数项不接受小数。"); return; }
        _actionExtraDrafts.Remove(key); _host.SetAuthoringIssue($"{NodeId}:action-extra:{key}", null);
        if (!_host.SetNodeProperty(NodeId, key, value))
            ExtraError(key, text, "字段内容不符合执行类型；命令必须为非空单行文本。");
        else { ActionExtraError = ""; OnPropertyChanged(nameof(ActionExtraError)); }
    }
    private void ExtraError(string key, string draft, string message)
    {
        _actionExtraDrafts[key] = draft; ActionExtraError = message;
        _host.SetAuthoringIssue($"{NodeId}:action-extra:{key}", new ValidationIssue("graph.story.action.authoring", message, key, ValidationSeverity.Error, NodeId));
        NotifyActionExtras();
    }
    private void RefreshActionExtras()
    {
        if (_extraActionType != _actionType)
        {
            foreach (var key in _actionExtraDrafts.Keys) _host.SetAuthoringIssue($"{NodeId}:action-extra:{key}", null);
            _actionExtraDrafts.Clear(); ActionExtraError = ""; _extraActionType = _actionType;
        }
        if (IsCommandAction) _advancedActions = true;
        if (IsGiveBuffAction)
        {
            foreach (var key in _actionExtraDrafts.Keys.ToArray())
                if ((ModBuff && key == "buff") || (!ModBuff && key is "mod_id" or "buff_name"))
                {
                    _actionExtraDrafts.Remove(key); _host.SetAuthoringIssue($"{NodeId}:action-extra:{key}", null);
                }
            if (_actionExtraDrafts.Count == 0) ActionExtraError = "";
        }
        NotifyActionExtras();
    }
    private void NotifyActionExtras()
    {
        foreach (var name in new[] { nameof(AdvancedActions), nameof(StoryActionTypeOptions), nameof(SelectedStoryActionType), nameof(IsGiveHealthAction),
            nameof(IsTeleportAction), nameof(IsGiveBuffAction), nameof(IsCommandAction), nameof(HealthDelta), nameof(TeleportDimension), nameof(TeleportX),
            nameof(TeleportY), nameof(TeleportZ), nameof(BuffDurationDelta), nameof(BuffLevelDelta), nameof(BuffModId), nameof(BuffInternalName),
            nameof(AdvancedCommand), nameof(ModBuff), nameof(VanillaBuff), nameof(SelectedVanillaBuff), nameof(ActionExtraError) }) OnPropertyChanged(name);
    }
}
