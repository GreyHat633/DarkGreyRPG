using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Items;

public static class ItemSerializer
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

    public static string Serialize(ItemResource resource)
    {
        ThrowIfInvalid(resource);
        var json = resource switch
        {
            IndividualItemResource individual => JsonSerializer.Serialize(individual, Options),
            CollectiveItemResource collective => JsonSerializer.Serialize(collective, Options),
            _ => throw new ItemValidationException([new("item.type.unsupported", "Item resource type is unsupported.", nameof(ItemResource.Type))]),
        };
        return json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static ItemResource Deserialize(string json, string? sourcePath = null)
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
                throw new JsonException("Item JSON root must be an object.");
            }

            if (!root.RootElement.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String)
            {
                throw new ItemValidationException([new("item.type.required", "Item type is required.", nameof(ItemResource.Type))]);
            }

            EnsureRequiredProperties(root.RootElement, type.GetString());

            ItemResource resource = type.GetString() switch
            {
                IndividualItemResource.ResourceType => JsonSerializer.Deserialize<IndividualItemResource>(json, Options)
                    ?? throw new JsonException("Item JSON root cannot be null."),
                CollectiveItemResource.ResourceType => JsonSerializer.Deserialize<CollectiveItemResource>(json, Options)
                    ?? throw new JsonException("Item JSON root cannot be null."),
                _ => throw new ItemValidationException([new("item.type.unsupported", "Item type is unsupported.", nameof(ItemResource.Type))]),
            };

            var issues = ItemValidator.Validate(resource).ToList();
            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                var id = resource is IndividualItemResource individual ? individual.ItemId : ((CollectiveItemResource)resource).GroupId;
                var expected = id + ".json";
                var actual = Path.GetFileName(sourcePath);
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    issues.Add(new("item.filename.mismatch", $"Item file name must match its ID: expected '{expected}', got '{actual}'.", nameof(ItemResource.Type)));
                }
            }

            ThrowIfInvalid(issues);
            return resource;
        }
        catch (ItemValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new ItemDataException("Item JSON is invalid or contains unsupported fields.", exception);
        }
    }

    public static IndividualItemResource DeserializeIndividual(string json, string? sourcePath = null)
    {
        var resource = Deserialize(json, sourcePath);
        return resource as IndividualItemResource
            ?? throw new ItemValidationException([new("item.type.mismatch", "Item resource is not an individual item.", nameof(ItemResource.Type))]);
    }

    public static CollectiveItemResource DeserializeCollective(string json, string? sourcePath = null)
    {
        var resource = Deserialize(json, sourcePath);
        return resource as CollectiveItemResource
            ?? throw new ItemValidationException([new("item.type.mismatch", "Item resource is not a collective item.", nameof(ItemResource.Type))]);
    }

    public static ItemResource Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            return Deserialize(File.ReadAllText(path), path);
        }
        catch (Exception exception) when (exception is ItemValidationException or ItemDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ItemDataException($"Could not read Item file '{path}'.", exception);
        }
    }

    private static void ThrowIfInvalid(ItemResource resource) => ThrowIfInvalid(ItemValidator.Validate(resource));

    private static void ThrowIfInvalid(IEnumerable<ValidationIssue> issues)
    {
        var all = issues.ToArray();
        if (all.Any(issue => issue.Severity == ValidationSeverity.Error)) throw new ItemValidationException(all);
    }

    private static void EnsureRequiredProperties(JsonElement root, string? type)
    {
        var required = new List<string> { "schema_version", "type", "display_name", "tags" };
        if (string.Equals(type, IndividualItemResource.ResourceType, StringComparison.Ordinal))
        {
            required.Add("item_id");
        }
        else if (string.Equals(type, CollectiveItemResource.ResourceType, StringComparison.Ordinal))
        {
            required.Add("group_id");
        }

        var missing = required
            .Where(name => !root.TryGetProperty(name, out _))
            .Select(name => new ValidationIssue("item.field.required", $"Item field '{name}' is required.", name))
            .ToArray();
        if (missing.Length > 0) throw new ItemValidationException(missing);

    }
}
