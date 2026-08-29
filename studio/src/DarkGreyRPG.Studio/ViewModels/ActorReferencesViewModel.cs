using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;

namespace DarkGreyRPG.Studio.ViewModels;

public sealed class ActorReferencesViewModel
{
    public ActorReferencesViewModel(ActorResourceInfo actor, IReadOnlyList<ResourceDescriptor> references)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        References = references ?? throw new ArgumentNullException(nameof(references));
    }

    public ActorResourceInfo Actor { get; }
    public IReadOnlyList<ResourceDescriptor> References { get; }
    public string Title => $"“{Actor.DisplayName}”的引用";
    public string Summary => References.Count == 0
        ? "当前没有其它故事引用这个角色。"
        : $"以下 {References.Count} 个故事仍引用这个角色：";
}
