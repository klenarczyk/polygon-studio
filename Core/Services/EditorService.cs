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
}