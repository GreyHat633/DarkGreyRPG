namespace DarkGreyRPG.Studio.ViewModels.Graph;

/// <summary>Per-resource, in-session camera state. It is deliberately not serialized.</summary>
public sealed class GraphViewportState
{
    public double PanX { get; set; }
    public double PanY { get; set; }
    public double Zoom { get; set; } = 1d;
}
