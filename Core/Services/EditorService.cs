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
}