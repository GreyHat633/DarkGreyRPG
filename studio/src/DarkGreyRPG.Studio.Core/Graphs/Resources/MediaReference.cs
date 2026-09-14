using System.Text.RegularExpressions;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Internal content-addressed runtime media path; never a user-created ID.</summary>
public static partial class MediaReference
{
    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);
    public static bool IsImage(string? value) => IsValid(value) && (value!.EndsWith(".png", StringComparison.Ordinal) || value.EndsWith(".jpg", StringComparison.Ordinal));
    public static bool IsAudio(string? value) => IsValid(value) && value!.EndsWith(".ogg", StringComparison.Ordinal);
    [GeneratedRegex(@"\Amedia/[0-9a-f]{64}\.(png|jpg|ogg)\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
