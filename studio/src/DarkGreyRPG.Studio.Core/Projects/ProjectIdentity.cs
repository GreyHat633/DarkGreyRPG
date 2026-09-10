namespace DarkGreyRPG.Studio.Core.Projects;

public static class ProjectIdentity
{
    public static bool IsValid(string? id) => id is { Length: > 0 }
        && char.IsAsciiLetterOrDigit(id[0])
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');
}
