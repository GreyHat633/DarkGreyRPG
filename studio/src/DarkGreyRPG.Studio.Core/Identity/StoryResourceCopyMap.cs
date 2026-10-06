using System.Collections.ObjectModel;

namespace DarkGreyRPG.Studio.Core.Identity;

/// <summary>Allocates all owned resource copies before any definition is rewritten or written.</summary>
public sealed class StoryResourceCopyMap
{
    private readonly IReadOnlyDictionary<ResourceAddress, ResourceAddress> _copies;

    private StoryResourceCopyMap(IDictionary<ResourceAddress, ResourceAddress> copies)
        => _copies = new ReadOnlyDictionary<ResourceAddress, ResourceAddress>(copies);

    public IReadOnlyDictionary<ResourceAddress, ResourceAddress> Copies => _copies;

    /// <summary>External references retain their original owner and identity.</summary>
    public ResourceAddress Resolve(ResourceAddress source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _copies.TryGetValue(source, out var copy) ? copy : source;
    }

    public static StoryResourceCopyMap Create(
        StoryUid sourceStory,
        StoryUid targetStory,
        IEnumerable<ResourceAddress> sourceOwned,
        IEnumerable<ResourceAddress> targetOwned)
    {
        ArgumentNullException.ThrowIfNull(sourceStory);
        ArgumentNullException.ThrowIfNull(targetStory);
        ArgumentNullException.ThrowIfNull(sourceOwned);
        ArgumentNullException.ThrowIfNull(targetOwned);
        if (sourceStory == targetStory) throw new ArgumentException("Cannot copy a Story into itself.");

        var sources = ValidateOwnership(sourceOwned, sourceStory);
        var occupied = ValidateOwnership(targetOwned, targetStory);
        var copies = new Dictionary<ResourceAddress, ResourceAddress>();
        var collisions = new List<ResourceAddress>();
        foreach (var source in sources.OrderBy(value => value.KindToken, StringComparer.Ordinal)
                     .ThenBy(value => value.LocalId, StringComparer.Ordinal))
        {
            var desired = new ResourceAddress(targetStory, source.Kind, source.LocalId);
            if (occupied.Add(desired)) copies.Add(source, desired);
            else collisions.Add(source);
        }
        // Reserve every reusable local ID first so collision allocation cannot steal one.
        foreach (var source in collisions)
        {
            var copy = ResourceAddress.Create(targetStory, source.Kind, occupied);
            occupied.Add(copy);
            copies.Add(source, copy);
        }
        return new StoryResourceCopyMap(copies);
    }

    private static HashSet<ResourceAddress> ValidateOwnership(IEnumerable<ResourceAddress> addresses, StoryUid owner)
    {
        var result = new HashSet<ResourceAddress>();
        foreach (var address in addresses)
        {
            if (address is null || address.StoryUid != owner)
                throw new ArgumentException("Owned resources must have exactly the declared Story owner.", nameof(addresses));
            if (!result.Add(address)) throw new ArgumentException("Duplicate owned resource address.", nameof(addresses));
        }
        return result;
    }
}
