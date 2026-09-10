using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Items;

public sealed class ItemRepository
{
    private readonly IAtomicFileWriter _writer;

    public ItemRepository(string projectDirectory, IAtomicFileWriter? atomicFileWriter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        ItemsDirectory = Path.Combine(ProjectDirectory, "items");
        GroupsDirectory = Path.Combine(ProjectDirectory, "item_groups");
        _writer = atomicFileWriter ?? new AtomicFileWriter();
    }

    public string ProjectDirectory { get; }
    public string ItemsDirectory { get; }
    public string GroupsDirectory { get; }
    public string ItemGroupsDirectory => GroupsDirectory;

    public IReadOnlyList<ItemResourceInfo> ListItems() => ListDirectory(ItemsDirectory, IndividualItemResource.ResourceType);
    public IReadOnlyList<ItemResourceInfo> ListGroups() => ListDirectory(GroupsDirectory, CollectiveItemResource.ResourceType);
    public IReadOnlyList<ItemResourceInfo> ListCollectiveItems() => ListGroups();
    public IReadOnlyList<ItemResourceInfo> ListItemGroups() => ListGroups();

    public IndividualItemResource CreateItem(string itemId, string displayName)
    {
        ValidateId(itemId);
        if (FindPath(ItemsDirectory, itemId) is not null) throw new ItemCollisionException(itemId);
        return new IndividualItemResource { ItemId = itemId, DisplayName = displayName };
    }

    public IndividualItemResource CreateItem() => CreateItem(GetAvailableItemId("new_item"), "新物品");

    public CollectiveItemResource CreateGroup(string groupId, string displayName)
    {
        ValidateId(groupId);
        if (FindPath(GroupsDirectory, groupId) is not null) throw new ItemCollisionException(groupId);
        return new CollectiveItemResource { GroupId = groupId, DisplayName = displayName };
    }

    public CollectiveItemResource CreateGroup() => CreateGroup(GetAvailableGroupId("new_group"), "新物品组");

    public IndividualItemResource LoadItem(string itemId)
    {
        ValidateId(itemId);
        var path = FindPath(ItemsDirectory, itemId);
        if (path is null) throw new ItemNotFoundException(itemId);
        return ReadResource(path) as IndividualItemResource
            ?? throw new ItemValidationException([new("item.type.mismatch", "Item file contains the wrong resource type.", nameof(ItemResource.Type))]);
    }

    public CollectiveItemResource LoadGroup(string groupId)
    {
        ValidateId(groupId);
        var path = FindPath(GroupsDirectory, groupId);
        if (path is null) throw new ItemNotFoundException(groupId);
        return ReadResource(path) as CollectiveItemResource
            ?? throw new ItemValidationException([new("item.type.mismatch", "Item file contains the wrong resource type.", nameof(ItemResource.Type))]);
    }

    public IndividualItemResource LoadIndividual(string itemId) => LoadItem(itemId);
    public CollectiveItemResource LoadCollective(string groupId) => LoadGroup(groupId);

    public IndividualItemResource SaveItem(IndividualItemResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var path = GetItemPath(resource.ItemId);
        Save(resource, path, resource.ItemId, ItemsDirectory, isNew: !File.Exists(path));
        return resource;
    }

    public CollectiveItemResource SaveGroup(CollectiveItemResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var path = GetGroupPath(resource.GroupId);
        Save(resource, path, resource.GroupId, GroupsDirectory, isNew: !File.Exists(path));
        return resource;
    }

    public IndividualItemResource SaveIndividual(IndividualItemResource resource) => SaveItem(resource);
    public CollectiveItemResource SaveCollective(CollectiveItemResource resource) => SaveGroup(resource);

    public ItemResource Save(ItemResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return resource switch
        {
            IndividualItemResource individual => SaveItem(individual),
            CollectiveItemResource collective => SaveGroup(collective),
            _ => throw new ItemValidationException([new("item.type.unsupported", "Item resource type is unsupported.", nameof(ItemResource.Type))]),
        };
    }

    public void DeleteItem(string itemId)
    {
        ValidateId(itemId);
        Delete(GetItemPath(itemId), itemId);
    }

    public void DeleteGroup(string groupId)
    {
        ValidateId(groupId);
        Delete(GetGroupPath(groupId), groupId);
    }

