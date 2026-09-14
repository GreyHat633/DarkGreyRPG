using System.Reflection;

namespace DarkGreyRPG.Studio;

internal static class StudioBuildInfo
{
    internal static string ProductTitle { get; } = "DarkGrey RPG Studio " +
        typeof(StudioBuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
}
