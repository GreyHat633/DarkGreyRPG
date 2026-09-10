using System.Collections.ObjectModel;

namespace DarkGreyRPG.Studio.Core.Identity;

public sealed class NamespacePolicy
{
    public NamespacePolicy(string globalNamespace)
        : this(globalNamespace, null)
    {
    }

    public NamespacePolicy(string globalNamespace, IReadOnlyDictionary<string, string>? storyOverrides)
    {
        if (!DgrResourceId.IsValidNamespace(globalNamespace))
        {
            throw new ArgumentException("Global namespace is invalid.", nameof(globalNamespace));
        }

        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        if (storyOverrides is not null)
        {
            foreach (var pair in storyOverrides)
            {
                if (!DgrResourceId.IsFullId(pair.Key))
                {
                    throw new ArgumentException("Story override keys must be full Story IDs.", nameof(storyOverrides));
                }

                if (!DgrResourceId.IsValidNamespace(pair.Value))
                {
                    throw new ArgumentException("Story override namespaces are invalid.", nameof(storyOverrides));
                }

                if (!overrides.TryAdd(pair.Key, pair.Value))
                {
                    throw new ArgumentException($"Duplicate Story override '{pair.Key}'.", nameof(storyOverrides));
                }
            }
        }

        GlobalNamespace = globalNamespace;
        StoryOverrides = new ReadOnlyDictionary<string, string>(overrides);
    }

    public string GlobalNamespace { get; }

    public IReadOnlyDictionary<string, string> StoryOverrides { get; }

    public string EffectiveNamespace(string storyId)
    {
        RequireStoryId(storyId);
        return StoryOverrides.TryGetValue(storyId, out var customNamespace)
            ? customNamespace
            : GlobalNamespace;
    }

    public bool IsCustom(string storyId)
    {
        RequireStoryId(storyId);
        return StoryOverrides.ContainsKey(storyId);
    }

    private static void RequireStoryId(string? storyId)
    {
        if (!DgrResourceId.IsFullId(storyId))
        {
            throw new ArgumentException("Story ID must be a full DGR resource ID.", nameof(storyId));
        }
    }
}
