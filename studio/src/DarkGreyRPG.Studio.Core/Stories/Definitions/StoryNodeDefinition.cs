using System.Collections.ObjectModel;

namespace DarkGreyRPG.Studio.Core.Stories.Definitions;

/// <summary>Immutable, UI-independent contract for one canonical Runtime story node.</summary>
public sealed record StoryNodeDefinition
{
    public StoryNodeDefinition(
        string canonicalType,
        string persistedType,
        string displayName,
        string category,
        bool allowsInput,
        StoryNodeOutputStrategy outputStrategy,
        bool isTerminal = false,
        IEnumerable<StoryPropertyDefinition>? properties = null,
        IEnumerable<string>? compatibilityAliases = null,
        IEnumerable<string>? compatibilityOutputs = null)
    {
        if (string.IsNullOrWhiteSpace(canonicalType)) throw new ArgumentException("Canonical type is required.", nameof(canonicalType));
        if (string.IsNullOrWhiteSpace(persistedType)) throw new ArgumentException("Persisted type is required.", nameof(persistedType));
        CanonicalType = canonicalType.Trim();
        PersistedType = persistedType.Trim();
        DisplayName = displayName?.Trim() ?? string.Empty;
        Category = category?.Trim() ?? string.Empty;
        AllowsInput = allowsInput;
        OutputStrategy = outputStrategy;
        IsTerminal = isTerminal || outputStrategy == StoryNodeOutputStrategy.Terminal;
        Properties = new ReadOnlyCollection<StoryPropertyDefinition>((properties ?? []).ToArray());
        CompatibilityAliases = new ReadOnlyCollection<string>((compatibilityAliases ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        CompatibilityOutputs = new ReadOnlyCollection<string>((compatibilityOutputs ?? []).Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct(StringComparer.Ordinal).ToArray());
    }

    public string CanonicalType { get; }
    public string Type => CanonicalType;
    public string PersistedType { get; }
    public string DisplayName { get; }
    public string ChineseDisplayName => DisplayName;
    public string Category { get; }
    public bool AllowsInput { get; }
    public bool HasInput => AllowsInput;
    public bool InputAllowed => AllowsInput;
    public StoryNodeOutputStrategy OutputStrategy { get; }
    public StoryNodeOutputStrategy OutputPolicy => OutputStrategy;
    public bool IsTerminal { get; }
    public bool IsTerminalNode => IsTerminal;
    public IReadOnlyList<StoryPropertyDefinition> Properties { get; }
    public IReadOnlyList<StoryPropertyDefinition> CoreProperties => new ReadOnlyCollection<StoryPropertyDefinition>(Properties.Where(p => p.IsCore).ToArray());
    public IReadOnlyList<StoryPropertyDefinition> RequiredProperties => new ReadOnlyCollection<StoryPropertyDefinition>(Properties.Where(p => p.IsRequired).ToArray());
    public IReadOnlyList<StoryPropertyDefinition> CollapsedProperties => CoreProperties;
    public IReadOnlyList<string> CompatibilityAliases { get; }
    public IReadOnlyList<string> Aliases => CompatibilityAliases;
    public IReadOnlyList<string> CompatibilityOutputs { get; }
    public IReadOnlyDictionary<string, string?> DefaultProperties =>
        new ReadOnlyDictionary<string, string?>(Properties.Where(p => p.DefaultValue is not null).ToDictionary(p => p.Name, p => p.DefaultValue, StringComparer.Ordinal));
    public IReadOnlyDictionary<string, string?> DefaultPropertyValues => DefaultProperties;
    public IReadOnlyList<StoryPortDefinition> CompatibilityPorts =>
        new ReadOnlyCollection<StoryPortDefinition>(CompatibilityOutputs.Select(output => new StoryPortDefinition(output, IsCompatibility: true)).ToArray());

    public IReadOnlyList<StoryPortDefinition> StaticOutputs => OutputStrategy switch
    {
        StoryNodeOutputStrategy.SingleNext or StoryNodeOutputStrategy.LegacyPreserved => new ReadOnlyCollection<StoryPortDefinition>([new StoryPortDefinition("next")]),
        StoryNodeOutputStrategy.Boolean => new ReadOnlyCollection<StoryPortDefinition>([new StoryPortDefinition("true"), new StoryPortDefinition("false")]),
        _ => Array.AsReadOnly(Array.Empty<StoryPortDefinition>()),
    };

    public IReadOnlyList<StoryPortDefinition> Outputs => StaticOutputs;
}
