using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using PolygonStudio.App.Viewport;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.App.Rendering;

public static class EdgeGeometryRenderer
{
    private const double ControlPointSize = 8.0;
    
    public static void DrawBezier(
        DrawingContext context,
        Pen curvePen, Pen controlPen,
        ViewTransform transform,
        Point2D start, Point2D end,
        CubicBezierGeometry bezier)
    {
        // Control polygon
        var p0 = transform.WorldToCanvas(start);
        var p1 = transform.WorldToCanvas(bezier.ControlPoint1);
        var p2 = transform.WorldToCanvas(bezier.ControlPoint2);
        var p3 = transform.WorldToCanvas(end);
        
        context.DrawLine(controlPen, p0, p1);
        context.DrawLine(controlPen, p1, p2);
        context.DrawLine(controlPen, p2, p3);
        
        // Curve
        DrawSampleCurve(
            context, curvePen, transform,
            EdgeGeometrySampler.Sample(bezier, start, end)
        );
    }

    public static void DrawCircularArc(
        DrawingContext context,
        Pen pen,
        ViewTransform transform,
        Point2D start, Point2D end,
        CircularArcGeometry arc)
    {
        DrawSampleCurve(
            context, pen, transform,
            EdgeGeometrySampler.Sample(arc, start, end)
        );
    }
    
    public static void DrawControlPoint(
        DrawingContext context,
        Point screenPoint,
        bool isSelected)
    {
        const double halfSize = ControlPointSize / 2.0;

        var rect = new Rect(
            screenPoint.X - halfSize,
            screenPoint.Y - halfSize,
            ControlPointSize,
            ControlPointSize
        );
        
        context.DrawRectangle(
            isSelected ? Brushes.LightBlue : Brushes.White,
            new Pen(Brushes.SlateGray, 1.5),
            rect
        );
    }

    private static void DrawSampleCurve(
        DrawingContext context,
        Pen pen,
        ViewTransform transform,
        IReadOnlyList<Point2D> points)
    {
        for (var i = 1; i < points.Count; i++)
        {
            context.DrawLine(
                pen,
                transform.WorldToCanvas(points[i - 1]),
                transform.WorldToCanvas(points[i])
            );
        }
    }
}