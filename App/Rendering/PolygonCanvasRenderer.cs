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
    private const double ConstraintMarkerSpacing = 30.0;
    
    private static readonly Pen EdgePen = new(Brushes.Black, 2);
    private static readonly Pen SelectedEdgePen = new(Brushes.LightBlue, 2);
    private static readonly Pen VertexPen = new(Brushes.Black, 2);
    private static readonly Pen ControlPen = new(Brushes.SlateGray, 1, DashStyle.Dash);
    private static readonly Pen FixedLengthPen = new(Brushes.LightCoral, 1.5);
    private static readonly Pen PerpendicularPen = new(Brushes.RoyalBlue, 1.5);
    
    public static void Draw(
        DrawingContext context,
        Scene scene,
        ViewTransform transform,
        CanvasSelection selection,
        DragState dragState,
        LineDrawingAlgorithm lineDrawingAlgorithm)
    {
        var annotations = BuildConstraintAnnotations(scene);

        DrawEdges(context, scene, transform, selection, annotations, lineDrawingAlgorithm);
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
                ? Brushes.LightBlue
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
                new Point(point.X + VertexRadius + 8, point.Y),
                Brushes.SlateGray,
                AnnotationFontSize
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
        ConstraintAnnotations annotations,
        LineDrawingAlgorithm lineDrawingAlgorithm)
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
                        EdgeGeometryRenderer.DrawLine(
                            context,
                            pen,
                            start, end,
                            lineDrawingAlgorithm
                        );
                        break;

                    case CubicBezierGeometry bezier:
                        EdgeGeometryRenderer.DrawBezier(
                            context,
                            pen,
                            ControlPen,
                            transform,
                            from.Position,
                            to.Position,
                            bezier,
                            lineDrawingAlgorithm
                        );
                        break;

                    case CircularArcGeometry arc:
                        EdgeGeometryRenderer.DrawCircularArc(
                            context,
                            pen,
                            transform,
                            from.Position,
                            to.Position,
                            arc,
                            lineDrawingAlgorithm
                        );
                        break;
                }

                var edgeKey = (polygon.Id, edge.Id);

                if (annotations.ByEdge.TryGetValue(edgeKey, out var edgeAnnotations))
                    DrawEdgeAnnotations(context, start, end, edgeAnnotations);
            }
        }
    }

    // ---

    private static void DrawEdgeAnnotations(
        DrawingContext context,
        Point start, Point end,
        List<EdgeAnnotation> annotations)
    {
        if (annotations.Count == 0) return;

        var midpoint = new Point(
            (start.X + end.X) / 2,
            (start.Y + end.Y) / 2
        );

        double totalWidth = (annotations.Count - 1) * ConstraintMarkerSpacing;
        double startX = midpoint.X - totalWidth / 2;

        for (int i = 0; i < annotations.Count; i++)
        {
            var markerPos = new Point(startX + i * ConstraintMarkerSpacing, midpoint.Y);
            var annotation = annotations[i];

            switch (annotation.Type)
            {
                case ConstraintType.FixedLength:
                    DrawFixedLengthMarker(context, markerPos, annotation.Label);
                    break;
                
                case ConstraintType.Perpendicular:
                    DrawPerpendicularMarker(context, markerPos, annotation.Label);
                    break;
            }
        }
    }
    
    private static void DrawFixedLengthMarker(DrawingContext context, Point position, string label)
    {
        context.DrawEllipse(
            Brushes.White,
            FixedLengthPen,
            position,
            ConstraintMarkerRadius,
            ConstraintMarkerRadius
        );

        DrawText(
            context,
            "L",
            position,
            Brushes.LightCoral,
            AnnotationFontSize
        );

        DrawText(
            context,
            label,
            new Point(position.X, position.Y + ConstraintMarkerRadius + 8),
            Brushes.LightCoral,
            AnnotationFontSize
        );
    }

    private static void DrawPerpendicularMarker(DrawingContext context, Point position, string label)
    {
        context.DrawEllipse(
            Brushes.White,
            PerpendicularPen,
            position,
            ConstraintMarkerRadius,
            ConstraintMarkerRadius
        );

        DrawText(
            context,
            "⊥",
            position,
            Brushes.RoyalBlue,
            12
        );

        DrawText(
            context,
            label,
            new Point(position.X, position.Y + ConstraintMarkerRadius + 8),
            Brushes.RoyalBlue,
            AnnotationFontSize
        );
    }
    
    private static void DrawText(
        DrawingContext context,
        string text,
        Point position,
        IBrush brush,
        double fontSize)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            fontSize,
            brush
        );

        double x = position.X - formatted.Width / 2;
        double y = position.Y - formatted.Height / 2;

        context.DrawText(formatted, new Point(x, y));
    }

    private static ConstraintAnnotations BuildConstraintAnnotations(Scene scene)
    {
        var byEdge = new Dictionary<(Guid PolygonId, Guid EdgeId), List<EdgeAnnotation>>();

        foreach (var constraint in scene.Constraints.OfType<FixedLengthConstraint>())
        {
            AddAnnotation(
                constraint.PolygonId, 
                constraint.EdgeId, 
                new EdgeAnnotation(ConstraintType.FixedLength, constraint.Length.ToString("0.##"))
            );
        }

        var index = 1;
        foreach (var constraint in scene.Constraints.OfType<PerpendicularConstraint>())
        {
            var label = $"P{index++}";

            AddAnnotation(
                constraint.PolygonId1, 
                constraint.EdgeId1, 
                new EdgeAnnotation(ConstraintType.Perpendicular, label)
            );
            
            AddAnnotation(
                constraint.PolygonId2, 
                constraint.EdgeId2, 
                new EdgeAnnotation(ConstraintType.Perpendicular, label)
            );
        }

        return new ConstraintAnnotations(byEdge);

        void AddAnnotation(Guid polygonId, Guid edgeId, EdgeAnnotation annotation)
        {
            var key = (polygonId, edgeId);
            if (!byEdge.TryGetValue(key, out var list))
            {
                list = [];
                byEdge[key] = list;
            }
            
            list.Add(annotation);
        }
    }

    private enum ConstraintType
    {
        FixedLength, Perpendicular
    }

    private sealed record EdgeAnnotation(ConstraintType Type, string Label);

    private sealed record ConstraintAnnotations(
        Dictionary<(Guid PolygonId, Guid EdgeId), List<EdgeAnnotation>> ByEdge
    );
}