using DarkGreyRPG.Studio.Core.Actors;

namespace DarkGreyRPG.Studio.Core.Projects;

/// <summary>Current author project and its shared Actor files; graph services use the canonical store.</summary>
public sealed record ProjectSession(string ProjectDirectory, ProjectResource Project, ActorRepository Actors);
