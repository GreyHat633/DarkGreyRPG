using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed partial class CanonicalNodeInspectorViewModel
{
    private static readonly ConditionalWeakTable<GraphEditorHostViewModel, Dictionary<string, LogicInputSelection>> LogicSelections = new();
    private LogicInputSelection LogicSelection
    {
        get
        {
            var selections = LogicSelections.GetOrCreateValue(_host);
            if (!selections.TryGetValue(NodeId, out var selection)) selections[NodeId] = selection = new();
            return selection;
        }
    }
    private sealed class LogicInputSelection
    {
        public string? Id { get; private set; }
        public event EventHandler? Changed;
        public void Select(string? id)
        {
            if (Id == id) return;
            Id = id;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
    public bool IsLogicCombination => _host.Scope is GraphScope.StoryFlow or GraphScope.Session or GraphScope.Task
        && NodeType is "and" or "or";
    public ObservableCollection<LogicInputRowViewModel> LogicInputs { get; } = [];
    public string LogicInputsTitle => "逻辑输入";
    public RelayCommand AddLogicInputCommand { get; private set; } = null!;
    public RelayCommand RemoveSelectedLogicInputCommand { get; private set; } = null!;
    private Func<bool>? _canEditLogicInputs;
    public Func<bool>? CanEditLogicInputs
    {
        get => _canEditLogicInputs;
        set { _canEditLogicInputs = value; NotifyLogicInputState(); }
    }
    public Func<string, int, bool>? LogicInputRemovalConfirmationRequested { get; set; }
    private bool CanEditLogic => !_disposed && IsLogicCombination && (CanEditLogicInputs?.Invoke() ?? true);
    public bool CanRemoveSelectedLogicInput => CanEditLogic && LogicInputs.Count > 2
        && LogicInputs.Any(row => row.PortId == LogicSelection.Id);
    public void ClearLogicInputSelection() => LogicSelection.Select(null);
    public void SelectLogicInput(string portId)
    {
        if (!_disposed && LogicInputs.Any(row => row.PortId == portId)) LogicSelection.Select(portId);
    }
    private void OnLogicInputSelectionChanged(object? sender, EventArgs args) => NotifyLogicInputState();
    private void NotifyLogicInputState()
    {
        foreach (var row in LogicInputs) row.RefreshState();
        OnPropertyChanged(nameof(CanRemoveSelectedLogicInput));
        AddLogicInputCommand?.RaiseCanExecuteChanged();
        RemoveSelectedLogicInputCommand?.RaiseCanExecuteChanged();
    }
    public bool AddLogicInput()
    {
        if (!CanEditLogic) return false;
        var previous = LogicInputs.Select(row => row.PortId).ToHashSet(StringComparer.Ordinal);
        var names = Node.Inputs.Concat(Node.Outputs).Select(port => port.DisplayName).ToHashSet(StringComparer.Ordinal);
        int index = 1;
        while (names.Contains($"输入 {index}")) index++;
        var changed = _host.AddDynamicPort(NodeId, $"输入 {index}", GraphPortDirection.Input, GraphInterfaceKind.Logic);
        RefreshFromHost();
        if (changed && LogicInputs.FirstOrDefault(row => !previous.Contains(row.PortId)) is { } added) SelectLogicInput(added.PortId);
        return changed;
    }
    public bool RemoveSelectedLogicInput() => LogicSelection.Id is { } id && RemoveLogicInput(id);
    public bool RemoveLogicInput(string portId)
    {
        if (!CanEditLogic || LogicInputs.Count <= 2) return false;
        var row = LogicInputs.FirstOrDefault(candidate => candidate.PortId == portId);
        if (row is null) return false;
        int references = _host.GetDynamicPortReferences(NodeId, portId).Count;
        if (references > 0 && LogicInputRemovalConfirmationRequested?.Invoke(row.DisplayName, references) != true) return false;
        var changed = _host.RemoveDynamicPort(NodeId, portId, confirmReferencedRemoval: references > 0);
        RefreshFromHost();
        return changed;
    }
    public bool RenameLogicInput(string portId, string name)
    {
        if (!CanEditLogic || !LogicInputs.Any(row => row.PortId == portId)) return false;
        name = name.Trim();
        if (string.IsNullOrEmpty(name) || Node.Inputs.Concat(Node.Outputs)
            .Any(port => port.PortId != portId && port.DisplayName == name)) return false;
        if (LogicInputs.Single(row => row.PortId == portId).DisplayName == name) return true;
        var changed = _host.RenamePortDisplayName(NodeId, portId, name);
        RefreshFromHost();
        return changed;
    }
    public bool MoveLogicInput(string portId, int index)
    {
        if (!CanEditLogic || !LogicInputs.Any(row => row.PortId == portId)) return false;
        var changed = _host.MoveDynamicPort(NodeId, portId, index);
        RefreshFromHost();
        return changed;
    }
    private void RefreshLogicInputs(GraphEditorNodeViewModel node)
    {
        var desired = IsLogicCombination ? node.Inputs.Where(port => port.GraphInterfaceKind == GraphInterfaceKind.Logic)
            .OrderBy(port => port.Order).ToArray() : [];
        var ids = desired.Select(port => port.PortId).ToHashSet(StringComparer.Ordinal);
        foreach (var old in LogicInputs.Where(row => !ids.Contains(row.PortId)).ToArray()) LogicInputs.Remove(old);
        for (int index = 0; index < desired.Length; index++)
        {
            var port = desired[index];
            var row = LogicInputs.FirstOrDefault(candidate => candidate.PortId == port.PortId);
            if (row is null) { row = new(this, port.PortId, port.DisplayName); LogicInputs.Insert(index, row); }
            else if (LogicInputs.IndexOf(row) != index) LogicInputs.Move(LogicInputs.IndexOf(row), index);
            row.Project(port.DisplayName);
        }
        if (LogicSelection.Id is { } selected && !ids.Contains(selected)) ClearLogicInputSelection();
        NotifyLogicInputState();
    }
    public sealed class LogicInputRowViewModel : ObservableObject
    {
        private string _name;
        private string _nameError = string.Empty;
        public LogicInputRowViewModel(CanonicalNodeInspectorViewModel owner, string id, string name)
        {
            Owner = owner; PortId = id; _name = name;
            RemoveCommand = new(() => owner.RemoveLogicInput(id), () => owner.CanEditLogic && owner.LogicInputs.Count > 2);
        }
        public CanonicalNodeInspectorViewModel Owner { get; }
        public string PortId { get; }
        public bool IsSelected => Owner.LogicSelection.Id == PortId;
        public bool IsReadOnly => !Owner.CanEditLogic;
        public bool CanEdit => Owner.CanEditLogic;
        public string NameError => _nameError;
        public string DisplayName
        {
            get => _name;
            set
            {
                bool accepted = Owner.RenameLogicInput(PortId, value);
                SetProperty(ref _nameError, accepted ? string.Empty : "请输入非空且不重复的输入名称。", nameof(NameError));
                OnPropertyChanged(nameof(DisplayName));
            }
        }
        public RelayCommand RemoveCommand { get; }
        public void MoveTo(int index) => Owner.MoveLogicInput(PortId, index);
        internal void Project(string name) => SetProperty(ref _name, name, nameof(DisplayName));
        internal void RefreshState()
        {
            OnPropertyChanged(nameof(IsSelected)); OnPropertyChanged(nameof(IsReadOnly)); OnPropertyChanged(nameof(CanEdit));
            RemoveCommand.RaiseCanExecuteChanged();
        }
    }
}
