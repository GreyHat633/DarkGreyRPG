using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalNodeInspectorViewModel
{
    public bool IsTaskReward => IsTaskNode && NodeType == CanonicalTaskRewardSchema.NodeType;
    public ObservableCollection<CanonicalTaskRewardEntryViewModel> RewardEntries { get; } = [];
    public RelayCommand AddRewardEntryCommand { get; }
    public IReadOnlyList<CanonicalResourceSelectionOption> RewardItemOptions { get; }

    private void AddRewardEntry()
    {
        if (!IsTaskReward) return;
        var entries = RewardEntries.Select(entry => entry.ToValue()).ToList();
        entries.Add(new() { ["type"] = "xp", ["amount"] = 0 });
        _host.SetNodeProperty(NodeId, "entries", JsonSerializer.SerializeToElement(entries));
    }

    internal bool SaveRewardEntries()
    {
        _suppressTargetedRefresh = true;
        try { return _host.SetNodeProperty(NodeId, "entries", JsonSerializer.SerializeToElement(RewardEntries.Select(entry => entry.ToValue()))); }
        finally { _suppressTargetedRefresh = false; }
    }

    internal void RemoveRewardEntry(CanonicalTaskRewardEntryViewModel entry)
    {
        var remaining = RewardEntries.Where(item => !ReferenceEquals(item, entry)).Select(item => item.ToValue()).ToArray();
        _host.SetNodeProperty(NodeId, "entries", JsonSerializer.SerializeToElement(remaining));
    }

    internal void RewardError(string key, string? message) => _host.SetAuthoringIssue($"{NodeId}:reward:{key}",
        message is null ? null : new ValidationIssue("graph.reward.authoring", message, "entries", ValidationSeverity.Error, NodeId));

    private void RefreshRewardEntries(GraphEditorNodeViewModel current)
    {
        var values = IsTaskReward && current.Properties.TryGetValue("entries", out var entries)
            && entries.ValueKind == JsonValueKind.Array ? entries.EnumerateArray().ToArray() : [];
        // The persisted list is positional. Preserve surviving prefix/suffix rows
        // for insert/remove, and update field edits without removing their controls.
        var prefix = 0;
        while (prefix < Math.Min(RewardEntries.Count, values.Length) && RewardEntries[prefix].Matches(values[prefix])) prefix++;
        var suffix = 0;
        while (suffix < Math.Min(RewardEntries.Count, values.Length) - prefix
            && RewardEntries[RewardEntries.Count - suffix - 1].Matches(values[values.Length - suffix - 1])) suffix++;
        var oldMiddle = RewardEntries.Count - prefix - suffix;
        var newMiddle = values.Length - prefix - suffix;
        var shared = Math.Min(oldMiddle, newMiddle);
        for (var index = 0; index < shared; index++) RewardEntries[prefix + index].UpdateProjection(values[prefix + index]);
        for (var index = oldMiddle - 1; index >= shared; index--)
        {
            RewardError(RewardEntries[prefix + index].DraftKey, null);
            RewardEntries.RemoveAt(prefix + index);
        }
        for (var index = shared; index < newMiddle; index++)
        {
            var entry = values[prefix + index];
            RewardEntries.Insert(prefix + index, new(this, entry.GetProperty("type").GetString()!,
                entry.TryGetProperty("item", out var item) ? item.GetString() : null, entry.GetProperty("amount").GetInt32()));
        }
        for (var index = 0; index < RewardEntries.Count; index++) RewardEntries[index].Number = index + 1;
        OnPropertyChanged(nameof(IsTaskReward));
        AddRewardEntryCommand.RaiseCanExecuteChanged();
    }
}

public sealed class CanonicalTaskRewardEntryViewModel : ObservableObject
{
    private int _number;
    public int Number
    {
        get => _number;
        internal set { if (SetProperty(ref _number, value)) OnPropertyChanged(nameof(EntryTitle)); }
    }
    public string EntryTitle => $"奖励 {Number}";
    private readonly CanonicalNodeInspectorViewModel _owner;
    private string _type;
    private string? _item;
    private int _amount;
    private string _amountText;
    private string _error = "";
    private bool _projecting;
    internal string DraftKey { get; } = Guid.NewGuid().ToString("N");

