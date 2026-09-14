using System.Text;
using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Core.Identity;

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
        if (resource.SchemaVersion is not (ActorResource.LegacySchemaVersion or ActorResource.StorySchemaVersion or ActorResource.CurrentSchemaVersion))
        {
            issues.Add(new(
                "actor.schema.unsupported",
                $"Actor schema_version must be {ActorResource.LegacySchemaVersion}, {ActorResource.StorySchemaVersion}, or {ActorResource.CurrentSchemaVersion}.",
                nameof(ActorResource.SchemaVersion)));
        }

        // A few 2.x callers used CurrentSchemaVersion while constructing the
        // old id/notes object. Keep that in-memory form writable as schema 2;
        // JSON schema 3 still requires the explicit type and identity fields.
        var legacyCompatibility = resource.SchemaVersion == ActorResource.CurrentSchemaVersion
            && string.IsNullOrWhiteSpace(resource.Type)
            && !string.IsNullOrWhiteSpace(resource.Id)
            && string.IsNullOrWhiteSpace(resource.NpcId)
            && string.IsNullOrWhiteSpace(resource.GroupId);
        var isV3 = resource.SchemaVersion == ActorResource.CurrentSchemaVersion && !legacyCompatibility;
        if (isV3)
        {
            if (resource.HasExplicitLegacyId)
            {
                issues.Add(new("actor.id.forbidden", "Schema 3 Actor resources derive their ID from the type-specific identity.", nameof(ActorResource.Id)));
            }
            var individual = string.Equals(resource.Type, IndividualActorResource.ResourceType, StringComparison.Ordinal);
            var collective = string.Equals(resource.Type, CollectiveActorResource.ResourceType, StringComparison.Ordinal);
            if (!individual && !collective)
            {
                issues.Add(new("actor.type.unsupported", "Actor type must be 'individual' or 'collective'.", nameof(ActorResource.Type)));
            }

            if (!string.IsNullOrWhiteSpace(resource.Notes))
            {
                issues.Add(new("actor.notes.forbidden", "Schema 3 Actor resources must not contain notes.", nameof(ActorResource.Notes)));
            }

            if (individual)
            {
                ValidateId(resource.NpcId, idPolicy, issues, "npc_id");
                if (!string.IsNullOrWhiteSpace(resource.GroupId))
                {
                    issues.Add(new("actor.identity.both", "An individual Actor cannot also contain group_id.", nameof(ActorResource.GroupId)));
                }
            }
            else if (collective)
            {
                ValidateId(resource.GroupId, idPolicy, issues, "group_id");
                if (!string.IsNullOrWhiteSpace(resource.NpcId))
                {
                    issues.Add(new("actor.identity.both", "A collective Actor cannot also contain npc_id.", nameof(ActorResource.NpcId)));
                }
            }
        }
        else
        {
            ValidateId(resource.Id, idPolicy, issues);
            if (resource.SchemaVersion == ActorResource.LegacySchemaVersion
                && !string.IsNullOrWhiteSpace(resource.HomeStoryId))
            {
                issues.Add(new("actor.home_story_id.forbidden", "Schema 1 Actor resources must not contain home_story_id.", nameof(ActorResource.HomeStoryId)));
            }
            if (!string.IsNullOrWhiteSpace(resource.Type)
                || !string.IsNullOrWhiteSpace(resource.NpcId)
                || !string.IsNullOrWhiteSpace(resource.GroupId))
            {
                issues.Add(new("actor.v3_fields.forbidden", "Schema 1/2 Actor resources cannot contain schema 3 identity fields.", nameof(ActorResource.Type)));
            }
        }

        if (isV3)
            issues.AddRange(ActorPortraitSchema.Validate(resource));
        else if (resource.DefaultPortraitRef is not null || resource.PortraitVariants is null || resource.PortraitVariants.Count != 0)
            issues.Add(new("actor.portrait.legacy", "旧 Actor 结构不能携带头像字段。", "portrait_variants"));

        if (string.IsNullOrWhiteSpace(resource.DisplayName))
        {
            issues.Add(new(
                "actor.display_name.required",
                "Actor display_name is required.",
                nameof(ActorResource.DisplayName)));
        }

        if (!isV3 && resource.Notes is null)
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

        if (resource.SchemaVersion == ActorResource.StorySchemaVersion || legacyCompatibility || isV3)
        {
            if (string.IsNullOrWhiteSpace(resource.HomeStoryId))
            {
                issues.Add(new(
                    "actor.home_story_id.required",
                    $"Actor schema_version {resource.SchemaVersion} requires home_story_id.",
                    nameof(ActorResource.HomeStoryId)));
            }
            else if (!DgrResourceId.IsFullId(resource.HomeStoryId) && !RuntimeIdRegex().IsMatch(resource.HomeStoryId))
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

    private static void ValidateId(string? id, ActorIdPolicy idPolicy, ICollection<ValidationIssue> issues, string field = "id")
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            issues.Add(new($"actor.{field}.required", $"Actor {field} is required.", field));
            return;
        }

        if (!DgrResourceId.IsFullId(id) && !RuntimeIdRegex().IsMatch(id))
        {
            issues.Add(new(
                $"actor.{field}.invalid",
                $"Actor {field} '{id}' is not Runtime-compatible. Expected {RuntimeIdPattern}.",
                field));
            return;
        }

        if (idPolicy == ActorIdPolicy.NewResource && !DgrResourceId.IsFullId(id) && !NewResourceIdRegex().IsMatch(id))
        {
            issues.Add(new(
                $"actor.{field}.new_resource_invalid",
                $"New Actor {field} '{id}' must match {NewResourceIdPattern}.",
                field));
        }
        else if (idPolicy == ActorIdPolicy.ExistingResource && !DgrResourceId.IsFullId(id) && !NewResourceIdRegex().IsMatch(id))
        {
            issues.Add(new(
                $"actor.{field}.legacy_compatible",
                $"This existing Actor {field} is Runtime-compatible but uses the legacy dot form. Rename it explicitly to remove the compatibility warning.",
                field,
                ValidationSeverity.Warning));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeIdRegex();

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NewResourceIdRegex();
}
