using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PolygonStudio.App.Controls.Canvas;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;
using PolygonStudio.Core.Services;
using LineGeometry = PolygonStudio.Core.Model.Geometries.LineGeometry;

namespace PolygonStudio.App.Controls;

public sealed class PolygonCanvas : Control
{
    private const double VertexRadius = 5.0;
    private const double HitRadius = 10.0;
    
    private const double Padding = 30.0;

    private Guid? _selectedPolygonId;
    private Guid? _selectedVertexId;
    private Guid? _selectedEdgeId;
    
    private DragTarget _dragTarget = DragTarget.None;
    private Guid? _draggedPolygonId;
    private Guid? _draggedVertexId;
    private Guid? _draggedEdgeId;
    private int _draggedControlPointIdx = -1;
    
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
        var selectedEdgePen = new Pen(Brushes.LightCoral, 2);
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

                bool isSelected = polygon.Id == _selectedPolygonId &&
                                  edge.Id == _selectedEdgeId;

                var currentEdgePen = isSelected ? selectedEdgePen : edgePen;
                
                switch (edge.Geometry)
                {
                    case LineGeometry:
                        context.DrawLine(currentEdgePen, start, end);
                        break;
                    
                    case CubicBezierGeometry bezier:
                        EdgeGeometryRenderer.DrawBezier(
                            context,
                            currentEdgePen, controlPen,
                            transform,
                            from.Position, to.Position,
                            bezier);
                        break;
                    
                    case CircularArcGeometry arc:
                        EdgeGeometryRenderer.DrawCircularArc(
                            context,
                            currentEdgePen,
                            transform,
                            from.Position, to.Position,
                            arc);
                        break;
                }
            }
        }
        
        // Control points
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                if (edge.Geometry is not CubicBezierGeometry bezier)
                    continue;

                bool isControlSelected = _dragTarget == DragTarget.BezierControlPoint &&
                                         _draggedEdgeId == edge.Id;
                
                EdgeGeometryRenderer.DrawControlPoint(
                    context,
                    transform.ToScreen(bezier.ControlPoint1),
                    isControlSelected && _draggedControlPointIdx == 0);
                
                EdgeGeometryRenderer.DrawControlPoint(
                    context,
                    transform.ToScreen(bezier.ControlPoint2),
                    isControlSelected && _draggedControlPointIdx == 1);
            }
        }
        
        // Vertices
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                var point = transform.ToScreen(vertex.Position);
                bool isSelected = _selectedVertexId == vertex.Id || _draggedVertexId == vertex.Id;
                
                var currentVertexBrush = isSelected 
                    ? Brushes.LightCoral
                    : Brushes.White;
                
                context.DrawEllipse(
                    currentVertexBrush,
                    vertexPen,
                    point,
                    VertexRadius,
                    VertexRadius);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            HandleRightClick(e);
            return;
        }
        
        if (_selectedVertexId is not null || _selectedEdgeId is not null)
        {
            _selectedPolygonId = null;
            _selectedVertexId = null;
            _selectedEdgeId = null;
            InvalidateVisual();
        }
        
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

        if (foundPolygonId is not null && foundVertexId is not null)
        {
            _dragTarget = DragTarget.Vertex;
            _draggedPolygonId = foundPolygonId;
            _draggedVertexId = foundVertexId;
            _draggedEdgeId = null;
            _draggedControlPointIdx = -1;
        
            e.Pointer.Capture(this);
            e.Handled = true;
        
            InvalidateVisual();
        }
        
        foreach (var polygon in Scene.Polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                if (edge.Geometry is not CubicBezierGeometry bezier)
                    continue;

                Point[] controlPoints =
                [
                    transform.ToScreen(bezier.ControlPoint1),
                    transform.ToScreen(bezier.ControlPoint2)
                ];

                for (var i = 0; i < controlPoints.Length; i++)
                {
                    var point = controlPoints[i];

                    double dx = point.X - pointerPos.X;
                    double dy = point.Y - pointerPos.Y;

                    if (dx * dx + dy * dy > HitRadius * HitRadius)
                        continue;

                    _dragTarget = DragTarget.BezierControlPoint;
                    _draggedPolygonId = polygon.Id;
                    _draggedEdgeId = edge.Id;
                    _draggedControlPointIdx = i;
                    _draggedVertexId = null;

                    e.Pointer.Capture(this);
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }
            }
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragTarget == DragTarget.None ||
            _draggedPolygonId is not { } polygonId ||
            _viewTransform is not { } transform ||
            Editor is null)
        {
            return;
        }

        var screenPos = e.GetPosition(this);
        var worldPos = transform.ToWorld(screenPos);
        
        bool changed = _dragTarget switch
        {
            DragTarget.Vertex when _draggedVertexId is { } vertexId =>
                Editor.MoveVertex(polygonId, vertexId, worldPos),

            DragTarget.BezierControlPoint when _draggedEdgeId is { } edgeId =>
                Editor.MoveBezierControlPoint(
                    polygonId,
                    edgeId,
                    _draggedControlPointIdx,
                    worldPos),

            _ => false
        };

        if (changed)
            InvalidateVisual();

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_dragTarget == DragTarget.None) return;

        e.Pointer.Capture(null);

        _dragTarget = DragTarget.None;
        _draggedPolygonId = null;
        _draggedVertexId = null;
        _draggedEdgeId = null;
        _draggedControlPointIdx = -1;
        
        InvalidateVisual();
        e.Handled = true;
    }
    
    // ---

    private void HandleRightClick(PointerPressedEventArgs e)
    {
        if (Scene is null || Editor is null || _viewTransform is not { } transform)
            return;

        var hitTester = new CanvasHitTester(Scene, transform);
        var pointerPos = e.GetPosition(this);
        
        if (_selectedVertexId is not null || _selectedEdgeId is not null)
        {
            _selectedPolygonId = null;
            _selectedVertexId = null;
            _selectedEdgeId = null;
            InvalidateVisual();
        }
        
        var vertexHit = hitTester.FindVertexAt(pointerPos);
        if (vertexHit is { } vertexRes)
        {
            _selectedPolygonId = vertexRes.Polygon.Id;
            _selectedVertexId = vertexRes.Vertex.Id;
            
            InvalidateVisual();
            
            VertexContextMenu.Show(
                this,
                Editor,
                vertexRes.Polygon.Id, vertexRes.Vertex.Id,
                InvalidateVisual);
            
            e.Handled = true;
            return;
        }
        
        var edgeHit = hitTester.FindEdgeAt(pointerPos);
        if (edgeHit is { } edgeRes)
        {
            _selectedPolygonId = edgeRes.Polygon.Id;
            _selectedEdgeId = edgeRes.Edge.Id;
            
            InvalidateVisual();
            
            EdgeContextMenu.Show(
                this,
                Editor,
                edgeRes.Polygon.Id, edgeRes.Edge.Id,
                edgeRes.Edge.Geometry,
                InvalidateVisual
            );   
        }
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
    
    // ---

    private enum DragTarget
    {
        None,
        Vertex,
        BezierControlPoint
    }
}