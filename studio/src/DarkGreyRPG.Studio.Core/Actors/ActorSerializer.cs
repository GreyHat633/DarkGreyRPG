using DarkGreyRPG.Studio.Core.Identity;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Actors;

public static class ActorSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    public static string Serialize(ActorResource resource, ActorIdPolicy idPolicy)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ThrowIfInvalid(resource, idPolicy);

        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        fields["schema_version"] = ActorResource.CurrentSchemaVersion;
        fields["identity_format"] = "story-uid-v1";
        fields["default_portrait_ref"] = resource.DefaultPortraitRef;
        fields["portrait_variants"] = resource.PortraitVariants;
        fields["type"] = resource.Type;
        fields[resource.Type == IndividualActorResource.ResourceType ? "npc_id" : "group_id"] = ResourceAddress.FromKey(resource.Id);
        fields["display_name"] = resource.DisplayName;
        fields["tags"] = resource.Tags;
        fields["home_story_id"] = resource.HomeStoryId;

        return JsonSerializer.Serialize(fields, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static ActorResource Deserialize(string json, string? sourcePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var root = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
            if (root.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Actor JSON root must be an object.");
            }

            RejectDuplicateProperties(root.RootElement);
            var schema = RequiredInt(root.RootElement, "schema_version");
            var resource = schema switch
            {
                ActorResource.CurrentSchemaVersion => ReadCurrent(root.RootElement, json),
                _ => throw new ActorValidationException([new(
                    "actor.schema.unsupported",
                    "Actor schema_version is unsupported.",
                    nameof(ActorResource.SchemaVersion))]),
            };

            var issues = ActorValidator.Validate(resource, ActorIdPolicy.ExistingResource).ToList();
            ThrowIfInvalid(issues);
            return resource;
        }
        catch (Exception exception) when (exception is ActorValidationException or ActorDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new ActorDataException("Actor JSON is invalid or contains unsupported fields.", exception);
        }
    }

    public static ActorResource Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return Deserialize(File.ReadAllText(path), path);
        }
        catch (Exception exception) when (exception is ActorDataException or ActorValidationException)
        {
            throw;
        }
        catch (IOException exception)
        {
            throw new ActorDataException($"Could not read Actor file '{path}'.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new ActorDataException($"Could not read Actor file '{path}'.", exception);
        }
    }

    public static IndividualActorResource DeserializeIndividual(string json, string? sourcePath = null) =>
        Deserialize(json, sourcePath) as IndividualActorResource
        ?? throw new ActorValidationException([new("actor.type.mismatch", "Actor resource is not individual.", nameof(ActorResource.Type))]);

    public static CollectiveActorResource DeserializeCollective(string json, string? sourcePath = null) =>
        Deserialize(json, sourcePath) as CollectiveActorResource
        ?? throw new ActorValidationException([new("actor.type.mismatch", "Actor resource is not collective.", nameof(ActorResource.Type))]);

    private static ActorResource ReadCurrent(JsonElement root, string json)
    {
        if (!root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
        {
            throw new ActorValidationException([new("actor.type.required", "Actor type is required.", nameof(ActorResource.Type))]);
        }

        var typeValue = type.GetString();
        var expectedIdentity = typeValue switch
        {
            IndividualActorResource.ResourceType => "npc_id",
            CollectiveActorResource.ResourceType => "group_id",
            _ => throw new ActorValidationException([new("actor.type.unsupported", "Actor type must be 'individual' or 'collective'.", nameof(ActorResource.Type))]),
        };
        if (!root.TryGetProperty("identity_format", out var marker) || marker.ValueKind != JsonValueKind.String
            || marker.GetString() != "story-uid-v1") throw new JsonException("Current Actor identity format is required.");
        var allowed = new[] { "schema_version", "identity_format", "type", expectedIdentity, "display_name", "tags", "home_story_id", "default_portrait_ref", "portrait_variants" };
        EnsureExactFields(root, allowed);
        EnsureRequired(root, ["type", expectedIdentity, "display_name", "tags", "home_story_id"]);
        return typeValue == IndividualActorResource.ResourceType
            ? JsonSerializer.Deserialize<IndividualActorResource>(json, Options) ?? throw new JsonException("Actor JSON root cannot be null.")
            : JsonSerializer.Deserialize<CollectiveActorResource>(json, Options) ?? throw new JsonException("Actor JSON root cannot be null.");
    }

    private static void EnsureExactFields(JsonElement root, IReadOnlyCollection<string> allowed)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                if (property.Name is "id" or "notes" or "npc_id" or "group_id" or "type")
                {
                    throw new ActorValidationException([new(
                        "actor.field.forbidden",
                        $"Actor field '{property.Name}' is not valid for this schema/type.",
                        property.Name)]);
                }
                throw new ActorDataException($"Actor field '{property.Name}' is not supported for this schema.", new JsonException("Unsupported Actor field."));
            }
        }
    }

    private static void EnsureRequired(JsonElement root, IEnumerable<string> fields)
    {
        var missing = fields.Where(field => !root.TryGetProperty(field, out _)).ToArray();
        if (missing.Length > 0)
        {
            throw new ActorValidationException(missing.Select(field => new ValidationIssue(
                "actor.field.required", $"Actor field '{field}' is required.", field)).ToArray());
        }
    }

    private static int RequiredInt(JsonElement root, string field)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            throw new ActorValidationException([new("actor.schema.type", "Actor schema_version must be an integer.", field)]);
        }
        return result;
    }

    private static void RejectDuplicateProperties(JsonElement root)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new ActorDataException($"Actor JSON contains duplicate field '{property.Name}'.", new JsonException("Duplicate field."));
            }
        }
    }

    private static void ThrowIfInvalid(ActorResource resource, ActorIdPolicy idPolicy) => ThrowIfInvalid(ActorValidator.Validate(resource, idPolicy));

    private static void ThrowIfInvalid(IEnumerable<ValidationIssue> issues)
    {
        var allIssues = issues.ToArray();
        if (allIssues.Any(issue => issue.Severity == ValidationSeverity.Error)) throw new ActorValidationException(allIssues);
    }
}

public sealed class ActorDataException : Exception
{
    public ActorDataException(string message, Exception innerException) : base(message, innerException) { }
}
