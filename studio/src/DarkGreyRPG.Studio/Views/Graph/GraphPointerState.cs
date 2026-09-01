namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>
/// The mutually exclusive pointer gestures understood by graph views.
/// </summary>
public enum GraphPointerMode
{
    Idle,
    PortPressed,
    NodeDrag,
    BoxSelect,
    WireDrag,
    Pan
}

/// <summary>
/// View-independent state machine for graph pointer gestures.
/// </summary>
public sealed class GraphPointerState
{
    /// <summary>The one currently active pointer mode.</summary>
    public GraphPointerMode Mode { get; private set; }

    /// <summary>Returns whether <paramref name="mode"/> is currently active.</summary>
    public bool Is(GraphPointerMode mode) => Mode == mode;

    /// <summary>
    /// Starts a gesture when the state is idle. A normal begin never displaces an
    /// existing gesture; callers should use <see cref="PreemptForPan"/> for that.
    /// </summary>
    public bool Begin(GraphPointerMode mode)
    {
        if (mode == GraphPointerMode.Idle || Mode != GraphPointerMode.Idle)
            return false;

        Mode = mode;
        return true;
    }

    /// <summary>
    /// Ends the active gesture only when the caller supplies that same mode.
    /// </summary>
    public bool End(GraphPointerMode expectedMode)
    {
        if (expectedMode == GraphPointerMode.Idle || Mode != expectedMode)
            return false;

        Mode = GraphPointerMode.Idle;
        return true;
    }

    /// <summary>
    /// Cancels the current gesture and returns the mode that was canceled.
    /// Calling this while idle is safe and returns <see cref="GraphPointerMode.Idle"/>.
    /// </summary>
    public GraphPointerMode Cancel()
    {
        var displacedMode = Mode;
        Mode = GraphPointerMode.Idle;
        return displacedMode;
    }

    /// <summary>
    /// Enters Pan, returning the mode that was displaced so its payload can be
    /// canceled or restored by the caller. This is the only transition that may
    /// replace a non-idle mode.
    /// </summary>
    public GraphPointerMode PreemptForPan()
    {
        var displacedMode = Mode;
        Mode = GraphPointerMode.Pan;
        return displacedMode;
    }
}