    internal CanonicalTaskRewardEntryViewModel(CanonicalNodeInspectorViewModel owner, string type, string? item, int amount)
    {
        _owner = owner; _type = type; _item = item; _amount = amount;
        _amountText = amount.ToString(CultureInfo.InvariantCulture);
        RemoveCommand = new RelayCommand(() => _owner.RemoveRewardEntry(this));
    }

    public IReadOnlyList<CanonicalStoryActionTypeOption> TypeOptions { get; } = [new("item", "物品"), new("xp", "经验")];
    public CanonicalStoryActionTypeOption SelectedType
    {
        get => TypeOptions.Single(option => option.Value == _type);
        set
        {
            if (_projecting || value is null || value.Value == _type) return;
            var item = value.Value == "item" ? ItemOptions.FirstOrDefault()?.Id : null;
            if (value.Value == "item" && item is null) { SetError("项目中没有可选物品，请先创建或引用物品。"); return; }
            var oldType = _type; var oldItem = _item;
            _type = value.Value; _item = item;
            if (!_owner.SaveRewardEntries()) { _type = oldType; _item = oldItem; }
            else SetError(null);
            OnPropertyChanged(nameof(SelectedType)); OnPropertyChanged(nameof(IsItem)); OnPropertyChanged(nameof(SelectedItem));
        }
    }
    public bool IsItem => _type == "item";
    public IReadOnlyList<CanonicalResourceSelectionOption> ItemOptions => _owner.RewardItemOptions;
    public CanonicalResourceSelectionOption? SelectedItem
    {
        get => ItemOptions.FirstOrDefault(item => item.Id == _item)
            ?? (_item is null ? null : new(_item, "缺失物品：" + _item, false));
        set
        {
            if (_projecting || !IsItem || value is null || value.Id == _item) return;
            var old = _item; _item = value.Id;
            if (!_owner.SaveRewardEntries()) _item = old;
            OnPropertyChanged(nameof(SelectedItem));
        }
    }
    public string AmountText
    {
        get => _amountText;
        set
        {
            if (_projecting || _amountText == value) return;
            _amountText = value ?? ""; OnPropertyChanged();
            if (!int.TryParse(_amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            { SetError("数量必须为整数。"); return; }
            var old = _amount; _amount = amount;
            if (_owner.SaveRewardEntries()) SetError(null); else _amount = old;
        }
    }
    public string Error => _error;
    public RelayCommand RemoveCommand { get; }
    internal bool Matches(JsonElement entry)
        => _type == entry.GetProperty("type").GetString() && _amount == entry.GetProperty("amount").GetInt32()
            && _item == (entry.TryGetProperty("item", out var item) ? item.GetString() : null);

    internal void UpdateProjection(JsonElement entry)
    {
        if (Matches(entry)) return;
        _projecting = true;
        try
        {
            _type = entry.GetProperty("type").GetString()!;
            _item = entry.TryGetProperty("item", out var item) ? item.GetString() : null;
            _amount = entry.GetProperty("amount").GetInt32();
            _amountText = _amount.ToString(CultureInfo.InvariantCulture);
            SetError(null);
            foreach (var name in new[] { nameof(SelectedType), nameof(IsItem), nameof(SelectedItem), nameof(AmountText) })
                OnPropertyChanged(name);
        }
        finally { _projecting = false; }
    }
    private void SetError(string? error)
    {
        _error = error ?? ""; _owner.RewardError(DraftKey, error); OnPropertyChanged(nameof(Error));
    }
    internal Dictionary<string, object> ToValue()
    {
        var entry = new Dictionary<string, object> { ["type"] = _type, ["amount"] = _amount };
        if (IsItem) entry["item"] = _item ?? "";
        return entry;
    }
}
