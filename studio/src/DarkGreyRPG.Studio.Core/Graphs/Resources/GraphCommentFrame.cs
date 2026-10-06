namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Studio layout only. This type is never a graph node or an exported resource.</summary>
public sealed record GraphCommentFrame(string Id, string Title, double X, double Y, double Width, double Height, string[] Members)
{
    public static readonly string[] Palette = ["#5CA6CC", "#63B598", "#B68ACC", "#CC9D63", "#CC7D87", "#8EA65E"];
    public string Color { get; init; } = "#82919B";
    public bool Collapsed { get; init; }
    public static string RandomColor() => Palette[Random.Shared.Next(Palette.Length)];
    public string[] Groups { get; init; } = [];
    public bool IsValid => !string.IsNullOrWhiteSpace(Id) && Title is not null && Title.Length <= 1024
        && double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Width) && double.IsFinite(Height)
        && Width >= 80 && Height >= 50 && Members is not null;
}
