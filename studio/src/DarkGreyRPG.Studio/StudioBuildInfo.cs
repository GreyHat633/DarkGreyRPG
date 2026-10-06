using System.Reflection;

namespace DarkGreyRPG.Studio;

internal static class StudioBuildInfo
{
    internal static string Version { get; } =
        typeof(StudioBuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    internal static string ProductTitle { get; } = "DarkGrey RPG Studio " + Version;
}
