namespace DarkGreyRPG.Studio.ViewModels;

internal static class ResourceTagsInput
{
    public static IReadOnlyList<string> Parse(string text)
        => text.Split([',', '，', ';', '；', '、', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
