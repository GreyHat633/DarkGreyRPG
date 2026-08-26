using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Stories;

public static class StorySerializer
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

    public static string Serialize(StoryResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (resource.SchemaVersion is not (StoryResource.LegacySchemaVersion or StoryResource.CurrentSchemaVersion))
        {
            throw new StoryDataException($"Unsupported Story schema_version {resource.SchemaVersion}.");
        }

        var json = JsonSerializer.Serialize(resource, Options);
        return json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static StoryResource Deserialize(string json, string? sourcePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            var resource = JsonSerializer.Deserialize<StoryResource>(json, Options)
                ?? throw new JsonException("Story JSON root cannot be null.");
            if (resource.SchemaVersion is not (StoryResource.LegacySchemaVersion or StoryResource.CurrentSchemaVersion))
            {
                throw new JsonException($"Unsupported Story schema_version {resource.SchemaVersion}.");
            }

            if (!string.IsNullOrWhiteSpace(sourcePath)
                && !string.Equals(Path.GetFileName(sourcePath), resource.Id + ".json", StringComparison.Ordinal))
            {
                throw new StoryDataException($"Story file name must match its ID '{resource.Id}'.");
            }

            return resource;
        }
        catch (StoryDataException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new StoryDataException("Story JSON is invalid or contains unsupported fields.", exception);
        }
    }

    public static StoryResource Read(string path)
    {
        try
        {
            return Deserialize(File.ReadAllText(path), path);
        }
        catch (StoryDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new StoryDataException($"Could not read Story file '{path}'.", exception);
        }
    }
}

public sealed class StoryDataException : Exception
{
    public StoryDataException(string message) : base(message) { }
    public StoryDataException(string message, Exception innerException) : base(message, innerException) { }
}
