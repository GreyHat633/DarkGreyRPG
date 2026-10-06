using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>One reference entry point; origin routing never changes the referenced definition.</summary>
public sealed class CanonicalProjectResourceReferenceService(CanonicalProjectGraphStore store)
{
    public void AddReference(string targetStoryId, DgrResourceKind kind, string id, string sourceStoryId, bool external)
    {
        if (external)
        {
            if (OfflineNativeContentCatalog.Load(store.ProjectDirectory).Any(resource => resource.Kind == kind && resource.Id == id))
                throw new InvalidOperationException("引用故事包与本地资源存在身份冲突，请先处理来源冲突。");
            var providers = OfflineProviderCatalog.Load(store.ProjectDirectory);
            var provider = providers.Resolve(kind, id);
            if (provider is null || provider.OwnerStoryUid != sourceStoryId)
                throw new InvalidOperationException("引用来源已失效，请重新选择资源。");
            new CanonicalExternalReferenceService(store).AddReference(targetStoryId, kind, id);
            return;
        }
        var owners = store.Memberships.List().Select(info => store.Memberships.Load(info.StoryId))
            .Where(member => Owned(member.OwnedResources, kind).Contains(id, StringComparer.Ordinal)).ToArray();
        if (owners.Length != 1 || owners[0].StoryId != sourceStoryId)
            throw new InvalidOperationException("资源所属故事已变化或存在冲突，请重新选择资源。");
        if (OfflineProviderCatalog.Load(store.ProjectDirectory).Resources.Any(resource => resource.Kind == kind && resource.Id == id))
            throw new InvalidOperationException("本地资源与引用故事包存在身份冲突，请先处理来源冲突。");
        switch (kind)
        {
            case DgrResourceKind.Actor: new CanonicalStoryActorLifecycleService(store).AddReference(targetStoryId, id); break;
            case DgrResourceKind.Item: new CanonicalStoryItemLifecycleService(store).AddReference(targetStoryId, CanonicalStoryItemKind.Individual, id); break;
            case DgrResourceKind.ItemGroup: new CanonicalStoryItemLifecycleService(store).AddReference(targetStoryId, CanonicalStoryItemKind.Collective, id); break;
            case DgrResourceKind.Session: new CanonicalStoryResourceLifecycleService(store).AddReference(targetStoryId, GraphResourceKind.Session, id); break;
            case DgrResourceKind.Task: new CanonicalStoryResourceLifecycleService(store).AddReference(targetStoryId, GraphResourceKind.Task, id); break;
            default: throw new InvalidOperationException("该资源类型不能引用。");
        }
    }

    private static IEnumerable<string> Owned(CanonicalStoryMembershipSet set, DgrResourceKind kind) => kind switch
    {
        DgrResourceKind.Actor => set.Actors, DgrResourceKind.Item => set.Items,
        DgrResourceKind.ItemGroup => set.ItemGroups, DgrResourceKind.Session => set.Sessions,
        DgrResourceKind.Task => set.Tasks, _ => [],
    };
}
