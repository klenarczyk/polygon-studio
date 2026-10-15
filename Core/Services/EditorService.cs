using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;
using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Services;

public sealed class EditorService(Scene scene)
{
    public Scene Scene { get; } = scene;

    public bool MoveVertex(Guid polygonId, Guid vertexId, Point2D newPosition)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var vertex = polygon?.Vertices.FirstOrDefault(v => v.Id == vertexId);
        
        if (vertex is null) return false;
        
        vertex.Position = newPosition;
        return true;
    }

    public bool MoveBezierControlPoint(
        Guid polygonId,
        Guid edgeId,
        int controlPointIndex,
        Point2D newPosition)
    {
        if (controlPointIndex is not (0 or 1)) return false;
        
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var edge = polygon?.Edges.FirstOrDefault(e => e.Id == edgeId);
        
        if (edge?.Geometry is not CubicBezierGeometry bezier) 
            return false;

        edge.Geometry = bezier.WithControlPoint(controlPointIndex, newPosition);
        return true;
    }

    public bool SplitEdge(Guid polygonId, Guid edgeId)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var edge = polygon?.Edges.FirstOrDefault(e => e.Id == edgeId);

        if (polygon is null || edge is null) return false;
        
        var start = polygon.Vertices.First(v => v.Id == edge.FromVertexId).Position;
        var end = polygon.Vertices.First(v => v.Id == edge.ToVertexId).Position;
        
        var newVertexPosition = new Point2D((end.X + start.X) / 2, (end.Y + start.Y) / 2);
        var newVertex = new Vertex(newVertexPosition);
        
        polygon.AddVertex(newVertex);

        var edge1 = new Edge(edge.FromVertexId, newVertex.Id);
        var edge2 = new Edge(newVertex.Id, edge.ToVertexId);
        
        polygon.AddEdge(edge1);
        polygon.AddEdge(edge2);
        polygon.RemoveEdge(edge.Id);
        
        return true;
    }
    
    public bool SetEdgeGeometry(Guid polygonId, Guid edgeId, Type geometryType)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var edge = polygon?.Edges.FirstOrDefault(e => e.Id == edgeId);

        if (polygon is null || edge is null) return false;

        if (geometryType == typeof(CubicBezierGeometry) && edge.Geometry is CubicBezierGeometry)
            return true;

        if (geometryType == typeof(CircularArcGeometry) && edge.Geometry is CircularArcGeometry)
            return true;

        var start = polygon.Vertices.First(v => v.Id == edge.FromVertexId).Position;
        var end = polygon.Vertices.First(v => v.Id == edge.ToVertexId).Position;

        edge.Geometry = EdgeGeometryFactory.CreateDefault(start, end, geometryType);
        return true;
    }
    
    public bool RemoveVertex(Guid polygonId, Guid vertexId)
    {
        var polygon = Scene.Polygons.FirstOrDefault(p => p.Id == polygonId);
        var vertex = polygon?.Vertices.FirstOrDefault(v => v.Id == vertexId);

        if (polygon is null || vertex is null) 
            return false;

        if (polygon.Vertices.Count <= 3)
            return false;
        
        var edge1 = polygon.Edges.First(e => e.ToVertexId == vertex.Id);
        var edge2 = polygon.Edges.First(e => e.FromVertexId == vertex.Id);
        
        var newEdge = new Edge(edge1.FromVertexId, edge2.ToVertexId);
        
        polygon.AddEdge(newEdge);
        
        polygon.RemoveEdge(edge1.Id);
        polygon.RemoveEdge(edge2.Id);
        polygon.RemoveVertex(vertexId);
        
        return true;
    }
}