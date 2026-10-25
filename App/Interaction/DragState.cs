using System;
using Avalonia;

namespace PolygonStudio.App.Interaction;

public sealed class DragState
{
    public DragTarget Target { get; private set; } = DragTarget.None;
    
    public Guid? PolygonId;
    public Guid? VertexId;
    public Guid? EdgeId;
    public int? ControlPointIndex = -1;

    private Point _lastPointerPosition;
    
    public bool IsDragging => Target != DragTarget.None;

    public void StartVertexDrag(Guid polygonId, Guid vertexId, Point pointerPosition)
    {
        Reset();
        
        Target = DragTarget.Vertex;
        PolygonId = polygonId;
        VertexId = vertexId;
        _lastPointerPosition = pointerPosition;
    }

    public void StartPolygonDrag(Guid polygonId, Point pointerPosition)
    {
        Reset();
        
        Target = DragTarget.Polygon;
        PolygonId = polygonId;
        _lastPointerPosition = pointerPosition;
    }

    public void StartControlPointDrag(Guid polygonId, Guid edgeId, int controlPointIndex, Point pointerPosition)
    {
        if (controlPointIndex is not (0 or 1))
            throw new ArgumentOutOfRangeException(nameof(controlPointIndex));
        
        Reset();

        Target = DragTarget.BezierControlPoint;
        PolygonId = polygonId;
        EdgeId = edgeId;
        ControlPointIndex = controlPointIndex;
        _lastPointerPosition = pointerPosition;
    }

    public Point MovePointer(Point pointerPosition)
    {
        var delta = pointerPosition - _lastPointerPosition;
        _lastPointerPosition = pointerPosition;
        return delta;
    }
    
    public void Reset()
    {
        Target = DragTarget.None;
        PolygonId = null;
        VertexId = null;
        EdgeId = null;
        ControlPointIndex = null;
        _lastPointerPosition = default;
    }
}

// ---

public enum DragTarget
{
    None,
    Polygon,
    Vertex,
    BezierControlPoint
}