using System.Runtime.CompilerServices;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

/// <summary>Editor-only state shared by the inline and Inspector projections.</summary>
internal sealed class ChoiceOptionEditorState : ObservableObject
{
    private static readonly ConditionalWeakTable<GraphEditorHostViewModel, Dictionary<(string, string), ChoiceOptionEditorState>> Hosts = new();
    private bool _expanded;
    public bool IsExpanded { get => _expanded; set => SetProperty(ref _expanded, value); }
    public static ChoiceOptionEditorState For(GraphEditorHostViewModel host, string node, string option, bool initiallyExpanded = false)
    {
        var states = Hosts.GetOrCreateValue(host);
        if (!states.TryGetValue((node, option), out var state)) states[(node, option)] = state = new() { IsExpanded = initiallyExpanded };
        return state;
    }
}
