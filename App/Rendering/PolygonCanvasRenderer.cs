using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using PolygonStudio.App.Interaction;
using PolygonStudio.App.Viewport;
using PolygonStudio.Core.Constraints;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;
using LineGeometry = PolygonStudio.Core.Model.Geometries.LineGeometry;

namespace PolygonStudio.App.Rendering;

public static class PolygonCanvasRenderer
{
    private const double VertexRadius = 5.0;
    private const double ConstraintMarkerRadius = 10.0;
    private const double AnnotationFontSize = 11.0;
    
    private static readonly Pen EdgePen = new(Brushes.Black, 2);
    private static readonly Pen SelectedEdgePen = new(Brushes.LightCoral, 2);
    private static readonly Pen VertexPen = new(Brushes.Black, 2);
    private static readonly Pen ControlPen = new(Brushes.SlateGray, 1, DashStyle.Dash);
    private static readonly Pen FixedLengthPen = new(Brushes.DarkOrange, 1.5);
    private static readonly Pen PerpendicularPen = new Pen(Brushes.RoyalBlue, 1.5);
    
    public static void Draw(
        DrawingContext context,
        Scene scene,
        ViewTransform transform,
        CanvasSelection selection,
        DragState dragState)
    {
        var annotations = BuildConstraintAnnotations(scene);

        DrawEdges(context, scene, transform, selection, annotations);
        DrawControlPoints(context, scene, transform, dragState);
        DrawVertices(context, scene, transform, selection, dragState);
    }
    
    // ---
    
    private static void DrawVertices(
        DrawingContext context, 
        Scene scene, 
        ViewTransform transform, 
        CanvasSelection selection, 
        DragState dragState)
    {
        foreach (var polygon in scene.Polygons)
        foreach (var vertex in polygon.Vertices)
        {
            var point = transform.WorldToCanvas(vertex.Position);

            bool isSelected =
                selection.Vertex?.VertexId == vertex.Id ||
                (dragState.Target == DragTarget.Vertex && dragState.VertexId == vertex.Id);

            var brush = isSelected
                ? Brushes.LightCoral
                : Brushes.White;

            context.DrawEllipse(
                brush,
                VertexPen,
                point,
                VertexRadius,
                VertexRadius
            );

            if (vertex.Continuity == ContinuityType.G0)
                continue;

            DrawText(
                context,
                vertex.Continuity.ToString(),
                new Point(point.X + VertexRadius + 4, point.Y),
                Brushes.SlateGray,
                AnnotationFontSize,
                false, true
            );
        }
    }

    private static void DrawControlPoints(
        DrawingContext context, 
        Scene scene, 
        ViewTransform transform, 
        DragState dragState)
    {
        foreach (var polygon in scene.Polygons)
        foreach (var edge in polygon.Edges)
        {
            if (edge.Geometry is not CubicBezierGeometry bezier)
                continue;

            bool isSelected =
                dragState.Target == DragTarget.BezierControlPoint &&
                dragState.EdgeId == edge.Id;

            EdgeGeometryRenderer.DrawControlPoint(
                context,
                transform.WorldToCanvas(bezier.ControlPoint1),
                isSelected && dragState.ControlPointIndex == 0
            );

            EdgeGeometryRenderer.DrawControlPoint(
                context,
                transform.WorldToCanvas(bezier.ControlPoint2),
                isSelected && dragState.ControlPointIndex == 1
            );
        }
    }

