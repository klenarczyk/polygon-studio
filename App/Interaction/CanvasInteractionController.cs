using System;
using Avalonia;
using Avalonia.Input;
using PolygonStudio.App.Viewport;
using PolygonStudio.Core.Services;

namespace PolygonStudio.App.Interaction;

public sealed class CanvasInteractionController
{
    public CanvasSelection Selection { get; } = new();
    public DragState Drag { get; } = new();
    
    public bool IsPanning { get; private set; }
    
    private Point _lastPanPosition;
    
    public bool HandlePointerPressed(
        Point position,
        CanvasHitTester hitTester,
        KeyModifiers modifiers)
    {
        Drag.Reset();
        
        bool ctrlPressed = modifiers.HasFlag(KeyModifiers.Control);
        if (!ctrlPressed) Selection.Clear();
        
        var controlPoint = hitTester.FindControlPointAt(position);
        if (controlPoint is { } cp)
        {
            Drag.StartControlPointDrag(
                cp.Polygon.Id,
                cp.Edge.Id,
                cp.Index,
                position
            );
            
            return true;
        }
        
        var vertex = hitTester.FindVertexAt(position);
        if (vertex is { } v)
        {
            Selection.SelectVertex(v.Polygon.Id, v.Vertex.Id);
            
            Drag.StartVertexDrag(
                v.Polygon.Id,
                v.Vertex.Id,
                position
            );
            
            return true;
        }
        
        var edge = hitTester.FindEdgeAt(position);
        if (edge is { } e)
        {
            if (ctrlPressed)
            {
                Selection.ToggleEdge(e.Polygon.Id, e.Edge.Id);
                return true;
            }

            Selection.SelectEdge(e.Polygon.Id, e.Edge.Id);
            Drag.StartPolygonDrag(e.Polygon.Id, position);
            return true;
        }
        
        Selection.Clear();
        return false;
    }

    public void HandlePointerMoved(
        Point position,
        ViewTransform transform,
        EditorService editor,
        Action invalidate)
    {
        if (!Drag.IsDragging) 
            return;

        var worldPosition = transform.CanvasToWorld(position);
        
        var canvasDelta = Drag.MovePointer(position);
        var worldDelta = transform.CanvasDeltaToWorld(canvasDelta);

        switch (Drag.Target)
        {
            case DragTarget.Vertex:
            {
                if (Drag is { PolygonId: { } polygonId, VertexId: { } vertexId })
                    editor.MoveVertex(polygonId, vertexId, worldPosition);
                break;
            }
            
            case DragTarget.Polygon:
            {
                if (Drag.PolygonId is { } polygonId)
                    editor.MovePolygon(polygonId, worldDelta);
                break;
            }

            case DragTarget.BezierControlPoint:
            {
                if (Drag is { PolygonId: { } polygonId, EdgeId: { } edgeId, ControlPointIndex: { } idx})
                    editor.MoveBezierControlPoint(polygonId, edgeId, idx, worldPosition);
                break;
            }
        }

        invalidate();
    }

    public void HandlePointerReleased(Action invalidate)
    {
        if (!Drag.IsDragging)
            return;

        Drag.Reset();
        invalidate();
    }
    
    public void HandlePointerWheelChanged(
        Point position,
        double wheelDelta,
        CanvasViewport viewport)
    {
        if (wheelDelta == 0)
            return;

        double factor = Math.Pow(1.1, wheelDelta);
        viewport.ZoomAt(position, factor);
    }
    
    public void Reset()
    {
        Selection.Clear();
        Drag.Reset();
    }
    
    // ---
    
    public void SelectVertexForContextMenu(Guid polygonId, Guid vertexId)
    {
        Selection.SelectVertex(polygonId, vertexId);
        Drag.Reset();
    }

    public void SelectEdgeForContextMenu(Guid polygonId, Guid edgeId)
    {
        Drag.Reset();

        if (Selection.IsEdgeSelected(polygonId, edgeId))
            return;
        
        Selection.SelectEdge(polygonId, edgeId);
    }
    
    // ---
    
    public bool TryBeginPan(Point position, KeyModifiers modifiers)
    {
        if (!modifiers.HasFlag(KeyModifiers.Shift))
            return false;

        Drag.Reset();
        IsPanning = true;
        _lastPanPosition = position;
        return true;
    }

    public bool HandlePanPointerMoved(Point position, CanvasViewport viewport, Action invalidate)
    {
        if (!IsPanning) return false;

        var delta = position - _lastPanPosition;
        viewport.PanBy(new Point(delta.X, delta.Y));
        _lastPanPosition = position;

        invalidate();
        return true;
    }

    public bool EndPan()
    {
        if (!IsPanning) return false;

        IsPanning = false;
        return true;
    }
}