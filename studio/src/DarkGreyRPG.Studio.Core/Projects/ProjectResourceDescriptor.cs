namespace DarkGreyRPG.Studio.Core.Projects;

/// <summary>Current author resource categories, independent of graph scope.</summary>
public enum ProjectResourceType { Story, Session, Task, Actor, Item, ItemGroup }

public sealed record ResourceDescriptor(ProjectResourceType Type, string Id, string DisplayName,
    string Path, string? HomeStoryDisplayName = null);
