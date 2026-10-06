using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Packaging;

/// <summary>Transient export diagnostics retaining the member, resource and node identities.</summary>
public sealed record StoryPackageGraphIssue(
    string StoryId,
    string StoryDisplayName,
    string ResourcePath,
    GraphResourceKind ResourceKind,
    string ResourceId,
    string ResourceDisplayName,
    string? NodeDisplayName,
    ValidationIssue Issue);
