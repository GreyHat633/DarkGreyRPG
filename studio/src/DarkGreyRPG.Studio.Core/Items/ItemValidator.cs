using System.Text;
using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Items;

public static partial class ItemValidator
{
    public const string IdPattern = "[a-z0-9][a-z0-9_-]*";

    public static IReadOnlyList<ValidationIssue> Validate(ItemResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var issues = new List<ValidationIssue>();

        if (resource.SchemaVersion != ItemResource.CurrentSchemaVersion)
        {
            issues.Add(new("item.schema.unsupported", $"Item schema_version must be {ItemResource.CurrentSchemaVersion}.", nameof(ItemResource.SchemaVersion)));
        }

        if (resource is IndividualItemResource individual)
        {
            ValidateId(individual.ItemId, "item_id", issues);
            if (!string.Equals(individual.Type, IndividualItemResource.ResourceType, StringComparison.Ordinal))
            {
                issues.Add(new("item.type.unsupported", $"Item type must be '{IndividualItemResource.ResourceType}'.", nameof(ItemResource.Type)));
            }
        }
        else if (resource is CollectiveItemResource collective)
        {
            ValidateId(collective.GroupId, "group_id", issues);
            if (!string.Equals(collective.Type, CollectiveItemResource.ResourceType, StringComparison.Ordinal))
            {
                issues.Add(new("item.type.unsupported", $"Item type must be '{CollectiveItemResource.ResourceType}'.", nameof(ItemResource.Type)));
            }
        }
        else
        {
            issues.Add(new("item.type.unsupported", "Item resource type is unsupported.", nameof(ItemResource.Type)));
        }

        if (string.IsNullOrWhiteSpace(resource.DisplayName))
        {
            issues.Add(new("item.display_name.required", "Item display_name is required.", nameof(ItemResource.DisplayName)));
        }

        if (resource.Tags is null)
        {
            issues.Add(new("item.tags.type", "Item tags must be an array of strings.", nameof(ItemResource.Tags)));
        }
        else
        {
            var seenTags = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in resource.Tags)
            {
                if (tag is null)
                {
                    issues.Add(new("item.tags.entry_type", "Item tags must contain only strings.", nameof(ItemResource.Tags)));
                }
                else if (!seenTags.Add(tag))
                {
                    issues.Add(new("item.tags.duplicate", $"Item tags contain duplicate '{tag}'.", nameof(ItemResource.Tags)));
                }
            }
        }

        return issues;
    }

    public static IReadOnlyList<ValidationIssue> ValidateId(string? id)
    {
        var issues = new List<ValidationIssue>();
        ValidateId(id, "id", issues);
        return issues;
    }

    public static string NormalizeId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var builder = new StringBuilder();
        var underscore = false;
        foreach (var c in input.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(c))
            {
                if (builder.Length > 0 && !underscore) { builder.Append('_'); underscore = true; }
            }
            else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-')
            {
                builder.Append(c); underscore = false;
            }
            else if (c == '_' && builder.Length > 0 && !underscore)
            {
                builder.Append(c); underscore = true;
            }
        }
        return builder.ToString().Trim('_', '-');
    }

    private static void ValidateId(string? id, string field, ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            issues.Add(new($"item.{field}.required", $"Item {field} is required.", field));
        }
        else if (!IdRegex().IsMatch(id))
        {
            issues.Add(new($"item.{field}.invalid", $"Item {field} '{id}' is not valid. Expected {IdPattern}.", field));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();
}
