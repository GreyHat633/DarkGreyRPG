using System.Text.Json;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
namespace DarkGreyRPG.Studio.ViewModels;

public sealed class OfflineReadOnlyResourceViewModel : ObservableObject, IDisposable
{
    private readonly OfflineResourceChoice _root;
    private readonly Stack<OfflineResourceChoice> _history = new();
    public OfflineReadOnlyResourceViewModel(OfflineResourceChoice choice)
    {
        _root = choice ?? throw new ArgumentNullException(nameof(choice));
        Choice = choice;
        BackCommand = new RelayCommand(GoBack, () => _history.Count > 0);
        Load(choice);
    }
    public OfflineResourceChoice Choice { get; private set; }
    public CanonicalGraphResourceEditorViewModel? GraphPreview { get; private set; }
    public bool HasGraph => GraphPreview is not null;
    public int InitialTabIndex => HasGraph ? 0 : 1;
    public string Title => HasGraph ? $"[引用] {Choice.DisplayName} · 流程图（只读）" : $"[引用] {Choice.DisplayName}";
    public string ReadOnlySummary => "引用资源为只读，内容来自外部故事包。";
    public string DefinitionText => Choice.DefinitionJson;
    public RelayCommand BackCommand { get; }
    public bool TryOpenSubgraph(GraphEditorNodeViewModel node)
    {
        var kind = node.Type switch { "session" => "Session", "task" => "Task", _ => null };
        if (kind is null || !node.Properties.TryGetValue("resource_id", out var value)
            || value.ValueKind != JsonValueKind.String) return false;
        var candidates = _root.RelatedGraphs.Where(resource => resource.Kind == kind && resource.Id == value.GetString()).ToArray();
        if (candidates.Length != 1) return false;
        _history.Push(Choice);
        Load(candidates[0]);
        return true;
    }
    private void GoBack()
    {
        if (_history.TryPop(out var choice)) Load(choice);
    }
    private void Load(OfflineResourceChoice choice)
    {
        GraphPreview?.Dispose();
        Choice = choice;
        GraphPreview = choice.HasGraph ? new(GraphResourceEnvelopeSerializer.Deserialize(choice.DefinitionJson)) : null;
        OnPropertyChanged(nameof(Choice));
        OnPropertyChanged(nameof(GraphPreview));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasGraph));
        BackCommand.RaiseCanExecuteChanged();
    }
    public void Dispose() => GraphPreview?.Dispose();
}
