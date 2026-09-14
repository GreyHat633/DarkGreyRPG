using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Actors;

public static class ActorPortraitSchema
{
    public static IReadOnlyList<ValidationIssue> Validate(ActorResource actor)
    {
        var issues = new List<ValidationIssue>();
        if (actor.DefaultPortraitRef is not null && !MediaReference.IsImage(actor.DefaultPortraitRef))
            issues.Add(new("actor.portrait.default", "默认头像必须引用项目内图片。", "default_portrait_ref"));
        if (actor.PortraitVariants is null)
        {
            issues.Add(new("actor.portrait.variants", "头像变体必须为数组。", "portrait_variants"));
            return issues;
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variant in actor.PortraitVariants)
        {
            if (variant is null || string.IsNullOrWhiteSpace(variant.Name) || !names.Add(variant.Name))
                issues.Add(new("actor.portrait.name", "头像变体名称不能为空或重复。", "portrait_variants"));
            if (variant is not null && !MediaReference.IsImage(variant.MediaRef))
                issues.Add(new("actor.portrait.media", "头像变体必须引用项目内图片。", "portrait_variants"));
        }
        return issues;
    }

    public static string? Resolve(ActorResource actor, string? variant)
    {
        if (variant is null) return actor.DefaultPortraitRef;
        return actor.PortraitVariants.SingleOrDefault(value => value.Name == variant)?.MediaRef
            ?? throw new InvalidOperationException($"Actor 缺少头像变体：{variant}");
    }
}
