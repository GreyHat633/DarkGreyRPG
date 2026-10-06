using System.Collections.ObjectModel;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

public sealed class PublicOutputsViewModel : ObservableObject, IDisposable
{
    private readonly GraphEditorHostViewModel _host;
    internal GraphEditorHostViewModel SourceHost => _host;
    public bool CanRename { get; }
    public ObservableCollection<PublicOutputRow> Flow { get; } = [];
    public ObservableCollection<PublicOutputRow> Logic { get; } = [];
    public bool HasFlow => Flow.Count != 0;
    public bool HasLogic => Logic.Count != 0;
    public bool HasOutputs => HasFlow || HasLogic;
    public bool IsTask => _host.Scope == GraphScope.Task;
    private bool _flowExpanded = true;
    private bool _logicExpanded = true;
    public bool FlowExpanded { get => _flowExpanded; set => SetProperty(ref _flowExpanded, value); }
    public bool LogicExpanded { get => _logicExpanded; set => SetProperty(ref _logicExpanded, value); }
    public PublicOutputsViewModel(GraphEditorHostViewModel host, bool canRename, GraphEditorHostViewModel? historyHost = null)
    {
        _host = host; CanRename = canRename;
        _host.GraphChanged += Changed;
        Refresh();
    }
    private void Changed(object? sender, EventArgs e) => Refresh();
    private void Refresh()
    {
        RefreshKind(Flow, GraphInterfaceKind.Flow);
        RefreshKind(Logic, GraphInterfaceKind.Logic);
        OnPropertyChanged(nameof(HasFlow)); OnPropertyChanged(nameof(HasLogic)); OnPropertyChanged(nameof(HasOutputs));
    }
    private void RefreshKind(ObservableCollection<PublicOutputRow> rows, GraphInterfaceKind kind)
    {
        var nodes = _host.Graph.Nodes.Where(n => PublicOutputSchema.IsOutput(n) && PublicOutputSchema.Kind(n) == kind)
            .OrderBy(PublicOutputSchema.Order).ToArray();
        var alive = nodes.Select(node => node.Properties["port_id"].GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (var removed in rows.Where(row => !alive.Contains(row.PortId)).ToArray()) rows.Remove(removed);
        for (var i = 0; i < nodes.Length; i++)
        {
            var id = nodes[i].Properties["port_id"].GetString();
            var row = rows.FirstOrDefault(row => row.PortId == id);
            if (row is null) { row = new(this, nodes[i], i, nodes.Length); rows.Insert(i, row); }
            else { if (rows.IndexOf(row) != i) rows.Move(rows.IndexOf(row), i); row.Refresh(nodes[i], i, nodes.Length); }
        }
    }
    internal bool Move(string portId, int index)
    {
        if (!_host.MovePublicOutput(portId, index)) return false;
        return true;
    }
    internal bool Rename(string nodeId, string value) => CanRename && !string.IsNullOrWhiteSpace(value)
        && _host.SetNodeProperty(nodeId, "display_name", JsonSerializer.SerializeToElement(value.Trim()));
    public void Dispose() => _host.GraphChanged -= Changed;
}

public sealed class PublicOutputRow : ObservableObject
{
    private readonly PublicOutputsViewModel _owner;
    private readonly string _nodeId;
    private string _name;
    public string PortId { get; }
    public bool CanRename => _owner.CanRename;
    public bool IsTaskFlow => _owner.IsTask && _owner.Flow.Contains(this);
    public int Index { get; private set; }
    private int _count;
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public string DisplayName
    {
        get => _name;
        set
        {
            if (value == _name) return;
            if (_owner.Rename(_nodeId, value)) SetProperty(ref _name, value.Trim());
            else OnPropertyChanged(nameof(DisplayName));
        }
    }
    internal PublicOutputRow(PublicOutputsViewModel owner, GraphNode node, int index, int count)
    {
        _owner = owner; _nodeId = node.Id; Index = index; _count = count;
        PortId = node.Properties["port_id"].GetString()!;
        _name = node.Properties["display_name"].GetString()!;
        MoveUpCommand = new(() => MoveTo(Index - 1), () => Index > 0);
        MoveDownCommand = new(() => MoveTo(Index + 1), () => Index < _count - 1);
    }
    internal void Refresh(GraphNode node, int index, int count)
    {
        Index = index; _count = count;
        SetProperty(ref _name, node.Properties["display_name"].GetString()!, nameof(DisplayName));
        OnPropertyChanged(nameof(Index));
        MoveUpCommand.RaiseCanExecuteChanged(); MoveDownCommand.RaiseCanExecuteChanged();
    }
    public bool MoveTo(int index) => _owner.Move(PortId, index);
}
