using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.ViewModels;

public static class ResourceIdentityPresentation
{
    public static string Format(string label, string id)
        => $"[{label}] " + (DgrResourceId.IsFullId(id)
            ? $"{DgrResourceId.Namespace(id)} : {DgrResourceId.LocalId(id)}" : id);
}
