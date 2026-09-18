namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Studio layout only. This type is never a graph node or an exported resource.</summary>
public sealed record GraphCommentFrame(string Id, string Title, double X, double Y, double Width, double Height, string[] Members)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Title is not null && Title.Length <= 1024
        && double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height)
        && Width >= 80 && Height >= 50 && Members is not null;
}
