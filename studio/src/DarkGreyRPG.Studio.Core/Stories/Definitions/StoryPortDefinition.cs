namespace DarkGreyRPG.Studio.Core.Stories.Definitions;

/// <summary>An immutable control-flow port description.</summary>
public sealed record StoryPortDefinition(string Name, bool IsCompatibility = false, bool IsDynamic = false)
{
    public string Label => Name;
    public bool Compatibility => IsCompatibility;
}
