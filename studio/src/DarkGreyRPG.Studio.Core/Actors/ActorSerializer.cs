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
        ThrowIfInvalid(resource, idPolicy);
        if (resource.SchemaVersion < ActorResource.CurrentSchemaVersion
            && !string.IsNullOrWhiteSpace(resource.HomeStoryId))
        {
            resource = new ActorResource
            {
                SchemaVersion = ActorResource.CurrentSchemaVersion,
                Id = resource.Id,
                DisplayName = resource.DisplayName,
                Notes = resource.Notes,
                Tags = [.. resource.Tags],
                HomeStoryId = resource.HomeStoryId,
            };
        }
        // A default-constructed ActorResource represents the 2.0 shape. It is
        // retained as schema 1 only when it has no 2.1-only home story value;
        // all documents and migrations explicitly provide schema 2.
        return JsonSerializer.Serialize(resource, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static ActorResource Deserialize(string json, string? sourcePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        ActorResource resource;
        try
        {
            resource = JsonSerializer.Deserialize<ActorResource>(json, Options)
                ?? throw new JsonException("Actor JSON root cannot be null.");
        }
        catch (JsonException exception)
        {
            throw new ActorDataException("Actor JSON is invalid or contains unsupported fields.", exception);
        }

        var issues = ActorValidator.Validate(resource, ActorIdPolicy.ExistingResource).ToList();
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            var expectedFileName = resource.Id + ".json";
            var actualFileName = Path.GetFileName(sourcePath);
            if (!string.Equals(expectedFileName, actualFileName, StringComparison.Ordinal))
            {
                issues.Add(new(
                    "actor.filename.mismatch",
                    $"Actor file name must match its ID: expected '{expectedFileName}', got '{actualFileName}'.",
                    nameof(ActorResource.Id)));
            }
        }

        ThrowIfInvalid(issues);
        return resource;
    }

    public static ActorResource Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return Deserialize(File.ReadAllText(path), path);
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

    private static void ThrowIfInvalid(ActorResource resource, ActorIdPolicy idPolicy) =>
        ThrowIfInvalid(ActorValidator.Validate(resource, idPolicy));

    private static void ThrowIfInvalid(IEnumerable<ValidationIssue> issues)
    {
        var allIssues = issues.ToArray();
        if (allIssues.Any(issue => issue.Severity == ValidationSeverity.Error))
        {
            throw new ActorValidationException(allIssues);
        }
    }
}

public sealed class ActorDataException : Exception
{
    public ActorDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
