using System;
using System.Collections.Generic;
using System.Linq;

namespace PolygonStudio.App.Interaction;

public sealed class CanvasSelection
{
    public SelectedVertex? Vertex { get; private set; }

    private readonly HashSet<SelectedEdge> _edges = [];
    public IReadOnlySet<SelectedEdge> Edges => _edges;

    public void SelectVertex(Guid polygonId, Guid vertexId)
    {
        Vertex = new SelectedVertex(polygonId, vertexId);
        _edges.Clear();
    }

    public void SelectEdge(Guid polygonId, Guid edgeId)
    {
        Vertex = null;
        _edges.Clear();
        
        _edges.Add(new SelectedEdge(polygonId, edgeId));
    }

    public void ToggleEdge(Guid polygonId, Guid edgeId)
    {
        Vertex = null;
        
        var edge = new SelectedEdge(polygonId, edgeId);
        
        if (_edges.Remove(edge))
            return;

        while (_edges.Count >= 2)
        {
            _edges.Remove(_edges.First());
        }

        _edges.Add(edge);
    }
    
    public bool IsEdgeSelected(Guid polygonId, Guid edgeId)
        => _edges.Contains(new SelectedEdge(polygonId, edgeId));

    public void Clear()
    {
        Vertex = null;
        _edges.Clear();
    }
    
    public void RemoveEdgesForPolygon(Guid polygonId) =>
        _edges.RemoveWhere(e => e.PolygonId == polygonId);
    
    public void RemoveEdge(Guid polygonId, Guid edgeId) => 
        _edges.Remove(new SelectedEdge(polygonId, edgeId));
}

// ---

public readonly record struct SelectedVertex(
    Guid PolygonId,
    Guid VertexId
);

public readonly record struct SelectedEdge(
    Guid PolygonId,
    Guid EdgeId
);