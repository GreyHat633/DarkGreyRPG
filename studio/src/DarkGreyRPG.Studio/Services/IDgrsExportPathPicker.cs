namespace DarkGreyRPG.Studio.Services;

/// <summary>UI boundary for choosing the destination of an exported DGRS story package.</summary>
public interface IDgrsExportPathPicker
{
    string? PickExportPath(string storyId, string suggestedDirectory);
}

public sealed class NullDgrsExportPathPicker : IDgrsExportPathPicker
{
    public string? PickExportPath(string storyId, string suggestedDirectory) => null;
}
