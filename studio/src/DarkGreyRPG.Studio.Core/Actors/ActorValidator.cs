using System.Text;
using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Actors;

public enum ActorIdPolicy
{
    ExistingResource,
    NewResource,
}

public static partial class ActorValidator
{
    public const string RuntimeIdPattern = "[a-z0-9][a-z0-9_.-]*";
    public const string NewResourceIdPattern = "[a-z0-9][a-z0-9_-]*";

    public static IReadOnlyList<ValidationIssue> Validate(
        ActorResource resource,
        ActorIdPolicy idPolicy = ActorIdPolicy.ExistingResource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var issues = new List<ValidationIssue>();
        if (resource.SchemaVersion is not (ActorResource.LegacySchemaVersion or ActorResource.CurrentSchemaVersion))
        {
            issues.Add(new(
                "actor.schema.unsupported",
                $"Actor schema_version must be {ActorResource.LegacySchemaVersion} or {ActorResource.CurrentSchemaVersion}.",
                nameof(ActorResource.SchemaVersion)));
        }

        ValidateId(resource.Id, idPolicy, issues);

        if (string.IsNullOrWhiteSpace(resource.DisplayName))
        {
            issues.Add(new(
                "actor.display_name.required",
                "Actor display_name is required.",
                nameof(ActorResource.DisplayName)));
        }

        if (resource.Notes is null)
        {
            issues.Add(new(
                "actor.notes.type",
                "Actor notes must be a string.",
                nameof(ActorResource.Notes)));
        }

        if (resource.Tags is null)
        {
            issues.Add(new(
                "actor.tags.type",
                "Actor tags must be an array of strings.",
                nameof(ActorResource.Tags)));
        }
        else if (resource.Tags.Any(tag => tag is null))
        {
            issues.Add(new(
                "actor.tags.entry_type",
                "Actor tags must contain only strings.",
                nameof(ActorResource.Tags)));
        }

        if (resource.SchemaVersion >= ActorResource.CurrentSchemaVersion)
        {
            if (string.IsNullOrWhiteSpace(resource.HomeStoryId))
            {
                issues.Add(new(
                    "actor.home_story_id.required",
                    "Actor schema_version 2 requires home_story_id.",
                    nameof(ActorResource.HomeStoryId)));
            }
            else if (!RuntimeIdRegex().IsMatch(resource.HomeStoryId))
            {
                issues.Add(new(
                    "actor.home_story_id.invalid",
                    $"Actor home_story_id '{resource.HomeStoryId}' is not Runtime-compatible.",
                    nameof(ActorResource.HomeStoryId)));
            }
        }

        return issues;
    }

    public static IReadOnlyList<ValidationIssue> ValidateId(
        string? id,
        ActorIdPolicy idPolicy = ActorIdPolicy.NewResource)
    {
        var issues = new List<ValidationIssue>();
        ValidateId(id, idPolicy, issues);
        return issues;
    }

    public static string NormalizeId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Trim().ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        var previousWasUnderscore = false;

        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0 && !previousWasUnderscore)
                {
                    builder.Append('_');
                    previousWasUnderscore = true;
                }

                continue;
            }

            if ((character is >= 'a' and <= 'z') || (character is >= '0' and <= '9') || character == '-')
            {
                builder.Append(character);
                previousWasUnderscore = false;
                continue;
            }

            if (character == '_')
            {
                if (builder.Length > 0 && !previousWasUnderscore)
                {
                    builder.Append(character);
                    previousWasUnderscore = true;
                }
            }
        }

        return builder.ToString().Trim('_', '-');
    }

    public static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in tags)
        {
            var tag = value?.Trim() ?? string.Empty;
            if (tag.Length > 0 && seen.Add(tag))
            {
                result.Add(tag);
            }
        }

        return result;
    }

    private static void ValidateId(string? id, ActorIdPolicy idPolicy, ICollection<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            issues.Add(new("actor.id.required", "Actor ID is required.", nameof(ActorResource.Id)));
            return;
        }

        if (!RuntimeIdRegex().IsMatch(id))
        {
            issues.Add(new(
                "actor.id.invalid",
                $"Actor ID '{id}' is not Runtime-compatible. Expected {RuntimeIdPattern}.",
                nameof(ActorResource.Id)));
            return;
        }

        if (idPolicy == ActorIdPolicy.NewResource && !NewResourceIdRegex().IsMatch(id))
        {
            issues.Add(new(
                "actor.id.new_resource_invalid",
                $"New Actor ID '{id}' must match {NewResourceIdPattern}.",
                nameof(ActorResource.Id)));
        }
        else if (idPolicy == ActorIdPolicy.ExistingResource && !NewResourceIdRegex().IsMatch(id))
        {
            issues.Add(new(
                "actor.id.legacy_compatible",
                "This existing Actor ID is Runtime-compatible but uses the legacy dot form. Rename it explicitly to remove the compatibility warning.",
                nameof(ActorResource.Id),
                ValidationSeverity.Warning));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeIdRegex();

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NewResourceIdRegex();
}
