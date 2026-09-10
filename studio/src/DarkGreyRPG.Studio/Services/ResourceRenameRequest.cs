namespace DarkGreyRPG.Studio.Services;

public sealed record ResourceRenameRequest(string Id, string DisplayName, IReadOnlyList<string>? Tags = null);
