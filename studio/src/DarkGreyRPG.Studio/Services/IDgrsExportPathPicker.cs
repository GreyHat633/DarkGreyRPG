namespace DarkGreyRPG.Studio.Services;

/// <summary>UI boundary for choosing the destination of an exported DGRS story package.</summary>
public interface IDgrsExportPathPicker
{
    string? PickExportPath(string displayName, string suggestedDirectory);
    string? PickGroupExportPath(string displayName, string suggestedDirectory) => null;
}

public sealed class NullDgrsExportPathPicker : IDgrsExportPathPicker
{
    public string? PickExportPath(string displayName, string suggestedDirectory) => null;
}
