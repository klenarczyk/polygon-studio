using PolygonStudio.Core.Model.Geometries;

namespace PolygonStudio.Core.Model;

public sealed class Edge(Guid fromVertexId, Guid toVertexId, EdgeGeometry? geometry = null)
{
    public Guid Id { get; } = Guid.NewGuid();

    public Guid FromVertexId { get; } = fromVertexId;
    public Guid ToVertexId { get; } = toVertexId;

    public EdgeGeometry Geometry { get; internal set; } = geometry ?? new LineGeometry();
}