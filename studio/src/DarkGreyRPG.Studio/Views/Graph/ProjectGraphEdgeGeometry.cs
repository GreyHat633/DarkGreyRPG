using System.Windows;
using System.Windows.Media;

namespace DarkGreyRPG.Studio.Views.Graph;

public sealed record ProjectGraphEdgeShape(
    PathGeometry Geometry,
    Point End,
    Point EndControl,
    Point LabelPoint,
    bool IsSelfLoop);

public static class ProjectGraphEdgeGeometry
{
    public static ProjectGraphEdgeShape Create(Rect source, Rect target, bool isSelfLoop)
    {
        if (isSelfLoop)
        {
            var start = new Point(source.Right - source.Width * .24, source.Top);
            var end = new Point(source.Right, source.Top + source.Height * .34);
            var lift = Math.Max(72, source.Height * 1.15);
            var reach = Math.Max(86, source.Width * .44);
            var firstControl = new Point(source.Right + reach * .15, source.Top - lift);
            var endControl = new Point(source.Right + reach, source.Top - lift * .2);
            var segment = new BezierSegment(firstControl, endControl, end, true);
            var geometry = new PathGeometry([new PathFigure(start, [segment], false)]);
            return new(geometry, end, endControl, new Point(source.Right + reach * .52, source.Top - lift * .56), true);
        }

        var startPoint = new Point(source.Right, source.Top + source.Height / 2);
        var endPoint = new Point(target.Left, target.Top + target.Height / 2);
        var delta = endPoint.X - startPoint.X;
        var bend = delta >= 0 ? Math.Max(45, delta * .42) : Math.Max(100, Math.Abs(delta) * .45);
        var first = new Point(startPoint.X + bend, startPoint.Y);
        var second = new Point(endPoint.X - bend, endPoint.Y);
        var path = new PathGeometry([new PathFigure(startPoint, [new BezierSegment(first, second, endPoint, true)], false)]);
        return new(path, endPoint, second, new Point((startPoint.X + endPoint.X) / 2, (startPoint.Y + endPoint.Y) / 2 - 12), false);
    }

    public static PointCollection CreateArrow(ProjectGraphEdgeShape shape, double length = 13, double halfWidth = 6)
    {
        var direction = shape.End - shape.EndControl;
        if (direction.Length <= double.Epsilon || !double.IsFinite(direction.X) || !double.IsFinite(direction.Y))
            direction = new Vector(1, 0);
        else
            direction.Normalize();
        var perpendicular = new Vector(-direction.Y, direction.X);
        return [shape.End, shape.End - direction * length + perpendicular * halfWidth, shape.End - direction * length - perpendicular * halfWidth];
    }
}
