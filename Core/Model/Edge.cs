using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Model;

public sealed class Edge
{
    public Guid Id { get; } = Guid.NewGuid();

    public Guid FromVertexId { get; }
    public Guid ToVertexId { get; }

    public EdgeGeometry Geometry { get; internal set; }

    public Edge(Guid fromVertexId, Guid toVertexId, EdgeGeometry? geometry = null)
    {
        if (fromVertexId == toVertexId)
            throw new ArgumentException("Edges must connect two distinct vertices.");
        
        FromVertexId = fromVertexId;
        ToVertexId = toVertexId;
        
        Geometry = geometry ?? new LineGeometry();
    }
}