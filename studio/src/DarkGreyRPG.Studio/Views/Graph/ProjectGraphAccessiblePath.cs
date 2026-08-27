using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DarkGreyRPG.Studio.Views.Graph;

public sealed class ProjectGraphAccessiblePath : Shape
{
    private Geometry _data = Geometry.Empty;

    public Geometry Data
    {
        get => _data;
        set
        {
            _data = value ?? Geometry.Empty;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    protected override Geometry DefiningGeometry => Data;

    public Action? InvokeAction { get; init; }

    protected override AutomationPeer OnCreateAutomationPeer() => new ProjectGraphPathAutomationPeer(this);
}

internal sealed class ProjectGraphPathAutomationPeer(ProjectGraphAccessiblePath owner) : FrameworkElementAutomationPeer(owner), IInvokeProvider
{
    protected override string GetClassNameCore() => nameof(ProjectGraphAccessiblePath);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;

    protected override Point GetClickablePointCore()
    {
        var path = (ProjectGraphAccessiblePath)Owner;
        var flattened = path.Data.GetFlattenedPathGeometry();
        flattened.GetPointAtFractionLength(0.5, out var point, out _);
        return path.PointToScreen(point);
    }

    protected override bool IsControlElementCore() => true;

    protected override bool IsContentElementCore() => true;

    public void Invoke()
    {
        var path = (ProjectGraphAccessiblePath)Owner;
        path.Dispatcher.Invoke(() => path.InvokeAction?.Invoke());
    }

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Invoke ? this : base.GetPattern(patternInterface);
}
