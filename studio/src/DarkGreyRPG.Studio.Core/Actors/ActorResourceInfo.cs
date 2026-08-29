namespace DarkGreyRPG.Studio.Core.Actors;

public sealed record ActorResourceInfo(
    string Id,
    string DisplayName,
    string SourcePath,
    IReadOnlyList<string> Tags,
    string Type = ActorResource.LegacyResourceType);
