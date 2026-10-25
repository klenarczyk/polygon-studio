using System;
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
    
    public static void DrawLine(
        DrawingContext context,
        Pen pen,
        Point start,
        Point end,
        LineDrawingAlgorithm algorithm)
    {
        switch (algorithm)
        {
            case LineDrawingAlgorithm.Avalonia:
                context.DrawLine(pen, start, end);
                break;

            case LineDrawingAlgorithm.Bresenham:
                BresenhamLineRenderer.Draw(context, pen, start, end);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm));
        }
    }
    
    public static void DrawBezier(
        DrawingContext context,
        Pen curvePen, Pen controlPen,
        ViewTransform transform,
        Point2D start, Point2D end,
        CubicBezierGeometry bezier,
        LineDrawingAlgorithm algorithm)
    {
        // Control polygon
        var p0 = transform.WorldToCanvas(start);
        var p1 = transform.WorldToCanvas(bezier.ControlPoint1);
        var p2 = transform.WorldToCanvas(bezier.ControlPoint2);
        var p3 = transform.WorldToCanvas(end);
        
        DrawLine(context, controlPen, p0, p1, algorithm);
        DrawLine(context, controlPen, p1, p2, algorithm);
        DrawLine(context, controlPen, p2, p3, algorithm);
        
        // Curve
        DrawSampleCurve(
            context, curvePen, transform,
            EdgeGeometrySampler.Sample(bezier, start, end),
            algorithm
        );
    }

    public static void DrawCircularArc(
        DrawingContext context,
        Pen pen,
        ViewTransform transform,
        Point2D start, Point2D end,
        CircularArcGeometry arc,
        LineDrawingAlgorithm algorithm)
    {
        DrawSampleCurve(
            context, pen, transform,
            EdgeGeometrySampler.Sample(arc, start, end),
            algorithm
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
        IEnumerable<Point2D> points,
        LineDrawingAlgorithm algorithm)
    {
        Point2D? prev = null;
        
        foreach (var point in points)
        {
            if (prev is null)
            {
                prev = point;
                continue;
            }
            
            DrawLine(
                context,
                pen,
                transform.WorldToCanvas(prev.Value),
                transform.WorldToCanvas(point),
                algorithm
            );

            prev = point;
        }
    }
}