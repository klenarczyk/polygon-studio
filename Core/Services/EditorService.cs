using PolygonStudio.Core.Geometry;
using PolygonStudio.Core.Model;

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
}