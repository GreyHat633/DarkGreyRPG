namespace DarkGreyRPG.Studio.Core.Items;

public sealed record ItemResourceInfo(
    string Id,
    string DisplayName,
    string Path,
    IReadOnlyList<string> Tags,
    string Type);
