using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Core.Packaging;

internal static class SessionPortraitPackageValidation
{
    internal static void Validate(string root, StoryPackageRequiredResources required)
    {
        var actors = required.Actors.Select(path => ActorSerializer.Deserialize(File.ReadAllText(Path.Combine(root, path))))
            .ToDictionary(actor => actor.Id, StringComparer.Ordinal);
        foreach (var path in required.Sessions)
        {
            var session = GraphResourceEnvelopeSerializer.Deserialize(File.ReadAllText(Path.Combine(root, path)));
            foreach (var node in session.Graph?.Nodes ?? [])
            {
                if (node.Type != "line" || !node.Properties.TryGetValue("speaker_actor_id", out var speaker)
                    || speaker.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(speaker.GetString())) continue;
                if (!node.Properties.TryGetValue("portrait_variant", out var variant) || variant.ValueKind == JsonValueKind.Null) continue;
                if (actors.TryGetValue(speaker.GetString()!, out var actor))
                {
                    try { ActorPortraitSchema.Resolve(actor, variant.GetString()); }
                    catch (InvalidOperationException exception) { throw new StoryPackageException($"台词 {node.Id} 的头像变体不存在。", exception); }
                }
                // External Actors are resolved and validated against the installed provider set by the server.
            }
        }
    }
}
