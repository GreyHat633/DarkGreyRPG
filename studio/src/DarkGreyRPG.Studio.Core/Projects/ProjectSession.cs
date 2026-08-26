using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Quests;

namespace DarkGreyRPG.Studio.Core.Projects;

public sealed record ProjectSession(
    string ProjectDirectory,
    ProjectResource Project,
    ActorRepository Actors,
    StoryRepository Stories,
    DialogueRepository Dialogues,
    QuestRepository Quests,
    ProjectResourceRegistry Registry);