    public string GetAvailableItemId(string baseId) => GetAvailableId(baseId, ItemsDirectory);
    public string GetAvailableGroupId(string baseId) => GetAvailableId(baseId, GroupsDirectory);
    public string GetAvailableId(string baseId) => GetAvailableItemId(baseId);

    public string GetItemPath(string itemId) => GetPath(ItemsDirectory, itemId);
    public string GetGroupPath(string groupId) => GetPath(GroupsDirectory, groupId);

    private IReadOnlyList<ItemResourceInfo> ListDirectory(string directory, string expectedType)
    {
        if (!Directory.Exists(directory)) return [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return CanonicalResourceFileSystem.EnumerateJsonFiles(directory)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var resource = ReadResource(path);
                if (!string.Equals(resource.Type, expectedType, StringComparison.Ordinal))
                    throw new ItemValidationException([new("item.type.mismatch", "Item file contains the wrong resource type.", nameof(ItemResource.Type))]);
                var id = resource.Id;
                if (!seen.Add(id)) throw new ItemRepositoryException($"Item ID '{id}' is present in multiple files, including '{path}'.");
                return new ItemResourceInfo(id, resource.DisplayName, path, [.. resource.Tags], resource.Type);
            }).ToArray();
    }

    private void Save(ItemResource resource, string path, string id, string resourceDirectory, bool isNew)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var serialized = ItemSerializer.Serialize(resource);
        if (isNew && FindPath(resourceDirectory, id) is not null) throw new ItemCollisionException(id);
        try
        {
            _writer.Write(path, serialized, temporaryPath =>
            {
                var staged = ItemSerializer.Deserialize(File.ReadAllText(temporaryPath));
                var stagedId = staged is IndividualItemResource individual ? individual.ItemId : ((CollectiveItemResource)staged).GroupId;
                if (!string.Equals(stagedId, id, StringComparison.Ordinal))
                    throw new ItemRepositoryException("The staged item ID changed during serialization.");
            });
        }
        catch (Exception exception) when (exception is ItemValidationException or ItemDataException or ItemRepositoryException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ItemRepositoryException($"Could not save item resource '{id}'.", exception);
        }
    }

    private static void Delete(string path, string id)
    {
        if (!File.Exists(path)) throw new ItemNotFoundException(id);
        try { File.Delete(path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw new ItemRepositoryException($"Could not delete item resource '{id}'.", exception); }
    }

    private static string GetAvailableId(string baseId, string directory)
    {
        ValidateId(baseId);
        if (FindPath(directory, baseId) is null) return baseId;
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{baseId}_{suffix}";
            if (FindPath(directory, candidate) is null) return candidate;
        }
        throw new ItemRepositoryException($"Could not allocate an available item ID based on '{baseId}'.");
    }

    private static void ValidateId(string id)
    {
        var issues = ItemValidator.ValidateId(id);
        if (issues.Any(issue => issue.Severity == ValidationSeverity.Error)) throw new ItemValidationException(issues);
    }

    private static string GetPath(string directory, string id)
    {
        ValidateId(id);
        return FindPath(directory, id) ?? Path.Combine(directory, DgrResourceId.RelativeJsonPath(id));
    }

    private static string? FindPath(string directory, string id)
    {
        if (!DgrResourceId.IsFullId(id))
        {
            var legacyPath = Path.Combine(directory, id + ".json");
            return File.Exists(legacyPath) ? legacyPath : null;
        }
        return CanonicalResourceFileSystem.FindUniquePath(
            directory,
            id,
            path => ReadResource(path).Id,
            (logicalId, paths) => new ItemRepositoryException(
                $"Item ID '{logicalId}' is present in multiple files: {string.Join(", ", paths)}."),
            exception => exception is ItemValidationException or ItemDataException);
    }

    private static ItemResource ReadResource(string path)
    {
        try
        {
            var resource = ItemSerializer.Deserialize(File.ReadAllText(path));
            if (!DgrResourceId.IsFullId(resource.Id)
                && !string.Equals(Path.GetFileName(path), resource.Id + ".json", StringComparison.Ordinal))
                throw new ItemValidationException([new(
                    "item.filename.mismatch",
                    $"Item file name must match its ID: expected '{resource.Id}.json', got '{Path.GetFileName(path)}'.",
                    nameof(ItemResource.Type))]);
            return resource;
        }
        catch (ItemValidationException) { throw; }
        catch (ItemDataException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw new ItemDataException($"Could not read Item file '{path}'.", exception); }
    }
}
