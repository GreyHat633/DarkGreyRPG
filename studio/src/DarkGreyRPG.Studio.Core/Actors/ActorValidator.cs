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
        if (resource.SchemaVersion != ActorResource.CurrentSchemaVersion || resource.IdentityFormat != "story-uid-v1")
            issues.Add(new("actor.schema.unsupported", "Current Actor schema and identity format are required."));
        if (resource.HasExplicitLegacyId || !string.IsNullOrEmpty(resource.Notes))
            issues.Add(new("actor.legacy.forbidden", "Legacy Actor identity and notes are unsupported."));
        var individual = resource.Type == IndividualActorResource.ResourceType;
        var collective = resource.Type == CollectiveActorResource.ResourceType;
        if (!individual && !collective)
            issues.Add(new("actor.type.unsupported", "Actor type must be individual or collective."));
        if (individual && resource.GroupId is not null || collective && resource.NpcId is not null)
            issues.Add(new("actor.identity.both", "Actor has an identity for a different type."));
        if (!ResourceAddress.IsKey(resource.Id) || ResourceAddress.FromKey(resource.Id).Kind != ResourceKind.Actor)
            issues.Add(new("actor.address.invalid", "A current Actor address is required."));
        else if (ResourceAddress.FromKey(resource.Id).StoryUid.Value != resource.HomeStoryId)
            issues.Add(new("actor.owner.mismatch", "Actor home Story must match its address owner."));
        if (!StoryUid.IsValid(resource.HomeStoryId))
            issues.Add(new("actor.home_story_id.invalid", "A current Story UID is required."));
        issues.AddRange(ActorPortraitSchema.Validate(resource));

        if (string.IsNullOrWhiteSpace(resource.DisplayName))
        {
            issues.Add(new(
                "actor.display_name.required",
                "Actor display_name is required.",
                nameof(ActorResource.DisplayName)));
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

        if (!ResourceAddress.IsKey(id) || ResourceAddress.FromKey(id).Kind != ResourceKind.Actor)
            issues.Add(new($"actor.{field}.invalid", "A current Actor address is required.", field));
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeIdRegex();

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NewResourceIdRegex();
}
