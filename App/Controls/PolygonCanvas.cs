using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;
using PolygonStudio.Core.Services;
using LineGeometry = PolygonStudio.Core.Model.Geometries.LineGeometry;

namespace PolygonStudio.App.Controls;

public sealed class PolygonCanvas : Control
{
    private const double VertexRadius = 5.0;
    private const double ControlPointSize = 8.0;
    private const double HitRadius = 10.0;
    private const double Padding = 30.0;

    private Guid? _draggedPolygonId;
    private Guid? _draggedVertexId;
    
    private ViewTransform? _viewTransform;
    
    public Scene? Scene { get; set; }
    public EditorService? Editor { get; set; }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));
        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        
        EnsureViewTransform();
        if (_viewTransform is not { } transform) return;
        
        var edgePen = new Pen(Brushes.Black, 2);
        var vertexPen = new Pen(Brushes.Black, 2);
        var controlPen = new Pen(Brushes.SlateGray, 1, DashStyle.Dash);
        
        // Edges
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                var from = polygon.Vertices.FirstOrDefault(v => v.Id == edge.FromVertexId);
                var to = polygon.Vertices.FirstOrDefault(v => v.Id == edge.ToVertexId);
                
                if (from is null || to is null) continue;

                var start = transform.ToScreen(from.Position);
                var end = transform.ToScreen(to.Position);
                
                switch (edge.Geometry) // TODO: Add an edge drawing interface later on
                {
                    case LineGeometry:
                        context.DrawLine(edgePen, start, end);
                        break;
                    
                    case CubicBezierGeometry bezier:
                        DrawBezier(
                            context,
                            edgePen, controlPen,
                            transform,
                            from.Position, to.Position,
                            bezier);
                        break;
                }
            }
        }
        
        // Control points
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                if (edge.Geometry is not CubicBezierGeometry bezier) // TODO: Find a more elegant solution for this
                    continue;
                
                DrawControlPoint(
                    context,
                    transform.ToScreen(bezier.ControlPoint1),
                    false);
                
                DrawControlPoint(
                    context,
                    transform.ToScreen(bezier.ControlPoint2),
                    false);
            }
        }
        
        // Vertices
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                var point = transform.ToScreen(vertex.Position);
                bool isSelected = _draggedVertexId == vertex.Id;
                
                context.DrawEllipse(
                    isSelected ? Brushes.LightBlue : Brushes.White,
                    vertexPen,
                    point,
                    VertexRadius,
                    VertexRadius);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Scene is null || Editor is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        
        var pointerPos = e.GetPosition(this);
        
        EnsureViewTransform();
        if (_viewTransform is not { } transform) return;

        double bestDistSquared = HitRadius * HitRadius;
        Guid? foundPolygonId = null;
        Guid? foundVertexId = null;

        foreach (var polygon in Scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                var screenPos = transform.ToScreen(vertex.Position);
                double dx = screenPos.X - pointerPos.X;
                double dy = screenPos.Y - pointerPos.Y;
                double distSquared = dx * dx + dy * dy;

                if (!(distSquared < bestDistSquared)) continue;
                
                bestDistSquared = distSquared;
                foundPolygonId = polygon.Id;
                foundVertexId = vertex.Id;
            }
        }

        if (foundPolygonId is null || foundVertexId is null) return;

        _draggedPolygonId = foundPolygonId;
        _draggedVertexId = foundVertexId;
        
        e.Pointer.Capture(this);
        e.Handled = true;
        
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_draggedPolygonId is not { } polygonId ||
            _draggedVertexId is not { } vertexId ||
            _viewTransform is not { } transform ||
            Editor is null)
        {
            return;
        }

        var screenPos = e.GetPosition(this);
        var worldPos = transform.ToWorld(screenPos);
        
        if (Editor.MoveVertex(polygonId, vertexId, worldPos))
            InvalidateVisual();

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_draggedVertexId is null) return;

        e.Pointer.Capture(null);

        _draggedPolygonId = null;
        _draggedVertexId = null;
        
        InvalidateVisual();
        e.Handled = true;
    }

    // ---

    private void EnsureViewTransform()
    {
        if (_viewTransform is not null)
            return;

        if (Scene is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        _viewTransform = CreateTransform();
    }
    
    private ViewTransform CreateTransform()
    {
        if (Scene is null)
            return new ViewTransform(1, 0, 0, 0, 0);

        var vertices = Scene.Polygons
            .SelectMany(p => p.Vertices)
            .ToList();

        if (vertices.Count == 0)
            return new ViewTransform(1, 0, 0, 0, 0);

        double minX = vertices.Min(v => v.Position.X);
        double maxX = vertices.Max(v => v.Position.X);
        double minY = vertices.Min(v => v.Position.Y);
        double maxY = vertices.Max(v => v.Position.Y);

        double worldWidth = Math.Max(maxX - minX, 1e-6);
        double worldHeight = Math.Max(maxY - minY, 1e-6);

        double availableWidth = Math.Max(Bounds.Width - 2 * Padding, 1);
        double availableHeight = Math.Max(Bounds.Height - 2 * Padding, 1);

        double scale = Math.Min(
            availableWidth / worldWidth,
            availableHeight / worldHeight);

        double drawnWidth = worldWidth * scale;
        double drawnHeight = worldHeight * scale;

        double left = (Bounds.Width - drawnWidth) / 2;
        double top = (Bounds.Height - drawnHeight) / 2;

        return new ViewTransform(scale, left, top, minX, maxY);
    }
    
    private readonly record struct ViewTransform(
        double Scale,   // Screen / World
        double Left,    // Horizontal offset
        double Top,     // Vertical offset
        double MinX,    // Min world x
        double MaxY)    // Max world y
    {
        public Point ToScreen(Point2D point) =>
            new(
                Left + (point.X - MinX) * Scale,
                Top + (MaxY - point.Y) * Scale
            );

        public Point2D ToWorld(Point point) =>
            new(
                MinX + (point.X - Left) / Scale,
                MaxY - (point.Y - Top) / Scale
            );
    }
    
    // TODO: Move out of PolygonCanvas
    private static void DrawBezier(
        DrawingContext context,
        Pen curvePen, Pen controlPen,
        ViewTransform transform,
        Point2D start, Point2D end,
        CubicBezierGeometry bezier)
    {
        // Control polygon
        var p0 = transform.ToScreen(start);
        var p1 = transform.ToScreen(bezier.ControlPoint1);
        var p2 = transform.ToScreen(bezier.ControlPoint2);
        var p3 = transform.ToScreen(end);
        
        context.DrawLine(controlPen, p0, p1);
        context.DrawLine(controlPen, p1, p2);
        context.DrawLine(controlPen, p2, p3);
        context.DrawLine(controlPen, p3, p0);
        
        // Curve
        const int steps = 64;
        var previous = p0;

        for (var i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double u = 1.0 - t;

            double x =
                u * u * u * start.X +
                3 * u * u * t * bezier.ControlPoint1.X +
                3 * u * t * t * bezier.ControlPoint2.X +
                t * t * t * end.X;

            double y =
                u * u * u * start.Y +
                3 * u * u * t * bezier.ControlPoint1.Y +
                3 * u * t * t * bezier.ControlPoint2.Y +
                t * t * t * end.Y;

            Point current = transform.ToScreen(new Point2D(x, y));
            context.DrawLine(curvePen, previous, current);
            previous = current;
        }
    }

    private static void DrawControlPoint(
        DrawingContext context,
        Point screenPoint,
        bool isSelected)
    {
        const double halfSize = ControlPointSize / 2.0;

        var rect = new Rect(
            screenPoint.X - halfSize,
            screenPoint.Y - halfSize,
            ControlPointSize,
            ControlPointSize);
        
        context.DrawRectangle(
            isSelected ? Brushes.LightBlue : Brushes.White,
            new Pen(Brushes.SlateGray, 1.5),
            rect);
    }
}