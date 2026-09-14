using System.Text.Json;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>
/// Immutable metadata for one untyped top-level JSON property on a node.
/// Defaults are detached from their source document and cloned on access.
/// </summary>
public sealed class GraphPropertyDefinition
{
    public GraphPropertyDefinition(
        string name,
        JsonValueKind kind,
        bool required = false,
        JsonElement? defaultValue = null,
        bool allowNull = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A property name is required.", nameof(name));
        if (kind == JsonValueKind.Undefined)
            throw new ArgumentOutOfRangeException(nameof(kind), "A property must declare an explicit JSON top-level kind.");
        if (defaultValue is { } value && value.ValueKind != kind && !(allowNull && value.ValueKind == JsonValueKind.Null))
            throw new ArgumentException(
                $"Default JSON value for '{name}' has kind '{value.ValueKind}', expected '{kind}'.",
                nameof(defaultValue));

        Name = name;
        Kind = kind;
        Required = required;
        AllowNull = allowNull;
        _defaultValue = defaultValue?.Clone();
    }

    // Convenience overload for callers that naturally specify the default
    // before the required flag.
    public GraphPropertyDefinition(string name, JsonValueKind kind, JsonElement? defaultValue, bool required)
        : this(name, kind, required, defaultValue) { }

    private readonly JsonElement? _defaultValue;

    public string Name { get; }
    public string PropertyName => Name;
    public JsonValueKind Kind { get; }
    public JsonValueKind ValueKind => Kind;
    public JsonValueKind JsonKind => Kind;
    public bool AllowNull { get; }
    public bool Required { get; }
    public bool IsRequired => Required;
    public bool HasDefault => _defaultValue.HasValue;

    /// <summary>Returns a detached clone so callers cannot retain definition-owned JSON state.</summary>
    public JsonElement? DefaultValue => _defaultValue?.Clone();
    public JsonElement? DefaultJsonValue => DefaultValue;
    public JsonElement? Default => DefaultValue;

    public bool TryGetDefault(out JsonElement value)
    {
        if (_defaultValue is { } defaultValue)
        {
            value = defaultValue.Clone();
            return true;
        }

        value = default;
        return false;
    }
}
