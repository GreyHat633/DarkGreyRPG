using System.Windows.Media;
using DarkGreyRPG.Studio.Core.Graphs;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>
/// Immutable presentation data for a graph connection. Keeping this mapping
/// separate from the WPF Path lets every graph scope share the same wire
/// language without coupling validation or persistence to a visual element.
/// </summary>
public sealed record GraphConnectionVisualStyle(
    GraphInterfaceKind InterfaceKind,
    Color StrokeColor,
    double StrokeThickness,
    string AutomationLabel)
{
    public Color Color => StrokeColor;
    public Color Stroke => StrokeColor;
    public string AutomationName => AutomationLabel;
    public Color NormalStrokeColor => InterfaceKind == GraphInterfaceKind.Flow ? FlowNormalColor : LogicNormalColor;
    public Color SelectedStrokeColor => InterfaceKind == GraphInterfaceKind.Flow ? FlowSelectedColor : LogicSelectedColor;
    public double NormalStrokeThickness => InterfaceKind == GraphInterfaceKind.Flow ? 3 : 2;
    public double SelectedStrokeThickness => InterfaceKind == GraphInterfaceKind.Flow ? 4 : 3;

    public static GraphConnectionVisualStyle For(GraphInterfaceKind kind, bool selected = false) =>
        kind switch
        {
            GraphInterfaceKind.Flow => selected
                ? new(kind, FlowSelectedColor, 4, "Flow 连接")
                : new(kind, FlowNormalColor, 3, "Flow 连接"),
            GraphInterfaceKind.Logic => selected
                ? new(kind, LogicSelectedColor, 3, "Logic 连接")
                : new(kind, LogicNormalColor, 2, "Logic 连接"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown graph interface kind."),
        };

    public static GraphConnectionVisualStyle Get(GraphInterfaceKind kind, bool selected = false) => For(kind, selected);
    public static GraphConnectionVisualStyle ForKind(GraphInterfaceKind kind, bool selected = false) => For(kind, selected);

    // Keep the palette centralized so normal and selected variants cannot drift
    // between Story Flow, Session, and Task graph renderers.
    public static readonly Color FlowNormalColor = Color.FromRgb(108, 177, 255);
    public static readonly Color FlowSelectedColor = Color.FromRgb(255, 255, 255);
    public static readonly Color LogicNormalColor = Color.FromRgb(245, 181, 61);
    public static readonly Color LogicSelectedColor = Color.FromRgb(255, 224, 138);
}
