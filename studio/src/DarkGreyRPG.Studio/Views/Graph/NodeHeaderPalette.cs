using System.Windows.Media;

namespace DarkGreyRPG.Studio.Views.Graph;

public static class NodeHeaderPalette
{
    public static readonly Brush Task = Frozen("#F5B53D");
    public static readonly Brush Session = Frozen("#65C3AD");
    public static readonly Brush Story = Frozen("#E58A83");
    public static readonly Brush Foreground = Frozen("#202020");
    public static Brush? ForType(string? type) => type switch { "task" => Task, "session" => Session, "story" => Story, _ => null };
    private static Brush Frozen(string color) { var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!; brush.Freeze(); return brush; }
}
