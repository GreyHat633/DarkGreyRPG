namespace DarkGreyRPG.Studio.Core.Stories.Definitions;

/// <summary>Describes how a story node exposes its control-flow outputs.</summary>
public enum StoryNodeOutputStrategy
{
    SingleNext,
    Boolean,
    DialogueExits,
    Sequence,
    Terminal,
    LegacyPreserved,
}
