namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>The three canonical graph scopes in 0.3.0.0.</summary>
public enum GraphScope
{
    StoryFlow,
    Session,
    Task,
    /// <summary>Studio-only projection; persisted as the project connection model.</summary>
    Project,

    // Short alias retained for callers that refer to the first scope as Story.
    Story = StoryFlow,
}
