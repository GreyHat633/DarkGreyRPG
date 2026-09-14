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
    public IReadOnlyList<CanonicalResourceSelectionOption> RewardItemOptions => _itemItems
        .Where(item => item.Item is DarkGreyRPG.Studio.Core.Items.IndividualItemResource)
        .Select(item => new CanonicalResourceSelectionOption(item.Id, item.DisplayName, true, item)).ToArray();

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
        foreach (var entry in RewardEntries) RewardError(entry.DraftKey, null);
        RewardEntries.Clear();
        if (IsTaskReward && current.Properties.TryGetValue("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
            foreach (var entry in entries.EnumerateArray())
                RewardEntries.Add(new(this, entry.GetProperty("type").GetString()!,
                    entry.TryGetProperty("item", out var item) ? item.GetString() : null, entry.GetProperty("amount").GetInt32()));
        OnPropertyChanged(nameof(IsTaskReward));
        AddRewardEntryCommand.RaiseCanExecuteChanged();
    }
}

public sealed class CanonicalTaskRewardEntryViewModel : ObservableObject
{
    private readonly CanonicalNodeInspectorViewModel _owner;
    private string _type;
    private string? _item;
    private int _amount;
    private string _amountText;
    private string _error = "";
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
            if (value is null || value.Value == _type) return;
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
            if (!IsItem || value is null || value.Id == _item) return;
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
            if (_amountText == value) return;
            _amountText = value ?? ""; OnPropertyChanged();
            if (!int.TryParse(_amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
            { SetError("数量必须为整数。"); return; }
            var old = _amount; _amount = amount;
            if (_owner.SaveRewardEntries()) SetError(null); else _amount = old;
        }
    }
    public string Error => _error;
    public RelayCommand RemoveCommand { get; }
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
