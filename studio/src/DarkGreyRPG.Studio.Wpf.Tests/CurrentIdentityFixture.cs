using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Wpf.Tests;

internal static class CurrentIdentityFixture
{
    internal const string Story = "ST-2345-6789-ABCD-EFGH";
    internal static string GraphId(GraphResourceKind kind) => kind switch
    {
        GraphResourceKind.Story => Story,
        GraphResourceKind.Session => Story + "~session~resource",
        GraphResourceKind.Task => Story + "~task~resource",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
    internal static string GraphId(GraphScope scope) => GraphId(scope switch
    {
        GraphScope.Session => GraphResourceKind.Session,
        GraphScope.Task => GraphResourceKind.Task,
        _ => GraphResourceKind.Story,
    });
}
