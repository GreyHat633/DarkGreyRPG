using System.Collections.ObjectModel;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Scope and editor metadata for one canonical persisted node type.</summary>
public sealed record GraphNodeDefinition
{
    public GraphNodeDefinition(
        string type,
        GraphScope scope,
        string displayName,
        string category,
        IEnumerable<GraphInterfaceKind>? allowedInterfaceKinds = null,
        bool required = false,
        bool unique = false,
        bool nonDeletable = false,
        bool compatibilityOnly = false,
        IEnumerable<GraphPortDefinition>? fixedPorts = null,
        IEnumerable<GraphPropertyDefinition>? propertyDefinitions = null)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("A node type is required.", nameof(type));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("A node display name is required.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("A node category is required.", nameof(category));

        Type = type;
        Scope = scope;
        DisplayName = displayName.Trim();
        Category = category.Trim();
        AllowedInterfaceKinds = new ReadOnlyCollection<GraphInterfaceKind>(
            (allowedInterfaceKinds ?? [GraphInterfaceKind.Flow, GraphInterfaceKind.Logic]).Distinct().ToArray());
        var ports = (fixedPorts ?? [])
            .Select(port => port ?? throw new ArgumentException("Fixed port definitions cannot contain null values.", nameof(fixedPorts)))
            .ToArray();
        var duplicatePort = ports
            .GroupBy(port => port.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePort is not null)
            throw new ArgumentException($"Fixed port ID '{duplicatePort.Key}' is duplicated.", nameof(fixedPorts));
        var disallowedPort = ports.FirstOrDefault(port => !AllowedInterfaceKinds.Contains(port.InterfaceKind));
        if (disallowedPort is not null)
            throw new ArgumentException(
                $"Fixed port '{disallowedPort.Id}' uses interface kind '{disallowedPort.InterfaceKind}', which is not allowed by node type '{type}'.",
                nameof(fixedPorts));
        FixedPorts = new ReadOnlyCollection<GraphPortDefinition>(ports);

        var properties = (propertyDefinitions ?? [])
            .Select(property => property ?? throw new ArgumentException("Property definitions cannot contain null values.", nameof(propertyDefinitions)))
            .ToArray();
        var duplicateProperty = properties
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateProperty is not null)
            throw new ArgumentException($"Property name '{duplicateProperty.Key}' is duplicated.", nameof(propertyDefinitions));
        PropertyDefinitions = new ReadOnlyCollection<GraphPropertyDefinition>(properties);
        Required = required;
        Unique = unique;
        NonDeletable = nonDeletable;
        CompatibilityOnly = compatibilityOnly;
    }

    public string Type { get; }
    public string NodeType => Type;
    public string DisplayName { get; }
    public string Category { get; }
    public GraphScope Scope { get; }
    public IReadOnlyList<GraphInterfaceKind> AllowedInterfaceKinds { get; }
    public IReadOnlyList<GraphInterfaceKind> AllowedInterfaces => AllowedInterfaceKinds;
    public bool Required { get; }
    public bool Unique { get; }
    public bool NonDeletable { get; }
    public bool CompatibilityOnly { get; }

    // Descriptive aliases make the metadata unambiguous to editor callers.
    public bool IsRequired => Required;
    public bool IsUnique => Unique;
    public bool IsNonDeletable => NonDeletable;
    public bool IsCompatibilityOnly => CompatibilityOnly;

    /// <summary>Ports materialized on every newly-created node.</summary>
    public IReadOnlyList<GraphPortDefinition> FixedPorts { get; }
    public IReadOnlyList<GraphPortDefinition> FixedPortDefinitions => FixedPorts;
    public IReadOnlyList<GraphPropertyDefinition> PropertyDefinitions { get; }
    public IReadOnlyList<GraphPropertyDefinition> Properties => PropertyDefinitions;
}
