using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Core.Projects;

/// <summary>Rejects retired author data without interpreting, migrating or modifying it.</summary>
public static class ProjectAuthorDataBoundary
{
    public static string? FindRetiredDirectory(string projectDirectory)
    {
        foreach (var name in new[] { "stories", "dialogues", "quests" })
        {
            var path = Path.Combine(projectDirectory, name);
            if (Directory.Exists(path) && CanonicalResourceFileSystem.EnumerateJsonFiles(path).Count != 0)
                return name;
        }
        return null;
    }
}
