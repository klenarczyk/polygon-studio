using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Services;

namespace PolygonStudio.App.Controls;

public sealed class PolygonCanvas : Control
{
    private const double VertexRadius = 5.0;
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
        
        // Edges
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                var from = polygon.Vertices.FirstOrDefault(v => v.Id == edge.FromVertexId);
                var to = polygon.Vertices.FirstOrDefault(v => v.Id == edge.ToVertexId);
                
                if (from is null || to is null) continue;
                
                context.DrawLine(
                    edgePen, 
                    transform.ToScreen(from.Position), 
                    transform.ToScreen(to.Position)
                );
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
}