    private static void DrawEdges(
        DrawingContext context, 
        Scene scene, 
        ViewTransform transform, 
        CanvasSelection selection, 
        ConstraintAnnotations annotations)
    {
        foreach (var polygon in scene.Polygons)
        {
            var verticesById = polygon.Vertices.ToDictionary(v => v.Id);

            foreach (var edge in polygon.Edges)
            {
                if (!verticesById.TryGetValue(edge.FromVertexId, out var from) ||
                    !verticesById.TryGetValue(edge.ToVertexId, out var to))
                {
                    continue;
                }

                var start = transform.WorldToCanvas(from.Position);
                var end = transform.WorldToCanvas(to.Position);

                bool isSelected = selection.Edges.Contains(new SelectedEdge(polygon.Id, edge.Id));
                var pen = isSelected ? SelectedEdgePen : EdgePen;

                switch (edge.Geometry)
                {
                    case LineGeometry:
                        context.DrawLine(pen, start, end);
                        break;

                    case CubicBezierGeometry bezier:
                        EdgeGeometryRenderer.DrawBezier(
                            context,
                            pen,
                            ControlPen,
                            transform,
                            from.Position,
                            to.Position,
                            bezier
                        );
                        break;

                    case CircularArcGeometry arc:
                        EdgeGeometryRenderer.DrawCircularArc(
                            context,
                            pen,
                            transform,
                            from.Position,
                            to.Position,
                            arc
                        );
                        break;
                }

                var edgeKey = (polygon.Id, edge.Id);

                if (annotations.FixedLengths.TryGetValue(edgeKey, out string? fixedLength))
                    DrawFixedLengthAnnotation(context, start, end, fixedLength);

                if (annotations.Perpendiculars.TryGetValue(edgeKey, out string? perpendicularLabel))
                    DrawPerpendicularAnnotation(context, start, end, perpendicularLabel);
            }
        }
    }

    // ---

    private static void DrawFixedLengthAnnotation(
        DrawingContext context,
        Point start, Point end,
        string label)
    {
        var midpoint = new Point(
            (start.X + end.X) / 2,
            (start.Y + end.Y) / 2
        );

        context.DrawEllipse(
            Brushes.White,
            FixedLengthPen,
            midpoint,
            ConstraintMarkerRadius,
            ConstraintMarkerRadius
        );

        DrawText(
            context,
            "L",
            midpoint,
            Brushes.DarkOrange,
            AnnotationFontSize,
            true, true
        );

        DrawText(
            context,
            label,
            new Point(midpoint.X + 12, midpoint.Y),
            Brushes.DarkOrange,
            AnnotationFontSize,
            false, true
        );
    }

    private static void DrawPerpendicularAnnotation(
        DrawingContext context,
        Point start, Point end,
        string label)
    {
        var midpoint = new Point(
            (start.X + end.X) / 2,
            (start.Y + end.Y) / 2 - 18);

        context.DrawEllipse(
            Brushes.White,
            PerpendicularPen,
            midpoint,
            ConstraintMarkerRadius,
            ConstraintMarkerRadius);

        DrawText(
            context,
            "⊥",
            midpoint,
            Brushes.RoyalBlue,
            12,
            true, true
        );

        DrawText(
            context,
            label,
            new Point(midpoint.X + 12, midpoint.Y),
            Brushes.RoyalBlue,
            AnnotationFontSize,
            false, true
        );
    }
    
    private static void DrawText(
        DrawingContext context,
        string text,
        Point position,
        IBrush brush,
        double fontSize,
        bool centerHorizontally,
        bool centerVertically)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            brush
        );

        double x = centerHorizontally
            ? position.X - formatted.Width / 2
            : position.X;

        double y = centerVertically
            ? position.Y - formatted.Height / 2
            : position.Y;

        context.DrawText(formatted, new Point(x, y));
    }

    private static ConstraintAnnotations BuildConstraintAnnotations(Scene scene)
    {
        var fixedLengths = new Dictionary<(Guid, Guid), string>();
        var perpendiculars = new Dictionary<(Guid, Guid), string>();
        
        foreach (var constraint in scene.Constraints.OfType<FixedLengthConstraint>())
        {
            fixedLengths.TryAdd((constraint.PolygonId, constraint.EdgeId), constraint.Length.ToString("0.##"));
        }

        var index = 1;
        foreach (var constraint in scene.Constraints.OfType<PerpendicularConstraint>())
        {
            var label = $"P{index++}";

            perpendiculars.TryAdd((constraint.PolygonId1, constraint.EdgeId1), label);
            perpendiculars.TryAdd((constraint.PolygonId2, constraint.EdgeId2), label);
        }

        return new ConstraintAnnotations(fixedLengths, perpendiculars);
    }

    private sealed record ConstraintAnnotations(
        Dictionary<(Guid PolygonId, Guid EdgeId), string> FixedLengths,
        Dictionary<(Guid PolygonId, Guid EdgeId), string> Perpendiculars
    );
}