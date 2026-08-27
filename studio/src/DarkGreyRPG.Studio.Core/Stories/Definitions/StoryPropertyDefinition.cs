using System.Collections.ObjectModel;

namespace DarkGreyRPG.Studio.Core.Stories.Definitions;

/// <summary>An immutable description of a persisted node property.</summary>
public sealed record StoryPropertyDefinition
{
    public StoryPropertyDefinition(
        string name,
        bool isRequired = false,
        string? displayName = null,
        IEnumerable<string>? aliases = null,
        string? defaultValue = null,
        bool isCore = true)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Property name is required.", nameof(name));
        Name = name.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Name : displayName.Trim();
        IsRequired = isRequired;
        IsCore = isCore;
        Aliases = new ReadOnlyCollection<string>((aliases ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct(StringComparer.Ordinal).ToArray());
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public string DisplayName { get; }
    public bool IsRequired { get; }
    public bool IsCore { get; }
    public IReadOnlyList<string> Aliases { get; }
    public string? DefaultValue { get; }
    public bool Required => IsRequired;
}
